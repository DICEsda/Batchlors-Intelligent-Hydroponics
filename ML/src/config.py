"""
Configuration for ML environment.
Load settings from environment variables or .env file.
"""

import os
from pathlib import Path
from pydantic import BaseModel
from dotenv import load_dotenv

# Load .env file if it exists
env_path = Path(__file__).parent.parent / ".env"
load_dotenv(env_path)


class MongoDBConfig(BaseModel):
    """MongoDB connection configuration."""
    host: str = os.getenv("MONGODB_HOST", "localhost")
    port: int = int(os.getenv("MONGODB_PORT", "27017"))
    database: str = os.getenv("MONGODB_DATABASE", "iot_hydroponics")
    username: str | None = os.getenv("MONGODB_USERNAME")
    password: str | None = os.getenv("MONGODB_PASSWORD")
    
    @property
    def connection_string(self) -> str:
        """Build MongoDB connection string."""
        if self.username and self.password:
            return f"mongodb://{self.username}:{self.password}@{self.host}:{self.port}/{self.database}?authSource=admin"
        return f"mongodb://{self.host}:{self.port}/{self.database}"


class MQTTConfig(BaseModel):
    """MQTT broker configuration."""
    host: str = os.getenv("MQTT_HOST", "localhost")
    port: int = int(os.getenv("MQTT_PORT", "1883"))
    username: str | None = os.getenv("MQTT_USERNAME")
    password: str | None = os.getenv("MQTT_PASSWORD")
    client_id: str = os.getenv("MQTT_CLIENT_ID", "ml-service")
    
    # Topic patterns
    tower_telemetry_topic: str = "farm/+/coord/+/tower/+/telemetry"
    reservoir_telemetry_topic: str = "farm/+/coord/+/reservoir/telemetry"
    mmwave_topic: str = "farm/+/coord/+/mmwave"


class MLConfig(BaseModel):
    """ML model configuration."""
    model_dir: Path = Path(os.getenv("ML_MODEL_DIR", "models"))
    data_dir: Path = Path(os.getenv("ML_DATA_DIR", "data"))
    
    # Feature engineering
    rolling_window_hours: int = 24
    min_samples_for_training: int = 100
    
    # Model training
    test_split_ratio: float = 0.2
    random_seed: int = 42


class AzureDigitalTwinsConfig(BaseModel):
    """Azure Digital Twins configuration."""
    endpoint: str | None = os.getenv("AZURE_DT_ENDPOINT")
    
    # Service Principal authentication (optional)
    tenant_id: str | None = os.getenv("AZURE_TENANT_ID")
    client_id: str | None = os.getenv("AZURE_CLIENT_ID")
    client_secret: str | None = os.getenv("AZURE_CLIENT_SECRET")
    
    # Model IDs (DTDL)
    model_farm: str = "dtmi:iot:hydroponics:Farm;1"
    model_coordinator: str = "dtmi:iot:hydroponics:Coordinator;1"
    model_tower: str = "dtmi:iot:hydroponics:Tower;1"
    model_reservoir: str = "dtmi:iot:hydroponics:Reservoir;1"
    
    @property
    def is_configured(self) -> bool:
        """Check if ADT is configured."""
        return self.endpoint is not None


class Config:
    """Main configuration container."""
    mongodb = MongoDBConfig()
    mqtt = MQTTConfig()
    ml = MLConfig()
    azure_dt = AzureDigitalTwinsConfig()
    
    # API settings
    api_host: str = os.getenv("ML_API_HOST", "0.0.0.0")
    api_port: int = int(os.getenv("ML_API_PORT", "8000"))


# Global config instance
config = Config()
