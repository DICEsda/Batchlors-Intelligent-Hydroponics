#ifndef ESP_NOW_MESSAGE_H
#define ESP_NOW_MESSAGE_H

#include <Arduino.h>
#include <ArduinoJson.h>

// Message types signaled via the 'msg' string field in JSON
enum class MessageType {
	JOIN_REQUEST,
	JOIN_ACCEPT,
	SET_LIGHT,
	NODE_STATUS,
	ERROR,
	ACK,
	// Hydroponic system messages
	TOWER_JOIN_REQUEST,    // Tower node requesting to join coordinator
	TOWER_JOIN_ACCEPT,     // Coordinator accepting tower
	TOWER_TELEMETRY,       // Tower -> Coordinator: air temp, humidity, light
	TOWER_COMMAND,         // Coordinator -> Tower: pump/light control
	RESERVOIR_TELEMETRY,   // Coordinator internal: pH, EC, water temp, level
	
	// V2 Pairing Protocol (Zigbee-like permit-join model)
	PAIRING_ADVERTISEMENT, // Node -> Broadcast: announces availability for pairing
	PAIRING_OFFER,         // Coordinator -> Node (Unicast): invites node to pair
	PAIRING_ACCEPT,        // Node -> Coordinator: accepts the pairing offer
	PAIRING_CONFIRM,       // Coordinator -> Node: confirms binding complete
	PAIRING_REJECT,        // Coordinator -> Node: rejects/cancels pairing
	PAIRING_ABORT,         // Node -> Coordinator: cancels pairing attempt
	
	// ESP-NOW OTA (Over-The-Air) firmware update messages
	// Tower nodes cannot do HTTP OTA - coordinator proxies firmware via ESP-NOW chunks
	OTA_BEGIN,             // Coordinator -> Tower: Start OTA transfer (size, checksum, chunk_count)
	OTA_CHUNK,             // Coordinator -> Tower: One chunk of firmware data (~200 bytes)
	OTA_CHUNK_ACK,         // Tower -> Coordinator: Acknowledge chunk received
	OTA_ABORT,             // Either direction: Cancel OTA transfer
	OTA_COMPLETE           // Tower -> Coordinator: OTA finished successfully
};

// Device types for pairing
enum class DeviceType : uint8_t {
	UNKNOWN = 0,
	TOWER = 1,          // Hydroponic tower node
	SENSOR = 2,         // Standalone sensor node
	LIGHT_NODE = 3,     // Light/LED node
	COORDINATOR = 4     // Coordinator (for reference)
};

// Capability flags for V2 pairing (bitmask)
enum class CapabilityFlags : uint16_t {
	NONE            = 0x0000,
	DHT_SENSOR      = 0x0001,  // DHT22 temp/humidity
	LIGHT_SENSOR    = 0x0002,  // Ambient light sensor (TSL2561)
	PUMP_RELAY      = 0x0004,  // Pump/valve relay output
	GROW_LIGHT      = 0x0008,  // Grow light output
	RGBW_LED        = 0x0010,  // SK6812B RGBW LEDs
	DEEP_SLEEP      = 0x0020,  // Deep sleep capable
	BUTTON          = 0x0040,  // Button input available
	TEMP_I2C        = 0x0080,  // I2C temp sensor (TMP177)
	PRESENCE_SENSOR = 0x0100,  // LD2450 presence sensor
	BATTERY         = 0x0200   // Battery powered
};

// Pairing rejection/abort reason codes
enum class PairingRejectReason : uint8_t {
	NONE = 0,
	PERMIT_JOIN_DISABLED = 1,
	CAPACITY_FULL = 2,
	DUPLICATE_MAC = 3,
	TIMEOUT = 4,
	USER_REJECTED = 5,
	PROTOCOL_MISMATCH = 6,
	INTERNAL_ERROR = 7,
	NODE_CANCELLED = 8,
	INVALID_TOKEN = 9,
	ALREADY_PAIRED = 10
};

// Base message with common helpers
struct EspNowMessage {
	MessageType type;
	String msg;         // e.g. "join_request", "set_light"
	String cmd_id;      // for idempotency/acks (where applicable)
	uint32_t ts;        // timestamp (ms)

