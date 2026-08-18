import { inkTokens } from './themes/ink';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { DefaultThemeService, ThemeService } from './theme.service';

function stubMatchMedia(matches: boolean): void {
  vi.stubGlobal(
    'matchMedia',
    vi.fn((query: string) => ({
      matches,
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: () => false,
    })),
  );
}

function createService(): ThemeService {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [{ provide: ThemeService, useClass: DefaultThemeService }],
  });
  return TestBed.inject(ThemeService);
}

describe('DefaultThemeService', () => {
  beforeEach(() => {
    localStorage.clear();
    document.documentElement.classList.remove('dark');
    delete document.documentElement.dataset['theme'];
    stubMatchMedia(false);
  });

  it('activate() sets data-theme, persists, updates themeName, does not touch isDark', () => {
    const svc = createService();
    const beforeDark = svc.isDark();

    svc.activate('system');

    expect(document.documentElement.dataset['theme']).toBe('system');
    expect(localStorage.getItem('gewu:theme')).toBe('system');
    expect(svc.themeName()).toBe('system');
    expect(svc.isDark()).toBe(beforeDark);
  });

  it('setDark(true) toggles .dark, persists "1", updates isDark, does not touch themeName', () => {
    const svc = createService();
    const beforeName = svc.themeName();

    svc.setDark(true);

    expect(document.documentElement.classList.contains('dark')).toBe(true);
    expect(localStorage.getItem('gewu:dark')).toBe('1');
    expect(svc.isDark()).toBe(true);
    expect(svc.themeName()).toBe(beforeName);
  });

  it('initial resolution: localStorage dark value wins over OS prefers-color-scheme', () => {
    localStorage.setItem('gewu:dark', '0');
    stubMatchMedia(true);

    const svc = createService();

    expect(svc.isDark()).toBe(false);
  });

  it('initial resolution: invalid theme name in localStorage falls back to material and overwrites', () => {
    localStorage.setItem('gewu:theme', 'nonexistent-theme');

    const svc = createService();

    expect(svc.themeName()).toBe('material');
    expect(localStorage.getItem('gewu:theme')).toBe('material');
  });

  it('availableThemes() exposes the two registered themes', () => {
    const svc = createService();
    expect(svc.availableThemes()).toEqual(expect.arrayContaining(['material', 'system']));
  });

  it('registers the ink theme alongside material and system', () => {
    const svc = createService();

    expect(svc.availableThemes()).toEqual(
      expect.arrayContaining(['material', 'system', 'ink']),
    );
  });

  it('defines every token in both ink modes', () => {
    // A theme's contract is a matched pair. The 成语纵横 prototype is dark-only;
    // shipping ink without a light set would break the light/dark axis for
    // anyone who picks it.
    for (const mode of ['light', 'dark'] as const) {
      const set = inkTokens[mode];
      expect(Object.keys(set.colors).sort()).toEqual(
        Object.keys(inkTokens.light.colors).sort(),
      );
      expect(set.radii.card).toBeTruthy();
      expect(set.shadows.elevated).toBeTruthy();
    }
  });

});
