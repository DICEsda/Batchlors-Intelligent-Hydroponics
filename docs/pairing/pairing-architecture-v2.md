# Pairing Architecture v2: Zigbee-like Permit Join Model

## Overview

This document describes the migration from the current direct auto-accept pairing model to a **passive discovery + frontend-approved pairing** model, inspired by Zigbee's permit-join mechanism.

### Design Goals

1. **Frontend Control**: Operators explicitly approve which nodes join the network
2. **Observability**: Real-time visibility into discovered nodes and pairing progress
3. **Robustness**: Graceful handling of failures, timeouts, and edge cases
4. **Security**: Nodes only bind after explicit coordinator confirmation
5. **Scalability**: Support multiple discovered nodes without race conditions

---

## 1. ESP-NOW Message Definitions

### 1.1 New Message Types

| Message Type | Direction | Purpose |
|--------------|-----------|---------|
| `PAIRING_ADVERTISEMENT` | Node → Broadcast | Node announces availability for pairing |
| `PAIRING_OFFER` | Coordinator → Node (Unicast) | Coordinator invites node to pair |
| `PAIRING_ACCEPT` | Node → Coordinator | Node accepts the pairing offer |
| `PAIRING_CONFIRM` | Coordinator → Node | Coordinator confirms binding complete |
| `PAIRING_REJECT` | Coordinator → Node | Coordinator rejects/cancels pairing |
| `PAIRING_ABORT` | Node → Coordinator | Node cancels pairing attempt |

### 1.2 Message Structures

#### PAIRING_ADVERTISEMENT (Node → Broadcast)
```
Field               Type        Size    Description
─────────────────────────────────────────────────────────
msg_type            uint8_t     1       = 0x20 (PAIRING_ADVERTISEMENT)
protocol_version    uint8_t     1       Protocol version (currently 0x02)
node_mac            uint8_t[6]  6       Node's MAC address
device_type         uint8_t     1       Device type enum (TOWER=1, SENSOR=2, etc.)
firmware_version    uint32_t    4       Firmware version (major.minor.patch packed)
capabilities        uint16_t    2       Capability flags (LED, pump, sensors, etc.)
nonce               uint32_t    4       Random nonce for this advertisement cycle
sequence_num        uint16_t    2       Advertisement sequence counter
rssi_request        int8_t      1       Node's perceived RSSI (for coordinator reference)
─────────────────────────────────────────────────────────
Total: 22 bytes
```

#### PAIRING_OFFER (Coordinator → Node Unicast)
```
Field               Type        Size    Description
─────────────────────────────────────────────────────────
msg_type            uint8_t     1       = 0x21 (PAIRING_OFFER)
protocol_version    uint8_t     1       Protocol version
coord_mac           uint8_t[6]  6       Coordinator's MAC address
coord_id            uint16_t    2       Coordinator ID
farm_id             uint16_t    2       Farm/Site ID
offered_tower_id    uint16_t    2       Assigned tower ID for this node
nonce_echo          uint32_t    4       Echo of node's advertisement nonce
offer_token         uint32_t    4       Unique token for this offer (anti-replay)
channel             uint8_t     1       ESP-NOW channel to use
─────────────────────────────────────────────────────────
Total: 23 bytes
```

#### PAIRING_ACCEPT (Node → Coordinator)
```
Field               Type        Size    Description
─────────────────────────────────────────────────────────
msg_type            uint8_t     1       = 0x22 (PAIRING_ACCEPT)
node_mac            uint8_t[6]  6       Node's MAC address
offer_token         uint32_t    4       Token from PAIRING_OFFER (proves receipt)
accepted_tower_id   uint16_t    2       Echoed tower ID
─────────────────────────────────────────────────────────
Total: 13 bytes
```