	virtual ~EspNowMessage() = default;
	virtual String toJson() const = 0;
	virtual bool fromJson(const String& json) = 0;
};

// Join request message with capability reporting (PRD v0.5)
struct JoinRequestMessage : public EspNowMessage {
	String mac;            // station MAC
	String fw;             // firmware version
	struct Capabilities {
		bool rgbw;         // SK6812B RGBW support
		uint8_t led_count; // pixels per node (default 4)
		bool temp_i2c;     // TMP177 temp sensor via I2C
		bool deep_sleep;   // deep sleep capable
		bool button;       // button input available
	} caps;
	String token;          // rotating token for secure pairing

	JoinRequestMessage();
	String toJson() const override;
	bool fromJson(const String& json) override;
};

// Join accept (coordinator -> node)
struct JoinAcceptMessage : public EspNowMessage {
	String node_id;
	String light_id;
	String lmk;           // link master key (ESP-NOW LMK)
	uint8_t wifi_channel; // WiFi channel coordinator is using
	struct Cfg {
		int pwm_freq;
		int rx_window_ms;
		int rx_period_ms;
	} cfg;

	JoinAcceptMessage();
	String toJson() const override;
	bool fromJson(const String& json) override;
};

// set_light (PRD v0.5)
struct SetLightMessage : public EspNowMessage {
	String light_id;
	// RGBW values (0..255). If omitted, 'value' may be used as brightness fallback.
	uint8_t r = 0, g = 0, b = 0, w = 0;
	uint8_t value = 0; // optional fallback (PWM-like)
	uint16_t fade_ms = 0;
	bool override_status = false;
	uint16_t ttl_ms = 1500;
	String reason;
	int8_t pixel = -1; // -1 = all pixels, 0-3 = specific pixel index

	SetLightMessage();
	String toJson() const override;
	bool fromJson(const String& json) override;
};

// node_status (PRD v0.5)
struct NodeStatusMessage : public EspNowMessage {
	String node_id;
	String light_id;
	// average output per channel (0..255)
	uint8_t avg_r = 0, avg_g = 0, avg_b = 0, avg_w = 0;
	String status_mode;   // "operational", "pairing", "ota", "error"
	uint16_t vbat_mv = 0;
	float temperature = 0.0f; // temperature in Celsius from TMP177
	bool button_pressed = false; // current button state
	String fw;

	NodeStatusMessage();
	String toJson() const override;
	bool fromJson(const String& json) override;
};

// Error message (minimal)
struct ErrorMessage : public EspNowMessage {
	String node_id;
	String code;
	String info;

	ErrorMessage();
	String toJson() const override;
	bool fromJson(const String& json) override;
};

// Ack for a command id
struct AckMessage : public EspNowMessage {
	AckMessage();
	String toJson() const override;
	bool fromJson(const String& json) override;
};

// ============================================================================
// HYDROPONIC SYSTEM MESSAGES
// ============================================================================

// Tower join request (tower node -> coordinator)
struct TowerJoinRequestMessage : public EspNowMessage {
	String mac;            // station MAC address
	String fw;             // firmware version
	struct TowerCapabilities {
		bool dht_sensor;       // DHT22 temp/humidity sensor
		bool light_sensor;     // ambient light sensor
		bool pump_relay;       // pump/valve relay output
		bool grow_light;       // grow light output (PWM or on/off)
		uint8_t slot_count;    // number of plant slots (default 6)
	} caps;
	String token;          // rotating token for secure pairing

	TowerJoinRequestMessage();
	String toJson() const override;
	bool fromJson(const String& json) override;
};

// Tower join accept (coordinator -> tower)
struct TowerJoinAcceptMessage : public EspNowMessage {
	String tower_id;       // assigned tower ID
	String coord_id;       // coordinator ID
	String farm_id;        // farm ID for MQTT topic hierarchy
	String lmk;            // link master key (ESP-NOW LMK)
	uint8_t wifi_channel;  // WiFi channel coordinator is using
	struct TowerCfg {
		uint16_t telemetry_interval_ms;  // how often to send telemetry (default 30000)
		uint16_t pump_max_duration_s;    // max pump on time for safety (default 300)
	} cfg;

