"""
MQTT Subscriber for real-time telemetry data streaming.
Provides async data ingestion for online ML inference.
"""

import json
import asyncio
from datetime import datetime
from typing import Callable, Optional, Any
from dataclasses import dataclass, field
from collections import deque
import paho.mqtt.client as mqtt
from loguru import logger

from ..config import config


@dataclass
class TelemetryMessage:
    """Represents a parsed telemetry message."""
    topic: str
    payload: dict
    timestamp: datetime = field(default_factory=datetime.utcnow)
    
    @property
    def message_type(self) -> str:
        """Extract message type from topic."""
        parts = self.topic.split("/")
        if "tower" in parts:
            return "tower_telemetry"
        elif "reservoir" in parts:
            return "reservoir_telemetry"
        elif "mmwave" in parts:
            return "mmwave"
        return "unknown"
    
    @property
    def farm_id(self) -> Optional[str]:
        """Extract farm_id from topic."""
        parts = self.topic.split("/")
        if len(parts) >= 2 and parts[0] == "farm":
            return parts[1]
        return None
    
    @property
    def coord_id(self) -> Optional[str]:
        """Extract coord_id from topic."""
        parts = self.topic.split("/")
        try:
            coord_idx = parts.index("coord")
            return parts[coord_idx + 1] if coord_idx + 1 < len(parts) else None
        except ValueError:
            return None


