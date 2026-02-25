#pragma once

#include <Arduino.h>
#include <functional>

/**
 * LogStreamer - Routes Logger output to MQTT for real-time frontend display
 * 
 * Features:
 * - Circular buffer for log messages
 * - Rate limiting to prevent MQTT flooding
 * - Level filtering
 * - Callback interface for MQTT publishing
 */

class LogStreamer {
public:
    struct LogEntry {
        unsigned long timestamp;
        String level;
        String message;
        
        LogEntry() : timestamp(0) {}
        LogEntry(unsigned long ts, const String& lvl, const String& msg) 
            : timestamp(ts), level(lvl), message(msg) {}
    };
    
    using PublishCallback = std::function<void(const String& level, const String& message, unsigned long timestamp)>;
    
    LogStreamer() 
        : enabled(false)
        , minLevel(1) // INFO
        , maxMessagesPerSecond(10)
        , lastPublishMs(0)
        , publishCount(0)
        , publishWindowStart(0)
        , callback(nullptr) {}
    
    // Enable/disable log streaming
    void setEnabled(bool enable) { enabled = enable; }
    bool isEnabled() const { return enabled; }
    
    // Set minimum log level (0=DEBUG, 1=INFO, 2=WARN, 3=ERROR)
    void setMinLevel(uint8_t level) { minLevel = level; }
    
    // Set rate limit (messages per second)
    void setRateLimit(uint8_t messagesPerSecond) { maxMessagesPerSecond = messagesPerSecond; }
    
    // Set callback for publishing logs
    void setPublishCallback(PublishCallback cb) { callback = cb; }
    
    // Called by Logger to stream a log message
    void streamLog(uint8_t level, const char* levelStr, const char* message) {
        if (!enabled || !callback) return;
        if (level < minLevel) return;
        
        // Rate limiting
        unsigned long now = millis();
        
        // Reset counter every second
        if (now - publishWindowStart >= 1000) {
            publishWindowStart = now;
            publishCount = 0;
        }
        
        // Check rate limit
        if (publishCount >= maxMessagesPerSecond) {
            return; // Drop message to prevent flooding
        }
        
        // Prevent rapid-fire messages (min 10ms between messages)
        if (now - lastPublishMs < 10) {
            return;
        }
        
        // Publish via callback
        callback(String(levelStr), String(message), now);
        
        lastPublishMs = now;
        publishCount++;
    }
    
private:
    bool enabled;
    uint8_t minLevel;
    uint8_t maxMessagesPerSecond;
    unsigned long lastPublishMs;
    uint8_t publishCount;
    unsigned long publishWindowStart;
    PublishCallback callback;
};

// Global instance
extern LogStreamer gLogStreamer;