	TowerJoinAcceptMessage();
	String toJson() const override;
	bool fromJson(const String& json) override;
};

// Tower telemetry (tower node -> coordinator, periodic)
struct TowerTelemetryMessage : public EspNowMessage {
	String tower_id;       // tower identifier
	
	// Environmental sensors
	float air_temp_c;      // air temperature in Celsius (DHT22)
	float humidity_pct;    // relative humidity percentage (DHT22)
	float light_lux;       // ambient light level (optional sensor)
	
	// Actuator states
	bool pump_on;          // current pump state
	bool light_on;         // current grow light state
	uint8_t light_brightness; // grow light brightness (0-255, if PWM supported)
	
	// System status
	String status_mode;    // "operational", "pairing", "ota", "error", "idle"
	uint16_t vbat_mv;      // battery/supply voltage in millivolts
	String fw;             // firmware version
	uint32_t uptime_s;     // uptime in seconds

	TowerTelemetryMessage();
	String toJson() const override;
	bool fromJson(const String& json) override;
};

// Tower command (coordinator -> tower node)
struct TowerCommandMessage : public EspNowMessage {
	String tower_id;       // target tower ID
	
	// Command type: "set_pump", "set_light", "reboot", "ota"
	String command;
	
	// Pump control (for command = "set_pump")
	bool pump_on;          // turn pump on/off
	uint16_t pump_duration_s; // auto-off after N seconds (0 = manual control)
	
	// Light control (for command = "set_light")
	bool light_on;         // turn grow light on/off
	uint8_t light_brightness; // brightness (0-255)
	uint16_t light_duration_m; // auto-off after N minutes (0 = manual control)
	
	// OTA control (for command = "ota")
	String ota_url;        // firmware download URL (coordinator-proxied)
	String ota_checksum;   // MD5 or SHA256 checksum
	
	uint16_t ttl_ms;       // command time-to-live

	TowerCommandMessage();
	String toJson() const override;
	bool fromJson(const String& json) override;
};

// Reservoir telemetry (coordinator internal, for MQTT publishing)
// This is NOT sent via ESP-NOW, but defined here for consistency
struct ReservoirTelemetryMessage : public EspNowMessage {
	String coord_id;       // coordinator identifier
	String farm_id;        // farm identifier
	
	// Water quality sensors
	float ph;              // pH level (0-14 scale)
	float ec_ms_cm;        // electrical conductivity in mS/cm
	float tds_ppm;         // total dissolved solids in ppm (calculated from EC)
	float water_temp_c;    // water temperature in Celsius
	
	// Water level
	float water_level_pct; // water level percentage (0-100)
	float water_level_cm;  // water level in centimeters
	bool low_water_alert;  // true if below minimum threshold
	
	// Actuator states
	bool main_pump_on;     // main circulation pump state
	bool dosing_pump_ph_on;// pH adjustment pump state
	bool dosing_pump_nutrient_on; // nutrient dosing pump state
	
	// System status
	String status_mode;    // "operational", "maintenance", "error"
	uint32_t uptime_s;     // coordinator uptime in seconds

	ReservoirTelemetryMessage();
	String toJson() const override;
	bool fromJson(const String& json) override;
};

// ============================================================================
// V2 PAIRING PROTOCOL MESSAGES (Binary format for efficiency)
// ============================================================================

