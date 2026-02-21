/**
 * MongoDB seed script for the Digital Twin page.
 *
 * Run via:
 *   docker exec -i iot-mongodb mongosh -u admin -p admin123 --authenticationDatabase admin iot_smarttile < scripts/seed-data.js
 *
 * Seeds:
 *   - 1 farm
 *   - 2 coordinator_twins  (each with reservoir sensors)
 *   - 4 tower_twins         (2 per coordinator, with reported, desired, ML predictions)
 */

const now = new Date();
const oneHourAgo = new Date(now.getTime() - 3600_000);
const tenMinAgo  = new Date(now.getTime() - 600_000);
const twoMinAgo  = new Date(now.getTime() - 120_000);

// ============================================================================
// Farm
// ============================================================================
db.farms.deleteMany({});
db.farms.insertOne({
  farm_id:            "farm-alpha",
  name:               "Hydro Farm Alpha",
  description:        "Main vertical-farming facility",
  location:           "Building A, Lab 3",
  created_at:         oneHourAgo,
  last_seen:          twoMinAgo,
  coordinator_count:  2,
  tower_count:        4,
  active_alert_count: 0,
  auto_discovered:    false,
});

// ============================================================================
// Coordinator Twins
// ============================================================================
db.coordinator_twins.deleteMany({});

const coordAlpha = {
  _id:       "coord-01",
  coord_id:  "coord-01",
  site_id:   "site-1",
  farm_id:   "farm-alpha",
  name:      "Reservoir Unit A",
  reported: {
    fw_version:               "2.4.1",
    towers_online:            2,
    nodes_online:             2,
    wifi_rssi:                -42,
    status_mode:              "normal",
    uptime_s:                 NumberLong(86400),
    temp_c:                   22.3,
    ph:                       6.1,
    ec_ms_cm:                 1.45,
    tds_ppm:                  725,
    water_temp_c:             20.8,
    water_level_pct:          78.5,
    water_level_cm:           31.4,
    low_water_alert:          false,
    main_pump_on:             true,
    dosing_pump_ph_on:        false,
    dosing_pump_nutrient_on:  false,
  },
  desired: {
    main_pump_on:             true,
    dosing_pump_ph_on:        null,
    dosing_pump_nutrient_on:  null,
    status_mode:              "normal",
    setpoints: {
      ph_target:              6.0,
      ph_tolerance:           0.3,
      ec_target:              1.5,
      ec_tolerance:           0.2,
      water_level_min_pct:    20.0,
      water_temp_target_c:    20.0,
    },
  },
  metadata: {
    version:             NumberLong(5),
    sync_status:         "insync",
    last_reported_at:    twoMinAgo,
    last_desired_at:     tenMinAgo,
    last_sync_attempt:   twoMinAgo,
    sync_retry_count:    0,
    created_at:          oneHourAgo,
    updated_at:          twoMinAgo,
    is_connected:        true,
    connection_quality:  92,
  },
  capabilities: {
    ph_sensor:              true,
    ec_sensor:              true,
    water_temp_sensor:      true,
    water_level_sensor:     true,
    main_pump:              true,
    ph_dosing_pump:         true,
    nutrient_dosing_pump:   true,
    max_towers:             8,
    light_sensor:           true,
  },
};

const coordBeta = {
  _id:       "coord-02",
  coord_id:  "coord-02",
  site_id:   "site-1",
  farm_id:   "farm-alpha",
  name:      "Reservoir Unit B",
  reported: {
    fw_version:               "2.4.0",
    towers_online:            2,
    nodes_online:             2,
    wifi_rssi:                -55,
    status_mode:              "normal",
    uptime_s:                 NumberLong(43200),
    temp_c:                   23.1,
    ph:                       5.9,
    ec_ms_cm:                 1.62,
    tds_ppm:                  810,
    water_temp_c:             21.2,
    water_level_pct:          64.0,
    water_level_cm:           25.6,
    low_water_alert:          false,
    main_pump_on:             true,
    dosing_pump_ph_on:        false,
    dosing_pump_nutrient_on:  false,
  },
  desired: {
    main_pump_on:             true,
    dosing_pump_ph_on:        null,
    dosing_pump_nutrient_on:  null,
    status_mode:              "normal",
    setpoints: {
      ph_target:              6.0,
      ph_tolerance:           0.3,
      ec_target:              1.5,
      ec_tolerance:           0.2,
      water_level_min_pct:    20.0,
      water_temp_target_c:    20.0,
    },
  },
  metadata: {
    version:             NumberLong(3),
    sync_status:         "insync",
    last_reported_at:    twoMinAgo,
    last_desired_at:     tenMinAgo,
    last_sync_attempt:   twoMinAgo,
    sync_retry_count:    0,
    created_at:          oneHourAgo,
    updated_at:          twoMinAgo,
    is_connected:        true,
    connection_quality:  78,
  },
  capabilities: {
    ph_sensor:              true,
    ec_sensor:              true,
    water_temp_sensor:      true,
    water_level_sensor:     true,
    main_pump:              true,
    ph_dosing_pump:         false,
    nutrient_dosing_pump:   true,
    max_towers:             8,
    light_sensor:           false,
  },
};

db.coordinator_twins.insertMany([coordAlpha, coordBeta]);

// ============================================================================
// Tower Twins
// ============================================================================
db.tower_twins.deleteMany({});

