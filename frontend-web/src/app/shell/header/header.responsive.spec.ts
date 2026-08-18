import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { of } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '../../core/auth/auth.service';
import { LanguageService } from '../../core/i18n/language.service';
import { SoundService } from '../../core/sound/sound.service';
import { stubSoundService } from '../../testing/sound';
import { BoardSkinService } from '../../core/theme/board-skin.service';
import { ThemeService } from '../../core/theme/theme.service';
import { Header } from './header';

/*
 * Regression guard for `web-shell`'s 375px baseline.
 *
 * The bug this file exists for: the header laid out one non-wrapping row of
 * ten controls, so at a 375 x 812 viewport `documentElement.scrollWidth` was
 * 566 against a `clientWidth` of 375 — the whole page scrolled sideways, and
 * `header.scrollWidth` accounted for all 566 of it. Hiding only each control's
 * *label* below `sm:` left the borders, padding, gaps and values in place.
 *
 * jsdom has no layout engine, so `scrollWidth` is 0 here and cannot be the
 * assertion. What *can* be asserted — and what actually regressed — is the
 * structure that keeps the row short: which controls the header exposes
 * inline at a given width, and that everything it hides stays reachable
 * through the Settings menu. `displayAt` below resolves Tailwind's display
 * utilities for a width so these read as viewport assertions rather than
 * class-string assertions.
 */

const BREAKPOINTS: Record<string, number> = { sm: 640, md: 768, lg: 1024, xl: 1280, '2xl': 1536 };
const DISPLAY_UTILITY =
  /^(?:(sm|md|lg|xl|2xl):)?(hidden|flex|inline-flex|inline|inline-block|block|contents|grid)$/;

/**
 * The `display` an element resolves to at `width`, or null if it carries no
 * display utility. Among utilities whose media query is active, the one with
 * the largest breakpoint wins — Tailwind emits them in ascending order, so
 * that is what the cascade picks.
 */
function displayAt(el: Element, width: number): string | null {
  let winner: { at: number; value: string } | null = null;
  for (const cls of Array.from(el.classList)) {
    const match = DISPLAY_UTILITY.exec(cls);
    if (!match) continue;
    const at = match[1] ? BREAKPOINTS[match[1]] : 0;
    if (width < at) continue;
    if (!winner || at >= winner.at) winner = { at, value: match[2] };
  }
  return winner?.value ?? null;
}

/** Whether `el` is rendered at `width`, i.e. nothing from itself up to `root` is `hidden`. */
function isVisibleAt(el: Element, width: number, root: Element): boolean {
  for (let node: Element | null = el; node; node = node.parentElement) {
    if (displayAt(node, width) === 'hidden') return false;
    if (node === root) break;
  }
  return true;
}

/** Every focusable control the header actually shows at `width`, in DOM order. */
function visibleControls(host: HTMLElement, width: number): HTMLElement[] {
  const header = host.querySelector('header') as HTMLElement;
  return Array.from(header.querySelectorAll<HTMLElement>('a, button, input, select, textarea'))
    .filter((el) => isVisibleAt(el, width, header))
    .filter((el) => !el.hasAttribute('disabled'));
}

/** A stable identifier per control: its route, its aria-label, or its text. */
function label(el: HTMLElement): string {
  return el.getAttribute('href') ?? el.getAttribute('aria-label') ?? (el.textContent ?? '').trim();
}

const langs = { en: { header: { brand: 'Gewu' } }, 'zh-CN': { header: { brand: '格物' } } };

const THEMES = ['material', 'system'] as const;
const SKINS = ['wood', 'classic', 'midnight'] as const;
const PACKS = ['wood', 'chiptune', 'minimal'] as const;