// V2 Pairing timing constants
namespace PairingConstants {
	constexpr uint32_t ADV_INTERVAL_MS = 100;        // Advertisement broadcast interval
	constexpr uint32_t ADV_JITTER_MS = 20;           // Random jitter added to interval
	constexpr uint32_t ADV_TIMEOUT_MS = 300000;      // Stop advertising after 5 minutes
	constexpr uint32_t NONCE_ROTATION_MS = 30000;    // Generate new nonce every 30s
	constexpr uint32_t DISCOVERY_TTL_MS = 30000;     // Remove unseen nodes after 30s
	constexpr uint32_t DEFAULT_PERMIT_JOIN_MS = 60000; // Default permit-join window
	constexpr uint32_t MAX_PERMIT_JOIN_MS = 300000;  // Maximum permit-join window
	constexpr uint32_t BINDING_TIMEOUT_MS = 10000;   // Timeout waiting for PAIRING_ACCEPT
	constexpr uint32_t CONFIRM_TIMEOUT_MS = 5000;    // Timeout waiting for PAIRING_CONFIRM
	constexpr uint8_t PROTOCOL_VERSION = 0x02;       // Current protocol version
	constexpr size_t MAX_DISCOVERED_NODES = 32;      // Max nodes in discovery table
}

// PAIRING_ADVERTISEMENT (Node -> Broadcast, 22 bytes binary)
// Sent by node every 100ms when in pairing mode
struct PairingAdvertisementMessage : public EspNowMessage {
	uint8_t protocol_version;          // Protocol version (0x02)
	uint8_t node_mac[6];               // Node's MAC address
	DeviceType device_type;            // Device type enum (TOWER=1, SENSOR=2, etc.)
	uint32_t firmware_version;         // Packed firmware version (major.minor.patch)
	uint16_t capabilities;             // Capability flags bitmask
	uint32_t nonce;                    // Random nonce for this advertisement cycle
	uint16_t sequence_num;             // Advertisement sequence counter
	int8_t rssi_request;               // Node's perceived RSSI (for coordinator reference)
	
	PairingAdvertisementMessage();
	String toJson() const override;
	bool fromJson(const String& json) override;
	
	// Binary serialization for ESP-NOW efficiency
	size_t toBinary(uint8_t* buffer, size_t maxLen) const;
	bool fromBinary(const uint8_t* buffer, size_t len);
	static constexpr size_t BINARY_SIZE = 22;
	
	// Helper to pack firmware version
	static uint32_t packFirmwareVersion(uint8_t major, uint8_t minor, uint16_t patch);
	static void unpackFirmwareVersion(uint32_t packed, uint8_t& major, uint8_t& minor, uint16_t& patch);
};

// PAIRING_OFFER (Coordinator -> Node Unicast, 23 bytes binary)
// Sent when frontend approves a discovered node
struct PairingOfferMessage : public EspNowMessage {
	uint8_t protocol_version;          // Protocol version
	uint8_t coord_mac[6];              // Coordinator's MAC address
	uint16_t coord_id;                 // Coordinator ID
	uint16_t farm_id;                  // Farm/Site ID
	uint16_t offered_tower_id;         // Assigned tower ID for this node
	uint32_t nonce_echo;               // Echo of node's advertisement nonce
	uint32_t offer_token;              // Unique token for this offer (anti-replay)
	uint8_t channel;                   // ESP-NOW channel to use
	
	PairingOfferMessage();
	String toJson() const override;
	bool fromJson(const String& json) override;
	
	size_t toBinary(uint8_t* buffer, size_t maxLen) const;
	bool fromBinary(const uint8_t* buffer, size_t len);
	static constexpr size_t BINARY_SIZE = 23;
};

// PAIRING_ACCEPT (Node -> Coordinator, 13 bytes binary)
// Sent by node after receiving valid PAIRING_OFFER
struct PairingAcceptMessage : public EspNowMessage {
	uint8_t node_mac[6];               // Node's MAC address
	uint32_t offer_token;              // Token from PAIRING_OFFER (proves receipt)
	uint16_t accepted_tower_id;        // Echoed tower ID
	
	PairingAcceptMessage();
	String toJson() const override;
	bool fromJson(const String& json) override;
	
	size_t toBinary(uint8_t* buffer, size_t maxLen) const;
	bool fromBinary(const uint8_t* buffer, size_t len);
	static constexpr size_t BINARY_SIZE = 13;
};

// PAIRING_CONFIRM (Coordinator -> Node, 26 bytes binary)
// Sent after PAIRING_ACCEPT validates, completes binding
struct PairingConfirmMessage : public EspNowMessage {
	uint8_t coord_mac[6];              // Coordinator's MAC address
	uint16_t tower_id;                 // Final assigned tower ID
	uint8_t encryption_key[16];        // Optional LMK for encrypted ESP-NOW
	uint8_t config_flags;              // Configuration flags
	
