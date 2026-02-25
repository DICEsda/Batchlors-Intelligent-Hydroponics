#ifdef UNIT_TEST

#include <unity.h>
#include <Arduino.h>
#include "../src/EspNowMessage.h"

// ============================================================================
// JoinRequestMessage Tests
// ============================================================================

void test_join_request_serialization() {
    JoinRequestMessage msg;
    msg.mac = "AA:BB:CC:DD:EE:FF";
    msg.fw = "1.0.0";
    msg.caps.rgbw = true;
    msg.caps.led_count = 4;
    msg.caps.temp_i2c = true;
    msg.caps.deep_sleep = false;
    msg.caps.button = true;
    msg.token = "test_token_123";
    
    String json = msg.toJson();
    
    TEST_ASSERT_TRUE(json.indexOf("\"msg\":\"join_request\"") > 0);
    TEST_ASSERT_TRUE(json.indexOf("\"mac\":\"AA:BB:CC:DD:EE:FF\"") > 0);
    TEST_ASSERT_TRUE(json.indexOf("\"fw\":\"1.0.0\"") > 0);
    TEST_ASSERT_TRUE(json.indexOf("\"rgbw\":true") > 0);
    TEST_ASSERT_TRUE(json.indexOf("\"led_count\":4") > 0);
}

void test_join_request_deserialization() {
    String json = "{\"msg\":\"join_request\",\"mac\":\"11:22:33:44:55:66\",\"fw\":\"2.0.0\",\"caps\":{\"rgbw\":true,\"led_count\":8,\"temp_i2c\":false,\"deep_sleep\":true,\"button\":false},\"token\":\"abc123\"}";
    
    JoinRequestMessage msg;
    bool result = msg.fromJson(json);
    
    TEST_ASSERT_TRUE(result);
    TEST_ASSERT_EQUAL_STRING("join_request", msg.msg.c_str());
    TEST_ASSERT_EQUAL_STRING("11:22:33:44:55:66", msg.mac.c_str());
    TEST_ASSERT_EQUAL_STRING("2.0.0", msg.fw.c_str());
    TEST_ASSERT_TRUE(msg.caps.rgbw);
    TEST_ASSERT_EQUAL(8, msg.caps.led_count);
    TEST_ASSERT_FALSE(msg.caps.temp_i2c);
    TEST_ASSERT_TRUE(msg.caps.deep_sleep);
    TEST_ASSERT_FALSE(msg.caps.button);
    TEST_ASSERT_EQUAL_STRING("abc123", msg.token.c_str());
}

void test_join_request_roundtrip() {
    JoinRequestMessage original;
    original.mac = "DE:AD:BE:EF:00:01";
    original.fw = "3.1.4";
    original.caps.rgbw = true;
    original.caps.led_count = 4;
    original.caps.temp_i2c = true;
    original.caps.deep_sleep = false;
    original.caps.button = true;
    original.token = "roundtrip_test";
    
    String json = original.toJson();
    
    JoinRequestMessage parsed;
    TEST_ASSERT_TRUE(parsed.fromJson(json));
    TEST_ASSERT_EQUAL_STRING(original.mac.c_str(), parsed.mac.c_str());
    TEST_ASSERT_EQUAL_STRING(original.fw.c_str(), parsed.fw.c_str());
    TEST_ASSERT_EQUAL(original.caps.rgbw, parsed.caps.rgbw);
    TEST_ASSERT_EQUAL(original.caps.led_count, parsed.caps.led_count);
    TEST_ASSERT_EQUAL(original.caps.temp_i2c, parsed.caps.temp_i2c);
    TEST_ASSERT_EQUAL(original.caps.deep_sleep, parsed.caps.deep_sleep);
    TEST_ASSERT_EQUAL(original.caps.button, parsed.caps.button);
}

// ============================================================================
// JoinAcceptMessage Tests
// ============================================================================

void test_join_accept_serialization() {
    JoinAcceptMessage msg;
    msg.node_id = "node_001";
    msg.light_id = "LDEAD";
    msg.lmk = "secret_key";
    msg.wifi_channel = 6;
    msg.cfg.pwm_freq = 1000;
    msg.cfg.rx_window_ms = 25;
    msg.cfg.rx_period_ms = 150;
    
    String json = msg.toJson();
    
    TEST_ASSERT_TRUE(json.indexOf("\"msg\":\"join_accept\"") > 0);
    TEST_ASSERT_TRUE(json.indexOf("\"node_id\":\"node_001\"") > 0);
    TEST_ASSERT_TRUE(json.indexOf("\"light_id\":\"LDEAD\"") > 0);
    TEST_ASSERT_TRUE(json.indexOf("\"wifi_channel\":6") > 0);
}

