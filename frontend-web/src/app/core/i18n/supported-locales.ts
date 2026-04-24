/**
 * The set of locales this app ships. Adding a new locale = add a file at
 * `public/i18n/<tag>.json` and append the tag here. Nothing else changes.
 *
 * Tags follow BCP-47. We intentionally use `zh-CN` (not `zh` or `zh-Hans`)
 * so `navigator.language` values from real browsers match on the nose.
 */
export const SUPPORTED_LOCALES = ['zh-CN', 'en'] as const;

export type SupportedLocale = (typeof SUPPORTED_LOCALES)[number];

export const FALLBACK_LOCALE: SupportedLocale = 'en';

export function isSupportedLocale(value: unknown): value is SupportedLocale {
  return typeof value === 'string' && (SUPPORTED_LOCALES as readonly string[]).includes(value);
}
