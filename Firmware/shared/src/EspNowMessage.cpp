#include "EspNowMessage.h"

// --- JoinRequest ---
JoinRequestMessage::JoinRequestMessage() {
	type = MessageType::JOIN_REQUEST;
	msg = "join_request";
	ts = millis();
}

String JoinRequestMessage::toJson() const {
	DynamicJsonDocument doc(512);
	doc["msg"] = msg;
	doc["mac"] = mac;
	doc["fw"] = fw;
	doc["caps"]["rgbw"] = caps.rgbw;
	doc["caps"]["led_count"] = caps.led_count;
	doc["caps"]["temp_i2c"] = caps.temp_i2c;
	doc["caps"]["deep_sleep"] = caps.deep_sleep;
	doc["caps"]["button"] = caps.button;
	doc["token"] = token;
	String out; serializeJson(doc, out); return out;
}

bool JoinRequestMessage::fromJson(const String& json) {
	DynamicJsonDocument doc(512);
	DeserializationError err = deserializeJson(doc, json);
	if (err) return false;
	msg = doc["msg"].as<String>();
	mac = doc["mac"].as<String>();
	fw = doc["fw"].as<String>();
	caps.rgbw = doc["caps"]["rgbw"].as<bool>();
	caps.led_count = doc["caps"]["led_count"].as<uint8_t>();
	caps.temp_i2c = doc["caps"]["temp_i2c"] | false;
	caps.deep_sleep = doc["caps"]["deep_sleep"].as<bool>();
	caps.button = doc["caps"]["button"] | false;
	token = doc["token"].as<String>();
	return true;
}

// --- JoinAccept ---
JoinAcceptMessage::JoinAcceptMessage() {
	type = MessageType::JOIN_ACCEPT;
	msg = "join_accept";
	ts = millis();
	wifi_channel = 1; // Default to channel 1
	// Initialize config struct with defaults
	cfg.pwm_freq = 0;
	cfg.rx_window_ms = 20;
	cfg.rx_period_ms = 100;
}

String JoinAcceptMessage::toJson() const {
	DynamicJsonDocument doc(256);
	doc["msg"] = msg;
	doc["node_id"] = node_id;
	doc["light_id"] = light_id;
	doc["lmk"] = lmk;
	doc["wifi_channel"] = wifi_channel;
	doc["cfg"]["pwm_freq"] = cfg.pwm_freq;
	doc["cfg"]["rx_window_ms"] = cfg.rx_window_ms;
	doc["cfg"]["rx_period_ms"] = cfg.rx_period_ms;
	String out; serializeJson(doc, out); return out;
}

bool JoinAcceptMessage::fromJson(const String& json) {
	DynamicJsonDocument doc(768); // Increased to handle full join_accept with nested config
	DeserializationError err = deserializeJson(doc, json);
	if (err) {
		Serial.printf("JoinAccept parse error: %s (buffer may be too small)\n", err.c_str());
		Serial.printf("  Message length: %d bytes\n", json.length());
		return false;
	}
	msg = doc["msg"].as<String>();
	node_id = doc["node_id"].as<String>();
	light_id = doc["light_id"].as<String>();
	lmk = doc["lmk"].as<String>();
	wifi_channel = doc["wifi_channel"] | 1; // Default to 1 if missing
	cfg.pwm_freq = doc["cfg"]["pwm_freq"].as<int>();
	cfg.rx_window_ms = doc["cfg"]["rx_window_ms"].as<int>();
	cfg.rx_period_ms = doc["cfg"]["rx_period_ms"].as<int>();
	return true;
}

// --- SetLight ---
SetLightMessage::SetLightMessage() {
	type = MessageType::SET_LIGHT;
	msg = "set_light";
	ts = millis();
}

String SetLightMessage::toJson() const {
	DynamicJsonDocument doc(384);
	doc["msg"] = msg;
	doc["cmd_id"] = cmd_id;
	doc["light_id"] = light_id;
	doc["r"] = r; doc["g"] = g; doc["b"] = b; doc["w"] = w;
	doc["value"] = value;
	doc["fade_ms"] = fade_ms;
	doc["override_status"] = override_status;
	doc["ttl_ms"] = ttl_ms;
	doc["pixel"] = pixel; // -1 = all pixels, 0-3 = specific pixel
	if (reason.length()) doc["reason"] = reason;
	String out; serializeJson(doc, out); return out;
}

bool SetLightMessage::fromJson(const String& json) {
	DynamicJsonDocument doc(384);
	DeserializationError err = deserializeJson(doc, json);
	if (err) return false;
	msg = doc["msg"].as<String>();
	cmd_id = doc["cmd_id"].as<String>();
	light_id = doc["light_id"].as<String>();
	// Fix: Use ternary operator or explicit checks instead of logical OR for default values
	r = doc.containsKey("r") ? doc["r"].as<uint8_t>() : 0;
	g = doc.containsKey("g") ? doc["g"].as<uint8_t>() : 0;
	b = doc.containsKey("b") ? doc["b"].as<uint8_t>() : 0;
	w = doc.containsKey("w") ? doc["w"].as<uint8_t>() : 0;
	value = doc.containsKey("value") ? doc["value"].as<uint8_t>() : 0;
	fade_ms = doc.containsKey("fade_ms") ? doc["fade_ms"].as<uint16_t>() : 0;
	override_status = doc["override_status"] | false;
	ttl_ms = doc.containsKey("ttl_ms") ? doc["ttl_ms"].as<uint16_t>() : 1500;
	reason = doc["reason"].as<String>();
	pixel = doc.containsKey("pixel") ? doc["pixel"].as<int8_t>() : -1;
	return true;
}

// --- NodeStatus ---
NodeStatusMessage::NodeStatusMessage() {
	type = MessageType::NODE_STATUS;
	msg = "node_status";
	ts = millis();
}

String NodeStatusMessage::toJson() const {
	DynamicJsonDocument doc(384);
	doc["msg"] = msg;
	doc["node_id"] = node_id;
	doc["light_id"] = light_id;
	doc["avg_r"] = avg_r;
	doc["avg_g"] = avg_g;
	doc["avg_b"] = avg_b;
	doc["avg_w"] = avg_w;
	doc["status_mode"] = status_mode;
	doc["vbat_mv"] = vbat_mv;
	doc["temperature"] = temperature;
	doc["button_pressed"] = button_pressed;
	doc["fw"] = fw;
	doc["ts"] = ts;
	String out; serializeJson(doc, out); return out;
}

bool NodeStatusMessage::fromJson(const String& json) {
	DynamicJsonDocument doc(384); // Increased from 256 to handle 220-byte payload + overhead
	DeserializationError err = deserializeJson(doc, json);
	if (err) return false;
	msg = doc["msg"].as<String>();
	node_id = doc["node_id"].as<String>();
	light_id = doc["light_id"].as<String>();
	// Fix: Use proper default value checks
	avg_r = doc.containsKey("avg_r") ? doc["avg_r"].as<uint8_t>() : 0;
	avg_g = doc.containsKey("avg_g") ? doc["avg_g"].as<uint8_t>() : 0;
	avg_b = doc.containsKey("avg_b") ? doc["avg_b"].as<uint8_t>() : 0;
	avg_w = doc.containsKey("avg_w") ? doc["avg_w"].as<uint8_t>() : 0;
	status_mode = doc["status_mode"].as<String>();
	vbat_mv = doc.containsKey("vbat_mv") ? doc["vbat_mv"].as<uint16_t>() : 0;
	temperature = doc.containsKey("temperature") ? doc["temperature"].as<float>() : 0.0f;
	button_pressed = doc.containsKey("button_pressed") ? doc["button_pressed"].as<bool>() : false;
	fw = doc["fw"].as<String>();
	ts = doc.containsKey("ts") ? doc["ts"].as<uint32_t>() : millis();
	return true;
}