class MQTTSubscriber:
    """
    MQTT subscriber for real-time telemetry streaming.
    
    Supports callback-based processing and buffered batch retrieval.
    
    Usage:
        # Callback-based
        def on_telemetry(msg: TelemetryMessage):
            print(f"Received: {msg.payload}")
        
        subscriber = MQTTSubscriber()
        subscriber.add_callback(on_telemetry)
        subscriber.start()
        
        # Or buffered batch retrieval
        subscriber = MQTTSubscriber(buffer_size=1000)
        subscriber.start()
        
        # Later...
        messages = subscriber.get_buffered_messages()
    """
    
    def __init__(
        self,
        host: Optional[str] = None,
        port: Optional[int] = None,
        username: Optional[str] = None,
        password: Optional[str] = None,
        client_id: Optional[str] = None,
        buffer_size: int = 1000,
    ):
        """
        Initialize MQTT subscriber.
        
        Args:
            host: MQTT broker host
            port: MQTT broker port
            username: MQTT username
            password: MQTT password
            client_id: MQTT client identifier
            buffer_size: Max messages to buffer (0 = no buffering)
        """
        self.host = host or config.mqtt.host
        self.port = port or config.mqtt.port
        self.username = username or config.mqtt.username
        self.password = password or config.mqtt.password
        self.client_id = client_id or f"{config.mqtt.client_id}-{datetime.utcnow().timestamp():.0f}"
        
        self.buffer_size = buffer_size
        self._buffer: deque[TelemetryMessage] = deque(maxlen=buffer_size if buffer_size > 0 else None)
        
        self._callbacks: list[Callable[[TelemetryMessage], None]] = []
        self._client: Optional[mqtt.Client] = None
        self._connected = False
        self._running = False
        
        # Topics to subscribe
        self._topics = [
            config.mqtt.tower_telemetry_topic,
            config.mqtt.reservoir_telemetry_topic,
            config.mqtt.mmwave_topic,
        ]
    
    def add_callback(self, callback: Callable[[TelemetryMessage], None]) -> None:
        """Add a callback to be called for each message."""
        self._callbacks.append(callback)
    
    def remove_callback(self, callback: Callable[[TelemetryMessage], None]) -> None:
        """Remove a previously added callback."""
        self._callbacks.remove(callback)
    
    def start(self) -> None:
        """Start the MQTT subscriber (blocking)."""
        self._setup_client()
        self._running = True
        
        logger.info(f"Connecting to MQTT broker at {self.host}:{self.port}")
        self._client.connect(self.host, self.port, keepalive=60)
        
        # This blocks until stop() is called
        self._client.loop_forever()
    
    def start_async(self) -> None:
        """Start the MQTT subscriber in background thread."""
        self._setup_client()
        self._running = True
        
        logger.info(f"Connecting to MQTT broker at {self.host}:{self.port}")
        self._client.connect(self.host, self.port, keepalive=60)
        self._client.loop_start()
    
    def stop(self) -> None:
        """Stop the MQTT subscriber."""
        self._running = False
        if self._client:
            self._client.loop_stop()
            self._client.disconnect()
            logger.info("MQTT subscriber stopped")
    
    def get_buffered_messages(self, clear: bool = True) -> list[TelemetryMessage]:
        """
        Get buffered messages.
        
        Args:
            clear: Clear buffer after retrieval
        
        Returns:
            List of buffered TelemetryMessage objects
        """
        messages = list(self._buffer)
        if clear:
            self._buffer.clear()
        return messages
    
    @property
    def is_connected(self) -> bool:
        """Check if connected to MQTT broker."""
        return self._connected
    
    @property
    def buffer_count(self) -> int:
        """Get number of messages in buffer."""
        return len(self._buffer)
    
    # -------------------------------------------------------------------------
    # Internal Methods
    # -------------------------------------------------------------------------
    
    def _setup_client(self) -> None:
        """Set up MQTT client with callbacks."""
        self._client = mqtt.Client(
            client_id=self.client_id,
            callback_api_version=mqtt.CallbackAPIVersion.VERSION2,
        )
        
        if self.username and self.password:
            self._client.username_pw_set(self.username, self.password)
        
        self._client.on_connect = self._on_connect
        self._client.on_disconnect = self._on_disconnect
        self._client.on_message = self._on_message
    
    def _on_connect(
        self,
        client: mqtt.Client,
        userdata: Any,
        flags: dict,
        reason_code: mqtt.ReasonCode,
        properties: Any,
    ) -> None:
        """Handle MQTT connection."""
        if reason_code == 0:
            self._connected = True
            logger.success(f"Connected to MQTT broker")
            
            # Subscribe to telemetry topics
            for topic in self._topics:
                client.subscribe(topic, qos=1)
                logger.info(f"Subscribed to: {topic}")
        else:
            logger.error(f"MQTT connection failed: {reason_code}")
    
    def _on_disconnect(
        self,
        client: mqtt.Client,
        userdata: Any,
        flags: dict,
        reason_code: mqtt.ReasonCode,
        properties: Any,
    ) -> None:
        """Handle MQTT disconnection."""
        self._connected = False
        logger.warning(f"Disconnected from MQTT broker: {reason_code}")
        
        # Auto-reconnect if still running
        if self._running:
            logger.info("Attempting to reconnect...")
    
    def _on_message(
        self,
        client: mqtt.Client,
        userdata: Any,
        msg: mqtt.MQTTMessage,
    ) -> None:
        """Handle incoming MQTT message."""
        try:
            payload = json.loads(msg.payload.decode("utf-8"))
            
            telemetry = TelemetryMessage(
                topic=msg.topic,
                payload=payload,
                timestamp=datetime.utcnow(),
            )
            
            # Buffer the message
            if self.buffer_size > 0:
                self._buffer.append(telemetry)
            
            # Call callbacks
            for callback in self._callbacks:
                try:
                    callback(telemetry)
                except Exception as e:
                    logger.error(f"Callback error: {e}")
            
            logger.debug(f"Received {telemetry.message_type} from {msg.topic}")
            
        except json.JSONDecodeError as e:
            logger.warning(f"Invalid JSON payload on {msg.topic}: {e}")
        except Exception as e:
            logger.error(f"Error processing message: {e}")


class MQTTDataStream:
    """
    Async generator for streaming MQTT data.
    
    Usage:
        async for message in MQTTDataStream():
            process(message)
    """
    
    def __init__(self, subscriber: Optional[MQTTSubscriber] = None):
        self.subscriber = subscriber or MQTTSubscriber()
        self._queue: asyncio.Queue[TelemetryMessage] = asyncio.Queue()
    
    async def __aenter__(self):
        self.subscriber.add_callback(self._enqueue)
        self.subscriber.start_async()
        return self
    
    async def __aexit__(self, exc_type, exc_val, exc_tb):
        self.subscriber.stop()
    
    def _enqueue(self, message: TelemetryMessage) -> None:
        """Callback to enqueue messages."""
        try:
            self._queue.put_nowait(message)
        except asyncio.QueueFull:
            pass  # Drop oldest if full
    
    async def __aiter__(self):
        return self
    
    async def __anext__(self) -> TelemetryMessage:
        return await self._queue.get()
