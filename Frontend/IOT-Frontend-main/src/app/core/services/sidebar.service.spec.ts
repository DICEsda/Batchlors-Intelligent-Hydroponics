import { TestBed } from '@angular/core/testing';
import { provideExperimentalZonelessChangeDetection } from '@angular/core';

import { SidebarService } from './sidebar.service';

describe('SidebarService', () => {
  let service: SidebarService;

  beforeEach(() => {
    // Spy on localStorage before creating the service so that any
    // constructor-level access is also captured.
    spyOn(localStorage, 'getItem').and.returnValue(null);
    spyOn(localStorage, 'setItem');

    TestBed.configureTestingModule({
      providers: [provideExperimentalZonelessChangeDetection()],
    });
    service = TestBed.inject(SidebarService);
  });

  // ---------------------------------------------------------------------------
  // Initial state
  // ---------------------------------------------------------------------------

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should have collapsed = false initially', () => {
    expect(service.collapsed()).toBe(false);
  });

  // ---------------------------------------------------------------------------
  // toggle()
  // ---------------------------------------------------------------------------

  it('should toggle collapsed from false to true', () => {
    service.toggle();
    expect(service.collapsed()).toBe(true);
  });

  it('should toggle collapsed back to false on second call', () => {
    service.toggle(); // false → true
    service.toggle(); // true  → false
    expect(service.collapsed()).toBe(false);
  });

  it('should persist state to localStorage on toggle', () => {
    service.toggle();

    expect(localStorage.setItem).toHaveBeenCalledWith(
      'sidebar-collapsed',
      JSON.stringify(true)
    );
  });

  it('should persist correct value after double toggle', () => {
    service.toggle(); // → true
    service.toggle(); // → false

    // The most recent call should store false
    expect(localStorage.setItem).toHaveBeenCalledWith(
      'sidebar-collapsed',
      JSON.stringify(false)
    );
  });

  // ---------------------------------------------------------------------------
  // setCollapsed()
  // ---------------------------------------------------------------------------

  it('should set collapsed to true and persist', () => {
    service.setCollapsed(true);

    expect(service.collapsed()).toBe(true);
    expect(localStorage.setItem).toHaveBeenCalledWith(
      'sidebar-collapsed',
      JSON.stringify(true)
    );
  });

  it('should set collapsed to false and persist', () => {
    service.setCollapsed(true);
    service.setCollapsed(false);

    expect(service.collapsed()).toBe(false);
    expect(localStorage.setItem).toHaveBeenCalledWith(
      'sidebar-collapsed',
      JSON.stringify(false)
    );
  });

  // ---------------------------------------------------------------------------
  // restoreState()
  // ---------------------------------------------------------------------------

  it('should restore collapsed = true from localStorage', () => {
    (localStorage.getItem as jasmine.Spy).and.returnValue('true');

    service.restoreState();

    expect(service.collapsed()).toBe(true);
    expect(localStorage.getItem).toHaveBeenCalledWith('sidebar-collapsed');
  });

  it('should restore collapsed = false from localStorage', () => {
    (localStorage.getItem as jasmine.Spy).and.returnValue('false');

    service.restoreState();

    expect(service.collapsed()).toBe(false);
  });

  it('should not change default when localStorage has no saved state', () => {
    // getItem already returns null from the beforeEach spy
    service.restoreState();

    expect(service.collapsed()).toBe(false);
  });

  // ---------------------------------------------------------------------------
  // Combination scenarios
  // ---------------------------------------------------------------------------

  it('should persist after setCollapsed then reflect correctly after restoreState', () => {
    // setCollapsed persists true
    service.setCollapsed(true);
    expect(localStorage.setItem).toHaveBeenCalledWith(
      'sidebar-collapsed',
      JSON.stringify(true)
    );

    // Simulate a fresh read from localStorage
    (localStorage.getItem as jasmine.Spy).and.returnValue('true');
    service.restoreState();
    expect(service.collapsed()).toBe(true);
  });
});
