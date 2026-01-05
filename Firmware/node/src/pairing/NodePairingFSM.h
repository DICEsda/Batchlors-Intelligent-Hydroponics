#pragma once

#include <Arduino.h>
#include <functional>
#include "EspNowMessage.h"

/**
 * @file NodePairingFSM.h
 * @brief V2 Pairing State Machine for Tower Nodes
 * 
 * Implements the node-side of the Zigbee-inspired permit-join pairing protocol.
 * Handles advertisement broadcasting, offer acceptance, and credential storage.
 * 
 * Protocol Flow (from node perspective):
 * 1. User triggers pairing mode (button press, command, etc.)
 * 2. Node broadcasts PAIRING_ADVERTISEMENT every 100ms (±20ms jitter)
 * 3. Coordinator discovers node, user approves in frontend
 * 4. Node receives PAIRING_OFFER with nonce_echo validation
 * 5. Node sends PAIRING_ACCEPT with offer_token
 * 6. Node receives PAIRING_CONFIRM with credentials
 * 7. Node saves credentials to NVS, transitions to BOUND state
 * 
 * @see Assets/docs/Pairing_Architecture_v2.md for full protocol specification
 */

// Forward declaration
class TowerConfig;

/**
 * @brief Node pairing states
 */
enum class NodePairingState : uint8_t {
    INIT = 0,           // Initial state, checking NVS for existing credentials
    UNPAIRED = 1,       // No credentials found, waiting for user to trigger pairing
    ADVERTISING = 2,    // Sending PAIRING_ADVERTISEMENT broadcasts
    OFFER_RECEIVED = 3, // Received valid PAIRING_OFFER, preparing PAIRING_ACCEPT
    WAITING_CONFIRM = 4,// Sent PAIRING_ACCEPT, waiting for PAIRING_CONFIRM
    BOUND = 5,          // Successfully paired, credentials saved to NVS
    OPERATIONAL = 6     // Normal operation mode (telemetry, commands, etc.)
};

/**
 * @brief Result of pairing attempt
 */
enum class NodePairingResult : uint8_t {
    SUCCESS = 0,
    TIMEOUT = 1,            // Advertising or confirm timeout
    REJECTED = 2,           // Coordinator sent PAIRING_REJECT
    USER_CANCELLED = 3,     // User cancelled pairing
    NONCE_MISMATCH = 4,     // Offer nonce didn't match current nonce
    TOKEN_MISMATCH = 5,     // Confirm didn't match expected token
    NVS_ERROR = 6,          // Failed to save credentials
    INTERNAL_ERROR = 7
};

/**
 * @brief Reason for pairing rejection (subset of PairingRejectReason for node use)
 */
enum class NodeRejectReason : uint8_t {
    USER_CANCELLED = 0,
    TIMEOUT = 1,
    INTERNAL_ERROR = 2
};

/**
 * @brief Callback types for node pairing events
 */
using NodeStateChangedCallback = std::function<void(NodePairingState oldState, NodePairingState newState)>;
using NodePairingCompleteCallback = std::function<void(NodePairingResult result, uint16_t towerId)>;
using SendAdvertisementCallback = std::function<bool(const PairingAdvertisementMessage& adv)>;
using SendAcceptCallback = std::function<bool(const uint8_t* coordMac, const PairingAcceptMessage& accept)>;
using SendAbortCallback = std::function<bool(const uint8_t* coordMac, const PairingAbortMessage& abort)>;

