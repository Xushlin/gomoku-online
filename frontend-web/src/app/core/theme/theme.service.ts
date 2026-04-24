import { DOCUMENT, inject, Injectable, signal, type Signal } from '@angular/core';
import { materialTokens } from './themes/material';
import { systemTokens } from './themes/system';
import type { ThemeTokens } from './theme.tokens';

const THEME_STORAGE_KEY = 'gomoku:theme';
const DARK_STORAGE_KEY = 'gomoku:dark';
const DEFAULT_THEME = 'material';

/**
 * Cross-cutting theme API. Two orthogonal signals:
 *   - themeName: which theme is active (e.g. 'material', 'system')
 *   - isDark:    whether dark mode is on
 *
 * Painting is driven entirely by CSS: `<html data-theme="...">` + `.dark`
 * class selects which token values cascade. The registry on the TS side
 * exists to enumerate themes for switcher UIs and to validate at
 * registration time that each theme declares all required tokens.
 *
 * Injection goes through this abstract class as the DI token so tests can
 * supply a stub via `{ provide: ThemeService, useValue: ... }`.
 */
export abstract class ThemeService {
  abstract readonly themeName: Signal<string>;
  abstract readonly isDark: Signal<boolean>;
  abstract register(name: string, tokens: ThemeTokens): void;
  abstract activate(name: string): void;
  abstract setDark(isDark: boolean): void;
  abstract availableThemes(): readonly string[];
}

@Injectable()
export class DefaultThemeService extends ThemeService {
  private readonly doc = inject(DOCUMENT);
  private readonly _themeName = signal<string>(DEFAULT_THEME);
  private readonly _isDark = signal<boolean>(false);
  private readonly themes = new Map<string, ThemeTokens>();

  readonly themeName: Signal<string> = this._themeName.asReadonly();
  readonly isDark: Signal<boolean> = this._isDark.asReadonly();

  constructor() {
    super();
    this.register('material', materialTokens);
    this.register('system', systemTokens);

    const initialTheme = this.resolveInitialTheme();
    const initialDark = this.resolveInitialDark();
    this.applyTheme(initialTheme);
    this.applyDark(initialDark);
  }

  register(name: string, tokens: ThemeTokens): void {
    this.validateTokens(name, tokens);
    this.themes.set(name, tokens);
  }

  activate(name: string): void {
    if (!this.themes.has(name)) {
      this.warn(`activate('${name}'): theme not registered; ignoring.`);
      return;
    }
    this.applyTheme(name);
    this.persist(THEME_STORAGE_KEY, name);
  }

  setDark(isDark: boolean): void {
    this.applyDark(isDark);
    this.persist(DARK_STORAGE_KEY, isDark ? '1' : '0');
  }

  availableThemes(): readonly string[] {
    return Array.from(this.themes.keys());
  }

  private applyTheme(name: string): void {
    this.doc.documentElement.dataset['theme'] = name;
    this._themeName.set(name);
  }

  private applyDark(isDark: boolean): void {
    this.doc.documentElement.classList.toggle('dark', isDark);
    this._isDark.set(isDark);
  }

  private resolveInitialTheme(): string {
    const stored = this.read(THEME_STORAGE_KEY);
    if (stored && this.themes.has(stored)) {
      return stored;
    }
    if (stored) {
      // Stored value is invalid (e.g. theme was removed) — overwrite with default.
      this.persist(THEME_STORAGE_KEY, DEFAULT_THEME);
    }
    return DEFAULT_THEME;
  }

  private resolveInitialDark(): boolean {
    const stored = this.read(DARK_STORAGE_KEY);
    if (stored === '1') return true;
    if (stored === '0') return false;
    const win = this.doc.defaultView;
    if (win && typeof win.matchMedia === 'function') {
      return win.matchMedia('(prefers-color-scheme: dark)').matches;
    }
    return false;
  }

  private read(key: string): string | null {
    try {
      return this.doc.defaultView?.localStorage.getItem(key) ?? null;
    } catch {
      return null;
    }
  }

  private persist(key: string, value: string): void {
    try {
      this.doc.defaultView?.localStorage.setItem(key, value);
    } catch {
      // Private-mode / quota errors: accept that persistence is best-effort.
    }
  }

  private validateTokens(name: string, tokens: ThemeTokens): void {
    const modes: (keyof ThemeTokens)[] = ['light', 'dark'];
    for (const mode of modes) {
      const set = tokens[mode];
      if (!set?.colors || !set.radii || !set.shadows) {
        this.warn(`register('${name}'): missing ${mode}.colors/radii/shadows.`);
      }
    }
  }

  private warn(message: string): void {
    if (typeof console !== 'undefined' && typeof console.warn === 'function') {
      console.warn(`[ThemeService] ${message}`);
    }
  }
}