void test_join_accept_deserialization() {
    String json = "{\"msg\":\"join_accept\",\"node_id\":\"N123\",\"light_id\":\"L456\",\"lmk\":\"key123\",\"wifi_channel\":11,\"cfg\":{\"pwm_freq\":2000,\"rx_window_ms\":30,\"rx_period_ms\":200}}";
    
    JoinAcceptMessage msg;
    bool result = msg.fromJson(json);
    
    TEST_ASSERT_TRUE(result);
    TEST_ASSERT_EQUAL_STRING("join_accept", msg.msg.c_str());
    TEST_ASSERT_EQUAL_STRING("N123", msg.node_id.c_str());
    TEST_ASSERT_EQUAL_STRING("L456", msg.light_id.c_str());
    TEST_ASSERT_EQUAL_STRING("key123", msg.lmk.c_str());
    TEST_ASSERT_EQUAL(11, msg.wifi_channel);
    TEST_ASSERT_EQUAL(2000, msg.cfg.pwm_freq);
    TEST_ASSERT_EQUAL(30, msg.cfg.rx_window_ms);
    TEST_ASSERT_EQUAL(200, msg.cfg.rx_period_ms);
}

void test_join_accept_default_channel() {
    // Test that missing wifi_channel defaults to 1
    String json = "{\"msg\":\"join_accept\",\"node_id\":\"N1\",\"light_id\":\"L1\",\"lmk\":\"\",\"cfg\":{\"pwm_freq\":0,\"rx_window_ms\":20,\"rx_period_ms\":100}}";
    
    JoinAcceptMessage msg;
    TEST_ASSERT_TRUE(msg.fromJson(json));
    TEST_ASSERT_EQUAL(1, msg.wifi_channel);
}

// ============================================================================
// SetLightMessage Tests
// ============================================================================

void test_set_light_serialization() {
    SetLightMessage msg;
    msg.cmd_id = "cmd_001";
    msg.light_id = "LBEEF";
    msg.r = 255;
    msg.g = 128;
    msg.b = 64;
    msg.w = 200;
    msg.value = 100;
    msg.fade_ms = 500;
    msg.override_status = true;
    msg.ttl_ms = 2000;
    msg.pixel = 2;
    msg.reason = "user_request";
    
    String json = msg.toJson();
    
    TEST_ASSERT_TRUE(json.indexOf("\"msg\":\"set_light\"") > 0);
    TEST_ASSERT_TRUE(json.indexOf("\"r\":255") > 0);
    TEST_ASSERT_TRUE(json.indexOf("\"g\":128") > 0);
    TEST_ASSERT_TRUE(json.indexOf("\"b\":64") > 0);
    TEST_ASSERT_TRUE(json.indexOf("\"w\":200") > 0);
    TEST_ASSERT_TRUE(json.indexOf("\"fade_ms\":500") > 0);
    TEST_ASSERT_TRUE(json.indexOf("\"pixel\":2") > 0);
}

void test_set_light_deserialization() {
    String json = "{\"msg\":\"set_light\",\"cmd_id\":\"c123\",\"light_id\":\"LABC\",\"r\":100,\"g\":150,\"b\":200,\"w\":50,\"value\":80,\"fade_ms\":1000,\"override_status\":false,\"ttl_ms\":3000,\"pixel\":1,\"reason\":\"automation\"}";
    
    SetLightMessage msg;
    bool result = msg.fromJson(json);
    
    TEST_ASSERT_TRUE(result);
    TEST_ASSERT_EQUAL_STRING("set_light", msg.msg.c_str());
    TEST_ASSERT_EQUAL_STRING("c123", msg.cmd_id.c_str());
    TEST_ASSERT_EQUAL_STRING("LABC", msg.light_id.c_str());
    TEST_ASSERT_EQUAL(100, msg.r);
    TEST_ASSERT_EQUAL(150, msg.g);
    TEST_ASSERT_EQUAL(200, msg.b);
    TEST_ASSERT_EQUAL(50, msg.w);
    TEST_ASSERT_EQUAL(80, msg.value);
    TEST_ASSERT_EQUAL(1000, msg.fade_ms);
    TEST_ASSERT_FALSE(msg.override_status);
    TEST_ASSERT_EQUAL(3000, msg.ttl_ms);
    TEST_ASSERT_EQUAL(1, msg.pixel);
}