#### PAIRING_CONFIRM (Coordinator → Node)
```
Field               Type        Size    Description
─────────────────────────────────────────────────────────
msg_type            uint8_t     1       = 0x23 (PAIRING_CONFIRM)
coord_mac           uint8_t[6]  6       Coordinator's MAC address
tower_id            uint16_t    2       Final assigned tower ID
encryption_key      uint8_t[16] 16      Optional LMK for encrypted ESP-NOW
config_flags        uint8_t     1       Configuration flags
─────────────────────────────────────────────────────────
Total: 26 bytes
```

#### PAIRING_REJECT / PAIRING_ABORT
```
Field               Type        Size    Description
─────────────────────────────────────────────────────────
msg_type            uint8_t     1       = 0x24 (REJECT) or 0x25 (ABORT)
mac                 uint8_t[6]  6       Sender's MAC address
reason_code         uint8_t     1       Rejection/abort reason enum
offer_token         uint32_t    4       Token reference (if applicable)
─────────────────────────────────────────────────────────
Total: 12 bytes
```

### 1.3 Reason Codes

```cpp
enum class PairingRejectReason : uint8_t {
    NONE = 0,
    PERMIT_JOIN_DISABLED = 1,
    CAPACITY_FULL = 2,
    DUPLICATE_MAC = 3,
    TIMEOUT = 4,
    USER_REJECTED = 5,
    PROTOCOL_MISMATCH = 6,
    INTERNAL_ERROR = 7,
    NODE_CANCELLED = 8
};
```

---

## 2. Node State Machine

### 2.1 State Diagram

```
                    ┌─────────────────────────────────────────────┐
                    │                                             │
                    ▼                                             │
    ┌──────┐    ┌──────────┐    ┌───────────────────┐    ┌───────────────┐
    │ INIT │───►│ UNPAIRED │───►│ PAIRING_ADVERTISE │───►│ PAIRING_WAIT  │
    └──────┘    └──────────┘    └───────────────────┘    └───────────────┘
                    ▲                     │                      │
                    │                     │ timeout              │ timeout/reject
                    │                     ▼                      │
                    │              (continue advertising)        │
                    │                                            │
                    └────────────────────────────────────────────┘
                                          │
                                          │ PAIRING_CONFIRM received
                                          ▼
                              ┌───────────────────┐
                              │       BOUND       │
                              └───────────────────┘
                                          │
                                          │ credentials persisted
                                          ▼
                              ┌───────────────────┐
                              │   OPERATIONAL     │◄────────────────┐
                              └───────────────────┘                 │
                                          │                         │
                                          │ unpair command          │ reboot with
                                          │ or factory reset        │ valid creds
                                          ▼                         │
                              ┌───────────────────┐                 │
                              │     UNPAIRED      │─────────────────┘
                              └───────────────────┘
```

### 2.2 State Descriptions

| State | Description | Entry Actions | Exit Conditions |
|-------|-------------|---------------|-----------------|
| **INIT** | Boot/hardware initialization | Initialize peripherals, load NVS | Always → check stored credentials |
| **UNPAIRED** | No valid pairing credentials | Clear transient state, LED: slow blink red | Button press or auto-start → PAIRING_ADVERTISE |
| **PAIRING_ADVERTISE** | Broadcasting availability | Start advertisement timer (100ms interval), LED: fast blink blue | Receive PAIRING_OFFER → PAIRING_WAIT, Timeout (5min) → UNPAIRED |
| **PAIRING_WAIT** | Awaiting coordinator confirmation | Stop broadcasting, send PAIRING_ACCEPT, start confirmation timer (10s), LED: solid blue | Receive PAIRING_CONFIRM → BOUND, Timeout/Reject → UNPAIRED |
| **BOUND** | Pairing confirmed, persisting | Save credentials to NVS, LED: flash green 3x | Persistence complete → OPERATIONAL |
| **OPERATIONAL** | Normal operation | Start telemetry, register with coordinator, LED: off/status | Unpair command → UNPAIRED, Connection lost (5min) → UNPAIRED |

### 2.3 State Transitions

