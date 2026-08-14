## 1. Header control lists

- [x] 1.1 Add `PickerControl` / `ToggleControl` interfaces to `src/app/shell/header/header.ts` — `PickerControl` carries `prefix`, `options`, `value`, `hasVolume`, `apply`; `ToggleControl` carries `labelKey`, `stateKey`, `checked`, `toggle`.
- [x] 1.2 Expose `pickers` (language / theme / board skin / sound pack) and `toggles` (sound / dark) as getters, options read straight from `SUPPORTED_LOCALES` / `availableThemes()` / `availableSkins()` / `availablePacks()`. Getters rather than `computed` because the registries are plain methods, not signals.
- [x] 1.3 Drop the per-control `currentXxxKey` computeds and `xxxKey(name)` helpers — the `prefix` field replaces all of them. Narrow the locale on the way back in with `isSupportedLocale` instead of casting.

## 2. Header template

- [x] 2.1 Collapse the four `<ul cdkMenu>` option lists into one `<ng-template #optionPanel let-picker>`; each trigger passes its control via `[cdkMenuTriggerData]`. Keep the volume-slider row (`@if (picker.hasVolume)`) NOT marked `cdkMenuItem`.
- [x] 2.2 Render the inline control row as two `@for` loops inside `<div class="hidden shrink-0 items-center gap-2 lg:flex lg:gap-4">`. Move the label spans from `sm:inline` to `xl:inline`.
- [x] 2.3 Add the `lg:hidden` Settings trigger + `<ng-template #settingsMenu>` with `<div cdkMenu>`, rendering the same two lists as `cdkMenuItem` submenu rows and `cdkMenuItemCheckbox` rows.
- [x] 2.4 Add `shrink-0` to the controls that must not compress, `min-w-0 truncate` to the brand, and `max-w-32 truncate lg:inline-block` to the username, so no single long string can push the row past the viewport.
- [x] 2.5 Verify the control order is identical in both placements: 语言 → 主题 → 棋盘 → 音效皮肤 → 音效开关 → 深色.

## 3. i18n

- [x] 3.1 Add `header.settings.label` to `public/i18n/en.json` ("Settings") and `public/i18n/zh-CN.json`(「设置」).
- [x] 3.2 Confirm flattened key-set parity between the two files (existing `i18n-parity.spec.ts` passes).

## 4. Tests

- [x] 4.1 Add `src/app/shell/header/header.responsive.spec.ts` with a `displayAt(el, width)` helper resolving Tailwind display utilities, so assertions read as viewport assertions.
- [x] 4.2 At 375px: exactly four inline controls (`/home`, `/games`, Settings, Log out), no appearance control inline, no `flex-wrap` on the sticky header, and the same budget when logged out.
- [x] 4.3 Settings menu: all six controls in header order; four `menuitem` + two `menuitemcheckbox`; correct current values and `aria-checked`; the board submenu lists everything `availableSkins()` returns.
- [x] 4.4 At 1024px: inline controls back, Settings trigger gone. Labels hidden at 1024, visible at 1280.
- [x] 4.5 Existing `header.spec.ts` (brand, volume slider, `/games` link) still passes untouched — 244/244 green.

## 5. Verification

- [x] 5.1 Browser at 375 × 812: `documentElement.scrollWidth === clientWidth === 375`, `header.scrollWidth === 375`, header still one row (59px), 72px of slack.
- [x] 5.2 Sweep 640 / 1024 / 1280: no horizontal overflow at any width; labels appear at 1280; Settings trigger gone at 1024.
- [x] 5.3 Keyboard: `↓` opens the menu, `↓↓` reaches Board, `→` opens its submenu, `Esc Esc` closes and returns focus to the trigger; 2px focus ring visible throughout.
- [x] 5.4 Switch to `zh-CN` through the Settings menu — persists, re-renders, still no overflow (「格物 · 游戏 · 设置 · 登录」).
- [x] 5.5 `ng build` + `ng test` + `ng lint` green; bundle delta +1.4 kB raw / +0.26 kB gzipped (pre-existing 500 kB budget warning unchanged in kind).