// --- Error ---
ErrorMessage::ErrorMessage() {
	type = MessageType::ERROR;
	msg = "error";
	ts = millis();
}

String ErrorMessage::toJson() const {
	DynamicJsonDocument doc(192);
	doc["msg"] = msg;
	doc["node_id"] = node_id;
	doc["code"] = code;
	doc["info"] = info;
	String out; serializeJson(doc, out); return out;
}

bool ErrorMessage::fromJson(const String& json) {
	DynamicJsonDocument doc(192);
	DeserializationError err = deserializeJson(doc, json);
	if (err) return false;
	msg = doc["msg"].as<String>();
	node_id = doc["node_id"].as<String>();
	code = doc["code"].as<String>();
	info = doc["info"].as<String>();
	return true;
}

// --- Ack ---
AckMessage::AckMessage() {
	type = MessageType::ACK;
	msg = "ack";
	ts = millis();
}

String AckMessage::toJson() const {
	DynamicJsonDocument doc(96);
	doc["msg"] = msg;
	doc["cmd_id"] = cmd_id;
	String out; serializeJson(doc, out); return out;
}

bool AckMessage::fromJson(const String& json) {
	DynamicJsonDocument doc(96);
	DeserializationError err = deserializeJson(doc, json);
	if (err) return false;
	msg = doc["msg"].as<String>();
	cmd_id = doc["cmd_id"].as<String>();
	return true;
}

// ============================================================================
// HYDROPONIC SYSTEM MESSAGE IMPLEMENTATIONS
// ============================================================================

// --- TowerJoinRequest ---
TowerJoinRequestMessage::TowerJoinRequestMessage() {
	type = MessageType::TOWER_JOIN_REQUEST;
	msg = "tower_join_request";
	ts = millis();
	caps.dht_sensor = false;
	caps.light_sensor = false;
	caps.pump_relay = false;
	caps.grow_light = false;
	caps.slot_count = 6;
}

String TowerJoinRequestMessage::toJson() const {
	DynamicJsonDocument doc(384);
	doc["msg"] = msg;
	doc["mac"] = mac;
	doc["fw"] = fw;
	doc["caps"]["dht_sensor"] = caps.dht_sensor;
	doc["caps"]["light_sensor"] = caps.light_sensor;
	doc["caps"]["pump_relay"] = caps.pump_relay;
	doc["caps"]["grow_light"] = caps.grow_light;
	doc["caps"]["slot_count"] = caps.slot_count;
	doc["token"] = token;
	doc["ts"] = ts;
	String out; serializeJson(doc, out); return out;
}

bool TowerJoinRequestMessage::fromJson(const String& json) {
	DynamicJsonDocument doc(384);
	DeserializationError err = deserializeJson(doc, json);
	if (err) return false;
	msg = doc["msg"].as<String>();
	mac = doc["mac"].as<String>();
	fw = doc["fw"].as<String>();
	caps.dht_sensor = doc["caps"]["dht_sensor"] | false;
	caps.light_sensor = doc["caps"]["light_sensor"] | false;
	caps.pump_relay = doc["caps"]["pump_relay"] | false;
	caps.grow_light = doc["caps"]["grow_light"] | false;
	caps.slot_count = doc["caps"]["slot_count"] | 6;
	token = doc["token"].as<String>();
	ts = doc["ts"] | millis();
	return true;
}

// --- TowerJoinAccept ---
TowerJoinAcceptMessage::TowerJoinAcceptMessage() {
	type = MessageType::TOWER_JOIN_ACCEPT;
	msg = "tower_join_accept";
	ts = millis();
	wifi_channel = 1;
	cfg.telemetry_interval_ms = 30000;  // 30 seconds default
	cfg.pump_max_duration_s = 300;      // 5 minutes max pump time
}

String TowerJoinAcceptMessage::toJson() const {
	DynamicJsonDocument doc(384);
	doc["msg"] = msg;
	doc["tower_id"] = tower_id;
	doc["coord_id"] = coord_id;
	doc["farm_id"] = farm_id;
	doc["lmk"] = lmk;
	doc["wifi_channel"] = wifi_channel;
	doc["cfg"]["telemetry_interval_ms"] = cfg.telemetry_interval_ms;
	doc["cfg"]["pump_max_duration_s"] = cfg.pump_max_duration_s;
	doc["ts"] = ts;
	String out; serializeJson(doc, out); return out;
}

bool TowerJoinAcceptMessage::fromJson(const String& json) {
	DynamicJsonDocument doc(512);
	DeserializationError err = deserializeJson(doc, json);
	if (err) {
		Serial.printf("TowerJoinAccept parse error: %s\n", err.c_str());
		return false;
	}
	msg = doc["msg"].as<String>();
	tower_id = doc["tower_id"].as<String>();
	coord_id = doc["coord_id"].as<String>();
	farm_id = doc["farm_id"].as<String>();
	lmk = doc["lmk"].as<String>();
	wifi_channel = doc["wifi_channel"] | 1;
	cfg.telemetry_interval_ms = doc["cfg"]["telemetry_interval_ms"] | 30000;
	cfg.pump_max_duration_s = doc["cfg"]["pump_max_duration_s"] | 300;
	ts = doc["ts"] | millis();
	return true;
}

// --- TowerTelemetry ---
TowerTelemetryMessage::TowerTelemetryMessage() {
	type = MessageType::TOWER_TELEMETRY;
	msg = "tower_telemetry";
	ts = millis();
	air_temp_c = 0.0f;
	humidity_pct = 0.0f;
	light_lux = 0.0f;
	pump_on = false;
	light_on = false;
	light_brightness = 0;
	status_mode = "operational";
	vbat_mv = 0;
	uptime_s = 0;
}

String TowerTelemetryMessage::toJson() const {
	DynamicJsonDocument doc(512);
	doc["msg"] = msg;
	doc["tower_id"] = tower_id;
	doc["air_temp_c"] = air_temp_c;
	doc["humidity_pct"] = humidity_pct;
	doc["light_lux"] = light_lux;
	doc["pump_on"] = pump_on;
	doc["light_on"] = light_on;
	doc["light_brightness"] = light_brightness;
	doc["status_mode"] = status_mode;
	doc["vbat_mv"] = vbat_mv;
	doc["fw"] = fw;
	doc["uptime_s"] = uptime_s;
	doc["ts"] = ts;
	String out; serializeJson(doc, out); return out;
}

