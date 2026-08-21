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

  it('initial resolution: an invalid stored theme falls back to the default and overwrites', () => {
    localStorage.setItem('gewu:theme', 'nonexistent-theme');

    const svc = createService();

    expect(svc.themeName()).toBe('qq-game');
    expect(localStorage.getItem('gewu:theme')).toBe('qq-game');
  });

  it('a user with no stored preference gets the game-hall theme', () => {
    // 这个平台的默认长相:一个游戏厅,而不是一套后台管理系统的调色板。
    expect(localStorage.getItem('gewu:theme')).toBeNull();

    const svc = createService();

    expect(svc.themeName()).toBe('qq-game');
    expect(document.documentElement.dataset['theme']).toBe('qq-game');
  });

  it('a user who already chose material keeps material', () => {
    // **这条是改默认值时唯一要紧的断言。** 少了它,一个把所有人都改成新默认的
    // 实现在上一条下同样是绿的 —— 而那会抹掉每一个选过主题的人的选择。
    localStorage.setItem('gewu:theme', 'material');

    const svc = createService();

    expect(svc.themeName()).toBe('material');
    expect(localStorage.getItem('gewu:theme')).toBe('material');
  });

  it('availableThemes() exposes every registered theme', () => {
    const svc = createService();

    // 断言用**包含**,不是长度:加一套主题是规格明文承诺的单文件改动,所以一条
    // 会因为「多了一套」而变红的测试只是在要求别人来更新它。
    expect(svc.availableThemes()).toEqual(
      expect.arrayContaining(['material', 'system', 'ink', 'qq-game']),
    );
  });

  it('register() takes a name and nothing else', () => {
    // token 镜像删掉之后,注册表只存名字。这条钉住的是**它不再需要值** ——
    // 一个仍然收 token 的签名会让「加一套主题」重新变成两处编辑。
    const svc = createService();

    svc.register('borrowed');

    expect(svc.availableThemes()).toContain('borrowed');
    // 这里**不**断言 register 的参数个数:那是抽象签名的事,编译器已经钉住了,
    // 而第一版还写错了(抽象方法在原型上没有实现,`.length` 是 undefined)——
    // 一条断言不了自己想断言的东西的断言,不如没有。
  });

  it('activate() still refuses a name nobody registered', () => {
    // 编译期不再拦「注册一个 tokens.css 里没有对应块的名字」,所以运行时这道
    // 拒绝是仅剩的一道 —— 它必须还在。
    const svc = createService();
    const before = svc.themeName();

    svc.activate('never-registered');

    expect(svc.themeName()).toBe(before);
    expect(document.documentElement.dataset['theme']).toBe(before);
  });

});
