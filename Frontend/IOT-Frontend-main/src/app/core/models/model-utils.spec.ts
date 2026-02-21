import {
  getBatteryPercent,
  getBatteryStatus,
} from './node.model';

import {
  getCoordinatorStatus,
  getSignalStrength,
  Coordinator,
} from './coordinator.model';

import {
  getPairingSecondsRemaining,
  isPairingSessionActive,
  formatPairingCountdown,
  PairingSession,
} from './pairing.model';

/**
 * Unit tests for pure utility functions exported from the model files.
 * No Angular TestBed needed — these are plain functions.
 */
describe('Model Utility Functions', () => {

  // ==========================================================================
  //  node.model — getBatteryPercent
  // ==========================================================================

  describe('getBatteryPercent', () => {
    it('should return 100 for a fully charged battery (4200 mV)', () => {
      expect(getBatteryPercent(4200)).toBe(100);
    });

    it('should return 0 for a fully depleted battery (3000 mV)', () => {
      expect(getBatteryPercent(3000)).toBe(0);
    });

    it('should return 50 for mid-range voltage (3600 mV)', () => {
      expect(getBatteryPercent(3600)).toBe(50);
    });

    it('should clamp to 0 when voltage is below minimum (2000 mV)', () => {
      expect(getBatteryPercent(2000)).toBe(0);
    });

    it('should clamp to 100 when voltage is above maximum (5000 mV)', () => {
      expect(getBatteryPercent(5000)).toBe(100);
    });

    it('should return a rounded integer', () => {
      const result = getBatteryPercent(3333); // (333/1200)*100 = 27.75 → 28
      expect(result).toBe(Math.round(((3333 - 3000) / 1200) * 100));
      expect(Number.isInteger(result)).toBeTrue();
    });
  });

  // ==========================================================================
  //  node.model — getBatteryStatus
  // ==========================================================================

  describe('getBatteryStatus', () => {
    it('should return "good" for a healthy battery (4200 mV → 100%)', () => {
      expect(getBatteryStatus(4200)).toBe('good');
    });

    it('should return "low" for 25% battery (3300 mV)', () => {
      // (3300-3000)/1200*100 = 25%  → >10 but ≤30 → low
      expect(getBatteryStatus(3300)).toBe('low');
    });

    it('should return "critical" for ~4% battery (3050 mV)', () => {
      // (3050-3000)/1200*100 ≈ 4.17% → ≤10 → critical
      expect(getBatteryStatus(3050)).toBe('critical');
    });

    it('should return "critical" for 0% battery (3000 mV)', () => {
      expect(getBatteryStatus(3000)).toBe('critical');
    });

    it('should return "good" for a battery just above 30% (~3360 mV)', () => {
      // We need percent > 30, so getBatteryPercent must be 31
      // 31 = ((x-3000)/1200)*100 → x = 3372
      expect(getBatteryStatus(3372)).toBe('good');
    });
  });

  // ==========================================================================
  //  coordinator.model — getCoordinatorStatus
  // ==========================================================================

  describe('getCoordinatorStatus', () => {
    /** Helper to build a minimal Coordinator-like object. */
    function makeCoordinator(lastSeenMinutesAgo: number, rssi = -50): Coordinator {
      return {
        _id: 'c1',
        coord_id: 'coord-1',
        site_id: 'site-1',
        fw_version: '1.0.0',
        nodes_online: 5,
        wifi_rssi: rssi,
        mmwave_event_rate: 0,
        light_lux: 500,
        temp_c: 22,
        last_seen: new Date(Date.now() - lastSeenMinutesAgo * 60 * 1000),
      };
    }

    it('should return "offline" when last seen > 5 minutes ago', () => {
      expect(getCoordinatorStatus(makeCoordinator(10))).toBe('offline');
    });

    it('should return "warning" when last seen > 2 but ≤ 5 minutes ago', () => {
      expect(getCoordinatorStatus(makeCoordinator(3))).toBe('warning');
    });

    it('should return "online" when last seen ≤ 2 minutes ago with good RSSI', () => {
      expect(getCoordinatorStatus(makeCoordinator(1, -50))).toBe('online');
    });

    it('should return "warning" when RSSI is below -80 even if recently seen', () => {
      expect(getCoordinatorStatus(makeCoordinator(0.5, -85))).toBe('warning');
    });

    it('should return "online" for RSSI exactly -80 when recently seen', () => {
      // wifi_rssi < -80 triggers warning; -80 is NOT < -80
      expect(getCoordinatorStatus(makeCoordinator(0.5, -80))).toBe('online');
    });
  });

  // ==========================================================================
  //  coordinator.model — getSignalStrength
  // ==========================================================================

  describe('getSignalStrength', () => {
    it('should return "excellent" for RSSI ≥ -50', () => {
      expect(getSignalStrength(-40)).toBe('excellent');
      expect(getSignalStrength(-50)).toBe('excellent');
    });

    it('should return "good" for RSSI ≥ -60 and < -50', () => {
      expect(getSignalStrength(-55)).toBe('good');
      expect(getSignalStrength(-60)).toBe('good');
    });

    it('should return "fair" for RSSI ≥ -70 and < -60', () => {
      expect(getSignalStrength(-65)).toBe('fair');
      expect(getSignalStrength(-70)).toBe('fair');
    });

    it('should return "poor" for RSSI < -70', () => {
      expect(getSignalStrength(-80)).toBe('poor');
      expect(getSignalStrength(-100)).toBe('poor');
    });

    it('should return "excellent" for 0 (theoretical max)', () => {
      expect(getSignalStrength(0)).toBe('excellent');
    });
  });

  // ==========================================================================
  //  pairing.model — getPairingSecondsRemaining
  // ==========================================================================

  describe('getPairingSecondsRemaining', () => {
    /** Helper to build a PairingSession. */
    function makeSession(overrides: Partial<PairingSession> = {}): PairingSession {
      return {
        coord_id: 'coord-1',
        farm_id: 'farm-1',
        status: 'active',
        started_at: new Date(Date.now() - 30000).toISOString(),
        expires_at: new Date(Date.now() + 30000).toISOString(), // 30 s from now
        duration_s: 60,
        pending_requests: [],
        approved_towers: [],
        rejected_towers: [],
        ...overrides,
      };
    }

    it('should return ~30 for a session expiring in 30 seconds', () => {
      const session = makeSession({
        expires_at: new Date(Date.now() + 30000).toISOString(),
      });
      const remaining = getPairingSecondsRemaining(session);
      // Allow ±1 second tolerance for timing
      expect(remaining).toBeGreaterThanOrEqual(29);
      expect(remaining).toBeLessThanOrEqual(31);
    });

    it('should return 0 for an expired active session', () => {
      const session = makeSession({
        expires_at: new Date(Date.now() - 5000).toISOString(),
      });
      expect(getPairingSecondsRemaining(session)).toBe(0);
    });

    it('should return 0 for a cancelled session', () => {
      const session = makeSession({ status: 'cancelled' });
      expect(getPairingSecondsRemaining(session)).toBe(0);
    });

    it('should return 0 for a completed session', () => {
      const session = makeSession({ status: 'completed' });
      expect(getPairingSecondsRemaining(session)).toBe(0);
    });

    it('should return 0 for an expired-status session', () => {
      const session = makeSession({ status: 'expired' });
      expect(getPairingSecondsRemaining(session)).toBe(0);
    });
  });

  // ==========================================================================
  //  pairing.model — isPairingSessionActive
  // ==========================================================================

  describe('isPairingSessionActive', () => {
    function makeSession(overrides: Partial<PairingSession> = {}): PairingSession {
      return {
        coord_id: 'coord-1',
        farm_id: 'farm-1',
        status: 'active',
        started_at: new Date(Date.now() - 30000).toISOString(),
        expires_at: new Date(Date.now() + 30000).toISOString(),
        duration_s: 60,
        pending_requests: [],
        approved_towers: [],
        rejected_towers: [],
        ...overrides,
      };
    }

    it('should return true for an active session that has not expired', () => {
      expect(isPairingSessionActive(makeSession())).toBeTrue();
    });

    it('should return false for an active session that has expired', () => {
      const session = makeSession({
        expires_at: new Date(Date.now() - 5000).toISOString(),
      });
      expect(isPairingSessionActive(session)).toBeFalse();
    });

    it('should return false for a completed session', () => {
      expect(isPairingSessionActive(makeSession({ status: 'completed' }))).toBeFalse();
    });

    it('should return false for a cancelled session', () => {
      expect(isPairingSessionActive(makeSession({ status: 'cancelled' }))).toBeFalse();
    });

    it('should return false for an expired-status session', () => {
      expect(isPairingSessionActive(makeSession({ status: 'expired' }))).toBeFalse();
    });
  });

  // ==========================================================================
  //  pairing.model — formatPairingCountdown
  // ==========================================================================

  describe('formatPairingCountdown', () => {
    it('should format 65 seconds as "1:05"', () => {
      expect(formatPairingCountdown(65)).toBe('1:05');
    });

    it('should format 5 seconds as "0:05"', () => {
      expect(formatPairingCountdown(5)).toBe('0:05');
    });

    it('should format 0 seconds as "0:00"', () => {
      expect(formatPairingCountdown(0)).toBe('0:00');
    });

    it('should format 60 seconds as "1:00"', () => {
      expect(formatPairingCountdown(60)).toBe('1:00');
    });

    it('should format 125 seconds as "2:05"', () => {
      expect(formatPairingCountdown(125)).toBe('2:05');
    });

    it('should format 59 seconds as "0:59"', () => {
      expect(formatPairingCountdown(59)).toBe('0:59');
    });
  });
});