bool TowerTelemetryMessage::fromJson(const String& json) {
	DynamicJsonDocument doc(512);
	DeserializationError err = deserializeJson(doc, json);
	if (err) return false;
	msg = doc["msg"].as<String>();
	tower_id = doc["tower_id"].as<String>();
	air_temp_c = doc["air_temp_c"] | 0.0f;
	humidity_pct = doc["humidity_pct"] | 0.0f;
	light_lux = doc["light_lux"] | 0.0f;
	pump_on = doc["pump_on"] | false;
	light_on = doc["light_on"] | false;
	light_brightness = doc["light_brightness"] | 0;
	status_mode = doc["status_mode"].as<String>();
	vbat_mv = doc["vbat_mv"] | 0;
	fw = doc["fw"].as<String>();
	uptime_s = doc["uptime_s"] | 0;
	ts = doc["ts"] | millis();
	return true;
}

// --- TowerCommand ---
TowerCommandMessage::TowerCommandMessage() {
	type = MessageType::TOWER_COMMAND;
	msg = "tower_command";
	ts = millis();
	pump_on = false;
	pump_duration_s = 0;
	light_on = false;
	light_brightness = 0;
	light_duration_m = 0;
	ttl_ms = 5000;  // 5 second default TTL
}

String TowerCommandMessage::toJson() const {
	DynamicJsonDocument doc(512);
	doc["msg"] = msg;
	doc["cmd_id"] = cmd_id;
	doc["tower_id"] = tower_id;
	doc["command"] = command;
	
	// Pump control fields
	if (command == "set_pump") {
		doc["pump_on"] = pump_on;
		doc["pump_duration_s"] = pump_duration_s;
	}
	
	// Light control fields
	if (command == "set_light") {
		doc["light_on"] = light_on;
		doc["light_brightness"] = light_brightness;
		doc["light_duration_m"] = light_duration_m;
	}
	
	// OTA fields
	if (command == "ota") {
		doc["ota_url"] = ota_url;
		doc["ota_checksum"] = ota_checksum;
	}
	
	doc["ttl_ms"] = ttl_ms;
	doc["ts"] = ts;
	String out; serializeJson(doc, out); return out;
}

bool TowerCommandMessage::fromJson(const String& json) {
	DynamicJsonDocument doc(512);
	DeserializationError err = deserializeJson(doc, json);
	if (err) return false;
	msg = doc["msg"].as<String>();
	cmd_id = doc["cmd_id"].as<String>();
	tower_id = doc["tower_id"].as<String>();
	command = doc["command"].as<String>();
	
	// Pump control
	pump_on = doc["pump_on"] | false;
	pump_duration_s = doc["pump_duration_s"] | 0;
	
	// Light control
	light_on = doc["light_on"] | false;
	light_brightness = doc["light_brightness"] | 0;
	light_duration_m = doc["light_duration_m"] | 0;
	
	// OTA
	ota_url = doc["ota_url"].as<String>();
	ota_checksum = doc["ota_checksum"].as<String>();
	
	ttl_ms = doc["ttl_ms"] | 5000;
	ts = doc["ts"] | millis();
	return true;
}

// --- ReservoirTelemetry ---
ReservoirTelemetryMessage::ReservoirTelemetryMessage() {
	type = MessageType::RESERVOIR_TELEMETRY;
	msg = "reservoir_telemetry";
	ts = millis();
	ph = 0.0f;
	ec_ms_cm = 0.0f;
	tds_ppm = 0.0f;
	water_temp_c = 0.0f;
	water_level_pct = 0.0f;
	water_level_cm = 0.0f;
	low_water_alert = false;
	main_pump_on = false;
	dosing_pump_ph_on = false;
	dosing_pump_nutrient_on = false;
	status_mode = "operational";
	uptime_s = 0;
}

String ReservoirTelemetryMessage::toJson() const {
	DynamicJsonDocument doc(512);
	doc["msg"] = msg;
	doc["coord_id"] = coord_id;
	doc["farm_id"] = farm_id;
	doc["ph"] = ph;
	doc["ec_ms_cm"] = ec_ms_cm;
	doc["tds_ppm"] = tds_ppm;
	doc["water_temp_c"] = water_temp_c;
	doc["water_level_pct"] = water_level_pct;
	doc["water_level_cm"] = water_level_cm;
	doc["low_water_alert"] = low_water_alert;
	doc["main_pump_on"] = main_pump_on;
	doc["dosing_pump_ph_on"] = dosing_pump_ph_on;
	doc["dosing_pump_nutrient_on"] = dosing_pump_nutrient_on;
	doc["status_mode"] = status_mode;
	doc["uptime_s"] = uptime_s;
	doc["ts"] = ts;
	String out; serializeJson(doc, out); return out;
}

bool ReservoirTelemetryMessage::fromJson(const String& json) {
	DynamicJsonDocument doc(512);
	DeserializationError err = deserializeJson(doc, json);
	if (err) return false;
	msg = doc["msg"].as<String>();
	coord_id = doc["coord_id"].as<String>();
	farm_id = doc["farm_id"].as<String>();
	ph = doc["ph"] | 0.0f;
	ec_ms_cm = doc["ec_ms_cm"] | 0.0f;
	tds_ppm = doc["tds_ppm"] | 0.0f;
	water_temp_c = doc["water_temp_c"] | 0.0f;
	water_level_pct = doc["water_level_pct"] | 0.0f;
	water_level_cm = doc["water_level_cm"] | 0.0f;
	low_water_alert = doc["low_water_alert"] | false;
	main_pump_on = doc["main_pump_on"] | false;
	dosing_pump_ph_on = doc["dosing_pump_ph_on"] | false;
	dosing_pump_nutrient_on = doc["dosing_pump_nutrient_on"] | false;
	status_mode = doc["status_mode"].as<String>();
	uptime_s = doc["uptime_s"] | 0;
	ts = doc["ts"] | millis();
	return true;
}

// ============================================================================
// V2 PAIRING PROTOCOL MESSAGE IMPLEMENTATIONS
// ============================================================================

// Helper functions for MAC address conversion
String macToString(const uint8_t* mac) {
	char buf[18];
	snprintf(buf, sizeof(buf), "%02X:%02X:%02X:%02X:%02X:%02X",
		mac[0], mac[1], mac[2], mac[3], mac[4], mac[5]);
	return String(buf);
}

bool stringToMac(const String& str, uint8_t* mac) {
	if (str.length() != 17) return false;
	unsigned int values[6];
	if (sscanf(str.c_str(), "%02X:%02X:%02X:%02X:%02X:%02X",
		&values[0], &values[1], &values[2], &values[3], &values[4], &values[5]) != 6) {
		return false;
	}
	for (int i = 0; i < 6; i++) {
		mac[i] = (uint8_t)values[i];
	}
	return true;
}

// --- PairingAdvertisement ---
PairingAdvertisementMessage::PairingAdvertisementMessage() {
	type = MessageType::PAIRING_ADVERTISEMENT;
	msg = "pairing_advertisement";
	ts = millis();
	protocol_version = PairingConstants::PROTOCOL_VERSION;
	memset(node_mac, 0, 6);
	device_type = DeviceType::TOWER;
	firmware_version = 0;
	capabilities = 0;
	nonce = 0;
	sequence_num = 0;
	rssi_request = 0;
}