void test_set_light_defaults() {
    // Test default values when fields are missing
    String json = "{\"msg\":\"set_light\",\"cmd_id\":\"x\",\"light_id\":\"y\"}";
    
    SetLightMessage msg;
    TEST_ASSERT_TRUE(msg.fromJson(json));
    TEST_ASSERT_EQUAL(0, msg.r);
    TEST_ASSERT_EQUAL(0, msg.g);
    TEST_ASSERT_EQUAL(0, msg.b);
    TEST_ASSERT_EQUAL(0, msg.w);
    TEST_ASSERT_EQUAL(0, msg.value);
    TEST_ASSERT_EQUAL(0, msg.fade_ms);
    TEST_ASSERT_FALSE(msg.override_status);
    TEST_ASSERT_EQUAL(1500, msg.ttl_ms);  // Default TTL
    TEST_ASSERT_EQUAL(-1, msg.pixel);     // All pixels
}

void test_set_light_all_pixels() {
    SetLightMessage msg;
    msg.pixel = -1;  // All pixels
    
    String json = msg.toJson();
    TEST_ASSERT_TRUE(json.indexOf("\"pixel\":-1") > 0);
    
    SetLightMessage parsed;
    TEST_ASSERT_TRUE(parsed.fromJson(json));
    TEST_ASSERT_EQUAL(-1, parsed.pixel);
}

// ============================================================================
// NodeStatusMessage Tests
// ============================================================================

void test_node_status_serialization() {
    NodeStatusMessage msg;
    msg.node_id = "node_abc";
    msg.light_id = "LCAFE";
    msg.avg_r = 128;
    msg.avg_g = 64;
    msg.avg_b = 32;
    msg.avg_w = 255;
    msg.status_mode = "operational";
    msg.vbat_mv = 3700;
    msg.temperature = 25.5f;
    msg.button_pressed = true;
    msg.fw = "1.2.3";
    
    String json = msg.toJson();
    
    TEST_ASSERT_TRUE(json.indexOf("\"msg\":\"node_status\"") > 0);
    TEST_ASSERT_TRUE(json.indexOf("\"status_mode\":\"operational\"") > 0);
    TEST_ASSERT_TRUE(json.indexOf("\"vbat_mv\":3700") > 0);
    TEST_ASSERT_TRUE(json.indexOf("\"button_pressed\":true") > 0);
}

void test_node_status_deserialization() {
    String json = "{\"msg\":\"node_status\",\"node_id\":\"N999\",\"light_id\":\"L888\",\"avg_r\":200,\"avg_g\":100,\"avg_b\":50,\"avg_w\":150,\"status_mode\":\"pairing\",\"vbat_mv\":4100,\"temperature\":30.25,\"button_pressed\":false,\"fw\":\"2.0.0\",\"ts\":12345}";
    
    NodeStatusMessage msg;
    bool result = msg.fromJson(json);
    
    TEST_ASSERT_TRUE(result);
    TEST_ASSERT_EQUAL_STRING("node_status", msg.msg.c_str());
    TEST_ASSERT_EQUAL_STRING("N999", msg.node_id.c_str());
    TEST_ASSERT_EQUAL(200, msg.avg_r);
    TEST_ASSERT_EQUAL(100, msg.avg_g);
    TEST_ASSERT_EQUAL(50, msg.avg_b);
    TEST_ASSERT_EQUAL(150, msg.avg_w);
    TEST_ASSERT_EQUAL_STRING("pairing", msg.status_mode.c_str());
    TEST_ASSERT_EQUAL(4100, msg.vbat_mv);
    TEST_ASSERT_FLOAT_WITHIN(0.01f, 30.25f, msg.temperature);
    TEST_ASSERT_FALSE(msg.button_pressed);
    TEST_ASSERT_EQUAL(12345, msg.ts);
}

// ============================================================================
// ErrorMessage Tests
// ============================================================================

void test_error_message_serialization() {
    ErrorMessage msg;
    msg.node_id = "node_err";
    msg.code = "SENSOR_FAIL";
    msg.info = "TMP117 not responding";
    
    String json = msg.toJson();
    
    TEST_ASSERT_TRUE(json.indexOf("\"msg\":\"error\"") > 0);
    TEST_ASSERT_TRUE(json.indexOf("\"code\":\"SENSOR_FAIL\"") > 0);
    TEST_ASSERT_TRUE(json.indexOf("\"info\":\"TMP117 not responding\"") > 0);
}

