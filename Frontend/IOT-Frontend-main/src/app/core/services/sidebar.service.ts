import { Injectable, signal } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class SidebarService {
  // Signal to track sidebar collapsed state
  readonly collapsed = signal<boolean>(false);

  /**
   * Toggle sidebar collapsed state
   */
  toggle(): void {
    this.collapsed.update(value => !value);
    
    // Optionally save to localStorage for persistence
    localStorage.setItem('sidebar-collapsed', JSON.stringify(this.collapsed()));
  }

  /**
   * Set sidebar collapsed state
   */
  setCollapsed(collapsed: boolean): void {
    this.collapsed.set(collapsed);
    localStorage.setItem('sidebar-collapsed', JSON.stringify(collapsed));
  }

  /**
   * Restore sidebar state from localStorage
   */
  restoreState(): void {
    const saved = localStorage.getItem('sidebar-collapsed');
    if (saved !== null) {
      this.collapsed.set(JSON.parse(saved));
    }
  }
}