String PairingAdvertisementMessage::toJson() const {
	DynamicJsonDocument doc(384);
	doc["msg"] = msg;
	doc["protocol_version"] = protocol_version;
	doc["node_mac"] = macToString(node_mac);
	doc["device_type"] = static_cast<uint8_t>(device_type);
	doc["firmware_version"] = firmware_version;
	doc["capabilities"] = capabilities;
	doc["nonce"] = nonce;
	doc["sequence_num"] = sequence_num;
	doc["rssi_request"] = rssi_request;
	doc["ts"] = ts;
	String out; serializeJson(doc, out); return out;
}

bool PairingAdvertisementMessage::fromJson(const String& json) {
	DynamicJsonDocument doc(384);
	DeserializationError err = deserializeJson(doc, json);
	if (err) return false;
	msg = doc["msg"].as<String>();
	protocol_version = doc["protocol_version"] | PairingConstants::PROTOCOL_VERSION;
	String macStr = doc["node_mac"].as<String>();
	stringToMac(macStr, node_mac);
	device_type = static_cast<DeviceType>(doc["device_type"] | 1);
	firmware_version = doc["firmware_version"] | 0;
	capabilities = doc["capabilities"] | 0;
	nonce = doc["nonce"] | 0;
	sequence_num = doc["sequence_num"] | 0;
	rssi_request = doc["rssi_request"] | 0;
	ts = doc["ts"] | millis();
	return true;
}

size_t PairingAdvertisementMessage::toBinary(uint8_t* buffer, size_t maxLen) const {
	if (maxLen < BINARY_SIZE) return 0;
	size_t pos = 0;
	buffer[pos++] = 0x20; // PAIRING_ADVERTISEMENT message type marker
	buffer[pos++] = protocol_version;
	memcpy(&buffer[pos], node_mac, 6); pos += 6;
	buffer[pos++] = static_cast<uint8_t>(device_type);
	memcpy(&buffer[pos], &firmware_version, 4); pos += 4;
	memcpy(&buffer[pos], &capabilities, 2); pos += 2;
	memcpy(&buffer[pos], &nonce, 4); pos += 4;
	memcpy(&buffer[pos], &sequence_num, 2); pos += 2;
	buffer[pos++] = static_cast<uint8_t>(rssi_request);
	return pos;
}

bool PairingAdvertisementMessage::fromBinary(const uint8_t* buffer, size_t len) {
	if (len < BINARY_SIZE || buffer[0] != 0x20) return false;
	size_t pos = 1;
	protocol_version = buffer[pos++];
	memcpy(node_mac, &buffer[pos], 6); pos += 6;
	device_type = static_cast<DeviceType>(buffer[pos++]);
	memcpy(&firmware_version, &buffer[pos], 4); pos += 4;
	memcpy(&capabilities, &buffer[pos], 2); pos += 2;
	memcpy(&nonce, &buffer[pos], 4); pos += 4;
	memcpy(&sequence_num, &buffer[pos], 2); pos += 2;
	rssi_request = static_cast<int8_t>(buffer[pos++]);
	ts = millis();
	return true;
}

uint32_t PairingAdvertisementMessage::packFirmwareVersion(uint8_t major, uint8_t minor, uint16_t patch) {
	return (static_cast<uint32_t>(major) << 24) | 
	       (static_cast<uint32_t>(minor) << 16) | 
	       patch;
}

void PairingAdvertisementMessage::unpackFirmwareVersion(uint32_t packed, uint8_t& major, uint8_t& minor, uint16_t& patch) {
	major = (packed >> 24) & 0xFF;
	minor = (packed >> 16) & 0xFF;
	patch = packed & 0xFFFF;
}

// --- PairingOffer ---
PairingOfferMessage::PairingOfferMessage() {
	type = MessageType::PAIRING_OFFER;
	msg = "pairing_offer";
	ts = millis();
	protocol_version = PairingConstants::PROTOCOL_VERSION;
	memset(coord_mac, 0, 6);
	coord_id = 0;
	farm_id = 0;
	offered_tower_id = 0;
	nonce_echo = 0;
	offer_token = 0;
	channel = 1;
}

String PairingOfferMessage::toJson() const {
	DynamicJsonDocument doc(384);
	doc["msg"] = msg;
	doc["protocol_version"] = protocol_version;
	doc["coord_mac"] = macToString(coord_mac);
	doc["coord_id"] = coord_id;
	doc["farm_id"] = farm_id;
	doc["offered_tower_id"] = offered_tower_id;
	doc["nonce_echo"] = nonce_echo;
	doc["offer_token"] = offer_token;
	doc["channel"] = channel;
	doc["ts"] = ts;
	String out; serializeJson(doc, out); return out;
}

bool PairingOfferMessage::fromJson(const String& json) {
	DynamicJsonDocument doc(384);
	DeserializationError err = deserializeJson(doc, json);
	if (err) return false;
	msg = doc["msg"].as<String>();
	protocol_version = doc["protocol_version"] | PairingConstants::PROTOCOL_VERSION;
	String macStr = doc["coord_mac"].as<String>();
	stringToMac(macStr, coord_mac);
	coord_id = doc["coord_id"] | 0;
	farm_id = doc["farm_id"] | 0;
	offered_tower_id = doc["offered_tower_id"] | 0;
	nonce_echo = doc["nonce_echo"] | 0;
	offer_token = doc["offer_token"] | 0;
	channel = doc["channel"] | 1;
	ts = doc["ts"] | millis();
	return true;
}

size_t PairingOfferMessage::toBinary(uint8_t* buffer, size_t maxLen) const {
	if (maxLen < BINARY_SIZE) return 0;
	size_t pos = 0;
	buffer[pos++] = 0x21; // PAIRING_OFFER message type marker
	buffer[pos++] = protocol_version;
	memcpy(&buffer[pos], coord_mac, 6); pos += 6;
	memcpy(&buffer[pos], &coord_id, 2); pos += 2;
	memcpy(&buffer[pos], &farm_id, 2); pos += 2;
	memcpy(&buffer[pos], &offered_tower_id, 2); pos += 2;
	memcpy(&buffer[pos], &nonce_echo, 4); pos += 4;
	memcpy(&buffer[pos], &offer_token, 4); pos += 4;
	buffer[pos++] = channel;
	return pos;
}

bool PairingOfferMessage::fromBinary(const uint8_t* buffer, size_t len) {
	if (len < BINARY_SIZE || buffer[0] != 0x21) return false;
	size_t pos = 1;
	protocol_version = buffer[pos++];
	memcpy(coord_mac, &buffer[pos], 6); pos += 6;
	memcpy(&coord_id, &buffer[pos], 2); pos += 2;
	memcpy(&farm_id, &buffer[pos], 2); pos += 2;
	memcpy(&offered_tower_id, &buffer[pos], 2); pos += 2;
	memcpy(&nonce_echo, &buffer[pos], 4); pos += 4;
	memcpy(&offer_token, &buffer[pos], 4); pos += 4;
	channel = buffer[pos++];
	ts = millis();
	return true;
}

// --- PairingAccept ---
PairingAcceptMessage::PairingAcceptMessage() {
	type = MessageType::PAIRING_ACCEPT;
	msg = "pairing_accept";
	ts = millis();
	memset(node_mac, 0, 6);
	offer_token = 0;
	accepted_tower_id = 0;
}

String PairingAcceptMessage::toJson() const {
	DynamicJsonDocument doc(256);
	doc["msg"] = msg;
	doc["node_mac"] = macToString(node_mac);
	doc["offer_token"] = offer_token;
	doc["accepted_tower_id"] = accepted_tower_id;
	doc["ts"] = ts;
	String out; serializeJson(doc, out); return out;
}

