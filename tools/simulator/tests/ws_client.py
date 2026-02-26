"""
WebSocketTestClient — connects to the backend's /ws/broadcast WebSocket
endpoint and collects broadcast messages for test assertions.

Uses the `websockets` library running in a background thread with its own
asyncio event loop, providing a synchronous interface for pytest tests.
"""

import asyncio
import json
import os
import threading
import time
import uuid
from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional

try:
    import websockets
    import websockets.client

    # Detect which API generation is available.
    # websockets < 14  → legacy API  (extra_headers, websockets.client.connect)
    # websockets >= 14 → new API     (additional_headers, websockets.asyncio.client.connect)
    _ws_major = int(getattr(websockets, "__version__", "0").split(".")[0])
    _WS_LEGACY = _ws_major < 14
except ImportError:
    websockets = None  # type: ignore
    _WS_LEGACY = True


API_URL = os.environ.get("SIM_API_URL", "http://backend-sim:8000").rstrip("/")


@dataclass
class WsMessage:
    """A WebSocket message received from the backend."""
    type: str
    payload: Dict[str, Any]
    raw: str
    timestamp: float = field(default_factory=time.time)


class WebSocketTestClient:
    """
    Synchronous wrapper around an async WebSocket connection to /ws/broadcast.
    
    Usage:
        ws = WebSocketTestClient()
        ws.connect()
        # ... trigger some action that causes a WS broadcast ...
        msg = ws.wait_for_message("tower_telemetry", timeout=10)
        assert msg.payload["tower_id"] == "tower-1"
        ws.disconnect()
    """

    def __init__(self, base_url: Optional[str] = None):
        if websockets is None:
            raise ImportError(
                "websockets library is required. Install with: pip install websockets"
            )

        http_url = base_url or API_URL
        # Convert http:// to ws://
        self._ws_url = http_url.replace("http://", "ws://").replace("https://", "wss://")
        self._ws_url = f"{self._ws_url}/ws/broadcast"

        self._messages: List[WsMessage] = []
        self._waiters: List[tuple] = []  # (type_filter, event, container)
        self._lock = threading.Lock()

        self._loop: Optional[asyncio.AbstractEventLoop] = None
        self._thread: Optional[threading.Thread] = None
        self._ws: Optional[Any] = None
        self._connected = threading.Event()
        self._stop_event: Optional[asyncio.Event] = None
        self._connect_error: Optional[Exception] = None
        self._client_id = f"ws-test-{uuid.uuid4().hex[:8]}"

    def connect(self, timeout: float = 15):
        """Connect to the WebSocket endpoint in a background thread."""
        self._thread = threading.Thread(target=self._run_loop, daemon=True)
        self._thread.start()

        if not self._connected.wait(timeout):
            raise ConnectionError(
                f"WebSocketTestClient failed to connect to {self._ws_url} "
                f"within {timeout}s"
            )

        # If the background loop caught an exception during connect, propagate it.
        if self._connect_error is not None:
            raise ConnectionError(
                f"WebSocketTestClient connection to {self._ws_url} failed: "
                f"{self._connect_error}"
            ) from self._connect_error

    def _run_loop(self):
        """Background thread: create event loop, connect, and receive."""
        self._loop = asyncio.new_event_loop()
        asyncio.set_event_loop(self._loop)
        self._stop_event = asyncio.Event()
        self._loop.run_until_complete(self._receive_loop())

    async def _receive_loop(self):
        """Async receive loop — connects and processes messages."""
        try:
            # Build keyword arguments compatible with the installed websockets version.
            # Legacy (< 14) uses ``extra_headers``; new (>= 14) uses ``additional_headers``.
            header_kwarg = (
                "extra_headers" if _WS_LEGACY else "additional_headers"
            )
            connect_kwargs = {
                header_kwarg: {"X-Client-Id": self._client_id},
                "ping_interval": 20,
                "ping_timeout": 10,
                "close_timeout": 5,
            }

            async with websockets.client.connect(
                self._ws_url,
                **connect_kwargs,
            ) as ws:
                self._ws = ws
                self._connected.set()

                while not self._stop_event.is_set():
                    try:
                        raw = await asyncio.wait_for(ws.recv(), timeout=1.0)
                        if isinstance(raw, bytes):
                            raw = raw.decode("utf-8", errors="replace")
                        self._process_message(raw)
                    except asyncio.TimeoutError:
                        continue
                    except websockets.exceptions.ConnectionClosed:
                        break
        except Exception as e:
            # Connection failed — store the error and set connected event
            # so connect() doesn't hang forever. Caller can inspect _connect_error.
            self._connect_error = e
            self._connected.set()

    def _process_message(self, raw: str):
        """Parse a raw WS message and store it."""
        try:
            data = json.loads(raw)
        except (json.JSONDecodeError, TypeError):
            return

        msg_type = data.get("type", "unknown")
        msg = WsMessage(
            type=msg_type,
            payload=data,
            raw=raw,
        )

        with self._lock:
            self._messages.append(msg)
            # Wake up matching waiters
            for type_filter, event, container in self._waiters:
                if type_filter is None or msg_type == type_filter:
                    container.append(msg)
                    event.set()

    def wait_for_message(
        self,
        type_filter: Optional[str] = None,
        timeout: float = 15,
    ) -> WsMessage:
        """
        Block until a WebSocket message of the given type arrives.
        If type_filter is None, matches any message type.
        """
        event = threading.Event()
        container: List[WsMessage] = []

        with self._lock:
            # Check already-received messages
            for msg in self._messages:
                if type_filter is None or msg.type == type_filter:
                    return msg
            # Register waiter
            waiter = (type_filter, event, container)
            self._waiters.append(waiter)

        event.wait(timeout=timeout)

        with self._lock:
            if waiter in self._waiters:
                self._waiters.remove(waiter)

        if not container:
            types_seen = [m.type for m in self._messages]
            raise TimeoutError(
                f"WebSocketTestClient waited {timeout}s for message type "
                f"'{type_filter}', got nothing. "
                f"Types seen so far: {types_seen}"
            )
        return container[0]

    def get_messages(self, type_filter: Optional[str] = None) -> List[WsMessage]:
        """Return all received messages, optionally filtered by type."""
        with self._lock:
            if type_filter is None:
                return list(self._messages)
            return [m for m in self._messages if m.type == type_filter]

    def clear(self):
        """Clear all received messages."""
        with self._lock:
            self._messages.clear()

    def disconnect(self):
        """Disconnect and stop the background thread."""
        if self._stop_event and self._loop:
            self._loop.call_soon_threadsafe(self._stop_event.set)
        if self._thread:
            self._thread.join(timeout=10)
        self._connected.clear()

    @property
    def message_count(self) -> int:
        with self._lock:
            return len(self._messages)

    def send_subscribe(self, target: str, target_id: Optional[str] = None):
        """Send a subscribe message to the backend WebSocket."""
        msg = {"type": "subscribe", "target": target}
        if target_id:
            msg["id"] = target_id
        if self._ws and self._loop:
            asyncio.run_coroutine_threadsafe(
                self._ws.send(json.dumps(msg)), self._loop
            )
