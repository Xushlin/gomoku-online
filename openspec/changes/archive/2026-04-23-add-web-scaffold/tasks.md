## 1. Workspace bootstrap

- [x] 1.1 `npx --yes @angular/cli@21 new gomoku-web --directory . --routing --style tailwind --test-runner vitest --skip-git --skip-tests --ai-config none --package-manager npm --commit false --defaults` (Angular 21 CLI supports `--style tailwind` and `--test-runner vitest` first-class, so the scaffold ships Tailwind-wired and Vitest-ready; the recommended `@angular/build:application` builder is selected automatically)
- [x] 1.2 Stripped the CLI marketing HTML — `app.html` and `app.css` removed; `App` now inlines a `<app-shell />` template
- [x] 1.3 `tsconfig.json` has `compilerOptions.strict === true` (CLI default)
- [x] 1.4 Verified: no `@NgModule` decorators under `src/` — standalone-only
- [x] 1.5 `npm run build` passes against the stripped skeleton

## 2. Dependencies

- [x] 2.1 Added runtime deps: `@angular/material@^21`, `@angular/cdk@^21`, `@jsverse/transloco@^8`. (Skipped `transloco-messageformat` — no ICU needs yet.)
- [x] 2.2 Added dev deps: `vitest@^4`, `jsdom@^29`. Tailwind v4 (`tailwindcss` + `@tailwindcss/postcss`) and PostCSS were installed by the Angular CLI scaffold via `--style tailwind`. Angular 21's native `@angular/build:unit-test` builder is used instead of `@analogjs/vitest-angular` — saves a dep and is the upstream-supported path.
- [x] 2.3 `npm ci` simulated by installing into empty `node_modules` — succeeds
- [x] 2.4 `package.json` + `package-lock.json` ready to commit

## 3. Tailwind + CSS variable tokens

- [x] 3.1 **Obsolete in Tailwind v4** — no `tailwind.config.js` / no `tailwindcss init`. Tailwind is imported from CSS via `@import 'tailwindcss'` and configured entirely in CSS via the `@theme` directive. PostCSS pipeline already wired by the CLI (`.postcssrc.json` with `@tailwindcss/postcss`).
- [x] 3.2 **Obsolete in Tailwind v4** — `darkMode: 'class'` replaced by the explicit `@custom-variant dark (&:where(.dark, .dark *))` declaration in `src/styles/tailwind.css`
- [x] 3.3 Token utilities exposed via the `@theme { ... }` block in `src/styles/tokens.css` — declares `--color-bg/surface/primary/text/muted/border/danger/success/warning`, `--radius-card`, `--shadow-elevated`. Tailwind generates `bg-bg`, `text-text`, `rounded-card`, `shadow-elevated` etc. automatically
- [x] 3.4 `src/styles/tokens.css` holds `@theme` placeholders + four blocks: `[data-theme='material']`, `[data-theme='material'].dark`, `[data-theme='system']`, `[data-theme='system'].dark`. Values chosen for WCAG AA contrast on text-on-bg in all four combinations (darker primary tones in light mode, lighter tones in dark mode)
- [x] 3.5 `src/styles/global.css` has `*, *::before, *::after { box-sizing: border-box; }`, visible `:focus-visible` outline via `var(--color-primary)`, `@media (prefers-reduced-motion: reduce)` cap on all transitions/animations, and base body font stack incl. `Noto Sans SC` for CJK
- [x] 3.6 `angular.json` `build.options.styles` lists: `@angular/cdk/overlay-prebuilt.css`, `@angular/cdk/a11y-prebuilt.css`, `src/styles/tailwind.css`, `src/styles/tokens.css`, `src/styles/global.css`
- [x] 3.7 `npm run build` produces CSS with the mapped utilities and no raw hex colors leak outside `tokens.css`

## 4. Theme service