bool PairingAcceptMessage::fromJson(const String& json) {
	DynamicJsonDocument doc(256);
	DeserializationError err = deserializeJson(doc, json);
	if (err) return false;
	msg = doc["msg"].as<String>();
	String macStr = doc["node_mac"].as<String>();
	stringToMac(macStr, node_mac);
	offer_token = doc["offer_token"] | 0;
	accepted_tower_id = doc["accepted_tower_id"] | 0;
	ts = doc["ts"] | millis();
	return true;
}

size_t PairingAcceptMessage::toBinary(uint8_t* buffer, size_t maxLen) const {
	if (maxLen < BINARY_SIZE) return 0;
	size_t pos = 0;
	buffer[pos++] = 0x22; // PAIRING_ACCEPT message type marker
	memcpy(&buffer[pos], node_mac, 6); pos += 6;
	memcpy(&buffer[pos], &offer_token, 4); pos += 4;
	memcpy(&buffer[pos], &accepted_tower_id, 2); pos += 2;
	return pos;
}

bool PairingAcceptMessage::fromBinary(const uint8_t* buffer, size_t len) {
	if (len < BINARY_SIZE || buffer[0] != 0x22) return false;
	size_t pos = 1;
	memcpy(node_mac, &buffer[pos], 6); pos += 6;
	memcpy(&offer_token, &buffer[pos], 4); pos += 4;
	memcpy(&accepted_tower_id, &buffer[pos], 2); pos += 2;
	ts = millis();
	return true;
}

// --- PairingConfirm ---
PairingConfirmMessage::PairingConfirmMessage() {
	type = MessageType::PAIRING_CONFIRM;
	msg = "pairing_confirm";
	ts = millis();
	memset(coord_mac, 0, 6);
	tower_id = 0;
	memset(encryption_key, 0, 16);
	config_flags = 0;
}

String PairingConfirmMessage::toJson() const {
	DynamicJsonDocument doc(512);
	doc["msg"] = msg;
	doc["coord_mac"] = macToString(coord_mac);
	doc["tower_id"] = tower_id;
	// Encode encryption_key as hex string
	char keyHex[33];
	for (int i = 0; i < 16; i++) {
		snprintf(&keyHex[i*2], 3, "%02X", encryption_key[i]);
	}
	doc["encryption_key"] = String(keyHex);
	doc["config_flags"] = config_flags;
	doc["ts"] = ts;
	String out; serializeJson(doc, out); return out;
}

bool PairingConfirmMessage::fromJson(const String& json) {
	DynamicJsonDocument doc(512);
	DeserializationError err = deserializeJson(doc, json);
	if (err) return false;
	msg = doc["msg"].as<String>();
	String macStr = doc["coord_mac"].as<String>();
	stringToMac(macStr, coord_mac);
	tower_id = doc["tower_id"] | 0;
	// Decode encryption_key from hex string
	String keyHex = doc["encryption_key"].as<String>();
	if (keyHex.length() == 32) {
		for (int i = 0; i < 16; i++) {
			unsigned int byte;
			sscanf(keyHex.substring(i*2, i*2+2).c_str(), "%02X", &byte);
			encryption_key[i] = (uint8_t)byte;
		}
	}
	config_flags = doc["config_flags"] | 0;
	ts = doc["ts"] | millis();
	return true;
}

size_t PairingConfirmMessage::toBinary(uint8_t* buffer, size_t maxLen) const {
	if (maxLen < BINARY_SIZE) return 0;
	size_t pos = 0;
	buffer[pos++] = 0x23; // PAIRING_CONFIRM message type marker
	memcpy(&buffer[pos], coord_mac, 6); pos += 6;
	memcpy(&buffer[pos], &tower_id, 2); pos += 2;
	memcpy(&buffer[pos], encryption_key, 16); pos += 16;
	buffer[pos++] = config_flags;
	return pos;
}

bool PairingConfirmMessage::fromBinary(const uint8_t* buffer, size_t len) {
	if (len < BINARY_SIZE || buffer[0] != 0x23) return false;
	size_t pos = 1;
	memcpy(coord_mac, &buffer[pos], 6); pos += 6;
	memcpy(&tower_id, &buffer[pos], 2); pos += 2;
	memcpy(encryption_key, &buffer[pos], 16); pos += 16;
	config_flags = buffer[pos++];
	ts = millis();
	return true;
}

// --- PairingReject ---
PairingRejectMessage::PairingRejectMessage() {
	type = MessageType::PAIRING_REJECT;
	msg = "pairing_reject";
	ts = millis();
	memset(sender_mac, 0, 6);
	reason_code = PairingRejectReason::NONE;
	offer_token = 0;
}

String PairingRejectMessage::toJson() const {
	DynamicJsonDocument doc(256);
	doc["msg"] = msg;
	doc["sender_mac"] = macToString(sender_mac);
	doc["reason_code"] = static_cast<uint8_t>(reason_code);
	doc["offer_token"] = offer_token;
	doc["ts"] = ts;
	String out; serializeJson(doc, out); return out;
}

bool PairingRejectMessage::fromJson(const String& json) {
	DynamicJsonDocument doc(256);
	DeserializationError err = deserializeJson(doc, json);
	if (err) return false;
	msg = doc["msg"].as<String>();
	String macStr = doc["sender_mac"].as<String>();
	stringToMac(macStr, sender_mac);
	reason_code = static_cast<PairingRejectReason>(doc["reason_code"] | 0);
	offer_token = doc["offer_token"] | 0;
	ts = doc["ts"] | millis();
	return true;
}

size_t PairingRejectMessage::toBinary(uint8_t* buffer, size_t maxLen) const {
	if (maxLen < BINARY_SIZE) return 0;
	size_t pos = 0;
	buffer[pos++] = 0x24; // PAIRING_REJECT message type marker
	memcpy(&buffer[pos], sender_mac, 6); pos += 6;
	buffer[pos++] = static_cast<uint8_t>(reason_code);
	memcpy(&buffer[pos], &offer_token, 4); pos += 4;
	return pos;
}

bool PairingRejectMessage::fromBinary(const uint8_t* buffer, size_t len) {
	if (len < BINARY_SIZE || buffer[0] != 0x24) return false;
	size_t pos = 1;
	memcpy(sender_mac, &buffer[pos], 6); pos += 6;
	reason_code = static_cast<PairingRejectReason>(buffer[pos++]);
	memcpy(&offer_token, &buffer[pos], 4); pos += 4;
	ts = millis();
	return true;
}

// --- PairingAbort ---
PairingAbortMessage::PairingAbortMessage() {
	type = MessageType::PAIRING_ABORT;
	msg = "pairing_abort";
	ts = millis();
	memset(sender_mac, 0, 6);
	reason_code = PairingRejectReason::NODE_CANCELLED;
	offer_token = 0;
}

String PairingAbortMessage::toJson() const {
	DynamicJsonDocument doc(256);
	doc["msg"] = msg;
	doc["sender_mac"] = macToString(sender_mac);
	doc["reason_code"] = static_cast<uint8_t>(reason_code);
	doc["offer_token"] = offer_token;
	doc["ts"] = ts;
	String out; serializeJson(doc, out); return out;
}