function towerTwin(towerId, coordId, name, cropTypeInt, opts) {
  return {
    _id:        towerId,
    tower_id:   towerId,
    coord_id:   coordId,
    farm_id:    "farm-alpha",
    name:       name,
    reported: {
      air_temp_c:        opts.airTemp,
      humidity_pct:      opts.humidity,
      light_lux:         opts.light,
      pump_on:           true,
      light_on:          true,
      light_brightness:  80,
      status_mode:       "normal",
      vbat_mv:           opts.vbat,
      fw_version:        "1.8.3",
      uptime_s:          NumberLong(opts.uptime),
      signal_quality:    opts.signal,
    },
    desired: {
      pump_on:           true,
      light_on:          true,
      light_brightness:  80,
      status_mode:       "normal",
    },
    metadata: {
      version:             NumberLong(opts.version),
      sync_status:         opts.syncStatus ?? "insync",
      last_reported_at:    twoMinAgo,
      last_desired_at:     tenMinAgo,
      last_sync_attempt:   twoMinAgo,
      sync_retry_count:    0,
      created_at:          oneHourAgo,
      updated_at:          twoMinAgo,
      is_connected:        opts.connected ?? true,
      connection_quality:  opts.connQuality ?? 85,
    },
    capabilities: {
      dht_sensor:     true,
      light_sensor:   true,
      pump_relay:     true,
      grow_light:     true,
      slot_count:     6,
    },
    crop_type:              cropTypeInt,
    planting_date:          new Date(now.getTime() - 14 * 86400_000),   // 14 days ago
    last_height_cm:         opts.height,
    last_height_at:         twoMinAgo,
    predicted_height_cm:    opts.predHeight,
    expected_harvest_date:  new Date(now.getTime() + opts.daysLeft * 86400_000),
    ml_predictions: {
      predicted_height_cm:      opts.predHeight,
      expected_harvest_date:    new Date(now.getTime() + opts.daysLeft * 86400_000),
      days_to_harvest:          opts.daysLeft,
      growth_rate_cm_per_day:   opts.growthRate,
      health_score:             opts.healthScore,
      confidence:               opts.confidence,
      model_name:               "growth_rf_v3",
      model_version:            "3.1.0",
      last_updated_at:          twoMinAgo,
      input_avg_temp_c:         opts.airTemp,
      input_avg_humidity_pct:   opts.humidity,
      input_avg_light_lux:      opts.light,
    },
  };
}

const towers = [
  towerTwin("tower-A1", "coord-01", "Tower A1 - Lettuce", 1, {
    airTemp: 23.4, humidity: 68.2, light: 12500, vbat: 3320,
    uptime: 86000, signal: 90, version: 4,
    height: 14.2, predHeight: 18.5, daysLeft: 12, growthRate: 0.62,
    healthScore: 87, confidence: 0.91,
  }),
  towerTwin("tower-A2", "coord-01", "Tower A2 - Basil", 7, {
    airTemp: 24.1, humidity: 62.5, light: 14200, vbat: 3280,
    uptime: 85000, signal: 88, version: 3,
    height: 10.8, predHeight: 15.0, daysLeft: 18, growthRate: 0.45,
    healthScore: 92, confidence: 0.88,
  }),
  towerTwin("tower-B1", "coord-02", "Tower B1 - Kale", 3, {
    airTemp: 21.8, humidity: 71.0, light: 11800, vbat: 3150,
    uptime: 43000, signal: 72, version: 2,
    syncStatus: "pending", connected: true, connQuality: 72,
    height: 16.5, predHeight: 22.0, daysLeft: 8, growthRate: 0.78,
    healthScore: 74, confidence: 0.85,
  }),
  towerTwin("tower-B2", "coord-02", "Tower B2 - Spinach", 2, {
    airTemp: 22.0, humidity: 69.8, light: 10500, vbat: 2980,
    uptime: 42000, signal: 65, version: 2,
    connected: true, connQuality: 65,
    height: 8.3, predHeight: 12.0, daysLeft: 21, growthRate: 0.38,
    healthScore: 81, confidence: 0.82,
  }),
];

db.tower_twins.insertMany(towers);

// ============================================================================
// Create indexes (same as TwinRepository.cs)
// ============================================================================
db.tower_twins.createIndex({ farm_id: 1, coord_id: 1 }, { name: "farm_coord_idx" });
db.tower_twins.createIndex({ farm_id: 1 }, { name: "farm_idx" });
db.tower_twins.createIndex({ "metadata.sync_status": 1 }, { name: "sync_status_idx" });
db.tower_twins.createIndex({ "metadata.last_reported_at": 1 }, { name: "last_reported_idx" });

db.coordinator_twins.createIndex({ farm_id: 1 }, { name: "farm_idx" });
db.coordinator_twins.createIndex({ "metadata.sync_status": 1 }, { name: "sync_status_idx" });
db.coordinator_twins.createIndex({ "metadata.last_reported_at": 1 }, { name: "last_reported_idx" });

db.farms.createIndex({ farm_id: 1 }, { name: "farm_id_idx", unique: true });

print("--- Seed complete ---");
print("Farms:              " + db.farms.countDocuments());
print("Coordinator twins:  " + db.coordinator_twins.countDocuments());
print("Tower twins:        " + db.tower_twins.countDocuments());