```cpp
enum class NodePairingState : uint8_t {
    INIT = 0,
    UNPAIRED = 1,
    PAIRING_ADVERTISE = 2,
    PAIRING_WAIT = 3,
    BOUND = 4,
    OPERATIONAL = 5
};

// Transition triggers
enum class NodePairingEvent : uint8_t {
    BOOT_NO_CREDENTIALS,
    BOOT_WITH_CREDENTIALS,
    START_PAIRING_BUTTON,
    ADVERTISEMENT_TIMEOUT,
    OFFER_RECEIVED,
    CONFIRM_RECEIVED,
    REJECT_RECEIVED,
    WAIT_TIMEOUT,
    CREDENTIALS_SAVED,
    UNPAIR_COMMAND,
    CONNECTION_LOST_TIMEOUT
};
```

### 2.4 Advertisement Behavior

- **Interval**: 100ms between broadcasts (configurable)
- **Jitter**: ±20ms random jitter to avoid collisions
- **Nonce rotation**: New nonce every 30 seconds
- **Sequence counter**: Increments each broadcast (wraps at 65535)
- **Timeout**: Stop advertising after 5 minutes if no offer received
- **Backoff**: If channel busy, exponential backoff up to 500ms

---

## 3. Coordinator State Machine

### 3.1 State Diagram

```
                              ┌─────────────────────────────────────┐
                              │                                     │
                              ▼                                     │
┌─────────────┐    ┌───────────────────┐    ┌───────────────────┐  │
│ OPERATIONAL │◄──►│  DISCOVERY_ACTIVE │───►│ PENDING_APPROVAL  │──┘
└─────────────┘    └───────────────────┘    └───────────────────┘
       ▲                    ▲                        │
       │                    │                        │ user approves
       │                    │ user rejects           ▼
       │                    │              ┌───────────────────┐
       │                    └──────────────│     BINDING       │
       │                                   └───────────────────┘
       │                                             │
       │                                             │ binding complete
       └─────────────────────────────────────────────┘
```

### 3.2 Coordinator Pairing Manager States

| State | Description | Actions |
|-------|-------------|---------|
| **OPERATIONAL** | Normal operation, discovery disabled | Ignore PAIRING_ADVERTISEMENT messages |
| **DISCOVERY_ACTIVE** | Permit-join enabled, collecting advertisements | Buffer discovered nodes, publish to MQTT, update last-seen timestamps |
| **PENDING_APPROVAL** | Node selected, awaiting frontend decision | Highlight node in discovered list, await approve/reject |
| **BINDING** | Actively binding a node | Send PAIRING_OFFER, await PAIRING_ACCEPT, send PAIRING_CONFIRM |

### 3.3 Discovered Nodes Table

The coordinator maintains an in-memory table of discovered nodes:

```cpp
struct DiscoveredNode {
    uint8_t mac[6];                    // Node MAC address
    DeviceType device_type;            // Tower, sensor, etc.
    uint32_t firmware_version;         // Packed firmware version
    uint16_t capabilities;             // Capability flags
    uint32_t last_nonce;               // Last seen nonce
    uint16_t last_sequence;            // Last sequence number
    int8_t rssi;                       // Signal strength
    uint32_t first_seen_ms;            // Timestamp first discovered
    uint32_t last_seen_ms;             // Timestamp last advertisement
    DiscoveryState state;              // DISCOVERED, PENDING, BINDING, BOUND
    uint32_t offer_token;              // Token if offer sent
};

enum class DiscoveryState : uint8_t {
    DISCOVERED = 0,     // Seen but not acted upon
    PENDING = 1,        // Awaiting frontend decision
    OFFER_SENT = 2,     // PAIRING_OFFER sent, awaiting accept
    BINDING = 3,        // PAIRING_ACCEPT received, confirming
    BOUND = 4,          // Successfully paired
    REJECTED = 5,       // User rejected
    FAILED = 6          // Binding failed
};
```

