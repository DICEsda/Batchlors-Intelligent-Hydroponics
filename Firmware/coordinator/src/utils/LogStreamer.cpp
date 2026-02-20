#include "LogStreamer.h"

// Global instance definition
LogStreamer gLogStreamer;

// Implementation of forward-declared function from Logger.h
void streamLogToMqtt(uint8_t level, const char* levelStr, const char* message) {
    gLogStreamer.streamLog(level, levelStr, message);
}