	PairingConfirmMessage();
	String toJson() const override;
	bool fromJson(const String& json) override;
	
	size_t toBinary(uint8_t* buffer, size_t maxLen) const;
	bool fromBinary(const uint8_t* buffer, size_t len);
	static constexpr size_t BINARY_SIZE = 26;
	
	// Config flag helpers
	bool hasEncryption() const { return config_flags & 0x01; }
	void setEncryption(bool enabled) { if (enabled) config_flags |= 0x01; else config_flags &= ~0x01; }
};

// PAIRING_REJECT (Coordinator -> Node, 12 bytes binary)
// Sent when coordinator rejects a node (user rejected, timeout, etc.)
struct PairingRejectMessage : public EspNowMessage {
	uint8_t sender_mac[6];             // Sender's MAC address
	PairingRejectReason reason_code;   // Rejection reason enum
	uint32_t offer_token;              // Token reference (if applicable)
	
	PairingRejectMessage();
	String toJson() const override;
	bool fromJson(const String& json) override;
	
	size_t toBinary(uint8_t* buffer, size_t maxLen) const;
	bool fromBinary(const uint8_t* buffer, size_t len);
	static constexpr size_t BINARY_SIZE = 12;
};

// PAIRING_ABORT (Node -> Coordinator, 12 bytes binary)  
// Sent when node cancels pairing (timeout, user cancelled, etc.)
struct PairingAbortMessage : public EspNowMessage {
	uint8_t sender_mac[6];             // Sender's MAC address
	PairingRejectReason reason_code;   // Abort reason enum
	uint32_t offer_token;              // Token reference (if in PAIRING_WAIT state)
	
	PairingAbortMessage();
	String toJson() const override;
	bool fromJson(const String& json) override;
	
	size_t toBinary(uint8_t* buffer, size_t maxLen) const;
	bool fromBinary(const uint8_t* buffer, size_t len);
	static constexpr size_t BINARY_SIZE = 12;
};

// ============================================================================
// ESP-NOW OTA (OVER-THE-AIR) MESSAGES (Binary format for efficiency)
// Tower nodes cannot do HTTP OTA - coordinator proxies firmware via ESP-NOW chunks
// Binary message type markers: 0x30-0x34
// ============================================================================

// OTA timing and size constants
namespace OtaConstants {
	constexpr size_t MAX_CHUNK_SIZE = 200;           // Max data bytes per chunk (ESP-NOW limit ~250 total)
	constexpr uint32_t CHUNK_ACK_TIMEOUT_MS = 1000;  // Wait time for chunk acknowledgment
	constexpr uint8_t MAX_CHUNK_RETRIES = 3;         // Retries per chunk before abort
	constexpr uint32_t OTA_TOTAL_TIMEOUT_MS = 600000; // 10 min total OTA timeout
	constexpr uint8_t CHECKSUM_NONE = 0;
	constexpr uint8_t CHECKSUM_MD5 = 1;
	constexpr uint8_t CHECKSUM_SHA256 = 2;
}

// OTA abort/error reason codes
enum class OtaAbortReason : uint8_t {
	NONE = 0,
	USER_CANCELLED = 1,
	TIMEOUT = 2,
	CHECKSUM_MISMATCH = 3,
	FLASH_WRITE_ERROR = 4,
	OUT_OF_MEMORY = 5,
	INVALID_FIRMWARE = 6,
	CHUNK_SEQUENCE_ERROR = 7,
	COMMUNICATION_ERROR = 8,
	INTERNAL_ERROR = 9
};

// OTA_BEGIN (Coordinator -> Tower, 45 bytes binary)
// Initiates OTA transfer - tower should prepare flash and respond with ACK
struct OtaBeginMessage : public EspNowMessage {
	uint32_t firmware_size;            // Total firmware size in bytes
	uint16_t chunk_count;              // Total number of chunks
	uint16_t chunk_size;               // Size of each chunk (except possibly last)
	uint8_t checksum_type;             // 0=none, 1=MD5, 2=SHA256
	uint8_t checksum[32];              // Checksum bytes (16 for MD5, 32 for SHA256)
	uint32_t firmware_version;         // Target firmware version (packed)
	
