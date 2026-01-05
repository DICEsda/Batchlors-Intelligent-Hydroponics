#pragma once

#include <Arduino.h>
#include <WiFi.h>
#include <functional>

/**
 * OtaUpdater - HTTP-based OTA firmware update handler for Coordinator
 * 
 * Features:
 * - Downloads firmware from HTTP URL
 * - Supports SHA256 checksum verification (backend uses sha256:hexstring format)
 * - Progress callback for MQTT status reporting
 * - Automatic reboot on successful update
 */
class OtaUpdater {
public:
    // OTA status codes for progress reporting
    enum class Status {
        IDLE,
        CONNECTING,
        DOWNLOADING,
        VERIFYING,
        APPLYING,
        SUCCESS,
        FAILED
    };

    struct Result {
        bool ok;
        String message;
        int httpCode;
        Status finalStatus;
    };

    // Progress callback: (status, progressPercent, message)
    using ProgressCallback = std::function<void(Status status, int progress, const String& message)>;

    /**
     * Perform OTA update from a URL
     * @param url HTTP URL to download firmware from
     * @param checksum Optional checksum in format "sha256:hexstring" or "md5:hexstring"
     * @param progressCb Optional callback for progress updates
     * @return Result with success/failure info
     */
    static Result updateFromUrl(
        const char* url, 
        const char* checksum = nullptr,
        ProgressCallback progressCb = nullptr
    );

    /**
     * Ensure WiFi is connected before OTA
     * Note: Coordinator should already have WiFi via WifiManager, but this
     * provides a fallback for direct WiFi connection if needed
     */
    static bool ensureWifi(const char* ssid, const char* pass, uint32_t timeoutMs = 15000);

    /**
     * Convert Status enum to string for logging/MQTT
     */
    static const char* statusToString(Status status);

private:
    static bool parseChecksum(const char* checksum, String& algorithm, String& hash);
    static bool verifySha256(const uint8_t* data, size_t len, const String& expectedHex);
};