bool PairingAbortMessage::fromJson(const String& json) {
	DynamicJsonDocument doc(256);
	DeserializationError err = deserializeJson(doc, json);
	if (err) return false;
	msg = doc["msg"].as<String>();
	String macStr = doc["sender_mac"].as<String>();
	stringToMac(macStr, sender_mac);
	reason_code = static_cast<PairingRejectReason>(doc["reason_code"] | 8);
	offer_token = doc["offer_token"] | 0;
	ts = doc["ts"] | millis();
	return true;
}

size_t PairingAbortMessage::toBinary(uint8_t* buffer, size_t maxLen) const {
	if (maxLen < BINARY_SIZE) return 0;
	size_t pos = 0;
	buffer[pos++] = 0x25; // PAIRING_ABORT message type marker
	memcpy(&buffer[pos], sender_mac, 6); pos += 6;
	buffer[pos++] = static_cast<uint8_t>(reason_code);
	memcpy(&buffer[pos], &offer_token, 4); pos += 4;
	return pos;
}

bool PairingAbortMessage::fromBinary(const uint8_t* buffer, size_t len) {
	if (len < BINARY_SIZE || buffer[0] != 0x25) return false;
	size_t pos = 1;
	memcpy(sender_mac, &buffer[pos], 6); pos += 6;
	reason_code = static_cast<PairingRejectReason>(buffer[pos++]);
	memcpy(&offer_token, &buffer[pos], 4); pos += 4;
	ts = millis();
	return true;
}

// ============================================================================
// OTA (OVER-THE-AIR) MESSAGE IMPLEMENTATIONS
// Binary message type markers: 0x30-0x34
// ============================================================================

// --- OtaBeginMessage (0x30) ---
OtaBeginMessage::OtaBeginMessage() {
	type = MessageType::OTA_BEGIN;
	msg = "ota_begin";
	firmware_size = 0;
	chunk_count = 0;
	chunk_size = OtaConstants::MAX_CHUNK_SIZE;
	checksum_type = OtaConstants::CHECKSUM_NONE;
	memset(checksum, 0, sizeof(checksum));
	firmware_version = 0;
}

String OtaBeginMessage::toJson() const {
	DynamicJsonDocument doc(512);
	doc["msg"] = msg;
	doc["firmware_size"] = firmware_size;
	doc["chunk_count"] = chunk_count;
	doc["chunk_size"] = chunk_size;
	doc["checksum_type"] = checksum_type;
	// Encode checksum as hex string
	String checksumHex;
	size_t checksumLen = (checksum_type == OtaConstants::CHECKSUM_MD5) ? 16 : 
	                     (checksum_type == OtaConstants::CHECKSUM_SHA256) ? 32 : 0;
	for (size_t i = 0; i < checksumLen; i++) {
		char hex[3];
		snprintf(hex, sizeof(hex), "%02x", checksum[i]);
		checksumHex += hex;
	}
	doc["checksum"] = checksumHex;
	doc["firmware_version"] = firmware_version;
	doc["ts"] = ts;
	String out; serializeJson(doc, out); return out;
}

bool OtaBeginMessage::fromJson(const String& json) {
	DynamicJsonDocument doc(512);
	DeserializationError err = deserializeJson(doc, json);
	if (err) return false;
	msg = doc["msg"].as<String>();
	firmware_size = doc["firmware_size"] | 0;
	chunk_count = doc["chunk_count"] | 0;
	chunk_size = doc["chunk_size"] | OtaConstants::MAX_CHUNK_SIZE;
	checksum_type = doc["checksum_type"] | OtaConstants::CHECKSUM_NONE;
	// Decode checksum from hex string
	String checksumHex = doc["checksum"].as<String>();
	memset(checksum, 0, sizeof(checksum));
	size_t checksumLen = checksumHex.length() / 2;
	if (checksumLen > 32) checksumLen = 32;
	for (size_t i = 0; i < checksumLen; i++) {
		char hexByte[3] = { checksumHex[i*2], checksumHex[i*2+1], 0 };
		checksum[i] = (uint8_t)strtol(hexByte, nullptr, 16);
	}
	firmware_version = doc["firmware_version"] | 0;
	ts = doc["ts"] | millis();
	return true;
}

size_t OtaBeginMessage::toBinary(uint8_t* buffer, size_t maxLen) const {
	if (maxLen < BINARY_SIZE) return 0;
	size_t pos = 0;
	buffer[pos++] = 0x30; // OTA_BEGIN message type marker
	memcpy(&buffer[pos], &firmware_size, 4); pos += 4;
	memcpy(&buffer[pos], &chunk_count, 2); pos += 2;
	memcpy(&buffer[pos], &chunk_size, 2); pos += 2;
	buffer[pos++] = checksum_type;
	memcpy(&buffer[pos], checksum, 32); pos += 32;
	memcpy(&buffer[pos], &firmware_version, 4); pos += 4;
	return pos; // 45 bytes total
}

bool OtaBeginMessage::fromBinary(const uint8_t* buffer, size_t len) {
	if (len < BINARY_SIZE || buffer[0] != 0x30) return false;
	size_t pos = 1;
	memcpy(&firmware_size, &buffer[pos], 4); pos += 4;
	memcpy(&chunk_count, &buffer[pos], 2); pos += 2;
	memcpy(&chunk_size, &buffer[pos], 2); pos += 2;
	checksum_type = buffer[pos++];
	memcpy(checksum, &buffer[pos], 32); pos += 32;
	memcpy(&firmware_version, &buffer[pos], 4); pos += 4;
	ts = millis();
	return true;
}

// --- OtaChunkMessage (0x31) ---
OtaChunkMessage::OtaChunkMessage() {
	type = MessageType::OTA_CHUNK;
	msg = "ota_chunk";
	chunk_index = 0;
	data_len = 0;
	memset(data, 0, sizeof(data));
}

String OtaChunkMessage::toJson() const {
	DynamicJsonDocument doc(1024);
	doc["msg"] = msg;
	doc["chunk_index"] = chunk_index;
	doc["data_len"] = data_len;
	// Encode data as base64 for JSON transport
	String dataB64;
	// Simple base64 encoding - each byte becomes 2 hex chars (simpler than base64)
	for (size_t i = 0; i < data_len; i++) {
		char hex[3];
		snprintf(hex, sizeof(hex), "%02x", data[i]);
		dataB64 += hex;
	}
	doc["data"] = dataB64;
	doc["ts"] = ts;
	String out; serializeJson(doc, out); return out;
}

bool OtaChunkMessage::fromJson(const String& json) {
	DynamicJsonDocument doc(1024);
	DeserializationError err = deserializeJson(doc, json);
	if (err) return false;
	msg = doc["msg"].as<String>();
	chunk_index = doc["chunk_index"] | 0;
	data_len = doc["data_len"] | 0;
	if (data_len > OtaConstants::MAX_CHUNK_SIZE) data_len = OtaConstants::MAX_CHUNK_SIZE;
	// Decode hex data
	String dataHex = doc["data"].as<String>();
	memset(data, 0, sizeof(data));
	for (size_t i = 0; i < data_len && i*2+1 < dataHex.length(); i++) {
		char hexByte[3] = { dataHex[i*2], dataHex[i*2+1], 0 };
		data[i] = (uint8_t)strtol(hexByte, nullptr, 16);
	}
	ts = doc["ts"] | millis();
	return true;
}

