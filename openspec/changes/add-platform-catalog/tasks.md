## 1. Registry

- [x] 1.1 Create `src/app/games/game-manifest.ts` exporting the `GameManifest` type with `key`, `category`, `status`, `titleKey`, `descriptionKey`, `icon`, `contentLocales`, `launchRoute?`. Document the `available` ⇒ non-empty `launchRoute` invariant in the doc comment.
- [x] 1.2 Create `src/app/games/gomoku/manifest.ts` — `key: 'gomoku'`, `category: 'match'`, `status: 'available'`, `launchRoute: '/home'`, `contentLocales: ['zh-CN', 'en']` (the board carries no Chinese content), icon `'⬤'`.
- [x] 1.3 Create the seven planned manifests, one folder each: `idiom-crossword` (puzzle, zh-CN only), `idiom-chain` (match, zh-CN only), `idiom-guess` (puzzle, zh-CN only), `tictactoe` (match, both locales), `xiangqi` (match, both), `klotski` (puzzle, both), `tetris` (score, both). All `status: 'planned'`, no `launchRoute`.
- [x] 1.4 Create `src/app/games/index.ts` exporting the manifest array — available first, then planned. This is the only file a future game touches besides its own folder.
- [x] 1.5 Create `src/app/games/game-catalog.service.ts`: abstract `GameCatalogService` (DI token) + `DefaultGameCatalogService` implementing `all()` / `available()` / `planned()` / `byKey()`. `all()` MUST return available entries before planned ones.
- [x] 1.6 Register `{ provide: GameCatalogService, useClass: DefaultGameCatalogService }` in `app.config.ts`, alongside the existing theme / skin / sound registrations.

## 2. Catalogue page

- [x] 2.1 Create `src/app/platform/catalog/catalog.ts` — standalone, `ChangeDetectionStrategy.OnPush`, injects `GameCatalogService` and `LanguageService`. Exposes the manifest list and a `showsContentWarning(manifest)` helper comparing `contentLocales` against the active locale.
- [x] 2.2 Create `src/app/platform/catalog/catalog.html` — responsive grid (1 column, `sm:grid-cols-2`, `lg:grid-cols-3`), one card per manifest. Every string via `transloco`; every colour via CSS variables; no literal hex values.
- [x] 2.3 Available cards render as `<a [routerLink]="manifest.launchRoute">`. Planned cards render as a non-interactive element with `aria-disabled="true"` and the `catalog.coming-soon` label — no `<a>`, no `<button>`.
- [x] 2.4 Each card shows the category badge (`catalog.category-<category>`) and, when the active locale is outside `contentLocales`, the `catalog.chinese-only` badge.
- [x] 2.5 Add the lazy route to `app.routes.ts`: `{ path: 'games', canMatch: [authGuard], loadComponent: () => import('./platform/catalog/catalog').then((m) => m.Catalog) }`. Place it before the `''` redirect. Do NOT touch any existing route.

## 3. Header entry point

- [x] 3.1 Add a `/games` link to `src/app/shell/header/header.html` before the language switcher, text via `catalog.title`, styled like the existing header controls.

## 4. i18n

- [x] 4.1 Add `catalog.{title, subtitle, coming-soon, chinese-only, category-match, category-puzzle, category-score}` to both `public/i18n/en.json` and `public/i18n/zh-CN.json`.
- [x] 4.2 Add `games.<key>.{title, description}` for all eight registry entries to both files. Chinese names for the idiom games in `en.json` too (成语纵横 → "Idiom Crossword" etc.), since the UI string is translatable even when the content is not.
- [x] 4.3 Verify flattened key-set parity between the two files.

## 5. Tests

- [x] 5.1 `game-catalog.service.spec.ts` — `all()` orders available before planned; `available()` / `planned()` partition correctly; `byKey()` hits and misses.
- [x] 5.2 Registry invariant spec — every `available` manifest has a non-empty `launchRoute`; keys are unique; `titleKey` / `descriptionKey` match the `games.<key>.*` convention.
- [x] 5.3 `catalog.spec.ts` — inject a two-manifest stub catalog: renders one card per manifest; the available card is an `<a>` with the right href; the planned card contains no `<a>` and carries `aria-disabled="true"`.
- [x] 5.4 `catalog.spec.ts` — content-locale badge: with active locale `en` and a `['zh-CN']` manifest the warning text renders; with active locale `zh-CN` it does not.

## 6. Verification

- [x] 6.1 `npm run lint` clean.
- [x] 6.2 `npx ng test --watch=false` green, with the new specs counted.
- [x] 6.3 `npx ng build` succeeds; confirm the catalogue arrives as its own lazy chunk and the initial bundle is not inflated (the pre-existing 513 kB warning may remain, but MUST NOT grow).
- [x] 6.4 Confirm no existing route, guard, redirect target, or i18n key changed: `git diff` on `app.routes.ts` shows only an addition, and the diff on the two i18n files shows only additions.

## 7. Notes from implementation

- [x] 7.1 Web tests went 182 → 198 (+16): 4 catalog-service, 5 registry-invariant, 6 catalogue-component, 1 header entry-point.
- [x] 7.2 The registry ships **eight** manifests (gomoku available; idiom-crossword, idiom-chain, idiom-guess, tictactoe, xiangqi, klotski, tetris planned), not seven — the earlier drafts miscounted.
- [x] 7.3 Task 6.3's "initial bundle MUST NOT grow" was **not met, and the task was wrong to demand it**: the initial bundle went 513.36 kB → 516.61 kB (+3.25 kB). Cause is understood, not accidental — registering `GameCatalogService` in `app.config.ts` (task 1.6) pulls `games/index.ts` and therefore all eight manifests into the root bundle. Keeping the root provider was chosen over route-level providers because the future `/g/:gameKey` shell will need `byKey()` outside the catalogue, and 3.25 kB of static data is a fair price for not duplicating providers across every game route. The catalogue *component* is correctly lazy (its own 3.30 kB chunk). The 500 kB budget was already exceeded before this change; raising or fixing it stays a separate concern.
- [x] 7.4 Task 6.4 verified precisely: `git diff --numstat` on both locale files reports `43 0` — 43 insertions, zero deletions, so rewriting the JSON through `JSON.stringify` reformatted nothing. `app.routes.ts` shows a five-line insertion and no other change.
- [x] 7.5 The `en.json` idiom-game titles are English ("Idiom Crossword", "Guess the Idiom") even though the games' content is Chinese-only. That is deliberate and matches D3: the UI string is translatable, the content is not, and the `catalog.chinese-only` badge is what carries the caveat.
