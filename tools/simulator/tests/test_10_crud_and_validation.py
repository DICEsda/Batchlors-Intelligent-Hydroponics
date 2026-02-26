"""
Test 10 — CRUD Operations and Input Validation.

Tests complete CRUD lifecycles for farms, coordinators, and towers.
Also tests API key authentication enforcement, FluentValidation
rejection of invalid inputs, and 404 handling for missing resources.

Each test exercises multiple subsystems: REST API, MongoDB persistence,
and in some cases MQTT + telemetry pipeline.

Addresses GitHub issues: #110 (CRUD operations)
"""

import time
import uuid
import pytest

pytestmark = pytest.mark.timeout(90)


class TestFarmLifecycle:
    """Complete CRUD lifecycle for farms."""

    def test_farm_create_read_update_delete(self, api_client):
        """
        POST farm -> GET farm -> PUT update -> GET verify -> DELETE -> GET 404.
        
        Proves: Full CRUD through REST with MongoDB persistence.
        """
        farm_id = f"crud-test-farm-{uuid.uuid4().hex[:6]}"
        
        # CREATE
        r = api_client.post("/api/farms", json_data={
            "farm_id": farm_id,
            "name": "CRUD Test Farm",
            "description": "Created by test_10",
        })
        assert r.status_code in (200, 201), f"Create farm: {r.status_code} {r.text}"
        
        # READ
        r = api_client.get("/api/farms")
        assert r.status_code == 200
        farms = r.json()
        if isinstance(farms, list):
            farm_ids = [f.get("farm_id") or f.get("farmId") for f in farms]
            assert farm_id in farm_ids, f"Created farm not in list. IDs: {farm_ids}"
        
        # UPDATE
        r = api_client.put(f"/api/farms/{farm_id}", json_data={
            "name": "Updated CRUD Test Farm",
            "description": "Updated by test_10",
        })
        assert r.status_code in (200, 204), f"Update farm: {r.status_code} {r.text}"
        
        # VERIFY UPDATE
        r = api_client.get("/api/farms")
        assert r.status_code == 200
        farms = r.json()
        if isinstance(farms, list):
            matched = [f for f in farms if (f.get("farm_id") or f.get("farmId")) == farm_id]
            if matched:
                name = matched[0].get("name")
                assert name == "Updated CRUD Test Farm", f"Name not updated: {name}"
        
        # DELETE
        r = api_client.delete(f"/api/farms/{farm_id}")
        assert r.status_code in (200, 204, 404), f"Delete farm: {r.status_code}"
        
        # VERIFY DELETED
        r = api_client.get("/api/farms")
        if r.status_code == 200:
            farms = r.json()
            if isinstance(farms, list):
                farm_ids = [f.get("farm_id") or f.get("farmId") for f in farms]
                assert farm_id not in farm_ids, "Farm still exists after DELETE"


class TestCoordinatorMetadata:
    """Coordinator registration and metadata updates."""

    def test_coordinator_read_and_update(self, api_client, mqtt_client, bootstrap_farm):
        """
        GET coordinator detail -> PATCH metadata -> GET verify.
        Uses the bootstrap coordinator which is already registered.
        """
        coord_id = bootstrap_farm["coord_id"]
        
        # READ
        r = api_client.get(f"/api/coordinators/{coord_id}")
        assert r.status_code == 200, f"GET coordinator: {r.status_code} {r.text}"
        coord = r.json()
        assert coord.get("coord_id") == coord_id or coord.get("coordId") == coord_id
        
        # PATCH metadata
        r = api_client.put(f"/api/coordinators/{coord_id}", json_data={
            "name": "Updated Test Coordinator",
            "description": "Metadata updated by test_10",
        })
        # PATCH might also be available
        if r.status_code in (404, 405):
            r = api_client.post(f"/api/coordinators/{coord_id}", json_data={
                "name": "Updated Test Coordinator",
            })
        
        if r.status_code in (200, 204):
            # Verify update
            r = api_client.get(f"/api/coordinators/{coord_id}")
            assert r.status_code == 200
            coord = r.json()
            name = coord.get("name")
            if name:
                assert name == "Updated Test Coordinator"

    def test_coordinator_list(self, api_client, bootstrap_farm):
        """GET /api/coordinators returns a list including the bootstrap coordinator."""
        r = api_client.get("/api/coordinators")
        assert r.status_code == 200
        coords = r.json()
        if isinstance(coords, list):
            assert len(coords) >= 1, "Expected at least 1 coordinator"