### 3.4 Discovery Rules

1. **Permit-join disabled**: Ignore all PAIRING_ADVERTISEMENT messages
2. **Permit-join enabled**:
   - New MAC: Add to discovered table, publish MQTT event
   - Known MAC: Update last_seen, RSSI; publish update if significant change
   - Duplicate nonce from same MAC: Ignore (already processed)
3. **Table limits**:
   - Maximum 32 discovered nodes
   - TTL: Remove entries not seen for 30 seconds
   - BOUND entries persist until explicitly removed

---

## 4. Pairing Sequence (Step-by-Step)

### 4.1 Happy Path Flow

```
Time    Node                    Coordinator                 Frontend/MQTT
─────────────────────────────────────────────────────────────────────────────
T+0     [UNPAIRED]              [OPERATIONAL]               
        User presses                                        
        pairing button                                      
                                                            
T+1     [PAIRING_ADVERTISE]                                 
        Broadcast                                           
        PAIRING_ADVERTISEMENT                               
        (100ms interval)                                    
                                                            
T+2                             [OPERATIONAL]               User clicks
                                Ignores advertisement       "Enable Permit Join"
                                                            
T+3                             [DISCOVERY_ACTIVE]          ← MQTT: permit_join/enable
                                Start discovery window      
                                (default 60s)               
                                                            
T+4     Broadcast               Receives advertisement      
        PAIRING_ADVERTISEMENT   Add to discovered table     
        ──────────────────────► Publish MQTT event ─────────► "Node AA:BB:CC discovered"
                                                            
T+5                                                         User selects node
                                                            Clicks "Approve"
                                                            
T+6                             [BINDING]                   ← MQTT: approve node AA:BB:CC
                                Generate offer_token        
                                Send PAIRING_OFFER          
        ◄───────────────────────────────────────            
                                                            
T+7     [PAIRING_WAIT]                                      
        Receive PAIRING_OFFER                               
        Validate nonce_echo                                 
        Send PAIRING_ACCEPT                                 
        ──────────────────────►                             
                                                            
T+8                             Receive PAIRING_ACCEPT      
                                Validate offer_token        
                                Register node in TowerRegistry
                                Send PAIRING_CONFIRM        
        ◄───────────────────────────────────────            
                                Publish MQTT event ─────────► "Node AA:BB:CC bound"
                                                            
T+9     [BOUND]                                             
        Receive PAIRING_CONFIRM                             
        Store credentials in NVS                            
        LED flash green                                     
                                                            
T+10    [OPERATIONAL]           [DISCOVERY_ACTIVE]          
        Begin normal telemetry  Continue discovery window   
        ──────────────────────► (can pair more nodes)       
                                                            
T+63                            [OPERATIONAL]               
                                Discovery window timeout    
                                Publish MQTT event ─────────► "Permit join closed"
```

### 4.2 Rejection Flow

```
T+5                                                         User selects node
                                                            Clicks "Reject"
                                                            
T+6                             [DISCOVERY_ACTIVE]          ← MQTT: reject node AA:BB:CC
                                Mark node as REJECTED       
                                Send PAIRING_REJECT         
        ◄───────────────────────────────────────            
                                Remove from discovered table
                                                            
T+7     [UNPAIRED]                                          
        Receive PAIRING_REJECT                              
        reason=USER_REJECTED                                
        Stop advertising                                    
        (user must re-initiate)                             
```

### 4.3 Timeout Flow

```
T+6                             [BINDING]                   ← MQTT: approve node
                                Send PAIRING_OFFER          
        ◄───────────────────────────────────────            
                                Start binding timeout (10s) 
                                                            
T+7     [PAIRING_WAIT]                                      
        (Node crashes or                                    
        loses power)                                        
                                                            
T+16                            Binding timeout!            
                                Mark node as FAILED         
                                Publish MQTT event ─────────► "Binding AA:BB:CC failed: timeout"
                                [DISCOVERY_ACTIVE]          
                                (node can re-advertise)     
```