void test_error_message_deserialization() {
    String json = "{\"msg\":\"error\",\"node_id\":\"N_ERR\",\"code\":\"OOM\",\"info\":\"Out of memory\"}";
    
    ErrorMessage msg;
    TEST_ASSERT_TRUE(msg.fromJson(json));
    TEST_ASSERT_EQUAL_STRING("error", msg.msg.c_str());
    TEST_ASSERT_EQUAL_STRING("N_ERR", msg.node_id.c_str());
    TEST_ASSERT_EQUAL_STRING("OOM", msg.code.c_str());
    TEST_ASSERT_EQUAL_STRING("Out of memory", msg.info.c_str());
}

// ============================================================================
// AckMessage Tests
// ============================================================================

void test_ack_message_serialization() {
    AckMessage msg;
    msg.cmd_id = "cmd_ack_001";
    
    String json = msg.toJson();
    
    TEST_ASSERT_TRUE(json.indexOf("\"msg\":\"ack\"") > 0);
    TEST_ASSERT_TRUE(json.indexOf("\"cmd_id\":\"cmd_ack_001\"") > 0);
}

void test_ack_message_deserialization() {
    String json = "{\"msg\":\"ack\",\"cmd_id\":\"xyz789\"}";
    
    AckMessage msg;
    TEST_ASSERT_TRUE(msg.fromJson(json));
    TEST_ASSERT_EQUAL_STRING("ack", msg.msg.c_str());
    TEST_ASSERT_EQUAL_STRING("xyz789", msg.cmd_id.c_str());
}

// ============================================================================
// MessageFactory Tests
// ============================================================================

void test_factory_get_message_type() {
    TEST_ASSERT_EQUAL(MessageType::JOIN_REQUEST, MessageFactory::getMessageType("{\"msg\":\"join_request\"}"));
    TEST_ASSERT_EQUAL(MessageType::JOIN_ACCEPT, MessageFactory::getMessageType("{\"msg\":\"join_accept\"}"));
    TEST_ASSERT_EQUAL(MessageType::SET_LIGHT, MessageFactory::getMessageType("{\"msg\":\"set_light\"}"));
    TEST_ASSERT_EQUAL(MessageType::NODE_STATUS, MessageFactory::getMessageType("{\"msg\":\"node_status\"}"));
    TEST_ASSERT_EQUAL(MessageType::ACK, MessageFactory::getMessageType("{\"msg\":\"ack\"}"));
    TEST_ASSERT_EQUAL(MessageType::ERROR, MessageFactory::getMessageType("{\"msg\":\"error\"}"));
    TEST_ASSERT_EQUAL(MessageType::ERROR, MessageFactory::getMessageType("{\"msg\":\"unknown\"}"));
}

void test_factory_create_join_request() {
    String json = "{\"msg\":\"join_request\",\"mac\":\"AA:BB:CC:DD:EE:FF\",\"fw\":\"1.0\",\"caps\":{\"rgbw\":true,\"led_count\":4,\"temp_i2c\":true,\"deep_sleep\":false,\"button\":true},\"token\":\"tok\"}";
    
    EspNowMessage* msg = MessageFactory::createMessage(json);
    TEST_ASSERT_NOT_NULL(msg);
    TEST_ASSERT_EQUAL(MessageType::JOIN_REQUEST, msg->type);
    
    JoinRequestMessage* joinReq = static_cast<JoinRequestMessage*>(msg);
    TEST_ASSERT_EQUAL_STRING("AA:BB:CC:DD:EE:FF", joinReq->mac.c_str());
    
    delete msg;
}

void test_factory_create_set_light() {
    String json = "{\"msg\":\"set_light\",\"cmd_id\":\"c1\",\"light_id\":\"L1\",\"r\":255,\"g\":0,\"b\":0,\"w\":0}";
    
    EspNowMessage* msg = MessageFactory::createMessage(json);
    TEST_ASSERT_NOT_NULL(msg);
    TEST_ASSERT_EQUAL(MessageType::SET_LIGHT, msg->type);
    
    SetLightMessage* setLight = static_cast<SetLightMessage*>(msg);
    TEST_ASSERT_EQUAL(255, setLight->r);
    TEST_ASSERT_EQUAL(0, setLight->g);
    
    delete msg;
}