/**
 * @brief Node-side V2 Pairing State Machine
 * 
 * Manages the complete pairing workflow from the node's perspective:
 * - Advertisement broadcasting with timing and jitter
 * - Nonce generation and rotation
 * - Offer validation and acceptance
 * - Credential storage via TowerConfig
 * - Timeout and error handling
 * 
 * Usage:
 * @code
 * NodePairingFSM pairing(towerConfig);
 * pairing.begin(myMac, DeviceType::TOWER, 0x010000);
 * pairing.setSendAdvertisementCallback([](const PairingAdvertisementMessage& adv) {
 *     return espNowBroadcast(adv);
 * });
 * 
 * // In main loop:
 * pairing.tick();
 * 
 * // When user presses pair button:
 * pairing.startAdvertising();
 * 
 * // When ESP-NOW receives PAIRING_OFFER:
 * pairing.onPairingOfferReceived(offer);
 * @endcode
 */
class NodePairingFSM {
public:
    /**
     * @brief Construct a new NodePairingFSM
     * @param config Reference to TowerConfig for credential storage
     */
    explicit NodePairingFSM(TowerConfig& config);
    ~NodePairingFSM() = default;

    // Non-copyable
    NodePairingFSM(const NodePairingFSM&) = delete;
    NodePairingFSM& operator=(const NodePairingFSM&) = delete;

    /**
     * @brief Initialize the state machine
     * @param nodeMac This node's 6-byte MAC address
     * @param deviceType Type of device (TOWER, SENSOR, etc.)
     * @param firmwareVersion Packed firmware version (use packFirmwareVersion helper)
     * @param capabilities Optional capability flags
     */
    void begin(const uint8_t* nodeMac, DeviceType deviceType, 
               uint32_t firmwareVersion, uint16_t capabilities = 0);

    /**
     * @brief Process tick - handle timing, advertisements, and timeouts
     * Call this from main loop, ideally every 10-50ms
     */
    void tick();

    /**
     * @brief Get current state
     */
    NodePairingState getState() const { return _state; }

    /**
     * @brief Get state as string for logging
     */
    const char* getStateString() const;

    /**
     * @brief Check if currently in pairing mode (advertising or waiting)
     */
    bool isPairing() const;

    /**
     * @brief Check if successfully paired
     */
    bool isBound() const { return _state == NodePairingState::BOUND || 
                                  _state == NodePairingState::OPERATIONAL; }

    // =========================================================================
    // Commands
    // =========================================================================

    /**
     * @brief Start advertising for pairing
     * Transitions from UNPAIRED to ADVERTISING state
     * @return true if advertising started
     */
    bool startAdvertising();

    /**
     * @brief Stop advertising and cancel pairing
     * Sends PAIRING_ABORT if in WAITING_CONFIRM state
     * @return true if cancelled
     */
    bool cancelPairing();

    /**
     * @brief Transition to operational mode after successful pairing
     * Call this after BOUND state to begin normal operations
     */
    void enterOperational();

    /**
     * @brief Force reset to UNPAIRED state (for factory reset)
     * Clears credentials from NVS
     */
    void reset();

    // =========================================================================
    // Message Handlers (call from ESP-NOW receive callback)
    // =========================================================================

    /**
     * @brief Handle received PAIRING_OFFER message
     * @param offer The offer message from coordinator
     * @param senderMac Sender's MAC address (for response)
     * @return true if valid offer and PAIRING_ACCEPT sent
     */
    bool onPairingOfferReceived(const PairingOfferMessage& offer, const uint8_t* senderMac);

    /**
     * @brief Handle received PAIRING_CONFIRM message
     * @param confirm The confirm message from coordinator
     * @return true if valid confirm and credentials saved
     */
    bool onPairingConfirmReceived(const PairingConfirmMessage& confirm);

    /**
     * @brief Handle received PAIRING_REJECT message
     * @param reject The reject message from coordinator
     * @return true if processed
     */
    bool onPairingRejectReceived(const PairingRejectMessage& reject);

    // =========================================================================
    // Callbacks
    // =========================================================================

