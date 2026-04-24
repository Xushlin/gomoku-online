import { DOCUMENT, inject, Injectable, isDevMode, signal, type Signal } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import {
  FALLBACK_LOCALE,
  SUPPORTED_LOCALES,
  isSupportedLocale,
  type SupportedLocale,
} from './supported-locales';

const LANG_STORAGE_KEY = 'gomoku:lang';

/**
 * Cross-cutting i18n API. Wraps TranslocoService so consumers see Signals
 * and a typed locale set, and so tests can stub the whole thing through the
 * abstract class DI token.
 */
export abstract class LanguageService {
  static readonly supported = SUPPORTED_LOCALES;
  abstract readonly current: Signal<SupportedLocale>;
  abstract use(locale: SupportedLocale): void;
}

@Injectable()
export class DefaultLanguageService extends LanguageService {
  private readonly doc = inject(DOCUMENT);
  private readonly transloco = inject(TranslocoService);
  private readonly _current = signal<SupportedLocale>(FALLBACK_LOCALE);

  readonly current: Signal<SupportedLocale> = this._current.asReadonly();

  constructor() {
    super();
    const initial = this.resolveInitial();
    this.transloco.setActiveLang(initial);
    this._current.set(initial);
  }

  use(locale: SupportedLocale): void {
    if (!isSupportedLocale(locale)) {
      this.warn(`use('${String(locale)}'): unsupported locale; ignoring.`);
      return;
    }
    this.transloco.setActiveLang(locale);
    this._current.set(locale);
    this.persist(locale);
  }

  private resolveInitial(): SupportedLocale {
    const stored = this.read();
    if (stored && isSupportedLocale(stored)) {
      return stored;
    }

    const nav = this.doc.defaultView?.navigator;
    const navLang = nav?.language;
    if (navLang) {
      if (isSupportedLocale(navLang)) {
        return navLang;
      }
      const primary = navLang.split('-')[0]?.toLowerCase();
      if (primary) {
        const match = (SUPPORTED_LOCALES as readonly string[]).find(
          (tag) => tag.split('-')[0]?.toLowerCase() === primary,
        );
        if (match && isSupportedLocale(match)) {
          return match;
        }
      }
    }

    return FALLBACK_LOCALE;
  }

  private read(): string | null {
    try {
      return this.doc.defaultView?.localStorage.getItem(LANG_STORAGE_KEY) ?? null;
    } catch {
      return null;
    }
  }

  private persist(locale: SupportedLocale): void {
    try {
      this.doc.defaultView?.localStorage.setItem(LANG_STORAGE_KEY, locale);
    } catch {
      // Best-effort.
    }
  }

  private warn(message: string): void {
    if (isDevMode() && typeof console !== 'undefined' && typeof console.warn === 'function') {
      console.warn(`[LanguageService] ${message}`);
    }
  }
}