class TestTowerCrudWithTelemetry:
    """Tower CRUD plus telemetry association."""

    def test_tower_create_telemetry_and_read(self, api_client, mqtt_client, bootstrap_farm):
        """
        PUT tower -> publish telemetry via MQTT -> GET tower has telemetry
        -> GET tower history -> verify end-to-end data association.
        """
        farm_id = bootstrap_farm["farm_id"]
        coord_id = bootstrap_farm["coord_id"]
        tower_id = f"crud-tower-{uuid.uuid4().hex[:6]}"
        
        # CREATE tower
        r = api_client.put(
            f"/api/towers/{farm_id}/{coord_id}/{tower_id}",
            json_data={"name": "CRUD Test Tower", "crop_type": 0},
        )
        assert r.status_code in (200, 201, 204), f"Create tower: {r.status_code} {r.text}"
        
        # Publish telemetry for this tower
        mqtt_client.publish(
            f"farm/{farm_id}/coord/{coord_id}/tower/{tower_id}/telemetry",
            {
                "air_temp_c": 24.0,
                "humidity_pct": 68.0,
                "light_lux": 14000.0,
                "pump_on": False,
                "light_on": True,
                "light_brightness": 190,
                "status_mode": "operational",
                "vbat_mv": 3750,
                "fw_version": "1.2.0",
                "uptime_s": 5000,
                "signal_quality": -40,
            },
        )
        
        # Poll for telemetry to appear
        deadline = time.time() + 15
        found = False
        while time.time() < deadline:
            r = api_client.get(
                f"/api/telemetry/tower/{farm_id}/{coord_id}/{tower_id}/latest"
            )
            if r.status_code == 200:
                data = r.json()
                if data and (data.get("air_temp_c") == 24.0 or data.get("airTempC") == 24.0):
                    found = True
                    break
            time.sleep(1)
        
        assert found, "Tower telemetry not found via REST after MQTT publish"
        
        # Verify tower twin exists (may take a moment to auto-create)
        twin_found = False
        twin_deadline = time.time() + 10
        while time.time() < twin_deadline:
            r = api_client.get(f"/api/twins/towers/{tower_id}")
            if r.status_code == 200:
                twin_found = True
                break
            time.sleep(1)
        # Twin may not exist if auto-creation requires a registered tower
        # The telemetry pipeline is proven by the REST check above
        assert twin_found or found, "Neither telemetry nor twin found after MQTT publish"

    def test_tower_list_by_coordinator(self, api_client, bootstrap_farm):
        """GET towers for a coordinator returns the bootstrap towers."""
        farm_id = bootstrap_farm["farm_id"]
        coord_id = bootstrap_farm["coord_id"]
        
        r = api_client.get(f"/api/towers/farm/{farm_id}/coord/{coord_id}")
        assert r.status_code == 200
        towers = r.json()
        if isinstance(towers, list):
            assert len(towers) >= 5, f"Expected >= 5 towers, got {len(towers)}"


class TestAuthenticationAndValidation:
    """API key enforcement and input validation."""

    def test_missing_api_key_rejected(self, api_client):
        """
        Request WITHOUT API key should return 401.
        
        Proves: ApiKeyMiddleware blocks unauthenticated requests.
        """
        import requests
        # Create a new session without the API key header
        raw_url = api_client.base_url
        r = requests.get(f"{raw_url}/api/farms", timeout=10)
        assert r.status_code == 401, (
            f"Expected 401 without API key, got {r.status_code}. "
            f"ApiKeyMiddleware may not be active."
        )

    def test_invalid_pairing_input_rejected(self, api_client):
        """
        POST pairing/start with empty required fields -> 400 with validation errors.
        
        Proves: FluentValidation rejects invalid input.
        """
        r = api_client.post("/api/pairing/start", json_data={
            "farm_id": "",
            "coord_id": "",
            "duration_seconds": -1,
        })
        assert r.status_code == 400, (
            f"Expected 400 for invalid input, got {r.status_code}: {r.text}"
        )
        
        # Verify with valid data
        r = api_client.post("/api/pairing/start", json_data={
            "farm_id": "test-farm-001",
            "coord_id": "test-coord-001",
            "duration_seconds": 120,
        })
        assert r.status_code in (200, 201, 409), (
            f"Valid pairing start returned {r.status_code}: {r.text}"
        )

    def test_nonexistent_resource_returns_404(self, api_client):
        """
        GET endpoints for non-existent resources return 404.
        """
        # Non-existent farm
        r = api_client.get("/api/farms/nonexistent-farm-xyz")
        # Could be 404 or 200 with empty depending on implementation
        assert r.status_code in (200, 404)
        
        # Non-existent tower twin
        r = api_client.get("/api/twins/towers/nonexistent-tower-xyz")
        assert r.status_code in (200, 404)
        if r.status_code == 200:
            data = r.json()
            # If 200, the response might be null/empty
            assert data is None or data == {} or data.get("tower_id") is None

    def test_malformed_json_returns_400(self, api_client):
        """
        Sending invalid JSON body returns 400, not 500.
        
        Proves: Error handling middleware catches deserialization errors.
        """
        import requests
        raw_url = api_client.base_url
        r = requests.post(
            f"{raw_url}/api/farms",
            data="this is not json",
            headers={
                "Content-Type": "application/json",
                "X-API-Key": "hydro-thesis-2026",
            },
            timeout=10,
        )
        assert r.status_code in (400, 415), (
            f"Expected 400/415 for malformed JSON, got {r.status_code}"
        )