- [x] 4.1 `src/app/core/theme/theme.tokens.ts` exports `ThemeTokenSet` + `ThemeTokens` — shape of colors/radii/shadows for light + dark
- [x] 4.2 `src/app/core/theme/theme.service.ts` exports abstract `ThemeService` with signals `themeName`, `isDark` and methods `register`, `activate`, `setDark`, `availableThemes`
- [x] 4.3 `DefaultThemeService` in the same file — uses `signal()`, `inject(DOCUMENT)`, persists to `localStorage['gomoku:theme']` and `localStorage['gomoku:dark']`
- [x] 4.4 Initial-resolution order implemented: theme = localStorage → `'material'` (overwrites invalid values); dark = localStorage → `matchMedia('(prefers-color-scheme: dark)')` → `false`
- [x] 4.5 `themes/material.ts` + `themes/system.ts` export `ThemeTokens` objects mirroring `tokens.css` for dev-time validation + future enumeration UI
- [x] 4.6 Both themes registered in the constructor
- [x] 4.7 `availableThemes(): readonly string[]` exposes the registered names for the header's theme switcher
- [x] 4.8 Wired in `app.config.ts` via `{ provide: ThemeService, useClass: DefaultThemeService }`

## 5. Language service + Transloco

- [x] 5.1 `public/i18n/en.json` + `public/i18n/zh-CN.json` populated with all shell keys: `home.hello`, `header.language.label`, `header.language.<locale>` (native names), `header.theme.label`, `header.theme.<theme>`, `header.theme.dark-toggle`, `header.theme.dark-on`, `header.theme.dark-off`. Note: Angular 21 replaced `src/assets/` with `public/` as the static-assets root — i18n JSON is served at `/i18n/<lang>.json`
- [x] 5.2 `src/app/core/i18n/supported-locales.ts` exports `SUPPORTED_LOCALES = ['zh-CN', 'en'] as const`, `SupportedLocale` type, `FALLBACK_LOCALE`, and `isSupportedLocale` type guard
- [x] 5.3 `src/app/core/i18n/transloco-root.config.ts` exports `provideAppI18n()` bundling `provideHttpClient()` + `provideTransloco({...})`. `TranslocoHttpLoader` at `src/app/core/i18n/transloco-loader.ts` fetches `/i18n/${lang}.json`. `availableLangs` is `[...SUPPORTED_LOCALES]` — same-source guarantee against drift
- [x] 5.4 `src/app/core/i18n/language.service.ts` exports abstract `LanguageService` + `DefaultLanguageService` — signal `current`, method `use`, static `supported = SUPPORTED_LOCALES`
- [x] 5.5 Initial-resolution implemented: `localStorage` → `navigator.language` (exact match → primary-tag match) → `'en'`. `setActiveLang(initial)` is called in `DefaultLanguageService`'s constructor, then `provideAppInitializer` in `app.config.ts` awaits `TranslocoService.load(current())` so the JSON is in memory before first render — no key-flash FOUC
- [x] 5.6 Wired in `app.config.ts`: `provideAppI18n()` + `{ provide: LanguageService, useClass: DefaultLanguageService }` + `provideAppInitializer(...)`
- [x] 5.7 Verified JSON files are served by dev server at `/i18n/en.json` and `/i18n/zh-CN.json`

## 6. Testing with Vitest

- [x] 6.1 Instead of `@analogjs/vitest-angular`, used Angular 21's native `@angular/build:unit-test` builder with `runner: 'vitest'`. `angular.json` has a `test` architect pointing at `tsconfig.spec.json`; no separate `vitest.config.ts` or setup file needed — Angular's builder initialises the test environment automatically
- [x] 6.2 `package.json` scripts: `start`, `build`, `watch`, `test` (`ng test`), `test:ci` (`ng test --watch=false`), `lint` (`ng lint`)
- [x] 6.3 `src/app/core/theme/theme.service.spec.ts` covers: `activate('system')` contract, `setDark(true)` contract, localStorage wins over `prefers-color-scheme`, invalid stored theme falls back to `'material'` and overwrites, `availableThemes()` returns both registered themes
- [x] 6.4 `src/app/core/i18n/language.service.spec.ts` covers: `use('zh-CN')` contract, localStorage wins over navigator, `zh-HK` → `zh-CN` primary-tag match, `ja-JP` → `en` fallback, `en-US` → `en` primary-tag match
- [x] 6.5 `src/app/pages/home/home.spec.ts` — mounts `Home` with `TranslocoTestingModule` set to `zh-CN` and asserts the rendered card contains `欢迎来到五子棋。`
- [x] 6.6 `npm run test:ci` — **3 files, 11 tests, all passing**

