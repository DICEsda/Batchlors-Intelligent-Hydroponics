#include "OtaUpdater.h"
#include <HTTPClient.h>
#include <Update.h>
#include <mbedtls/sha256.h>
#include "Logger.h"

bool OtaUpdater::ensureWifi(const char* ssid, const char* pass, uint32_t timeoutMs) {
    if (WiFi.status() == WL_CONNECTED) return true;
    WiFi.mode(WIFI_STA);
    WiFi.begin(ssid, pass);
    uint32_t start = millis();
    while (WiFi.status() != WL_CONNECTED && (millis() - start) < timeoutMs) {
        delay(200);
    }
    return WiFi.status() == WL_CONNECTED;
}

const char* OtaUpdater::statusToString(Status status) {
    switch (status) {
        case Status::IDLE:        return "idle";
        case Status::CONNECTING:  return "connecting";
        case Status::DOWNLOADING: return "downloading";
        case Status::VERIFYING:   return "verifying";
        case Status::APPLYING:    return "applying";
        case Status::SUCCESS:     return "success";
        case Status::FAILED:      return "failed";
        default:                  return "unknown";
    }
}

bool OtaUpdater::parseChecksum(const char* checksum, String& algorithm, String& hash) {
    if (!checksum || strlen(checksum) == 0) {
        return false;
    }
    
    String cs(checksum);
    int colonIdx = cs.indexOf(':');
    if (colonIdx < 0) {
        // Assume MD5 if no prefix (backward compatibility)
        algorithm = "md5";
        hash = cs;
    } else {
        algorithm = cs.substring(0, colonIdx);
        algorithm.toLowerCase();
        hash = cs.substring(colonIdx + 1);
    }
    
    hash.toLowerCase();
    return hash.length() > 0;
}

bool OtaUpdater::verifySha256(const uint8_t* data, size_t len, const String& expectedHex) {
    uint8_t hash[32];
    mbedtls_sha256_context ctx;
    mbedtls_sha256_init(&ctx);
    mbedtls_sha256_starts(&ctx, 0); // 0 = SHA256 (not SHA224)
    mbedtls_sha256_update(&ctx, data, len);
    mbedtls_sha256_finish(&ctx, hash);
    mbedtls_sha256_free(&ctx);
    
    // Convert to hex string
    String computed;
    computed.reserve(64);
    for (int i = 0; i < 32; i++) {
        char buf[3];
        snprintf(buf, sizeof(buf), "%02x", hash[i]);
        computed += buf;
    }
    
    return computed.equalsIgnoreCase(expectedHex);
}

