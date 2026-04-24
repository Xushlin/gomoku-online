import { TestBed } from '@angular/core/testing';
import { TranslocoService } from '@jsverse/transloco';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { DefaultLanguageService, LanguageService } from './language.service';

function makeTranslocoStub() {
  return {
    setActiveLang: vi.fn(),
    getActiveLang: vi.fn(() => 'en'),
  };
}

function setNavigatorLang(lang: string): void {
  Object.defineProperty(navigator, 'language', {
    value: lang,
    configurable: true,
  });
}

function createService(nav: string) {
  setNavigatorLang(nav);
  TestBed.resetTestingModule();
  const transloco = makeTranslocoStub();
  TestBed.configureTestingModule({
    providers: [
      { provide: TranslocoService, useValue: transloco },
      { provide: LanguageService, useClass: DefaultLanguageService },
    ],
  });
  const svc = TestBed.inject(LanguageService);
  return { svc, transloco };
}

describe('DefaultLanguageService', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.clearAllMocks();
  });

  it('use() sets Transloco active lang, persists, updates current()', () => {
    const { svc, transloco } = createService('en');

    svc.use('zh-CN');

    expect(transloco.setActiveLang).toHaveBeenLastCalledWith('zh-CN');
    expect(localStorage.getItem('gomoku:lang')).toBe('zh-CN');
    expect(svc.current()).toBe('zh-CN');
  });

  it('initial resolution: localStorage wins over navigator.language', () => {
    localStorage.setItem('gomoku:lang', 'en');

    const { svc } = createService('zh-CN');

    expect(svc.current()).toBe('en');
  });

  it('initial resolution: navigator.language primary-tag match (zh-HK -> zh-CN)', () => {
    const { svc } = createService('zh-HK');

    expect(svc.current()).toBe('zh-CN');
  });

  it('initial resolution: unsupported navigator.language falls back to en', () => {
    const { svc } = createService('ja-JP');

    expect(svc.current()).toBe('en');
  });

  it('initial resolution: navigator.language exact match wins (en-US -> en)', () => {
    // 'en-US' primary tag 'en' matches 'en' in SUPPORTED_LOCALES.
    const { svc } = createService('en-US');

    expect(svc.current()).toBe('en');
  });
});
