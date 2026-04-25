import { formatDate } from '@angular/common';
import { describe, expect, it } from 'vitest';
import './register-locales';
import { SUPPORTED_LOCALES } from './supported-locales';

/**
 * Regression test for `NG0701: Missing locale data for the locale "X"`.
 *
 * Angular bundles `en-US` data by default and ignores everything else
 * unless `registerLocaleData()` is called. This test imports the
 * `register-locales` side-effect module (same path the app uses) and
 * then asserts every entry in `SUPPORTED_LOCALES` can render through
 * `formatDate` without throwing.
 *
 * If a future locale is added to `SUPPORTED_LOCALES` but its data file
 * isn't registered, this test fires and tells you which tag.
 */
describe('register-locales', () => {
  const sample = new Date('2026-01-15T12:34:56Z');

  for (const tag of SUPPORTED_LOCALES) {
    it(`formatDate works for "${tag}"`, () => {
      expect(() => formatDate(sample, 'short', tag)).not.toThrow();
      expect(() => formatDate(sample, 'longDate', tag)).not.toThrow();
    });
  }
});
