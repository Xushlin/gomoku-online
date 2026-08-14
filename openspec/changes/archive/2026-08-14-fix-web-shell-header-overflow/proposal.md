## Why

`web-shell` declares mobile-first 375px normative: *「Shell 及 home 页 SHALL 在浏览器视口宽 375px 时保持可用(所有交互可达、**无水平滚动**、文本不被截断)」*. The shell has not met that requirement for some time.

Measured at 375 × 812 on `/g/idiom-crossword`:

| Metric | Value |
| --- | --- |
| `document.documentElement.scrollWidth` | 566 |
| `document.documentElement.clientWidth` | 375 |
| `main.scrollWidth` | 375 (no over-wide descendant) |
| `header.scrollWidth` | **566** |

The overflow is entirely the header, and it is not caused by any single change. The header lays out one non-wrapping row of ten controls — brand, `/games`, four CDK menu triggers (language / theme / board skin / sound pack), two `role="switch"` toggles (sound / dark), username, log out. Each control hides only its *label* below `sm:`; the values, borders, padding and gaps remain, and they add up to ~566px. `add-platform-catalog`'s `/games` link contributes ~63px of that, but removing it still leaves ~503px against a 375px viewport.

The same arithmetic breaks the wide end too: with labels visible the row needs ~1100px, so the header also overflows at `sm` (640) and `md` (768) — the current `sm:` label breakpoint is simply too low. Fixing only 375px would leave the middle of the range broken.

## What Changes

### Header layout — collapse, don't wrap

The six *appearance* controls (language, theme, board skin, sound pack, sound on/off, dark mode) collapse into a single **Settings** CDK menu below `lg:`. Above `lg:` the existing inline row is unchanged except that its labels now appear at `xl:` instead of `sm:`.

Three thresholds, all Tailwind defaults:

| Width | Header contents |
| --- | --- |
| `< lg` (< 1024) | brand · Games · **Settings ▾** · Log out |
| `lg` … `< xl` | brand · Games · six inline controls, values only · username · Log out |
| `≥ xl` (≥ 1280) | as above, with `Label:` prefixes |

Why a menu rather than the two alternatives:

- **Wrapping** (`flex-wrap`) is one class, but at 375px it makes a *sticky* header three rows / ~150px tall — 18% of a phone viewport, permanently, on the game-board route where vertical space is the scarcest resource. It also degrades further with every control a future game adds.
- **A drawer** is a new overlay surface with its own state, focus trap and a11y surface, and the header would still need a trigger button — strictly more work than a menu for the same result.
- **A CDK menu** is the pattern the header already uses four times over. It costs no new concept, nests natively (the four pickers become submenus), and inherits focus trapping, roving tabindex, `ESC`, and type-ahead from `@angular/cdk/menu`. Nothing above `lg:` changes, so there is no desktop regression to review.

### De-duplicating the controls

Declaring six controls twice (inline + in-menu) would double a template that is already the longest in the shell. The controls move into two config lists on `Header` instead — `pickers` (language, theme, board skin, sound pack) and `toggles` (sound, dark) — and both placements render from the same lists with `@for`. Adding a seventh control is one array entry and no template edit.

Each `PickerControl` carries the i18n namespace it draws every string from (`<prefix>.label`, `<prefix>.<option>`), the options straight from the owning service's registry, the current value, and the `apply` callback. That keeps `web-board-skins`' open/closed guarantee intact: options still enumerate from `availableSkins()` / `availablePacks()` / `availableThemes()`, so registering a new skin touches no template.

The four option lists also collapse into **one** `<ng-template #optionPanel let-picker>`; each trigger passes its own control through `cdkMenuTriggerData`, so the inline button and the Settings row open the same panel. Four near-identical `<ul cdkMenu>` blocks become one.

Two structures that look cleaner but do not work here, recorded so nobody retries them:

- **Extracting `<app-header-picker>` / `<app-header-toggle>` components.** `CdkMenu` collects its items with `@ContentChildren(CdkMenuItem, {descendants: true})`, and a content query does not reach into a child component's *view*. Menu items declared inside such a component are invisible to the menu that contains them, which silently breaks roving tabindex, type-ahead and arrow-key navigation.
- **Declaring the controls once in an `<ng-template>` and placing it twice with `ngTemplateOutlet`.** An outlet's embedded view resolves DI and content queries against the template's *declaration* site, not its insertion site, so the items never see the `cdkMenu` they render inside — this fails loudly with `NG0201: No provider found for InjectionToken cdk-menu-stack`.

Both constraints point the same way: CDK menu items must be declared in the same template as their `cdkMenu`. The config lists give single-source-of-truth without violating that.

### i18n

`header.settings.label` (en `Settings` / zh-CN `设置`) in both locale files. Key-set parity preserved.

### Tests

`header.responsive.spec.ts` asserts the invariant that keeps the header inside 375px — a *budget* on how many controls are visible below `lg:`, plus proof that each hidden control is still reachable through the Settings menu with menu-legal roles, plus the label breakpoint.

jsdom has no layout engine, so `scrollWidth` is 0 there and cannot be the assertion. The spec file carries a small `displayAt(el, width)` helper that resolves Tailwind's display utilities for a given width, so the tests read as viewport assertions rather than class-string assertions. Making the real measurement a unit test would mean adding Vitest browser mode plus Playwright to CI for one assertion; the browser numbers are recorded below instead, and the structural budget — which is what actually regressed, and what a future control would regress again — is what the suite holds.

## Capabilities

### Modified Capabilities

- `web-shell`: the 375px responsive baseline gains an explicit header rule (collapse below `lg:`, labels at `xl:`), the keyboard-reachability scenario now names the Settings menu as the narrow-viewport path to the appearance controls, and a new requirement fixes the Settings menu's contents and the `header.settings.*` key contract.

## Impact

- **New files**: `src/app/shell/header/header.responsive.spec.ts`.
- **Modified files**: `src/app/shell/header/header.{ts,html}`, `public/i18n/{en,zh-CN}.json`.
- **Backend**: none.
- **Bundle**: initial 520.66 kB → 522.03 kB raw, 132.66 kB → 132.92 kB gzipped. The `+1.4 kB` is `CdkMenuItemCheckbox` entering the eager shell chunk. The initial-budget warning (500 kB) is pre-existing and unrelated — it was already 20.66 kB over.
- **Not changed**: control order (语言 → 主题 → 棋盘 → 音效皮肤 → 音效开关 → 深色 → 用户) in both placements; the `/games` link's position before the language switcher; every existing i18n key; every service API; every existing test.

### Verified in a browser at 375 × 812

| | before | after |
| --- | --- | --- |
| `documentElement.scrollWidth` / `clientWidth` | 566 / 375 | **375 / 375** |
| `header.scrollWidth` | 566 | **375** |
| header height | 59px (1 row) | 59px (1 row) |
| inline controls | 10 | 4 — brand, Games, Settings, Log in/out |
| horizontal slack | −191px | +72px |

Also checked at 640 / 1024 / 1280: `scrollWidth === clientWidth` at each, labels appear at 1280, the Settings trigger disappears at 1024. Keyboard path through the menu (`↓` opens, `↓↓` to Board, `→` opens its submenu, `Esc Esc` closes and returns focus to the trigger) works with a visible 2px focus ring on every step, and switching to `zh-CN` through the menu persists and re-renders the header without overflow.
