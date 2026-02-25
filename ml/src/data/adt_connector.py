"""
Azure Digital Twins connector for ML service.

Provides read/write access to twin state for:
- Fetching current twin state for inference
- Updating twin properties with ML predictions
- Querying twin relationships
"""

from datetime import datetime
from typing import Any, Optional
import json
from dataclasses import dataclass, field
from azure.digitaltwins.core import DigitalTwinsClient
from azure.identity import DefaultAzureCredential, ClientSecretCredential
from azure.core.exceptions import ResourceNotFoundError, HttpResponseError
from loguru import logger

from ..config import config


@dataclass
class TwinState:
    """Represents the state of a digital twin."""
    twin_id: str
    model_id: str
    properties: dict
    metadata: dict = field(default_factory=dict)
    etag: Optional[str] = None
    
    @property
    def reported_state(self) -> dict:
        """Get reported state properties."""
        return self.properties.get("reported", {})
    
    @property
    def desired_state(self) -> dict:
        """Get desired state properties."""
        return self.properties.get("desired", {})


@dataclass
class TwinRelationship:
    """Represents a relationship between twins."""
    relationship_id: str
    source_id: str
    target_id: str
    relationship_name: str
    properties: dict = field(default_factory=dict)


class AzureDigitalTwinsConnector:
    """
    Connector for Azure Digital Twins service.
    
    Usage:
        # Using DefaultAzureCredential (recommended for production)
        connector = AzureDigitalTwinsConnector()
        
        # Using service principal
        connector = AzureDigitalTwinsConnector(
            tenant_id="...",
            client_id="...",
            client_secret="..."
        )
        
        # Get a twin
        twin = connector.get_twin("coordinator-001")
        print(twin.properties)
        
        # Update twin with ML predictions
        connector.update_twin_properties("tower-001", {
            "ml_predictions": {
                "predicted_height_cm": 15.5,
                "anomaly_score": 0.02,
                "predicted_harvest_date": "2026-02-15"
            }
        })
        
        # Query twins
        towers = connector.query_twins(
            "SELECT * FROM digitaltwins WHERE IS_OF_MODEL('dtmi:hydroponics:Tower;1')"
        )
    """
    
    # DTDL Model IDs
    MODEL_COORDINATOR = "dtmi:iot:hydroponics:Coordinator;1"
    MODEL_TOWER = "dtmi:iot:hydroponics:Tower;1"
    MODEL_RESERVOIR = "dtmi:iot:hydroponics:Reservoir;1"
    MODEL_FARM = "dtmi:iot:hydroponics:Farm;1"
    
    def __init__(
        self,
        endpoint: Optional[str] = None,
        tenant_id: Optional[str] = None,
        client_id: Optional[str] = None,
        client_secret: Optional[str] = None,
    ):
        """
        Initialize Azure Digital Twins connector.
        
        Args:
            endpoint: ADT instance endpoint URL
            tenant_id: Azure AD tenant ID (for service principal auth)
            client_id: Azure AD app client ID (for service principal auth)
            client_secret: Azure AD app client secret (for service principal auth)
        """
        self.endpoint = endpoint or config.azure_dt.endpoint
        
        # Determine authentication method
        if tenant_id and client_id and client_secret:
            # Service principal authentication
            self._credential = ClientSecretCredential(
                tenant_id=tenant_id,
                client_id=client_id,
                client_secret=client_secret,
            )
            logger.info("Using service principal authentication")
        else:
            # Default credential chain (Managed Identity, Azure CLI, etc.)
            self._credential = DefaultAzureCredential()
            logger.info("Using DefaultAzureCredential")
        
        self._client: Optional[DigitalTwinsClient] = None
    
    def connect(self) -> None:
        """Establish connection to Azure Digital Twins."""
        if not self.endpoint:
            raise ValueError(
                "Azure Digital Twins endpoint not configured. "
                "Set AZURE_DT_ENDPOINT environment variable or pass endpoint parameter."
            )
        
        logger.info(f"Connecting to Azure Digital Twins at {self.endpoint}")
        self._client = DigitalTwinsClient(self.endpoint, self._credential)
        logger.success("Connected to Azure Digital Twins")
    
    @property
    def client(self) -> DigitalTwinsClient:
        """Get ADT client, connecting if necessary."""
        if self._client is None:
            self.connect()
        return self._client
    
    def __enter__(self):
        self.connect()
        return self
    
    def __exit__(self, exc_type, exc_val, exc_tb):
        pass  # No explicit disconnect needed
    
    # -------------------------------------------------------------------------
    # Twin Operations
    # -------------------------------------------------------------------------
    
    def get_twin(self, twin_id: str) -> Optional[TwinState]:
        """
        Get a digital twin by ID.
        
        Args:
            twin_id: The twin identifier
        
        Returns:
            TwinState object or None if not found
        """
        try:
            twin = self.client.get_digital_twin(twin_id)
            
            return TwinState(
                twin_id=twin_id,
                model_id=twin.get("$metadata", {}).get("$model", ""),
                properties={k: v for k, v in twin.items() if not k.startswith("$")},
                metadata=twin.get("$metadata", {}),
                etag=twin.get("$etag"),
            )
        except ResourceNotFoundError:
            logger.warning(f"Twin not found: {twin_id}")
            return None
        except HttpResponseError as e:
            logger.error(f"Error fetching twin {twin_id}: {e}")
            raise
    
    def get_coordinator(self, coord_id: str) -> Optional[TwinState]:
        """Get a coordinator twin by ID."""
        return self.get_twin(f"coordinator-{coord_id}")
    
    def get_tower(self, tower_id: str) -> Optional[TwinState]:
        """Get a tower twin by ID."""
        return self.get_twin(f"tower-{tower_id}")
    
    def update_twin_properties(
        self,
        twin_id: str,
        properties: dict[str, Any],
        if_match: Optional[str] = None,
    ) -> bool:
        """
        Update properties on a digital twin.
        
        Uses JSON Patch format internally.
        
        Args:
            twin_id: The twin identifier
            properties: Dictionary of properties to update
            if_match: Optional ETag for optimistic concurrency
        
        Returns:
            True if successful
        """
        # Build JSON Patch operations
        patch = []
        for key, value in properties.items():
            patch.append({
                "op": "add",  # add creates or replaces
                "path": f"/{key}",
                "value": value,
            })
        
        try:
            self.client.update_digital_twin(
                twin_id,
                patch,
                etag=if_match,
                match_condition="IfMatch" if if_match else None,
            )
            logger.info(f"Updated twin {twin_id} with {len(properties)} properties")
            return True
        except HttpResponseError as e:
            logger.error(f"Error updating twin {twin_id}: {e}")
            raise
    
    def update_ml_predictions(
        self,
        twin_id: str,
        predictions: dict[str, Any],
        model_name: str = "default",
        model_version: str = "1.0",
    ) -> bool:
        """
        Update ML predictions on a twin.
        
        Wraps predictions with metadata (timestamp, model info).
        
        Args:
            twin_id: The twin identifier
            predictions: Dictionary of prediction values
            model_name: Name of the ML model that generated predictions
            model_version: Version of the ML model
        
        Returns:
            True if successful
        """
        ml_data = {
            "ml_predictions": {
                **predictions,
                "_metadata": {
                    "model_name": model_name,
                    "model_version": model_version,
                    "generated_at": datetime.utcnow().isoformat(),
                }
            }
        }
        
        return self.update_twin_properties(twin_id, ml_data)
    
    # -------------------------------------------------------------------------
    # Query Operations
    # -------------------------------------------------------------------------
    
    def query_twins(self, query: str) -> list[TwinState]:
        """
        Execute an ADT query.
        
        Args:
            query: ADT query string (SQL-like syntax)
        
        Returns:
            List of TwinState objects matching the query
        
        Example queries:
            - "SELECT * FROM digitaltwins"
            - "SELECT * FROM digitaltwins WHERE IS_OF_MODEL('dtmi:iot:hydroponics:Tower;1')"
            - "SELECT * FROM digitaltwins T WHERE T.status = 'online'"
        """
        try:
            results = self.client.query_twins(query)
            
            twins = []
            for twin in results:
                twins.append(TwinState(
                    twin_id=twin.get("$dtId", ""),
                    model_id=twin.get("$metadata", {}).get("$model", ""),
                    properties={k: v for k, v in twin.items() if not k.startswith("$")},
                    metadata=twin.get("$metadata", {}),
                    etag=twin.get("$etag"),
                ))
            
            logger.info(f"Query returned {len(twins)} twins")
            return twins
        except HttpResponseError as e:
            logger.error(f"Query error: {e}")
            raise
    
    def get_all_coordinators(self) -> list[TwinState]:
        """Get all coordinator twins."""
        return self.query_twins(
            f"SELECT * FROM digitaltwins WHERE IS_OF_MODEL('{self.MODEL_COORDINATOR}')"
        )
    
    def get_all_towers(self) -> list[TwinState]:
        """Get all tower twins."""
        return self.query_twins(
            f"SELECT * FROM digitaltwins WHERE IS_OF_MODEL('{self.MODEL_TOWER}')"
        )
    
    def get_towers_by_coordinator(self, coord_id: str) -> list[TwinState]:
        """Get all towers connected to a coordinator."""
        query = f"""
            SELECT tower 
            FROM digitaltwins coordinator
            JOIN tower RELATED coordinator.hasTower
            WHERE coordinator.$dtId = 'coordinator-{coord_id}'
        """
        return self.query_twins(query)
    
    def get_towers_needing_attention(self, anomaly_threshold: float = 0.5) -> list[TwinState]:
        """Get towers with high anomaly scores."""
        query = f"""
            SELECT * FROM digitaltwins T
            WHERE IS_OF_MODEL('{self.MODEL_TOWER}')
            AND T.ml_predictions.anomaly_score > {anomaly_threshold}
        """
        return self.query_twins(query)
    
    # -------------------------------------------------------------------------
    # Relationship Operations
    # -------------------------------------------------------------------------
    
    def get_relationships(
        self,
        twin_id: str,
        relationship_name: Optional[str] = None,
    ) -> list[TwinRelationship]:
        """
        Get relationships from a twin.
        
        Args:
            twin_id: Source twin ID
            relationship_name: Optional filter by relationship name
        
        Returns:
            List of TwinRelationship objects
        """
        try:
            rels = self.client.list_relationships(twin_id, relationship_name)
            
            relationships = []
            for rel in rels:
                relationships.append(TwinRelationship(
                    relationship_id=rel.get("$relationshipId", ""),
                    source_id=rel.get("$sourceId", ""),
                    target_id=rel.get("$targetId", ""),
                    relationship_name=rel.get("$relationshipName", ""),
                    properties={k: v for k, v in rel.items() if not k.startswith("$")},
                ))
            
            return relationships
        except HttpResponseError as e:
            logger.error(f"Error fetching relationships for {twin_id}: {e}")
            raise
    
    def get_incoming_relationships(self, twin_id: str) -> list[TwinRelationship]:
        """Get relationships pointing to a twin."""
        try:
            rels = self.client.list_incoming_relationships(twin_id)
            
            relationships = []
            for rel in rels:
                relationships.append(TwinRelationship(
                    relationship_id=rel.get("$relationshipId", ""),
                    source_id=rel.get("$sourceId", ""),
                    target_id=twin_id,
                    relationship_name=rel.get("$relationshipName", ""),
                ))
            
            return relationships
        except HttpResponseError as e:
            logger.error(f"Error fetching incoming relationships for {twin_id}: {e}")
            raise
    
    # -------------------------------------------------------------------------
    # Telemetry Operations
    # -------------------------------------------------------------------------
    
    def publish_telemetry(
        self,
        twin_id: str,
        telemetry: dict[str, Any],
        message_id: Optional[str] = None,
    ) -> bool:
        """
        Publish telemetry to a digital twin.
        
        Note: This sends telemetry TO the twin, not from it.
        Use this for ML-generated metrics or predictions.
        
        Args:
            twin_id: Target twin ID
            telemetry: Telemetry payload
            message_id: Optional message identifier
        
        Returns:
            True if successful
        """
        try:
            self.client.publish_telemetry(
                twin_id,
                telemetry,
                message_id=message_id,
            )
            logger.debug(f"Published telemetry to {twin_id}")
            return True
        except HttpResponseError as e:
            logger.error(f"Error publishing telemetry to {twin_id}: {e}")
            raise
    
    # -------------------------------------------------------------------------
    # Bulk Operations for ML
    # -------------------------------------------------------------------------
    
    def get_all_twins_as_dataframe(self) -> "pd.DataFrame":
        """
        Get all twins as a pandas DataFrame for ML analysis.
        
        Returns:
            DataFrame with twin properties flattened as columns
        """
        import pandas as pd
        
        twins = self.query_twins("SELECT * FROM digitaltwins")
        
        if not twins:
            return pd.DataFrame()
        
        # Flatten properties into rows
        rows = []
        for twin in twins:
            row = {
                "twin_id": twin.twin_id,
                "model_id": twin.model_id,
                **self._flatten_dict(twin.properties),
            }
            rows.append(row)
        
        return pd.DataFrame(rows)
    
    def batch_update_predictions(
        self,
        predictions: dict[str, dict[str, Any]],
        model_name: str = "default",
        model_version: str = "1.0",
    ) -> dict[str, bool]:
        """
        Update predictions for multiple twins.
        
        Args:
            predictions: Dict mapping twin_id -> prediction dict
            model_name: ML model name
            model_version: ML model version
        
        Returns:
            Dict mapping twin_id -> success boolean
        """
        results = {}
        
        for twin_id, preds in predictions.items():
            try:
                success = self.update_ml_predictions(
                    twin_id, preds, model_name, model_version
                )
                results[twin_id] = success
            except Exception as e:
                logger.error(f"Failed to update {twin_id}: {e}")
                results[twin_id] = False
        
        success_count = sum(results.values())
        logger.info(f"Batch update: {success_count}/{len(predictions)} successful")
        
        return results
    
    # -------------------------------------------------------------------------
    # Helpers
    # -------------------------------------------------------------------------
    
    def _flatten_dict(self, d: dict, parent_key: str = "", sep: str = "_") -> dict:
        """Flatten nested dictionary."""
        items = []
        for k, v in d.items():
            new_key = f"{parent_key}{sep}{k}" if parent_key else k
            if isinstance(v, dict):
                items.extend(self._flatten_dict(v, new_key, sep).items())
            else:
                items.append((new_key, v))
        return dict(items)