---

## 5. MQTT Interaction Model

### 5.1 Topic Structure

```
site/{siteId}/coord/{coordId}/pairing/...
```

### 5.2 Commands (Frontend → Coordinator)

| Topic | Payload | Description |
|-------|---------|-------------|
| `.../pairing/permit_join` | `{"enable": true, "duration_ms": 60000}` | Enable/disable permit join window |
| `.../pairing/approve` | `{"mac": "AA:BB:CC:DD:EE:FF"}` | Approve pairing for discovered node |
| `.../pairing/reject` | `{"mac": "AA:BB:CC:DD:EE:FF"}` | Reject discovered node |
| `.../pairing/cancel` | `{}` | Cancel active binding attempt |
| `.../pairing/unpair` | `{"tower_id": 5}` | Unpair existing node |

### 5.3 Events (Coordinator → Frontend)

| Topic | Payload | Description |
|-------|---------|-------------|
| `.../pairing/status` | `{"state": "discovery_active", "remaining_ms": 45000}` | Current pairing manager state |
| `.../pairing/discovered` | `{"mac": "...", "type": "tower", "rssi": -45, "fw": "1.2.3", "caps": [...]}` | New node discovered |
| `.../pairing/discovered_update` | `{"mac": "...", "rssi": -42, "last_seen": 1234567890}` | Discovered node updated |
| `.../pairing/discovered_expired` | `{"mac": "..."}` | Node advertisement TTL expired |
| `.../pairing/binding_started` | `{"mac": "...", "tower_id": 5}` | Binding process started |
| `.../pairing/binding_progress` | `{"mac": "...", "step": "offer_sent"}` | Binding progress update |
| `.../pairing/bound` | `{"mac": "...", "tower_id": 5}` | Node successfully bound |
| `.../pairing/binding_failed` | `{"mac": "...", "reason": "timeout"}` | Binding failed |
| `.../pairing/rejected` | `{"mac": "..."}` | Node rejected by user |

### 5.4 Retained Status

The coordinator publishes retained status:

```json
// site/{siteId}/coord/{coordId}/pairing/status (retained)
{
    "state": "operational",           // or "discovery_active", "binding"
    "permit_join_enabled": false,
    "permit_join_remaining_ms": 0,
    "discovered_count": 0,
    "binding_mac": null
}
```

---

## 6. Migration Checklist

### 6.1 Files to Modify

#### firmware/shared/src/EspNowMessage.h
- [ ] Add `MessageType` enum values: `PAIRING_ADVERTISEMENT`, `PAIRING_OFFER`, `PAIRING_ACCEPT`, `PAIRING_CONFIRM`, `PAIRING_REJECT`, `PAIRING_ABORT`
- [ ] Add message structs for each new type
- [ ] Add `PairingRejectReason` enum
- [ ] Add `DeviceType` enum if not present
- [ ] Keep legacy `JOIN_REQUEST`/`JOIN_ACCEPT` for backward compatibility (deprecated)

#### firmware/coordinator/src/comm/EspNow.cpp/.h
- [ ] Add `onPairingAdvertisement()` callback registration
- [ ] Modify `enablePairingMode()` to support passive listening vs active binding
- [ ] Add `sendPairingOffer()`, `sendPairingConfirm()`, `sendPairingReject()`
- [ ] Remove auto-accept logic from ESP-NOW receive handler

#### firmware/coordinator/src/towers/TowerRegistry.cpp/.h
- [ ] Remove `processPairingRequest()` auto-accept logic
- [ ] Add `registerBoundNode()` called only after full handshake
- [ ] Keep node persistence logic (NVS)

#### firmware/coordinator/src/core/Coordinator.cpp/.h
- [ ] Integrate new `DiscoveryManager` and `PairingStateMachine`
- [ ] Update MQTT command handlers for new pairing commands
- [ ] Remove legacy pairing button handler (or redirect to permit-join)