void test_factory_invalid_json() {
    EspNowMessage* msg = MessageFactory::createMessage("not valid json");
    TEST_ASSERT_NULL(msg);
}

void test_factory_missing_msg_field() {
    EspNowMessage* msg = MessageFactory::createMessage("{\"data\":\"no msg field\"}");
    // Should return ERROR type or null
    if (msg != nullptr) {
        TEST_ASSERT_EQUAL(MessageType::ERROR, msg->type);
        delete msg;
    }
}

// ============================================================================
// Message Size Tests (ESP-NOW limit is 250 bytes)
// ============================================================================

void test_message_sizes_within_limit() {
    const size_t ESP_NOW_MAX_SIZE = 250;
    
    // Test worst-case message sizes
    JoinRequestMessage joinReq;
    joinReq.mac = "AA:BB:CC:DD:EE:FF";
    joinReq.fw = "99.99.99";
    joinReq.caps.rgbw = true;
    joinReq.caps.led_count = 255;
    joinReq.caps.temp_i2c = true;
    joinReq.caps.deep_sleep = true;
    joinReq.caps.button = true;
    joinReq.token = "0123456789ABCDEF0123456789ABCDEF";  // 32 char token
    TEST_ASSERT_LESS_OR_EQUAL(ESP_NOW_MAX_SIZE, joinReq.toJson().length());
    
    SetLightMessage setLight;
    setLight.cmd_id = "cmd_0123456789ABCDEF";
    setLight.light_id = "L0123456789";
    setLight.r = 255; setLight.g = 255; setLight.b = 255; setLight.w = 255;
    setLight.value = 255;
    setLight.fade_ms = 65535;
    setLight.override_status = true;
    setLight.ttl_ms = 65535;
    setLight.pixel = 127;
    setLight.reason = "automation_presence_detected";
    TEST_ASSERT_LESS_OR_EQUAL(ESP_NOW_MAX_SIZE, setLight.toJson().length());
    
    NodeStatusMessage nodeStatus;
    nodeStatus.node_id = "node_0123456789";
    nodeStatus.light_id = "L0123456789";
    nodeStatus.avg_r = 255; nodeStatus.avg_g = 255; nodeStatus.avg_b = 255; nodeStatus.avg_w = 255;
    nodeStatus.status_mode = "operational";
    nodeStatus.vbat_mv = 65535;
    nodeStatus.temperature = 125.99f;
    nodeStatus.button_pressed = true;
    nodeStatus.fw = "99.99.99";
    nodeStatus.ts = 4294967295;  // Max uint32
    TEST_ASSERT_LESS_OR_EQUAL(ESP_NOW_MAX_SIZE, nodeStatus.toJson().length());
}

// ============================================================================
// Test Runner
// ============================================================================

void setup() {
    delay(2000);  // Wait for serial
    
    UNITY_BEGIN();
    
    // JoinRequestMessage tests
    RUN_TEST(test_join_request_serialization);
    RUN_TEST(test_join_request_deserialization);
    RUN_TEST(test_join_request_roundtrip);
    
    // JoinAcceptMessage tests
    RUN_TEST(test_join_accept_serialization);
    RUN_TEST(test_join_accept_deserialization);
    RUN_TEST(test_join_accept_default_channel);
    
    // SetLightMessage tests
    RUN_TEST(test_set_light_serialization);
    RUN_TEST(test_set_light_deserialization);
    RUN_TEST(test_set_light_defaults);
    RUN_TEST(test_set_light_all_pixels);
    
    // NodeStatusMessage tests
    RUN_TEST(test_node_status_serialization);
    RUN_TEST(test_node_status_deserialization);
    
    // ErrorMessage tests
    RUN_TEST(test_error_message_serialization);
    RUN_TEST(test_error_message_deserialization);
    
    // AckMessage tests
    RUN_TEST(test_ack_message_serialization);
    RUN_TEST(test_ack_message_deserialization);
    
    // MessageFactory tests
    RUN_TEST(test_factory_get_message_type);
    RUN_TEST(test_factory_create_join_request);
    RUN_TEST(test_factory_create_set_light);
    RUN_TEST(test_factory_invalid_json);
    RUN_TEST(test_factory_missing_msg_field);
    
    // Size constraint tests
    RUN_TEST(test_message_sizes_within_limit);
    
    UNITY_END();
}

void loop() {
    // Nothing to do here
}

#endif // UNIT_TEST
