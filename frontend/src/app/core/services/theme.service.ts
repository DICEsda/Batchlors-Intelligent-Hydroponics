import { Injectable, signal, effect } from '@angular/core';

export type Theme = 'light' | 'dark' | 'system';

@Injectable({
  providedIn: 'root'
})
export class ThemeService {
  private readonly THEME_KEY = 'theme';
  private readonly ACCENT_KEY = 'accentColor';
  
  // Current theme setting (what the user selected)
  readonly theme = signal<Theme>(this.loadSavedTheme());
  
  // Whether dark mode is actually active (resolved from 'system' if needed)
  readonly isDark = signal<boolean>(false);
  
  // Available accent colors
  readonly accentColors = [
    { id: 'stone', name: 'Stone', value: 'oklch(0.55 0.02 60)' },
    { id: 'zinc', name: 'Zinc', value: 'oklch(0.55 0 0)' },
    { id: 'blue', name: 'Blue', value: 'oklch(0.55 0.15 250)' },
    { id: 'green', name: 'Green', value: 'oklch(0.55 0.15 145)' },
    { id: 'amber', name: 'Amber', value: 'oklch(0.65 0.15 85)' },
    { id: 'rose', name: 'Rose', value: 'oklch(0.55 0.15 15)' },
    { id: 'violet', name: 'Violet', value: 'oklch(0.55 0.15 290)' },
    { id: 'cyan', name: 'Cyan', value: 'oklch(0.55 0.15 200)' },
  ];
  
  readonly accentColor = signal<string>(this.loadSavedAccentColor());

  private mediaQuery: MediaQueryList | null = null;

  constructor() {
    // Apply theme on initialization
    this.applyTheme(this.theme());
    this.applyAccentColor(this.accentColor());
    this.setupSystemThemeListener();
    
    // React to theme changes
    effect(() => {
      this.applyTheme(this.theme());
    });
    
    // React to accent color changes
    effect(() => {
      this.applyAccentColor(this.accentColor());
    });
  }

  private loadSavedTheme(): Theme {
    if (typeof localStorage === 'undefined') return 'dark';
    const saved = localStorage.getItem(this.THEME_KEY) as Theme | null;
    return saved || 'dark';
  }

  private loadSavedAccentColor(): string {
    if (typeof localStorage === 'undefined') return 'stone';
    return localStorage.getItem(this.ACCENT_KEY) || 'stone';
  }

  private setupSystemThemeListener() {
    if (typeof window === 'undefined') return;
    
    this.mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
    this.mediaQuery.addEventListener('change', (e: MediaQueryListEvent) => {
      if (this.theme() === 'system') {
        this.applyTheme('system');
      }
    });
  }

  private applyTheme(theme: Theme) {
    if (typeof document === 'undefined') return;
    
    const root = document.documentElement;
    let dark: boolean;

    if (theme === 'system') {
      dark = window.matchMedia('(prefers-color-scheme: dark)').matches;
    } else {
      dark = theme === 'dark';
    }

    // Add transition class for smooth theme change
    root.classList.add('theme-transition');
    
    // Use requestAnimationFrame for smoother transition
    requestAnimationFrame(() => {
      root.classList.toggle('dark', dark);
      this.isDark.set(dark);
      
      // Remove transition class after animation completes (150ms matches CSS)
      setTimeout(() => {
        root.classList.remove('theme-transition');
      }, 150);
    });
    
    localStorage.setItem(this.THEME_KEY, theme);
  }

  private applyAccentColor(colorId: string) {
    if (typeof document === 'undefined') return;
    
    const color = this.accentColors.find(c => c.id === colorId);
    if (color) {
      const root = document.documentElement;
      root.style.setProperty('--primary', color.value);
      
      // Set foreground color based on accent brightness
      const needsDarkForeground = ['amber'].includes(colorId);
      if (needsDarkForeground) {
        root.style.setProperty('--primary-foreground', 'oklch(0.22 0.01 60)');
      } else {
        root.style.setProperty('--primary-foreground', 'oklch(0.98 0.002 75)');
      }
      
      localStorage.setItem(this.ACCENT_KEY, colorId);
    }
  }

  /**
   * Set the theme
   */
  setTheme(theme: Theme) {
    this.theme.set(theme);
  }

  /**
   * Toggle between light and dark mode
   * If currently on 'system', will switch to the opposite of current effective theme
   */
  toggleTheme() {
    const currentTheme = this.theme();
    
    if (currentTheme === 'system') {
      // If on system, switch to opposite of current effective theme
      this.setTheme(this.isDark() ? 'light' : 'dark');
    } else {
      // Toggle between light and dark
      this.setTheme(currentTheme === 'dark' ? 'light' : 'dark');
    }
  }

  /**
   * Cycle through themes: light -> dark -> system -> light
   */
  cycleTheme() {
    const currentTheme = this.theme();
    const cycle: Theme[] = ['light', 'dark', 'system'];
    const currentIndex = cycle.indexOf(currentTheme);
    const nextIndex = (currentIndex + 1) % cycle.length;
    this.setTheme(cycle[nextIndex]);
  }

  /**
   * Set the accent color
   */
  setAccentColor(colorId: string) {
    this.accentColor.set(colorId);
  }
}
