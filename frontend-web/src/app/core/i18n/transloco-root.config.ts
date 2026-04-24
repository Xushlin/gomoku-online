import { isDevMode, type EnvironmentProviders } from '@angular/core';
import { provideTransloco } from '@jsverse/transloco';
import { FALLBACK_LOCALE, SUPPORTED_LOCALES } from './supported-locales';
import { TranslocoHttpLoader } from './transloco-loader';

/**
 * One-shot Transloco setup for the root ApplicationConfig.
 *
 * - `availableLangs` is sourced from the same constant as LanguageService.supported
 *   so the two cannot drift.
 * - `defaultLang` = 'en' is a fallback; LanguageService.resolveInitial() switches
 *   to the real initial lang before the first render.
 * - HttpClient is provided separately by `provideAppHttp()` (see core/http).
 */
export function provideAppI18n(): EnvironmentProviders[] {
  return [
    ...provideTransloco({
      config: {
        availableLangs: [...SUPPORTED_LOCALES],
        defaultLang: FALLBACK_LOCALE,
        fallbackLang: FALLBACK_LOCALE,
        reRenderOnLangChange: true,
        missingHandler: {
          useFallbackTranslation: true,
          logMissingKey: isDevMode(),
        },
        prodMode: !isDevMode(),
      },
      loader: TranslocoHttpLoader,
    }),
  ];
}

