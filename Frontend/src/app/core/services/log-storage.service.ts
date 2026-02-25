import { Injectable, signal } from '@angular/core';

export interface StoredLog {
  timestamp: number;
  level: string;
  message: string;
  coordId: string;
  formattedMessage: string;
  isConnectionEvent?: boolean; // Flag for special styling
}

/**
 * Service to persist coordinator logs across page navigation
 * Stores last 100 logs in memory and localStorage
 */
@Injectable({
  providedIn: 'root'
})
export class LogStorageService {
  private readonly STORAGE_KEY = 'coordinator_logs';
  private readonly MAX_LOGS = 100;
  
  // Signal to hold logs in memory
  readonly logs = signal<StoredLog[]>([]);

  constructor() {
    this.loadFromStorage();
  }

  /**
   * Add a new log entry
   */
  addLog(log: StoredLog): void {
    this.logs.update(current => {
      // Add new log at the beginning, keep only last MAX_LOGS
      const updated = [log, ...current].slice(0, this.MAX_LOGS);
      this.saveToStorage(updated);
      return updated;
    });
  }

  /**
   * Get logs filtered by coordinator ID
   */
  getLogsByCoordinator(coordId?: string): StoredLog[] {
    const allLogs = this.logs();
    if (!coordId) return allLogs;
    return allLogs.filter(log => log.coordId === coordId);
  }

  /**
   * Clear all logs
   */
  clearLogs(): void {
    this.logs.set([]);
    this.clearStorage();
  }

  /**
   * Clear logs for specific coordinator
   */
  clearCoordinatorLogs(coordId: string): void {
    this.logs.update(current => {
      const filtered = current.filter(log => log.coordId !== coordId);
      this.saveToStorage(filtered);
      return filtered;
    });
  }

  /**
   * Load logs from localStorage
   */
  private loadFromStorage(): void {
    try {
      const stored = localStorage.getItem(this.STORAGE_KEY);
      if (stored) {
        const parsed = JSON.parse(stored) as StoredLog[];
        // Keep only logs from last 24 hours
        const dayAgo = Date.now() - (24 * 60 * 60 * 1000);
        const recent = parsed.filter(log => log.timestamp > dayAgo);
        this.logs.set(recent.slice(0, this.MAX_LOGS));
      }
    } catch (error) {
      console.error('[LogStorage] Failed to load logs from localStorage:', error);
      this.clearStorage();
    }
  }

  /**
   * Save logs to localStorage
   */
  private saveToStorage(logs: StoredLog[]): void {
    try {
      localStorage.setItem(this.STORAGE_KEY, JSON.stringify(logs));
    } catch (error) {
      console.error('[LogStorage] Failed to save logs to localStorage:', error);
      // If storage is full, clear old logs and try again
      if (error instanceof Error && error.name === 'QuotaExceededError') {
        const reduced = logs.slice(0, 50); // Keep only 50 most recent
        try {
          localStorage.setItem(this.STORAGE_KEY, JSON.stringify(reduced));
          this.logs.set(reduced);
        } catch {
          console.error('[LogStorage] Still failed after reducing logs');
        }
      }
    }
  }

  /**
   * Clear localStorage
   */
  private clearStorage(): void {
    try {
      localStorage.removeItem(this.STORAGE_KEY);
    } catch (error) {
      console.error('[LogStorage] Failed to clear storage:', error);
    }
  }
}