## 7. App shell

- [x] 7.1 `src/app/shell/shell.ts` (standalone) + `shell.html` — header + `<main>` with `<router-outlet />`, `min-h-screen`, responsive max-width container
- [x] 7.2 `src/app/shell/header/header.ts` + `header.html` (standalone): Language switcher and Theme switcher as CDK `cdkMenuTriggerFor` buttons opening `cdkMenu` ng-templates with `cdkMenuItem` buttons; Dark toggle as a semantic `role="switch"` button with `aria-checked`. All three react to Signals so the header auto-updates on state change
- [x] 7.3 Header template has zero hardcoded UI strings — only `'Gomoku'` brand text (proper noun, explicit spec exception). Every label/state goes through `| transloco`
- [x] 7.4 Header styles use only token-backed Tailwind utilities (`bg-surface`, `text-text`, `border-border`, `rounded-card`, `shadow-elevated`, etc.) — no hex, no named colors
- [x] 7.5 Shell verified at 375px viewport via dev server; sticky header collapses the `Language:` / `Theme:` prefix labels to icon-only below `sm:` breakpoint so content fits without horizontal scroll

## 8. Home placeholder + routing

- [x] 8.1 `src/app/pages/home/home.ts` + `home.html` (standalone, presentational, OnPush) — ~10 LOC class body, renders one `<section>` card with `{{ 'home.hello' | transloco }}`. No service injection, no HTTP
- [x] 8.2 `src/app/app.routes.ts` — eager `{ path: 'home', component: Home }` + `{ path: '', pathMatch: 'full', redirectTo: 'home' }` + `{ path: '**', redirectTo: 'home' }`
- [x] 8.3 `src/app/app.config.ts` wired with `provideRouter(routes, withComponentInputBinding())`, `provideAppI18n()`, ThemeService + LanguageService providers, and a `provideAppInitializer` that preloads the active translation
- [x] 8.4 Dev server smoke-tested — served at `http://localhost:4200/` with i18n JSON reachable at `/i18n/en.json` and `/i18n/zh-CN.json`

## 9. Cross-cutting polish

- [x] 9.1 `grep -rnE '#[0-9a-fA-F]{3,8}\b|\brgb\(|\bhsl\(' src/app src/styles/tailwind.css src/styles/global.css` (excluding `src/app/core/theme/themes/` — data files, not component styling) returns no matches. `tokens.css` and the `themes/*.ts` files are the authorised homes for concrete token literals
- [x] 9.2 `grep -rnP '[\x{4e00}-\x{9fff}]' src/app` hits only `home.spec.ts` lines 7 and 32 — both are translation-assertion test fixtures comparing against `zh-CN.json`. No template / component CJK leaks
- [x] 9.3 `grep -rnE 'bg-(gray|white|black|...)-|text-(white|black|gray-|...)' src/app` returns no matches — zero hardcoded Tailwind color utilities
- [ ] 9.4 **Manual in DevTools**: switch through all 4 combinations (material/system × light/dark) and both locales (en / zh-CN); no broken text, no invisible elements, no untranslated strings
- [ ] 9.5 **Manual in DevTools**: Tab through header → focus ring visible on every control → Enter/Space activates each
- [ ] 9.6 **Manual in DevTools**: emulate `prefers-reduced-motion: reduce` → confirm transitions are disabled

## 10. Final verification

- [x] 10.1 `npm run lint` — passes (angular-eslint v21.3 configured via `ng add angular-eslint`)
- [x] 10.2 `npm run test:ci` — 11/11 tests pass across 3 files
- [x] 10.3 `npm run build` — production bundle: `main 88.86 kB gzip + styles 2.67 kB gzip = ~91.5 kB gzip` total initial, well under the 500 KB target
- [x] 10.4 Root `CLAUDE.md` "Current phase" updated with a one-liner about `frontend-web/` scaffold completion
- [x] 10.5 `openspec validate add-web-scaffold` — clean