	OtaBeginMessage();
	String toJson() const override;
	bool fromJson(const String& json) override;
	
	size_t toBinary(uint8_t* buffer, size_t maxLen) const;
	bool fromBinary(const uint8_t* buffer, size_t len);
	static constexpr size_t BINARY_SIZE = 45;
};

// OTA_CHUNK (Coordinator -> Tower, 5 + data_len bytes binary, max ~205)
// One chunk of firmware data - tower writes to flash and ACKs
struct OtaChunkMessage : public EspNowMessage {
	uint16_t chunk_index;              // Which chunk (0-based)
	uint8_t data_len;                  // Actual data length in this chunk (1-200)
	uint8_t data[OtaConstants::MAX_CHUNK_SIZE]; // Chunk data
	
	OtaChunkMessage();
	String toJson() const override;
	bool fromJson(const String& json) override;
	
	size_t toBinary(uint8_t* buffer, size_t maxLen) const;
	bool fromBinary(const uint8_t* buffer, size_t len);
	static constexpr size_t BINARY_HEADER_SIZE = 4; // type marker + chunk_index + data_len
};

// OTA_CHUNK_ACK (Tower -> Coordinator, 5 bytes binary)
// Acknowledge chunk received - coordinator sends next or retries
struct OtaChunkAckMessage : public EspNowMessage {
	uint16_t chunk_index;              // Which chunk was received
	uint8_t status;                    // 0=OK, 1=CRC error, 2=write error, 3=retry request
	uint8_t next_expected;             // Next expected chunk index (low byte) for flow control
	
	OtaChunkAckMessage();
	String toJson() const override;
	bool fromJson(const String& json) override;
	
	size_t toBinary(uint8_t* buffer, size_t maxLen) const;
	bool fromBinary(const uint8_t* buffer, size_t len);
	static constexpr size_t BINARY_SIZE = 5;
};

// OTA_ABORT (Either direction, 3 bytes binary)
// Cancel OTA transfer - can be sent by coordinator or tower
struct OtaAbortMessage : public EspNowMessage {
	OtaAbortReason reason;             // Abort reason code
	uint16_t last_chunk;               // Last successfully received chunk (for resume in future)
	
	OtaAbortMessage();
	String toJson() const override;
	bool fromJson(const String& json) override;
	
	size_t toBinary(uint8_t* buffer, size_t maxLen) const;
	bool fromBinary(const uint8_t* buffer, size_t len);
	static constexpr size_t BINARY_SIZE = 4;
};

// OTA_COMPLETE (Tower -> Coordinator, 3 bytes binary)
// OTA finished - tower will reboot with new firmware
struct OtaCompleteMessage : public EspNowMessage {
	uint8_t status;                    // 0=success, 1=checksum fail, 2=flash verify error
	uint8_t will_reboot;               // 1 if tower will reboot immediately
	
	OtaCompleteMessage();
	String toJson() const override;
	bool fromJson(const String& json) override;
	
	size_t toBinary(uint8_t* buffer, size_t maxLen) const;
	bool fromBinary(const uint8_t* buffer, size_t len);
	static constexpr size_t BINARY_SIZE = 3;
};

// ============================================================================
// MESSAGE FACTORY
// ============================================================================

class MessageFactory {
public:
	static EspNowMessage* createMessage(const String& json);
	static MessageType getMessageType(const String& json);
	
	// V2 Pairing binary message factory
	static EspNowMessage* createFromBinary(const uint8_t* buffer, size_t len);
	static MessageType getMessageTypeFromBinary(const uint8_t* buffer, size_t len);
};

// Helper to convert MAC array to String
String macToString(const uint8_t* mac);
// Helper to convert String to MAC array
bool stringToMac(const String& str, uint8_t* mac);

#endif // ESP_NOW_MESSAGE_H

