#pragma once

#include <Arduino.h>
#include <functional>
#include "DiscoveryManager.h"
#include "EspNowMessage.h"
#include "utils/SafeTimer.h"

// Default permit-join window duration (60 seconds as per spec)
constexpr uint32_t DEFAULT_PERMIT_JOIN_DURATION_MS = 60000;

// Binding timeout (10 seconds as per spec)
constexpr uint32_t BINDING_TIMEOUT_MS = 10000;

// Maximum concurrent binding attempts
constexpr size_t MAX_CONCURRENT_BINDINGS = 1;

/**
 * Coordinator pairing states
 */
enum class CoordinatorPairingState : uint8_t {
    OPERATIONAL = 0,        // Normal operation, discovery disabled
    DISCOVERY_ACTIVE = 1,   // Permit-join enabled, collecting advertisements
    BINDING = 2             // Actively binding a node
};

/**
 * Result of a binding attempt
 */
enum class BindingResult : uint8_t {
    SUCCESS = 0,
    TIMEOUT = 1,
    NODE_REJECTED = 2,
    NODE_ABORTED = 3,
    INTERNAL_ERROR = 4,
    ALREADY_BOUND = 5
};

/**
 * Information about an active binding attempt
 */
struct BindingAttempt {
    uint8_t node_mac[6];        // Target node MAC
    uint32_t offer_token;        // Unique token for this offer
    uint16_t assigned_tower_id;  // Tower ID assigned to this node
    uint32_t started_ms;         // When binding started
    bool accept_received;        // Whether PAIRING_ACCEPT was received
    
    BindingAttempt() :
        offer_token(0),
        assigned_tower_id(0),
        started_ms(0),
        accept_received(false) {
        memset(node_mac, 0, 6);
    }
    
    bool isTimedOut(uint32_t now_ms) const {
        return (now_ms - started_ms) > BINDING_TIMEOUT_MS;
    }
    
    String macString() const {
        char buf[18];
        snprintf(buf, sizeof(buf), "%02X:%02X:%02X:%02X:%02X:%02X",
                 node_mac[0], node_mac[1], node_mac[2], node_mac[3], node_mac[4], node_mac[5]);
        return String(buf);
    }
};

/**
 * Callback types for pairing events
 */
using PermitJoinChangedCallback = std::function<void(bool enabled, uint32_t remaining_ms)>;
using BindingStartedCallback = std::function<void(const DiscoveredNode& node)>;
using BindingCompletedCallback = std::function<void(const DiscoveredNode& node, BindingResult result)>;
using SendOfferCallback = std::function<bool(const uint8_t* mac, const PairingOfferMessage& offer)>;
using SendConfirmCallback = std::function<bool(const uint8_t* mac, const PairingConfirmMessage& confirm)>;
using SendRejectCallback = std::function<bool(const uint8_t* mac, const PairingRejectMessage& reject)>;

/**
 * PairingStateMachine: Manages the coordinator's pairing state and workflow
 * 
 * Responsibilities:
 * - Manage permit-join window timing
 * - Coordinate binding workflow (offer -> accept -> confirm)
 * - Handle timeouts and failures
 * - Integrate with DiscoveryManager for node tracking
 */
class PairingStateMachine {
public:
    PairingStateMachine(DiscoveryManager& discoveryMgr);
    ~PairingStateMachine() = default;
    
    /**
     * Initialize the state machine
     * @param coordMac Coordinator's MAC address
     * @param coordId Coordinator ID
     * @param farmId Farm/Site ID
     */
    void begin(const uint8_t* coordMac, uint16_t coordId, uint16_t farmId);
    
    /**
     * Process tick - handle timeouts and state transitions
     * Call this from main loop
     */
    void tick();
    
    /**
     * Get current state
     */
    CoordinatorPairingState getState() const { return state; }
    