size_t OtaChunkMessage::toBinary(uint8_t* buffer, size_t maxLen) const {
	size_t totalSize = BINARY_HEADER_SIZE + data_len;
	if (maxLen < totalSize) return 0;
	size_t pos = 0;
	buffer[pos++] = 0x31; // OTA_CHUNK message type marker
	memcpy(&buffer[pos], &chunk_index, 2); pos += 2;
	buffer[pos++] = data_len;
	memcpy(&buffer[pos], data, data_len); pos += data_len;
	return pos;
}

bool OtaChunkMessage::fromBinary(const uint8_t* buffer, size_t len) {
	if (len < BINARY_HEADER_SIZE || buffer[0] != 0x31) return false;
	size_t pos = 1;
	memcpy(&chunk_index, &buffer[pos], 2); pos += 2;
	data_len = buffer[pos++];
	if (data_len > OtaConstants::MAX_CHUNK_SIZE) return false;
	if (len < BINARY_HEADER_SIZE + data_len) return false;
	memcpy(data, &buffer[pos], data_len);
	ts = millis();
	return true;
}

// --- OtaChunkAckMessage (0x32) ---
OtaChunkAckMessage::OtaChunkAckMessage() {
	type = MessageType::OTA_CHUNK_ACK;
	msg = "ota_chunk_ack";
	chunk_index = 0;
	status = 0; // OK
	next_expected = 0;
}

String OtaChunkAckMessage::toJson() const {
	DynamicJsonDocument doc(256);
	doc["msg"] = msg;
	doc["chunk_index"] = chunk_index;
	doc["status"] = status;
	doc["next_expected"] = next_expected;
	doc["ts"] = ts;
	String out; serializeJson(doc, out); return out;
}

bool OtaChunkAckMessage::fromJson(const String& json) {
	DynamicJsonDocument doc(256);
	DeserializationError err = deserializeJson(doc, json);
	if (err) return false;
	msg = doc["msg"].as<String>();
	chunk_index = doc["chunk_index"] | 0;
	status = doc["status"] | 0;
	next_expected = doc["next_expected"] | 0;
	ts = doc["ts"] | millis();
	return true;
}

size_t OtaChunkAckMessage::toBinary(uint8_t* buffer, size_t maxLen) const {
	if (maxLen < BINARY_SIZE) return 0;
	size_t pos = 0;
	buffer[pos++] = 0x32; // OTA_CHUNK_ACK message type marker
	memcpy(&buffer[pos], &chunk_index, 2); pos += 2;
	buffer[pos++] = status;
	buffer[pos++] = next_expected;
	return pos; // 5 bytes total
}

bool OtaChunkAckMessage::fromBinary(const uint8_t* buffer, size_t len) {
	if (len < BINARY_SIZE || buffer[0] != 0x32) return false;
	size_t pos = 1;
	memcpy(&chunk_index, &buffer[pos], 2); pos += 2;
	status = buffer[pos++];
	next_expected = buffer[pos++];
	ts = millis();
	return true;
}

// --- OtaAbortMessage (0x33) ---
OtaAbortMessage::OtaAbortMessage() {
	type = MessageType::OTA_ABORT;
	msg = "ota_abort";
	reason = OtaAbortReason::NONE;
	last_chunk = 0;
}

String OtaAbortMessage::toJson() const {
	DynamicJsonDocument doc(256);
	doc["msg"] = msg;
	doc["reason"] = static_cast<uint8_t>(reason);
	doc["last_chunk"] = last_chunk;
	doc["ts"] = ts;
	String out; serializeJson(doc, out); return out;
}

bool OtaAbortMessage::fromJson(const String& json) {
	DynamicJsonDocument doc(256);
	DeserializationError err = deserializeJson(doc, json);
	if (err) return false;
	msg = doc["msg"].as<String>();
	reason = static_cast<OtaAbortReason>(doc["reason"] | 0);
	last_chunk = doc["last_chunk"] | 0;
	ts = doc["ts"] | millis();
	return true;
}

size_t OtaAbortMessage::toBinary(uint8_t* buffer, size_t maxLen) const {
	if (maxLen < BINARY_SIZE) return 0;
	size_t pos = 0;
	buffer[pos++] = 0x33; // OTA_ABORT message type marker
	buffer[pos++] = static_cast<uint8_t>(reason);
	memcpy(&buffer[pos], &last_chunk, 2); pos += 2;
	return pos; // 4 bytes total
}

bool OtaAbortMessage::fromBinary(const uint8_t* buffer, size_t len) {
	if (len < BINARY_SIZE || buffer[0] != 0x33) return false;
	size_t pos = 1;
	reason = static_cast<OtaAbortReason>(buffer[pos++]);
	memcpy(&last_chunk, &buffer[pos], 2); pos += 2;
	ts = millis();
	return true;
}

// --- OtaCompleteMessage (0x34) ---
OtaCompleteMessage::OtaCompleteMessage() {
	type = MessageType::OTA_COMPLETE;
	msg = "ota_complete";
	status = 0; // Success
	will_reboot = 1; // Default: will reboot
}

String OtaCompleteMessage::toJson() const {
	DynamicJsonDocument doc(256);
	doc["msg"] = msg;
	doc["status"] = status;
	doc["will_reboot"] = will_reboot;
	doc["ts"] = ts;
	String out; serializeJson(doc, out); return out;
}

bool OtaCompleteMessage::fromJson(const String& json) {
	DynamicJsonDocument doc(256);
	DeserializationError err = deserializeJson(doc, json);
	if (err) return false;
	msg = doc["msg"].as<String>();
	status = doc["status"] | 0;
	will_reboot = doc["will_reboot"] | 1;
	ts = doc["ts"] | millis();
	return true;
}

size_t OtaCompleteMessage::toBinary(uint8_t* buffer, size_t maxLen) const {
	if (maxLen < BINARY_SIZE) return 0;
	size_t pos = 0;
	buffer[pos++] = 0x34; // OTA_COMPLETE message type marker
	buffer[pos++] = status;
	buffer[pos++] = will_reboot;
	return pos; // 3 bytes total
}

bool OtaCompleteMessage::fromBinary(const uint8_t* buffer, size_t len) {
	if (len < BINARY_SIZE || buffer[0] != 0x34) return false;
	size_t pos = 1;
	status = buffer[pos++];
	will_reboot = buffer[pos++];
	ts = millis();
	return true;
}

// ============================================================================
// MESSAGE FACTORY IMPLEMENTATIONS
// ============================================================================

