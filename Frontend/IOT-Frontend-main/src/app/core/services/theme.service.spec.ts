import { TestBed } from '@angular/core/testing';
import { provideExperimentalZonelessChangeDetection } from '@angular/core';
import { ThemeService, Theme } from './theme.service';

/**
 * Unit tests for ThemeService
 *
 * The service reads from localStorage, manipulates document.documentElement,
 * and registers a matchMedia listener in its constructor.
 * We spy on DOM APIs to keep the tests isolated.
 */
describe('ThemeService', () => {
  const THEME_KEY = 'theme';
  const ACCENT_KEY = 'accentColor';

  // Keep a reference to the real matchMedia so we can restore it.
  let originalMatchMedia: typeof window.matchMedia;

  /** Create a minimal matchMedia stub that reports dark-mode preference. */
  function stubMatchMedia(prefersDark: boolean): void {
    (window as any).matchMedia = jasmine.createSpy('matchMedia').and.callFake((query: string) => ({
      matches: prefersDark,
      media: query,
      addEventListener: jasmine.createSpy('addEventListener'),
      removeEventListener: jasmine.createSpy('removeEventListener'),
      dispatchEvent: jasmine.createSpy('dispatchEvent'),
      onchange: null,
      addListener: jasmine.createSpy('addListener'),
      removeListener: jasmine.createSpy('removeListener'),
    }));
  }

  /** Inject the service AFTER localStorage / DOM stubs are set up. */
  function createService(): ThemeService {
    TestBed.configureTestingModule({
      providers: [provideExperimentalZonelessChangeDetection()],
    });
    return TestBed.inject(ThemeService);
  }

  beforeEach(() => {
    originalMatchMedia = window.matchMedia;
    stubMatchMedia(false); // default: light system preference

    localStorage.removeItem(THEME_KEY);
    localStorage.removeItem(ACCENT_KEY);

    // Spy on DOM methods that applyTheme / applyAccentColor touch.
    spyOn(document.documentElement.classList, 'add').and.callThrough();
    spyOn(document.documentElement.classList, 'remove').and.callThrough();
    spyOn(document.documentElement.classList, 'toggle').and.callThrough();
    spyOn(document.documentElement.style, 'setProperty').and.callThrough();
  });

  afterEach(() => {
    window.matchMedia = originalMatchMedia;
    localStorage.removeItem(THEME_KEY);
    localStorage.removeItem(ACCENT_KEY);
    // Clean up any dark class left behind
    document.documentElement.classList.remove('dark', 'theme-transition');
  });

  // --------------------------------------------------------------------------
  // 1. Default theme — defaults to 'dark' when localStorage is empty
  // --------------------------------------------------------------------------
  it('should default to "dark" theme when nothing is saved in localStorage', () => {
    const service = createService();
    expect(service.theme()).toBe('dark');
  });

  // --------------------------------------------------------------------------
  // 2. setTheme('light') updates signal
  // --------------------------------------------------------------------------
  it('should update the theme signal to "light"', () => {
    const service = createService();
    service.setTheme('light');
    expect(service.theme()).toBe('light');
  });

  // --------------------------------------------------------------------------
  // 3. setTheme('dark') updates signal
  // --------------------------------------------------------------------------
  it('should update the theme signal to "dark"', () => {
    const service = createService();
    service.setTheme('light');
    service.setTheme('dark');
    expect(service.theme()).toBe('dark');
  });

  // --------------------------------------------------------------------------
  // 4. setTheme('system') updates signal
  // --------------------------------------------------------------------------
  it('should update the theme signal to "system"', () => {
    const service = createService();
    service.setTheme('system');
    expect(service.theme()).toBe('system');
  });

  // --------------------------------------------------------------------------
  // 5. toggleTheme() from dark → light
  // --------------------------------------------------------------------------
  it('should toggle from "dark" to "light"', () => {
    const service = createService();
    expect(service.theme()).toBe('dark');
    service.toggleTheme();
    expect(service.theme()).toBe('light');
  });

  // --------------------------------------------------------------------------
  // 6. toggleTheme() from light → dark
  // --------------------------------------------------------------------------
  it('should toggle from "light" to "dark"', () => {
    const service = createService();
    service.setTheme('light');
    service.toggleTheme();
    expect(service.theme()).toBe('dark');
  });

  // --------------------------------------------------------------------------
  // 7. cycleTheme(): light → dark → system → light
  // --------------------------------------------------------------------------
  it('should cycle through light → dark → system → light', () => {
    const service = createService();

    service.setTheme('light');
    expect(service.theme()).toBe('light');

    service.cycleTheme();
    expect(service.theme()).toBe('dark');

    service.cycleTheme();
    expect(service.theme()).toBe('system');

    service.cycleTheme();
    expect(service.theme()).toBe('light');
  });

  // --------------------------------------------------------------------------
  // 8. setAccentColor('blue') updates accentColor signal
  // --------------------------------------------------------------------------
  it('should update the accentColor signal when setAccentColor is called', () => {
    const service = createService();
    service.setAccentColor('blue');
    expect(service.accentColor()).toBe('blue');
  });

  // --------------------------------------------------------------------------
  // 9. accentColors has 8 entries
  // --------------------------------------------------------------------------
  it('should expose exactly 8 accent color options', () => {
    const service = createService();
    expect(service.accentColors.length).toBe(8);
    expect(service.accentColors.map(c => c.id)).toEqual([
      'stone', 'zinc', 'blue', 'green', 'amber', 'rose', 'violet', 'cyan',
    ]);
  });

  // --------------------------------------------------------------------------
  // 10. Theme persistence — setTheme saves to localStorage
  // --------------------------------------------------------------------------
  it('should persist the theme to localStorage when setTheme is called', () => {
    const service = createService();
    service.setTheme('light');

    // The effect runs asynchronously; flush it so applyTheme() executes.
    TestBed.flushEffects();
    expect(localStorage.getItem(THEME_KEY)).toBe('light');
  });

  // --------------------------------------------------------------------------
  // 11. Accent persistence — setAccentColor saves to localStorage
  // --------------------------------------------------------------------------
  it('should persist the accent color to localStorage when setAccentColor is called', () => {
    const service = createService();
    service.setAccentColor('cyan');

    // The effect runs asynchronously; flush it so applyAccentColor() executes.
    TestBed.flushEffects();
    expect(localStorage.getItem(ACCENT_KEY)).toBe('cyan');
  });

  // --------------------------------------------------------------------------
  // 12. loadSavedTheme — reads from localStorage
  // --------------------------------------------------------------------------
  it('should load the saved theme from localStorage on construction', () => {
    localStorage.setItem(THEME_KEY, 'light');

    const service = createService();
    expect(service.theme()).toBe('light');
  });

  // --------------------------------------------------------------------------
  // 13. loadSavedAccentColor — reads from localStorage
  // --------------------------------------------------------------------------
  it('should load the saved accent color from localStorage on construction', () => {
    localStorage.setItem(ACCENT_KEY, 'violet');

    const service = createService();
    expect(service.accentColor()).toBe('violet');
  });

  // --------------------------------------------------------------------------
  // 14. Default accent color is 'stone' when nothing saved
  // --------------------------------------------------------------------------
  it('should default to "stone" accent color when nothing is saved', () => {
    const service = createService();
    expect(service.accentColor()).toBe('stone');
  });

  // --------------------------------------------------------------------------
  // 15. toggleTheme() from system (dark effective) → light
  // --------------------------------------------------------------------------
  it('should toggle from "system" (dark effective) to "light"', () => {
    stubMatchMedia(true); // system prefers dark
    const service = createService();
    service.setTheme('system');

    // isDark will be set inside requestAnimationFrame, so set it manually
    // for the purpose of toggleTheme logic
    (service as any).isDark.set(true);

    service.toggleTheme();
    expect(service.theme()).toBe('light');
  });

  // --------------------------------------------------------------------------
  // 16. toggleTheme() from system (light effective) → dark
  // --------------------------------------------------------------------------
  it('should toggle from "system" (light effective) to "dark"', () => {
    stubMatchMedia(false); // system prefers light
    const service = createService();
    service.setTheme('system');

    (service as any).isDark.set(false);

    service.toggleTheme();
    expect(service.theme()).toBe('dark');
  });

  // --------------------------------------------------------------------------
  // 17. accentColors entries all have required shape
  // --------------------------------------------------------------------------
  it('should have id, name, and value on every accent color', () => {
    const service = createService();
    for (const color of service.accentColors) {
      expect(color.id).toBeDefined();
      expect(color.name).toBeDefined();
      expect(color.value).toBeDefined();
      expect(typeof color.id).toBe('string');
      expect(typeof color.name).toBe('string');
      expect(typeof color.value).toBe('string');
    }
  });
});
