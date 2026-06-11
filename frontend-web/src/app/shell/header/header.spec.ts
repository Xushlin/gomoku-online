import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { of } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '../../core/auth/auth.service';
import { LanguageService } from '../../core/i18n/language.service';
import { SoundService } from '../../core/sound/sound.service';
import { BoardSkinService } from '../../core/theme/board-skin.service';
import { ThemeService } from '../../core/theme/theme.service';
import { Header } from './header';

function soundStub(opts: { muted?: boolean } = {}) {
  return {
    muted: signal(opts.muted ?? false),
    volume: signal(100),
    packName: signal('wood'),
    play: vi.fn(),
    setMuted: vi.fn(),
    setVolume: vi.fn(),
    register: vi.fn(),
    activate: vi.fn(),
    availablePacks: () => ['wood', 'chiptune', 'minimal'] as const,
  };
}

function mount(opts: { muted?: boolean } = {}) {
  const sound = soundStub(opts);
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      Header,
      TranslocoTestingModule.forRoot({
        langs: { en: {} },
        translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [
      provideRouter([]),
      { provide: SoundService, useValue: sound },
      {
        provide: LanguageService,
        useValue: { current: signal('en'), use: vi.fn() },
      },
      {
        provide: ThemeService,
        useValue: {
          themeName: signal('material'),
          isDark: signal(false),
          register: vi.fn(),
          activate: vi.fn(),
          setDark: vi.fn(),
          availableThemes: () => ['material', 'system'] as const,
        },
      },
      {
        provide: BoardSkinService,
        useValue: {
          skinName: signal('wood'),
          register: vi.fn(),
          activate: vi.fn(),
          availableSkins: () => ['wood', 'classic', 'midnight'] as const,
        },
      },
      {
        provide: AuthService,
        useValue: {
          accessToken: signal(null),
          user: signal(null),
          accessTokenExpiresAt: signal(null),
          isAuthenticated: signal(false),
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
  return { fixture, sound };
}

/** Open the sound-pack CDK menu and return the volume slider inside it. */
function openVolumeSlider(fixture: ReturnType<typeof TestBed.createComponent>) {
  const trigger = fixture.nativeElement.querySelector(
    'button[aria-label="header.sound-pack.label"]',
  ) as HTMLButtonElement;
  trigger.click();
  fixture.detectChanges();
  // CDK menus render into an overlay container attached to the body.
  return document.querySelector(
    'input[type="range"][aria-label="header.sound.volume"]',
  ) as HTMLInputElement | null;
}

describe('Header volume slider', () => {
  beforeEach(() => TestBed.resetTestingModule());

  afterEach(() => {
    // Drop any overlay leftovers so menus from one test don't leak into the next.
    document.querySelectorAll('.cdk-overlay-container').forEach((el) => el.remove());
  });

  it('renders inside the sound-pack menu with an aria-label', () => {
    const { fixture } = mount();
    const slider = openVolumeSlider(fixture);
    expect(slider).not.toBeNull();
    expect(slider!.value).toBe('100');
  });

  it('release calls setVolume once and keeps the menu open', () => {
    const { fixture, sound } = mount();
    const slider = openVolumeSlider(fixture)!;
    slider.value = '40';
    slider.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(sound.setVolume).toHaveBeenCalledTimes(1);
    expect(sound.setVolume).toHaveBeenCalledWith(40);
    // Menu (and the slider in it) must still be in the DOM — not auto-closed.
    expect(
      document.querySelector('input[type="range"][aria-label="header.sound.volume"]'),
    ).not.toBeNull();
  });

  it('auditions move-place on release when not muted', () => {
    const { fixture, sound } = mount({ muted: false });
    const slider = openVolumeSlider(fixture)!;
    slider.value = '40';
    slider.dispatchEvent(new Event('change'));
    expect(sound.play).toHaveBeenCalledWith('move-place');
  });

  it('stays silent on release when muted', () => {
    const { fixture, sound } = mount({ muted: true });
    const slider = openVolumeSlider(fixture)!;
    slider.value = '40';
    slider.dispatchEvent(new Event('change'));
    expect(sound.setVolume).toHaveBeenCalledWith(40);
    expect(sound.play).not.toHaveBeenCalled();
  });
});
