import { DOCUMENT, inject, Injectable, signal, type Signal } from '@angular/core';

const THEME_STORAGE_KEY = 'gewu:theme';
const DARK_STORAGE_KEY = 'gewu:dark';
/*
 * 没有存过偏好的用户看到的那一套。
 *
 * 从 'material' 改成 'qq-game':这是一个游戏厅,而 material 的调色板是一套后台
 * 管理系统的。**改默认值 MUST NOT 动到已经选过的人** —— 解析顺序里 localStorage
 * 优先,所以两个方向都要有断言:没存过 → 拿到新默认;存过 'material' → 仍然是
 * material。少了后一条,一个把所有人都改掉的实现在前一条下也是绿的。
 */
const DEFAULT_THEME = 'qq-game';

/**
 * Cross-cutting theme API. Two orthogonal signals:
 *   - themeName: which theme is active (e.g. 'material', 'system')
 *   - isDark:    whether dark mode is on
 *
 * Painting is driven entirely by CSS: `<html data-theme="...">` + `.dark`
 * class selects which token values cascade. **The registry on the TS side
 * holds names only** — it exists to enumerate themes for switcher UIs and to
 * reject `activate()` of a name nobody registered.
 *
 * It used to hold each theme's token values too, mirrored from `tokens.css`,
 * and validate them at registration. That was deleted: the mirror cost 4.88 kB
 * of the initial bundle (measured by stubbing, not reasoned), and it guarded
 * the **copy** rather than the source — a theme whose TS mirror was complete
 * while its CSS block was missing a token compiled fine and painted wrong.
 * Completeness is now asserted where the values actually live, by
 * `scripts/check-styles.mjs`, which derives the token list from `@theme` and
 * the theme list from the `[data-theme]` selectors, and fails CI.
 *
 * Injection goes through this abstract class as the DI token so tests can
 * supply a stub via `{ provide: ThemeService, useValue: ... }`.
 */
export abstract class ThemeService {
  abstract readonly themeName: Signal<string>;
  abstract readonly isDark: Signal<boolean>;
  abstract register(name: string): void;
  abstract activate(name: string): void;
  abstract setDark(isDark: boolean): void;
  abstract availableThemes(): readonly string[];
}

@Injectable()
export class DefaultThemeService extends ThemeService {
  private readonly doc = inject(DOCUMENT);
  private readonly _themeName = signal<string>(DEFAULT_THEME);
  private readonly _isDark = signal<boolean>(false);
  private readonly themes = new Set<string>();

  readonly themeName: Signal<string> = this._themeName.asReadonly();
  readonly isDark: Signal<boolean> = this._isDark.asReadonly();

  constructor() {
    super();
    this.register('material');
    this.register('system');
    this.register('ink');
    this.register('qq-game');

    const initialTheme = this.resolveInitialTheme();
    const initialDark = this.resolveInitialDark();
    this.applyTheme(initialTheme);
    this.applyDark(initialDark);
  }

  register(name: string): void {
    this.themes.add(name);
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
    return Array.from(this.themes);
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

  private warn(message: string): void {
    if (typeof console !== 'undefined' && typeof console.warn === 'function') {
      console.warn(`[ThemeService] ${message}`);
    }
  }
}