OtaUpdater::Result OtaUpdater::updateFromUrl(
    const char* url, 
    const char* checksum,
    ProgressCallback progressCb
) {
    Result res{false, "", 0, Status::IDLE};
    
    auto reportProgress = [&](Status status, int progress, const String& msg) {
        res.finalStatus = status;
        if (progressCb) {
            progressCb(status, progress, msg);
        }
        Logger::info("OTA [%s] %d%% - %s", statusToString(status), progress, msg.c_str());
    };
    
    // Parse checksum if provided
    String checksumAlgo, checksumHash;
    bool hasChecksum = parseChecksum(checksum, checksumAlgo, checksumHash);
    
    reportProgress(Status::CONNECTING, 0, "Connecting to server...");
    
    HTTPClient http;
    http.setFollowRedirects(HTTPC_STRICT_FOLLOW_REDIRECTS);
    http.setTimeout(30000); // 30 second timeout
    
    if (!http.begin(url)) {
        res.message = "HTTP begin failed";
        reportProgress(Status::FAILED, 0, res.message);
        return res;
    }

    int code = http.GET();
    res.httpCode = code;
    
    if (code != HTTP_CODE_OK) {
        res.message = String("HTTP GET failed: ") + code;
        http.end();
        reportProgress(Status::FAILED, 0, res.message);
        return res;
    }

    int contentLen = http.getSize();
    if (contentLen <= 0) {
        res.message = "Invalid content length";
        http.end();
        reportProgress(Status::FAILED, 0, res.message);
        return res;
    }
    
    Logger::info("OTA firmware size: %d bytes", contentLen);
    reportProgress(Status::DOWNLOADING, 0, String("Downloading ") + contentLen + " bytes");

    // For SHA256 verification, we need to read into a buffer first
    // For MD5, the Update library handles it natively
    if (hasChecksum && checksumAlgo == "sha256") {
        // SHA256 verification path - download to buffer first
        uint8_t* buffer = (uint8_t*)malloc(contentLen);
        if (!buffer) {
            res.message = "Failed to allocate buffer for SHA256 verification";
            http.end();
            reportProgress(Status::FAILED, 0, res.message);
            return res;
        }
        
        WiFiClient* stream = http.getStreamPtr();
        size_t totalRead = 0;
        int lastProgress = 0;
        
        while (totalRead < (size_t)contentLen && http.connected()) {
            size_t available = stream->available();
            if (available) {
                size_t toRead = min(available, (size_t)(contentLen - totalRead));
                size_t bytesRead = stream->readBytes(buffer + totalRead, toRead);
                totalRead += bytesRead;
                
                int progress = (totalRead * 100) / contentLen;
                if (progress != lastProgress && progress % 10 == 0) {
                    reportProgress(Status::DOWNLOADING, progress, 
                        String("Downloaded ") + totalRead + "/" + contentLen + " bytes");
                    lastProgress = progress;
                }
            }
            delay(1);
        }
        
        http.end();
        
        if (totalRead != (size_t)contentLen) {
            free(buffer);
            res.message = String("Download incomplete: ") + totalRead + "/" + contentLen;
            reportProgress(Status::FAILED, 0, res.message);
            return res;
        }
        
        // Verify SHA256
        reportProgress(Status::VERIFYING, 100, "Verifying SHA256 checksum...");
        
        if (!verifySha256(buffer, totalRead, checksumHash)) {
            free(buffer);
            res.message = "SHA256 checksum mismatch";
            reportProgress(Status::FAILED, 0, res.message);
            return res;
        }
        
        Logger::info("SHA256 checksum verified");
        reportProgress(Status::APPLYING, 0, "Applying firmware update...");
        
        // Apply the update from buffer
        if (!Update.begin(contentLen)) {
            free(buffer);
            res.message = String("Update.begin failed: ") + Update.getError();
            reportProgress(Status::FAILED, 0, res.message);
            return res;
        }
        
        size_t written = Update.write(buffer, contentLen);
        free(buffer);
        
        if (written != (size_t)contentLen) {
            Update.abort();
            res.message = String("Update write failed: wrote ") + written + "/" + contentLen;
            reportProgress(Status::FAILED, 0, res.message);
            return res;
        }
        
        if (!Update.end()) {
            res.message = String("Update.end failed: ") + Update.getError();
            reportProgress(Status::FAILED, 0, res.message);
            return res;
        }
    } else {
        // MD5 or no checksum - use streaming update (more memory efficient)
        reportProgress(Status::APPLYING, 0, "Starting firmware update...");
        
        if (!Update.begin(contentLen)) {
            res.message = String("Update.begin failed: ") + Update.getError();
            http.end();
            reportProgress(Status::FAILED, 0, res.message);
            return res;
        }
        
        // Set MD5 if provided
        if (hasChecksum && checksumAlgo == "md5") {
            Update.setMD5(checksumHash.c_str());
            Logger::info("MD5 checksum will be verified: %s", checksumHash.c_str());
        }
        
        WiFiClient* stream = http.getStreamPtr();
        uint8_t buf[1024];
        size_t totalWritten = 0;
        int lastProgress = 0;
        
        while (totalWritten < (size_t)contentLen && http.connected()) {
            size_t available = stream->available();
            if (available) {
                size_t toRead = min(available, sizeof(buf));
                toRead = min(toRead, (size_t)(contentLen - totalWritten));
                size_t bytesRead = stream->readBytes(buf, toRead);
                
                size_t written = Update.write(buf, bytesRead);
                if (written != bytesRead) {
                    Update.abort();
                    http.end();
                    res.message = "Write error during update";
                    reportProgress(Status::FAILED, 0, res.message);
                    return res;
                }
                
                totalWritten += written;
                
                int progress = (totalWritten * 100) / contentLen;
                if (progress != lastProgress && progress % 10 == 0) {
                    reportProgress(Status::APPLYING, progress,
                        String("Written ") + totalWritten + "/" + contentLen + " bytes");
                    lastProgress = progress;
                }
            }
            delay(1);
        }
        
        http.end();
        
        if (totalWritten != (size_t)contentLen) {
            Update.abort();
            res.message = String("Download incomplete: ") + totalWritten + "/" + contentLen;
            reportProgress(Status::FAILED, 0, res.message);
            return res;
        }
        
        if (!Update.end()) {
            res.message = String("Update.end failed: ") + Update.getError();
            reportProgress(Status::FAILED, 0, res.message);
            return res;
        }
    }
    
    if (!Update.isFinished()) {
        res.message = "Update not finished";
        reportProgress(Status::FAILED, 0, res.message);
        return res;
    }
    
    res.ok = true;
    res.message = "Update successful, rebooting...";
    reportProgress(Status::SUCCESS, 100, res.message);
    
    return res;
}
