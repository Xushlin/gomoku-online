import { registerLocaleData } from '@angular/common';
import localeZhHans from '@angular/common/locales/zh-Hans';

/**
 * Register all non-default Angular locale data once at app bootstrap.
 *
 * Angular bundles `en-US` data by default. Every other locale you ask the
 * `DatePipe` / `formatDate()` / `formatNumber()` etc. to render needs a
 * matching `registerLocaleData` call, otherwise you get a runtime
 * `NG0701: Missing locale data for the locale "X"` error.
 *
 * We intentionally use `zh-CN` as the BCP-47 tag in `SUPPORTED_LOCALES`
 * (so `navigator.language` matches on the nose). Angular ships the data
 * under the canonical `zh-Hans` (Simplified) script tag, so we register
 * the imported data under the alias `zh-CN`.
 *
 * Adding a new locale = `import x from '@angular/common/locales/<...>';`
 * and one more `registerLocaleData(x, '<tag>')` line here. No component
 * touches needed.
 */
registerLocaleData(localeZhHans, 'zh-CN');
