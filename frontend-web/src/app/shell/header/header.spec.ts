import { signal } from '@angular/core';
import { DeferBlockBehavior, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { of } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '../../core/auth/auth.service';
import { LanguageService } from '../../core/i18n/language.service';
import type { SupportedLocale } from '../../core/i18n/supported-locales';
import { SoundService } from '../../core/sound/sound.service';
import { stubSoundService } from '../../testing/sound';
import { BoardSkinService } from '../../core/theme/board-skin.service';
import { ThemeService } from '../../core/theme/theme.service';
import { Header } from './header';

// Only `header.brand` carries a real translation. Every other key the header
// renders falls through to transloco's missing-key behaviour, which returns the
// key itself — the volume-slider tests below depend on that, since they select
// controls by `aria-label="header.sound-pack.label"` and friends.
const langs = {
  en: { header: { brand: 'Gewu' } },
  'zh-CN': { header: { brand: '格物' } },
};

const soundStub = stubSoundService;

async function mount(opts: { muted?: boolean; lang?: SupportedLocale } = {}) {
  const lang = opts.lang ?? 'en';
  const sound = soundStub(opts);
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    /*
     * 外观控件那一组现在在 `@defer` 里(为了把 `@angular/cdk` 的 77.13 kB 挪出首屏),
     * 而 TestBed 默认对 defer 块是 Manual —— 延迟内容不渲染。设成 Playthrough,
     * 于是**下面每一条断言都一个字没改**:那是「搬家没有改行为」的可执行形式。
     */
    deferBlockBehavior: DeferBlockBehavior.Playthrough,
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
      { provide: SoundService, useValue: sound },
      {
        provide: LanguageService,
        useValue: { current: signal(lang), use: vi.fn() },
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
  // `@defer` 的依赖是动态 import,所以组件必须先异步编译。
  await TestBed.compileComponents();
  const fixture = TestBed.createComponent(Header);
  fixture.detectChanges();
  return { fixture, sound };
}

/**
 * Open the sound-pack CDK menu. Items render into an overlay attached to the body.
 *
 * 要 `await`:那一组控件在 `@defer` 里,占位上的第一次点击只是请求加载,加载完成之后
 * 真身才把它打开 —— 「首次点击等一个 chunk」在测试里就是这一行 `whenStable()`。
 */
async function openSoundPackMenu(
  fixture: ReturnType<typeof TestBed.createComponent>,
): Promise<void> {
  const trigger = fixture.nativeElement.querySelector(
    'button[aria-label="header.sound-pack.label"]',
  ) as HTMLButtonElement;
  trigger.click();
  fixture.detectChanges();
  await fixture.whenStable();
  // 真身把 open() 推到下一个宏任务(见 appearance-menus.ts:那是量出来的),所以这里也要等一个。
  await new Promise((resolve) => setTimeout(resolve));
  fixture.detectChanges();
}

/** The labels of the menu items, in render order. */
function soundPackMenuItems(): string[] {
  return [...document.querySelectorAll('[role="menu"] [role="menuitem"]')].map((el) =>
    (el.textContent ?? '').trim(),
  );
}

/** Open the sound-pack CDK menu and return the volume slider inside it. */
async function openVolumeSlider(fixture: ReturnType<typeof TestBed.createComponent>) {
  await openSoundPackMenu(fixture);
  // CDK menus render into an overlay container attached to the body.
  return document.querySelector(
    'input[type="range"][aria-label="header.sound.volume"]',
  ) as HTMLInputElement | null;
}

function brandLink(fixture: Awaited<Awaited<ReturnType<typeof mount>>>['fixture']): HTMLAnchorElement {
  const el = (fixture.nativeElement as HTMLElement).querySelector('a');
  expect(el).not.toBeNull();
  return el as HTMLAnchorElement;
}

describe('Header volume slider', () => {
  beforeEach(() => TestBed.resetTestingModule());

  afterEach(() => {
    // Drop any overlay leftovers so menus from one test don't leak into the next.
    document.querySelectorAll('.cdk-overlay-container').forEach((el) => el.remove());
  });

  it('lists one menu item per registered pack, in that order', async () => {
    // Derived from `availablePacks()`, never a literal count. The spec used to say
    // "lists `wood` and `chiptune`, two items", which was wrong from the day the
    // third pack shipped — and nothing here had ever counted them.
    const { fixture, sound } = await mount();
    await openSoundPackMenu(fixture);

    const expected = sound.availablePacks().map((name) => `header.sound-pack.${name}`);
    expect(expected.length).toBeGreaterThan(1);
    expect(soundPackMenuItems()).toEqual(expected);
  });

  it('renders inside the sound-pack menu with an aria-label', async () => {
    const { fixture } = await mount();
    const slider = await openVolumeSlider(fixture);
    expect(slider).not.toBeNull();
    expect(slider!.value).toBe('100');
  });

  it('release calls setVolume once and keeps the menu open', async () => {
    const { fixture, sound } = await mount();
    const slider = (await openVolumeSlider(fixture))!;
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

  it('auditions move-place on release when not muted', async () => {
    const { fixture, sound } = await mount({ muted: false });
    const slider = (await openVolumeSlider(fixture))!;
    slider.value = '40';
    slider.dispatchEvent(new Event('change'));
    expect(sound.play).toHaveBeenCalledWith('move-place');
  });

  it('stays silent on release when muted', async () => {
    const { fixture, sound } = await mount({ muted: true });
    const slider = (await openVolumeSlider(fixture))!;
    slider.value = '40';
    slider.dispatchEvent(new Event('change'));
    expect(sound.setVolume).toHaveBeenCalledWith(40);
    expect(sound.play).not.toHaveBeenCalled();
  });
});

describe('Header brand', () => {
  afterEach(() => {
    document.querySelectorAll('.cdk-overlay-container').forEach((el) => el.remove());
  });

  it('renders the Chinese brand name under zh-CN', async () => {
    const { fixture } = await mount({ lang: 'zh-CN' });
    expect(brandLink(fixture).textContent?.trim()).toBe('格物');
  });

  it('renders the English brand name under en', async () => {
    const { fixture } = await mount({ lang: 'en' });
    expect(brandLink(fixture).textContent?.trim()).toBe('Gewu');
  });

  it('keeps the brand link pointing at /home', async () => {
    const { fixture } = await mount({ lang: 'en' });
    expect(brandLink(fixture).getAttribute('href')).toBe('/home');
  });

  it('exposes the game catalogue entry point', async () => {
    const { fixture } = await mount({ lang: 'en' });
    const links = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('a'),
    ) as HTMLAnchorElement[];
    expect(links.some((a) => a.getAttribute('href') === '/games')).toBe(true);
  });

  it('resolves the brand from i18n rather than a hardcoded literal', async () => {
    // The same element yielding two different strings is the behavioural proof
    // that no display literal survives in the template.
    const en = brandLink((await mount({ lang: 'en' })).fixture).textContent?.trim();
    const zh = brandLink((await mount({ lang: 'zh-CN' })).fixture).textContent?.trim();
    expect(en).not.toBe(zh);
  });
});