function mount(opts: { authenticated?: boolean } = {}) {
  const authenticated = opts.authenticated ?? true;
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      Header,
      TranslocoTestingModule.forRoot({
        langs,
        translocoConfig: { availableLangs: ['en', 'zh-CN'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [
      provideRouter([]),
      { provide: SoundService, useValue: stubSoundService({ packs: PACKS }) },
      { provide: LanguageService, useValue: { current: signal('en'), use: vi.fn() } },
      {
        provide: ThemeService,
        useValue: {
          themeName: signal('material'),
          isDark: signal(false),
          register: vi.fn(),
          activate: vi.fn(),
          setDark: vi.fn(),
          availableThemes: () => THEMES,
        },
      },
      {
        provide: BoardSkinService,
        useValue: {
          skinName: signal('wood'),
          register: vi.fn(),
          activate: vi.fn(),
          availableSkins: () => SKINS,
        },
      },
      {
        provide: AuthService,
        useValue: {
          accessToken: signal(authenticated ? 'token' : null),
          user: signal(authenticated ? { id: 'u1', username: 'a-fairly-long-username' } : null),
          accessTokenExpiresAt: signal(null),
          isAuthenticated: signal(authenticated),
          login: vi.fn(() => of(undefined)),
          register: vi.fn(() => of(undefined)),
          logout: vi.fn(() => of(undefined)),
          changePassword: vi.fn(() => of(undefined)),
          refresh: vi.fn(() => of(undefined)),
          bootstrap: vi.fn(() => Promise.resolve()),
        },
      },
    ],
  });
  const fixture = TestBed.createComponent(Header);
  fixture.detectChanges();
  return fixture;
}

/** Open the mobile Settings menu and return its `role="menu"` element. */
function openSettings(host: HTMLElement): HTMLElement {
  const trigger = host.querySelector<HTMLButtonElement>(
    'button[aria-label="header.settings.label"]',
  );
  expect(trigger, 'Settings trigger must exist').not.toBeNull();
  trigger!.click();
  const menu = document.querySelector<HTMLElement>('.cdk-overlay-container [role="menu"]');
  expect(menu, 'Settings menu must open').not.toBeNull();
  return menu!;
}

/** Rows of an open menu, as `[visible label, current value]` pairs. */
function rows(menu: HTMLElement): { name: string; role: string; value: string }[] {
  return Array.from(menu.querySelectorAll<HTMLElement>(':scope > [role^="menuitem"]')).map(
    (el) => ({
      name: (el.firstElementChild?.textContent ?? '').trim(),
      role: el.getAttribute('role') ?? '',
      value: (el.lastElementChild?.textContent ?? '').trim(),
    }),
  );
}

/** The six appearance controls, in the order both placements must keep. */
const APPEARANCE = [
  'header.language.label',
  'header.theme.label',
  'header.board-skin.label',
  'header.sound-pack.label',
  'header.sound.label',
  'header.theme.dark-toggle',
];

describe('Header at 375px', () => {
  beforeEach(() => TestBed.resetTestingModule());
  afterEach(() => {
    document.querySelectorAll('.cdk-overlay-container').forEach((el) => el.remove());
  });

  it('exposes only navigation and identity inline — four controls, no appearance row', () => {
    const host = mount().nativeElement as HTMLElement;
    // The budget is the whole point: ten controls is what overflowed 375px.
    expect(visibleControls(host, 375).map(label)).toEqual([
      '/home',
      '/games',
      'header.settings.label',
      'header.auth.logout',
    ]);
  });

  it('hides every appearance control from the header row', () => {
    const host = mount().nativeElement as HTMLElement;
    const visible = visibleControls(host, 375).map(label);
    for (const control of APPEARANCE) expect(visible).not.toContain(control);
  });

  it('does not escape the overflow by wrapping the sticky header', () => {
    const host = mount().nativeElement as HTMLElement;
    const header = host.querySelector('header')!;
    // Wrapping would make the sticky header ~3 rows / ~150px tall at 375px.
    expect(header.className).toContain('sticky');
    expect(header.className).not.toMatch(/\bflex-wrap\b/);
  });

  it('keeps the budget when logged out', () => {
    const host = mount({ authenticated: false }).nativeElement as HTMLElement;
    expect(visibleControls(host, 375).map(label)).toEqual([
      '/home',
      '/games',
      'header.settings.label',
      '/login',
    ]);
  });
});

describe('Header Settings menu', () => {
  beforeEach(() => TestBed.resetTestingModule());
  afterEach(() => {
    document.querySelectorAll('.cdk-overlay-container').forEach((el) => el.remove());
  });

  it('holds all six appearance controls, in the header row order', () => {
    const host = mount().nativeElement as HTMLElement;
    expect(rows(openSettings(host)).map((r) => r.name)).toEqual(APPEARANCE);
  });

  it('uses menu-legal roles — submenus for pickers, checkboxes for toggles', () => {
    const host = mount().nativeElement as HTMLElement;
    // `role="switch"` is not a legal child of `role="menu"`; the toggles swap
    // to `menuitemcheckbox` in this placement.
    expect(rows(openSettings(host)).map((r) => r.role)).toEqual([
      'menuitem',
      'menuitem',
      'menuitem',
      'menuitem',
      'menuitemcheckbox',
      'menuitemcheckbox',
    ]);
  });

  it('shows each control current value and reflects toggle state in aria-checked', () => {
    const host = mount().nativeElement as HTMLElement;
    const menu = openSettings(host);
    expect(rows(menu).map((r) => r.value)).toEqual([
      'header.language.en',
      'header.theme.material',
      'header.board-skin.wood',
      'header.sound-pack.wood',
      'header.sound.on',
      'header.theme.dark-off',
    ]);
    const checkboxes = menu.querySelectorAll('[role="menuitemcheckbox"]');
    expect(checkboxes[0].getAttribute('aria-checked')).toBe('true'); // sound on
    expect(checkboxes[1].getAttribute('aria-checked')).toBe('false'); // dark off
  });

  it('opens a submenu listing everything the service registry offers', () => {
    const host = mount().nativeElement as HTMLElement;
    const menu = openSettings(host);
    const boardRow = menu.querySelectorAll<HTMLElement>(':scope > [role="menuitem"]')[2];
    expect(boardRow.getAttribute('aria-haspopup')).toBe('menu');
    boardRow.click();
    const submenus = document.querySelectorAll('.cdk-overlay-container [role="menu"]');
    const options = Array.from(
      submenus[submenus.length - 1].querySelectorAll('[role="menuitem"]'),
    ).map((el) => (el.textContent ?? '').trim());
    // Three registered skins, straight from `availableSkins()` — a new skin
    // reaches this menu with no template change, per web-board-skins.
    expect(options).toEqual(SKINS.map((s) => `header.board-skin.${s}`));
  });
});

describe('Header above lg', () => {
  beforeEach(() => TestBed.resetTestingModule());
  afterEach(() => {
    document.querySelectorAll('.cdk-overlay-container').forEach((el) => el.remove());
  });

  it('restores the inline controls and drops the Settings trigger at 1024px', () => {
    const host = mount().nativeElement as HTMLElement;
    const visible = visibleControls(host, 1024).map(label);
    for (const control of APPEARANCE) expect(visible).toContain(control);
    expect(visible).not.toContain('header.settings.label');
  });

  it('shows control labels only from xl up', () => {
    const host = mount().nativeElement as HTMLElement;
    const header = host.querySelector('header') as HTMLElement;
    const themeTrigger = header.querySelector<HTMLElement>(
      'button[aria-label="header.theme.label"]',
    )!;
    const labelSpan = themeTrigger.firstElementChild!;
    // Values only between lg and xl; the `Theme:` prefix arrives at 1280.
    expect(isVisibleAt(labelSpan, 1024, header)).toBe(false);
    expect(isVisibleAt(labelSpan, 1280, header)).toBe(true);
  });
});
