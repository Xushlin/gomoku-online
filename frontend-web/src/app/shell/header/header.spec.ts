import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { describe, expect, it, vi } from 'vitest';
import { AuthService } from '../../core/auth/auth.service';
import { LanguageService } from '../../core/i18n/language.service';
import type { SupportedLocale } from '../../core/i18n/supported-locales';
import { SoundService } from '../../core/sound/sound.service';
import { BoardSkinService } from '../../core/theme/board-skin.service';
import { ThemeService } from '../../core/theme/theme.service';
import { Header } from './header';

// Only the brand key is translated here. Every other key the header renders
// falls through to transloco's missing-key behaviour, which is irrelevant to
// these assertions — the point is that the brand text is *resolved*, not
// hardcoded, and that it follows the active language.
const langs = {
  en: { header: { brand: 'Gewu' } },
  'zh-CN': { header: { brand: '格物' } },
};

function renderHeader(lang: SupportedLocale) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      Header,
      TranslocoTestingModule.forRoot({
        langs,
        translocoConfig: { availableLangs: ['en', 'zh-CN'], defaultLang: lang },
        preloadLangs: true,
      }),
    ],
    providers: [
      provideRouter([]),
      {
        provide: ThemeService,
        useValue: {
          themeName: () => 'material',
          isDark: () => false,
          availableThemes: () => ['material'],
          activate: vi.fn(),
          setDark: vi.fn(),
        },
      },
      {
        provide: BoardSkinService,
        useValue: {
          skinName: () => 'wood',
          availableSkins: () => ['wood'],
          activate: vi.fn(),
        },
      },
      {
        provide: SoundService,
        useValue: {
          muted: () => false,
          packName: () => 'wood',
          availablePacks: () => ['wood'],
          activate: vi.fn(),
          setMuted: vi.fn(),
          play: vi.fn(),
        },
      },
      {
        provide: AuthService,
        useValue: { isAuthenticated: () => false, user: () => null, logout: vi.fn() },
      },
      { provide: LanguageService, useValue: { current: () => lang, use: vi.fn() } },
    ],
  });

  const fixture = TestBed.createComponent(Header);
  fixture.detectChanges();
  return fixture;
}

function brandLink(fixture: ReturnType<typeof renderHeader>): HTMLAnchorElement {
  const el = (fixture.nativeElement as HTMLElement).querySelector('a');
  expect(el).not.toBeNull();
  return el as HTMLAnchorElement;
}

describe('Header brand', () => {
  it('renders the Chinese brand name under zh-CN', () => {
    const fixture = renderHeader('zh-CN');
    expect(brandLink(fixture).textContent?.trim()).toBe('格物');
  });

  it('renders the English brand name under en', () => {
    const fixture = renderHeader('en');
    expect(brandLink(fixture).textContent?.trim()).toBe('Gewu');
  });

  it('keeps the brand link pointing at /home', () => {
    const fixture = renderHeader('en');
    expect(brandLink(fixture).getAttribute('href')).toBe('/home');
  });

  it('resolves the brand from i18n rather than a hardcoded literal', () => {
    // The same element yielding two different strings is the behavioural proof
    // that no display literal survives in the template.
    const en = brandLink(renderHeader('en')).textContent?.trim();
    const zh = brandLink(renderHeader('zh-CN')).textContent?.trim();
    expect(en).not.toBe(zh);
  });
});