#### firmware/coordinator/src/comm/Mqtt.cpp/.h
- [ ] Add subscription to `.../pairing/permit_join`, `.../pairing/approve`, etc.
- [ ] Add publish methods for pairing events
- [ ] Add retained status publishing

#### firmware/node/src/main.cpp
- [ ] Replace current state machine with `NodePairingFSM`
- [ ] Remove direct `JOIN_REQUEST` sending
- [ ] Add advertisement loop in `PAIRING_ADVERTISE` state
- [ ] Add `PAIRING_WAIT` state with timeout handling

#### firmware/node/src/config/TowerConfig.h
- [ ] Add `pairing_state` field for persistence
- [ ] Add `last_offer_token` for replay protection

### 6.2 New Files to Create

#### firmware/coordinator/src/pairing/DiscoveryManager.h/.cpp
```cpp
class DiscoveryManager {
public:
    void onAdvertisementReceived(const PairingAdvertisement& adv, int8_t rssi);
    void tick();  // Called every loop, handles TTL expiration
    const std::vector<DiscoveredNode>& getDiscoveredNodes() const;
    DiscoveredNode* findByMac(const uint8_t* mac);
    void clearExpired();
    void clearAll();
    
private:
    std::vector<DiscoveredNode> discovered_nodes_;
    static constexpr size_t MAX_DISCOVERED = 32;
    static constexpr uint32_t TTL_MS = 30000;
};
```

#### firmware/coordinator/src/pairing/PairingStateMachine.h/.cpp
```cpp
class PairingStateMachine {
public:
    enum class State { OPERATIONAL, DISCOVERY_ACTIVE, BINDING };
    
    void enablePermitJoin(uint32_t duration_ms);
    void disablePermitJoin();
    void approveNode(const uint8_t* mac);
    void rejectNode(const uint8_t* mac);
    void cancelBinding();
    void tick();
    
    State getState() const;
    uint32_t getRemainingPermitJoinMs() const;
    
private:
    State state_ = State::OPERATIONAL;
    uint32_t permit_join_end_ms_ = 0;
    uint8_t binding_mac_[6] = {0};
    uint32_t binding_start_ms_ = 0;
    uint32_t current_offer_token_ = 0;
    
    void startBinding(const uint8_t* mac);
    void onBindingTimeout();
    void onPairingAcceptReceived(const PairingAccept& msg);
};
```

#### firmware/node/src/pairing/NodePairingFSM.h/.cpp
```cpp
class NodePairingFSM {
public:
    enum class State { INIT, UNPAIRED, PAIRING_ADVERTISE, PAIRING_WAIT, BOUND, OPERATIONAL };
    
    void begin();
    void tick();
    void onPairingOfferReceived(const PairingOffer& offer);
    void onPairingConfirmReceived(const PairingConfirm& confirm);
    void onPairingRejectReceived(const PairingReject& reject);
    void startPairing();  // Triggered by button
    void unpair();
    
    State getState() const;
    
private:
    State state_ = State::INIT;
    uint32_t state_enter_ms_ = 0;
    uint32_t last_advertisement_ms_ = 0;
    uint32_t current_nonce_ = 0;
    uint16_t sequence_num_ = 0;
    PairingOffer pending_offer_;
    
    void enterState(State new_state);
    void sendAdvertisement();
    void sendPairingAccept();
    bool validateOffer(const PairingOffer& offer);
    void persistCredentials(const PairingConfirm& confirm);
};
```

### 6.3 Code to Remove

| File | What to Remove |
|------|----------------|
| `Coordinator.cpp` | Auto-accept in `onJoinRequest()` callback |
| `TowerRegistry.cpp` | `processPairingRequest()` immediate registration |
| `EspNow.cpp` | `handleJoinRequest()` that sends immediate `JOIN_ACCEPT` |
| `node/main.cpp` | Direct `sendJoinRequest()` calls, current simple state machine |

