#pragma once

#include <Arduino.h>

/**
 * Deadline-based timer that handles millis() overflow correctly.
 * 
 * The standard pattern `deadline = millis() + duration` overflows after ~49.7 days.
 * This class uses subtraction: `(millis() - startTime) >= duration` which is
 * overflow-safe due to unsigned integer arithmetic.
 * 
 * Usage:
 *   Deadline timer;
 *   timer.set(5000);             // 5 second deadline
 *   if (timer.expired()) { ... } // true after 5 seconds
 *   timer.clear();               // disable the deadline
 */
class Deadline {
public:
    Deadline() : startMs_(0), durationMs_(0), active_(false) {}

    /** Start a deadline that expires after durationMs milliseconds from now. */
    void set(uint32_t durationMs) {
        startMs_ = millis();
        durationMs_ = durationMs;
        active_ = true;
    }

    /** Returns true if the deadline has expired. Returns false if inactive. */
    bool expired() const {
        if (!active_) return false;
        return (uint32_t)(millis() - startMs_) >= durationMs_;
    }

    /** Returns true if the deadline is active and has NOT expired yet. */
    bool running() const {
        return active_ && !expired();
    }

    /** Deactivate the deadline. expired() will return false. */
    void clear() {
        active_ = false;
    }

    /** Returns true if a deadline has been set (may or may not be expired). */
    bool isActive() const { return active_; }

    /** Returns remaining milliseconds. 0 if expired or inactive. Overflow-safe. */
    uint32_t remainingMs() const {
        if (!active_) return 0;
        uint32_t elapsed = (uint32_t)(millis() - startMs_);
        if (elapsed >= durationMs_) return 0;
        return durationMs_ - elapsed;
    }

private:
    uint32_t startMs_;
    uint32_t durationMs_;
    bool active_;
};