    void setStateChangedCallback(NodeStateChangedCallback cb) { _onStateChanged = cb; }
    void setPairingCompleteCallback(NodePairingCompleteCallback cb) { _onPairingComplete = cb; }
    void setSendAdvertisementCallback(SendAdvertisementCallback cb) { _sendAdvertisement = cb; }
    void setSendAcceptCallback(SendAcceptCallback cb) { _sendAccept = cb; }
    void setSendAbortCallback(SendAbortCallback cb) { _sendAbort = cb; }

    // =========================================================================
    // Status/Diagnostics
    // =========================================================================

    /**
     * @brief Get current nonce value
     */
    uint32_t getCurrentNonce() const { return _currentNonce; }

    /**
     * @brief Get advertisement sequence number
     */
    uint16_t getSequenceNumber() const { return _sequenceNum; }

    /**
     * @brief Get time remaining in advertising mode (ms)
     * @return 0 if not advertising
     */
    uint32_t getAdvertisingRemainingMs() const;

    /**
     * @brief Get assigned tower ID (after pairing)
     */
    uint16_t getAssignedTowerId() const { return _assignedTowerId; }

private:
    // Configuration
    TowerConfig& _config;
    
    // Node identity
    uint8_t _nodeMac[6];
    DeviceType _deviceType;
    uint32_t _firmwareVersion;
    uint16_t _capabilities;

    // State machine
    NodePairingState _state;
    bool _initialized;

    // Advertisement timing
    uint32_t _advStartMs;           // When advertising started
    uint32_t _lastAdvMs;            // Last advertisement sent
    uint32_t _nextAdvMs;            // Next scheduled advertisement
    uint16_t _sequenceNum;          // Advertisement sequence counter

    // Nonce management
    uint32_t _currentNonce;         // Current advertisement nonce
    uint32_t _lastNonceRotationMs;  // When nonce was last rotated

    // Binding state
    uint8_t _pendingCoordMac[6];    // Coordinator MAC from offer
    uint32_t _pendingOfferToken;    // Token from PAIRING_OFFER
    uint16_t _pendingTowerId;       // Tower ID from offer
    uint32_t _acceptSentMs;         // When PAIRING_ACCEPT was sent

    // Result tracking
    uint16_t _assignedTowerId;      // Final assigned tower ID

    // Callbacks
    NodeStateChangedCallback _onStateChanged;
    NodePairingCompleteCallback _onPairingComplete;
    SendAdvertisementCallback _sendAdvertisement;
    SendAcceptCallback _sendAccept;
    SendAbortCallback _sendAbort;

    // Internal methods
    void transitionTo(NodePairingState newState);
    void generateNewNonce();
    void rotateNonceIfNeeded();
    uint32_t calculateNextAdvDelay();
    void sendAdvertisementMessage();
    void handleAdvertisingTimeout();
    void handleConfirmTimeout();
    void completePairing(NodePairingResult result);
    bool saveCredentials(const PairingConfirmMessage& confirm);
    bool loadExistingCredentials();
    bool compareMac(const uint8_t* mac1, const uint8_t* mac2) const;
    String macToString(const uint8_t* mac) const;
};

// ============================================================================
// State String Helper (inline for header-only convenience)
// ============================================================================

inline const char* NodePairingFSM::getStateString() const {
    switch (_state) {
        case NodePairingState::INIT:            return "INIT";
        case NodePairingState::UNPAIRED:        return "UNPAIRED";
        case NodePairingState::ADVERTISING:     return "ADVERTISING";
        case NodePairingState::OFFER_RECEIVED:  return "OFFER_RECEIVED";
        case NodePairingState::WAITING_CONFIRM: return "WAITING_CONFIRM";
        case NodePairingState::BOUND:           return "BOUND";
        case NodePairingState::OPERATIONAL:     return "OPERATIONAL";
        default:                                return "UNKNOWN";
    }
}

inline bool NodePairingFSM::isPairing() const {
    return _state == NodePairingState::ADVERTISING ||
           _state == NodePairingState::OFFER_RECEIVED ||
           _state == NodePairingState::WAITING_CONFIRM;
}