### 6.4 Backward Compatibility Notes

- Keep `JOIN_REQUEST` and `JOIN_ACCEPT` message types defined but deprecated
- Coordinator MAY respond to legacy `JOIN_REQUEST` with `PAIRING_REJECT` (reason=PROTOCOL_MISMATCH)
- Or, optionally support legacy mode via config flag for gradual migration

---

## 7. Testing Checklist

### 7.1 Unit Tests

- [ ] `NodePairingFSM` state transitions
- [ ] `PairingStateMachine` state transitions
- [ ] `DiscoveryManager` TTL expiration
- [ ] Message serialization/deserialization

### 7.2 Integration Tests

- [ ] Node advertisement received by coordinator
- [ ] Permit-join enable/disable via MQTT
- [ ] Full pairing happy path (advertisement → offer → accept → confirm)
- [ ] Rejection flow
- [ ] Timeout handling (offer timeout, accept timeout)
- [ ] Multiple nodes advertising simultaneously
- [ ] Node re-advertisement after failure

### 7.3 Edge Cases

- [ ] Node power loss during PAIRING_WAIT
- [ ] Coordinator restart with permit-join active
- [ ] Duplicate advertisements from same node
- [ ] RSSI changes during discovery
- [ ] Maximum discovered nodes limit
- [ ] Rapid approve/reject commands

---

## 8. Frontend Integration Notes

### 8.1 UI Components Needed

1. **Permit Join Toggle**: Button to enable/disable discovery mode with countdown timer
2. **Discovered Nodes List**: Real-time list showing MAC, type, RSSI, last seen
3. **Node Approval Dialog**: Approve/Reject buttons for each discovered node
4. **Binding Progress Indicator**: Shows current step (offer sent, waiting accept, etc.)
5. **Pairing History**: Log of recent pairing attempts and outcomes

### 8.2 WebSocket Events to Handle

```typescript
interface PairingDiscoveredEvent {
    type: 'pairing/discovered';
    mac: string;
    deviceType: 'tower' | 'sensor';
    rssi: number;
    firmwareVersion: string;
    capabilities: string[];
}

interface PairingStatusEvent {
    type: 'pairing/status';
    state: 'operational' | 'discovery_active' | 'binding';
    permitJoinEnabled: boolean;
    remainingMs: number;
    discoveredCount: number;
    bindingMac: string | null;
}

interface PairingBoundEvent {
    type: 'pairing/bound';
    mac: string;
    towerId: number;
}
```

---

## Appendix A: Timing Constants

| Constant | Value | Description |
|----------|-------|-------------|
| `ADV_INTERVAL_MS` | 100 | Advertisement broadcast interval |
| `ADV_JITTER_MS` | 20 | Random jitter added to interval |
| `ADV_TIMEOUT_MS` | 300000 | Stop advertising after 5 minutes |
| `NONCE_ROTATION_MS` | 30000 | Generate new nonce every 30s |
| `DISCOVERY_TTL_MS` | 30000 | Remove unseen nodes after 30s |
| `DEFAULT_PERMIT_JOIN_MS` | 60000 | Default permit-join window |
| `MAX_PERMIT_JOIN_MS` | 300000 | Maximum permit-join window |
| `BINDING_TIMEOUT_MS` | 10000 | Timeout waiting for PAIRING_ACCEPT |
| `CONFIRM_TIMEOUT_MS` | 5000 | Timeout waiting for PAIRING_CONFIRM |

## Appendix B: LED Feedback Patterns

| State | LED Pattern | Color |
|-------|-------------|-------|
| UNPAIRED | Slow blink (1Hz) | Red |
| PAIRING_ADVERTISE | Fast blink (4Hz) | Blue |
| PAIRING_WAIT | Solid | Blue |
| BOUND | 3x flash | Green |
| OPERATIONAL | Off / Status | - |
| Binding Error | 5x fast flash | Red |