// --- Factory ---
EspNowMessage* MessageFactory::createMessage(const String& json) {
	MessageType t = getMessageType(json);
	EspNowMessage* m = nullptr;
	switch (t) {
		case MessageType::JOIN_REQUEST: m = new JoinRequestMessage(); break;
		case MessageType::JOIN_ACCEPT:  m = new JoinAcceptMessage(); break;
		case MessageType::SET_LIGHT:    m = new SetLightMessage(); break;
		case MessageType::NODE_STATUS:  m = new NodeStatusMessage(); break;
		case MessageType::ERROR:        m = new ErrorMessage(); break;
		case MessageType::ACK:          m = new AckMessage(); break;
		// Hydroponic system messages
		case MessageType::TOWER_JOIN_REQUEST:  m = new TowerJoinRequestMessage(); break;
		case MessageType::TOWER_JOIN_ACCEPT:   m = new TowerJoinAcceptMessage(); break;
		case MessageType::TOWER_TELEMETRY:     m = new TowerTelemetryMessage(); break;
		case MessageType::TOWER_COMMAND:       m = new TowerCommandMessage(); break;
		case MessageType::RESERVOIR_TELEMETRY: m = new ReservoirTelemetryMessage(); break;
		// V2 Pairing messages
		case MessageType::PAIRING_ADVERTISEMENT: m = new PairingAdvertisementMessage(); break;
		case MessageType::PAIRING_OFFER:         m = new PairingOfferMessage(); break;
		case MessageType::PAIRING_ACCEPT:        m = new PairingAcceptMessage(); break;
		case MessageType::PAIRING_CONFIRM:       m = new PairingConfirmMessage(); break;
		case MessageType::PAIRING_REJECT:        m = new PairingRejectMessage(); break;
		case MessageType::PAIRING_ABORT:         m = new PairingAbortMessage(); break;
		// OTA messages
		case MessageType::OTA_BEGIN:       m = new OtaBeginMessage(); break;
		case MessageType::OTA_CHUNK:       m = new OtaChunkMessage(); break;
		case MessageType::OTA_CHUNK_ACK:   m = new OtaChunkAckMessage(); break;
		case MessageType::OTA_ABORT:       m = new OtaAbortMessage(); break;
		case MessageType::OTA_COMPLETE:    m = new OtaCompleteMessage(); break;
		default: return nullptr;
	}
	if (m && !m->fromJson(json)) { delete m; return nullptr; }
	return m;
}

MessageType MessageFactory::getMessageType(const String& json) {
	// Use filter to only extract "msg" field - more memory efficient
	DynamicJsonDocument filter(48);
	filter["msg"] = true;
	
	DynamicJsonDocument doc(256);
	DeserializationError err = deserializeJson(doc, json, DeserializationOption::Filter(filter));
	if (err) {
		Serial.printf("MessageFactory: Failed to parse message type: %s\n", err.c_str());
		return MessageType::ERROR;
	}
	
	String m = doc["msg"].as<String>();
	if (m == "join_request") return MessageType::JOIN_REQUEST;
	if (m == "join_accept") return MessageType::JOIN_ACCEPT;
	if (m == "set_light") return MessageType::SET_LIGHT;
	if (m == "node_status") return MessageType::NODE_STATUS;
	if (m == "ack") return MessageType::ACK;
	// Hydroponic system messages
	if (m == "tower_join_request") return MessageType::TOWER_JOIN_REQUEST;
	if (m == "tower_join_accept") return MessageType::TOWER_JOIN_ACCEPT;
	if (m == "tower_telemetry") return MessageType::TOWER_TELEMETRY;
	if (m == "tower_command") return MessageType::TOWER_COMMAND;
	if (m == "reservoir_telemetry") return MessageType::RESERVOIR_TELEMETRY;
	// V2 Pairing messages
	if (m == "pairing_advertisement") return MessageType::PAIRING_ADVERTISEMENT;
	if (m == "pairing_offer") return MessageType::PAIRING_OFFER;
	if (m == "pairing_accept") return MessageType::PAIRING_ACCEPT;
	if (m == "pairing_confirm") return MessageType::PAIRING_CONFIRM;
	if (m == "pairing_reject") return MessageType::PAIRING_REJECT;
	if (m == "pairing_abort") return MessageType::PAIRING_ABORT;
	// OTA messages
	if (m == "ota_begin") return MessageType::OTA_BEGIN;
	if (m == "ota_chunk") return MessageType::OTA_CHUNK;
	if (m == "ota_chunk_ack") return MessageType::OTA_CHUNK_ACK;
	if (m == "ota_abort") return MessageType::OTA_ABORT;
	if (m == "ota_complete") return MessageType::OTA_COMPLETE;
	return MessageType::ERROR;
}

// --- Binary Factory Methods ---

MessageType MessageFactory::getMessageTypeFromBinary(const uint8_t* buffer, size_t len) {
	if (buffer == nullptr || len < 1) return MessageType::ERROR;
	
	uint8_t typeMarker = buffer[0];
	switch (typeMarker) {
		// V2 Pairing binary message type markers (0x20-0x25)
		case 0x20: return MessageType::PAIRING_ADVERTISEMENT;
		case 0x21: return MessageType::PAIRING_OFFER;
		case 0x22: return MessageType::PAIRING_ACCEPT;
		case 0x23: return MessageType::PAIRING_CONFIRM;
		case 0x24: return MessageType::PAIRING_REJECT;
		case 0x25: return MessageType::PAIRING_ABORT;
		default:
			Serial.printf("MessageFactory: Unknown binary message type marker: 0x%02X\n", typeMarker);
			return MessageType::ERROR;
	}
}

EspNowMessage* MessageFactory::createFromBinary(const uint8_t* buffer, size_t len) {
	if (buffer == nullptr || len < 1) return nullptr;
	
	MessageType t = getMessageTypeFromBinary(buffer, len);
	EspNowMessage* m = nullptr;
	
	switch (t) {
		// V2 Pairing messages (binary format)
		case MessageType::PAIRING_ADVERTISEMENT: m = new PairingAdvertisementMessage(); break;
		case MessageType::PAIRING_OFFER:         m = new PairingOfferMessage(); break;
		case MessageType::PAIRING_ACCEPT:        m = new PairingAcceptMessage(); break;
		case MessageType::PAIRING_CONFIRM:       m = new PairingConfirmMessage(); break;
		case MessageType::PAIRING_REJECT:        m = new PairingRejectMessage(); break;
		case MessageType::PAIRING_ABORT:         m = new PairingAbortMessage(); break;
		default:
			Serial.printf("MessageFactory: Cannot create message from binary, type: %d\n", static_cast<int>(t));
			return nullptr;
	}
	
	if (m) {
		// Call the appropriate fromBinary method based on message type
		bool success = false;
		switch (t) {
			case MessageType::PAIRING_ADVERTISEMENT:
				success = static_cast<PairingAdvertisementMessage*>(m)->fromBinary(buffer, len);
				break;
			case MessageType::PAIRING_OFFER:
				success = static_cast<PairingOfferMessage*>(m)->fromBinary(buffer, len);
				break;
			case MessageType::PAIRING_ACCEPT:
				success = static_cast<PairingAcceptMessage*>(m)->fromBinary(buffer, len);
				break;
			case MessageType::PAIRING_CONFIRM:
				success = static_cast<PairingConfirmMessage*>(m)->fromBinary(buffer, len);
				break;
			case MessageType::PAIRING_REJECT:
				success = static_cast<PairingRejectMessage*>(m)->fromBinary(buffer, len);
				break;
			case MessageType::PAIRING_ABORT:
				success = static_cast<PairingAbortMessage*>(m)->fromBinary(buffer, len);
				break;
			default:
				break;
		}
		
		if (!success) {
			Serial.printf("MessageFactory: Failed to parse binary message of type %d\n", static_cast<int>(t));
			delete m;
			return nullptr;
		}
	}
	
	return m;
}