    /**
     * Check if permit-join is enabled
     */
    bool isPermitJoinEnabled() const { return state == CoordinatorPairingState::DISCOVERY_ACTIVE || 
                                               state == CoordinatorPairingState::BINDING; }
    
    /**
     * Get remaining permit-join time in ms
     */
    uint32_t getPermitJoinRemainingMs() const;
    
    // --- Commands ---
    
    /**
     * Enable permit-join window
     * @param duration_ms Duration in milliseconds (default 60s)
     * @return true if successfully enabled
     */
    bool enablePermitJoin(uint32_t duration_ms = DEFAULT_PERMIT_JOIN_DURATION_MS);
    
    /**
     * Disable permit-join window
     */
    void disablePermitJoin();
    
    /**
     * Approve a node for pairing (sends PAIRING_OFFER)
     * @param mac 6-byte MAC address of the node to approve
     * @return true if offer was sent
     */
    bool approveNode(const uint8_t* mac);
    
    /**
     * Approve a node by MAC string
     * @param macStr MAC address as string
     * @return true if offer was sent
     */
    bool approveNodeByMacString(const String& macStr);
    
    /**
     * Reject a node (sends PAIRING_REJECT)
     * @param mac 6-byte MAC address of the node to reject
     * @return true if rejection was sent
     */
    bool rejectNode(const uint8_t* mac);
    
    /**
     * Reject a node by MAC string
     * @param macStr MAC address as string
     * @return true if rejection was sent
     */
    bool rejectNodeByMacString(const String& macStr);
    
    // --- Message Handlers ---
    
    /**
     * Handle received PAIRING_ACCEPT message
     * @param msg The accept message
     * @return true if valid and processed
     */
    bool onPairingAcceptReceived(const PairingAcceptMessage& msg);
    
    /**
     * Handle received PAIRING_ABORT message
     * @param msg The abort message
     * @return true if valid and processed
     */
    bool onPairingAbortReceived(const PairingAbortMessage& msg);
    
    // --- Callbacks ---
    void setPermitJoinChangedCallback(PermitJoinChangedCallback cb) { onPermitJoinChanged = cb; }
    void setBindingStartedCallback(BindingStartedCallback cb) { onBindingStarted = cb; }
    void setBindingCompletedCallback(BindingCompletedCallback cb) { onBindingCompleted = cb; }
    void setSendOfferCallback(SendOfferCallback cb) { sendOffer = cb; }
    void setSendConfirmCallback(SendConfirmCallback cb) { sendConfirm = cb; }
    void setSendRejectCallback(SendRejectCallback cb) { sendReject = cb; }
    
    // --- Configuration ---
    
    /**
     * Set the next tower ID to assign
     * @param towerId Tower ID for the next paired node
     */
    void setNextTowerId(uint16_t towerId) { nextTowerId = towerId; }
    
    /**
     * Get the next tower ID that will be assigned
     */
    uint16_t getNextTowerId() const { return nextTowerId; }
    
private:
    DiscoveryManager& discovery;
    CoordinatorPairingState state;
    
    // Coordinator info
    uint8_t coordinatorMac[6];
    uint16_t coordinatorId;
    uint16_t farmId;
    uint16_t nextTowerId;
    
    // Permit-join timing
    Deadline permitJoinDl;
    
    // Active binding attempt
    BindingAttempt currentBinding;
    bool bindingActive;
    
    // Callbacks
    PermitJoinChangedCallback onPermitJoinChanged;
    BindingStartedCallback onBindingStarted;
    BindingCompletedCallback onBindingCompleted;
    SendOfferCallback sendOffer;
    SendConfirmCallback sendConfirm;
    SendRejectCallback sendReject;
    
    // Internal helpers
    uint32_t generateOfferToken();
    void transitionTo(CoordinatorPairingState newState);
    void handleBindingTimeout();
    void completeBinding(BindingResult result);
    bool compareMac(const uint8_t* mac1, const uint8_t* mac2) const;
};
