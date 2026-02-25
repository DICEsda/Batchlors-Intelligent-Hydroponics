#!/bin/bash
# ============================================================================
# Clear all IoT data from MongoDB
# Usage: bash tools/clear-db.sh [--keep-farms]
# ============================================================================

MONGO_USER="${MONGO_ROOT_USER:-admin}"
MONGO_PASS="${MONGO_ROOT_PASSWORD:-admin123}"
MONGO_DB="${MONGO_DB:-iot_smarttile}"
CONTAINER="iot-mongodb"
CONN="mongodb://${MONGO_USER}:${MONGO_PASS}@localhost:27017/${MONGO_DB}?authSource=admin"

# Collections to always clear
COLLECTIONS=(
  "coordinators"
  "coordinator_twins"
  "tower_twins"
  "reservoir_telemetry"
  "tower_telemetry"
  "alerts"
)

# Optionally keep farms
if [[ "$1" != "--keep-farms" ]]; then
  COLLECTIONS+=("farms")
fi

echo "Clearing MongoDB database: ${MONGO_DB}"
echo "Container: ${CONTAINER}"
echo "---"

for col in "${COLLECTIONS[@]}"; do
  count=$(docker exec "$CONTAINER" mongosh "$CONN" --quiet --eval "db.${col}.countDocuments()")
  docker exec "$CONTAINER" mongosh "$CONN" --quiet --eval "db.${col}.deleteMany({})"
  echo "  ${col}: deleted ${count} documents"
done

echo "---"
echo "Done. Restart backend to refresh in-memory caches:"
echo "  docker compose restart backend"
