import { TestBed } from '@angular/core/testing';
import { provideExperimentalZonelessChangeDetection } from '@angular/core';
import { LogStorageService, StoredLog } from './log-storage.service';

/**
 * Unit tests for LogStorageService
 *
 * The service reads localStorage in its constructor (via loadFromStorage()),
 * so all localStorage setup MUST happen before TestBed.inject().
 */
describe('LogStorageService', () => {
  const STORAGE_KEY = 'coordinator_logs';

  /** Helper: build a StoredLog with sensible defaults. */
  function makeLog(overrides: Partial<StoredLog> = {}): StoredLog {
    return {
      timestamp: Date.now(),
      level: 'info',
      message: 'test message',
      coordId: 'coord-1',
      formattedMessage: '[INFO] test message',
      ...overrides,
    };
  }

  /** Inject the service AFTER localStorage has been set up. */
  function createService(): LogStorageService {
    TestBed.configureTestingModule({
      providers: [provideExperimentalZonelessChangeDetection()],
    });
    return TestBed.inject(LogStorageService);
  }

  beforeEach(() => {
    // Ensure a clean slate — remove any leftover key.
    localStorage.removeItem(STORAGE_KEY);
  });

  afterEach(() => {
    localStorage.removeItem(STORAGE_KEY);
  });

  // --------------------------------------------------------------------------
  // 1. Initial state — empty localStorage
  // --------------------------------------------------------------------------
  it('should initialise with an empty logs signal when localStorage has nothing', () => {
    const service = createService();
    expect(service.logs()).toEqual([]);
  });

  // --------------------------------------------------------------------------
  // 2. addLog() — prepends to the list
  // --------------------------------------------------------------------------
  it('should prepend a new log to the front of the list', () => {
    const service = createService();

    const first = makeLog({ message: 'first' });
    const second = makeLog({ message: 'second' });

    service.addLog(first);
    service.addLog(second);

    expect(service.logs().length).toBe(2);
    expect(service.logs()[0].message).toBe('second');
    expect(service.logs()[1].message).toBe('first');
  });

  // --------------------------------------------------------------------------
  // 3. addLog() — respects MAX_LOGS (100)
  // --------------------------------------------------------------------------
  it('should keep at most 100 logs when more are added', () => {
    const service = createService();

    for (let i = 0; i < 105; i++) {
      service.addLog(makeLog({ message: `log-${i}` }));
    }

    expect(service.logs().length).toBe(100);
    // The most recent log (i = 104) should be first
    expect(service.logs()[0].message).toBe('log-104');
  });

  // --------------------------------------------------------------------------
  // 4. addLog() — persists to localStorage
  // --------------------------------------------------------------------------
  it('should save logs to localStorage after addLog()', () => {
    const service = createService();
    service.addLog(makeLog({ message: 'persisted' }));

    const stored = JSON.parse(localStorage.getItem(STORAGE_KEY)!) as StoredLog[];
    expect(stored.length).toBe(1);
    expect(stored[0].message).toBe('persisted');
  });

  // --------------------------------------------------------------------------
  // 5. getLogsByCoordinator(coordId) — filters by coordId
  // --------------------------------------------------------------------------
  it('should return only logs matching the given coordId', () => {
    const service = createService();
    service.addLog(makeLog({ coordId: 'A', message: 'a1' }));
    service.addLog(makeLog({ coordId: 'B', message: 'b1' }));
    service.addLog(makeLog({ coordId: 'A', message: 'a2' }));

    const result = service.getLogsByCoordinator('A');
    expect(result.length).toBe(2);
    expect(result.every(l => l.coordId === 'A')).toBeTrue();
  });

  // --------------------------------------------------------------------------
  // 6. getLogsByCoordinator() without coordId — returns all logs
  // --------------------------------------------------------------------------
  it('should return all logs when coordId is omitted', () => {
    const service = createService();
    service.addLog(makeLog({ coordId: 'A' }));
    service.addLog(makeLog({ coordId: 'B' }));

    expect(service.getLogsByCoordinator().length).toBe(2);
    expect(service.getLogsByCoordinator(undefined).length).toBe(2);
  });

  // --------------------------------------------------------------------------
  // 7. clearLogs() — empties logs signal and removes from localStorage
  // --------------------------------------------------------------------------
  it('should clear the logs signal and remove the localStorage key', () => {
    const service = createService();
    service.addLog(makeLog());
    expect(service.logs().length).toBe(1);

    service.clearLogs();

    expect(service.logs()).toEqual([]);
    expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
  });

  // --------------------------------------------------------------------------
  // 8. clearCoordinatorLogs(coordId) — removes only matching, keeps others
  // --------------------------------------------------------------------------
  it('should remove only logs for the given coordId', () => {
    const service = createService();
    service.addLog(makeLog({ coordId: 'A', message: 'a' }));
    service.addLog(makeLog({ coordId: 'B', message: 'b' }));
    service.addLog(makeLog({ coordId: 'A', message: 'a2' }));

    service.clearCoordinatorLogs('A');

    expect(service.logs().length).toBe(1);
    expect(service.logs()[0].coordId).toBe('B');
  });

  // --------------------------------------------------------------------------
  // 9. Constructor loads from localStorage — valid JSON pre-populated
  // --------------------------------------------------------------------------
  it('should load logs from localStorage on construction', () => {
    const seed: StoredLog[] = [
      makeLog({ message: 'from-storage-1' }),
      makeLog({ message: 'from-storage-2' }),
    ];
    localStorage.setItem(STORAGE_KEY, JSON.stringify(seed));

    const service = createService();
    expect(service.logs().length).toBe(2);
    expect(service.logs()[0].message).toBe('from-storage-1');
  });

  // --------------------------------------------------------------------------
  // 10. Constructor filters out logs older than 24 hours
  // --------------------------------------------------------------------------
  it('should discard logs older than 24 hours during construction', () => {
    const recent = makeLog({ message: 'recent', timestamp: Date.now() - 1000 });
    const old = makeLog({ message: 'old', timestamp: Date.now() - 25 * 60 * 60 * 1000 });

    localStorage.setItem(STORAGE_KEY, JSON.stringify([recent, old]));

    const service = createService();
    expect(service.logs().length).toBe(1);
    expect(service.logs()[0].message).toBe('recent');
  });

  // --------------------------------------------------------------------------
  // 11. Constructor handles corrupt localStorage gracefully
  // --------------------------------------------------------------------------
  it('should clear storage and start fresh when localStorage contains invalid JSON', () => {
    localStorage.setItem(STORAGE_KEY, '{{{not-json');

    const service = createService();

    expect(service.logs()).toEqual([]);
    // clearStorage() is called, so the key should be removed
    expect(localStorage.getItem(STORAGE_KEY)).toBeNull();
  });

  // --------------------------------------------------------------------------
  // 12. saveToStorage QuotaExceededError — retries with reduced logs
  // --------------------------------------------------------------------------
  it('should attempt to retry with reduced logs when localStorage.setItem throws QuotaExceededError', () => {
    const service = createService();

    // Pre-fill the service with 80 logs so it has enough to trim
    for (let i = 0; i < 80; i++) {
      service.addLog(makeLog({ message: `log-${i}` }));
    }
    expect(service.logs().length).toBe(80);

    // Now make setItem throw QuotaExceededError on the NEXT call, then succeed on retry.
    // The service checks `error instanceof Error && error.name === 'QuotaExceededError'`.
    let callCount = 0;
    const originalSetItem = localStorage.setItem.bind(localStorage);
    spyOn(localStorage, 'setItem').and.callFake((key: string, value: string) => {
      if (key === STORAGE_KEY) {
        callCount++;
        if (callCount === 1) {
          const err = new Error('Storage quota exceeded');
          err.name = 'QuotaExceededError';
          throw err;
        }
      }
      // Retry or other keys — succeed
      return originalSetItem(key, value);
    });

    // Trigger saveToStorage by adding another log
    service.addLog(makeLog({ message: 'trigger-quota' }));

    // saveToStorage is called inside the signal update callback.
    // On QuotaExceededError it retries with 50 logs (this.logs.set(reduced)),
    // but the outer update callback still returns the full array.
    // Verify that setItem was called twice (first throw, then retry with reduced data):
    expect(callCount).toBe(2);

    // The retry should have written a JSON array of 50 items to localStorage:
    const storedJson = localStorage.getItem(STORAGE_KEY);
    expect(storedJson).not.toBeNull();
    const stored = JSON.parse(storedJson!) as StoredLog[];
    expect(stored.length).toBe(50);
  });

  // --------------------------------------------------------------------------
  // 13. clearCoordinatorLogs also updates localStorage
  // --------------------------------------------------------------------------
  it('should update localStorage after clearCoordinatorLogs()', () => {
    const service = createService();
    service.addLog(makeLog({ coordId: 'A' }));
    service.addLog(makeLog({ coordId: 'B' }));

    service.clearCoordinatorLogs('A');

    const stored = JSON.parse(localStorage.getItem(STORAGE_KEY)!) as StoredLog[];
    expect(stored.length).toBe(1);
    expect(stored[0].coordId).toBe('B');
  });

  // --------------------------------------------------------------------------
  // 14. addLog preserves isConnectionEvent flag
  // --------------------------------------------------------------------------
  it('should preserve the isConnectionEvent flag on stored logs', () => {
    const service = createService();
    service.addLog(makeLog({ isConnectionEvent: true }));

    expect(service.logs()[0].isConnectionEvent).toBeTrue();
  });

  // --------------------------------------------------------------------------
  // 15. Constructor caps loaded logs to MAX_LOGS (100)
  // --------------------------------------------------------------------------
  it('should cap logs loaded from localStorage to 100 entries', () => {
    const seed: StoredLog[] = [];
    for (let i = 0; i < 120; i++) {
      seed.push(makeLog({ message: `seed-${i}`, timestamp: Date.now() }));
    }
    localStorage.setItem(STORAGE_KEY, JSON.stringify(seed));

    const service = createService();
    expect(service.logs().length).toBe(100);
  });
});
