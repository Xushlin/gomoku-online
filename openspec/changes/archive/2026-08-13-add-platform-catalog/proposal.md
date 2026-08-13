## Why

`rename-to-gewu` made the names say "platform"; nothing in the product does yet. A user who logs in lands on gomoku's lobby, and there is no surface where a second game could appear. The next change after this one is the idiom vertical (成语纵横), which needs two things to exist first: somewhere for a game to be *discovered*, and a route namespace for it to live in.

This change adds exactly those two things, plus the registry that makes "add a game" a one-folder-plus-one-line operation as the project's open/closed rule requires — the same registry pattern already used for themes, board skins, and sound packs.

## What Changes

### Game registry (`src/app/games/`)

- **`game-manifest.ts`** — the `GameManifest` type: `key`, `category` (`'match' | 'puzzle' | 'score'`), `status` (`'available' | 'planned'`), `titleKey`, `descriptionKey`, `icon`, `contentLocales`, and `launchRoute` (only meaningful when `status === 'available'`).
- **`gomoku/manifest.ts`** — the one available game. `launchRoute: '/home'`, which is where gomoku's lobby actually is (see *Out of scope*).
- **`index.ts`** — the single registration point: an array of manifests. Adding a game means adding one folder and one entry here.
- **`GameCatalogService`** — abstract class as DI token (per the project's dependency-inversion rule) with a default implementation over the registry: `all()`, `available()`, `planned()`, `byKey(key)`.

The seven unbuilt games (成语纵横, 成语接龙, 猜成语, 一字棋, 中国象棋, 华容道, 俄罗斯方块) ship as `status: 'planned'` manifests so the catalogue shows the shape of the platform from day one and each later change flips exactly one `status` field.

### Catalogue page (`/games`)

- New lazy route `/games`, auth-guarded, `loadComponent`.
- Renders one card per manifest: icon, translated title and description, category badge, and a `Chinese only` badge when `contentLocales` excludes the active locale.
- `available` cards navigate to `launchRoute`. `planned` cards are non-interactive, marked `aria-disabled`, and carry a "coming soon" label — they MUST NOT be focusable links to nowhere.
- Responsive grid: single column at 375 px, progressively more columns via `sm:` / `lg:`. Loading is not applicable (the registry is static, no fetch), so there is no skeleton state — the empty and error states that the project's UX rules require do not arise for a static list.

### Header

- A link to `/games` so the catalogue is reachable. Placed before the language switcher, following the existing control ordering convention.

### i18n

- `catalog.{title, subtitle, coming-soon, chinese-only, category-match, category-puzzle, category-score}`.
- `games.<key>.{title, description}` for all eight games.
- Both locales, key sets identical.

### Out of scope — and why

- **Moving gomoku off `/home` to `/g/gomoku`, and making the catalogue the post-login landing page.** `/home` is normative in three live specs: `web-lobby` defines it as the lobby route, `web-game-board` names it nine times as the post-leave / post-dissolve / post-game-over destination, and `web-auth` names it eight times as the guard and post-login redirect target. Moving it means MODIFIED deltas reproducing all of those requirement blocks verbatim — a large amount of copied spec text that unblocks nothing here. `generalize-match-contract` has to rewrite `web-game-board` and `web-lobby` anyway when `MakeMove` becomes `SubmitMove`, so the route move rides along there.
  - Consequence, stated plainly: until then gomoku is reached at `/home` while every new game lives under `/g/<key>`. That inconsistency is real, temporary, and owned by a named change.
- **A `/g/:gameKey` route shell.** No game needs it yet — the first one to need it is 成语纵横, and it should be introduced by the change that has a concrete second route to hang on it rather than speculatively here.
- **Per-game leaderboards or stats on the cards.** Needs `UserGameStats`, which is `add-per-game-rating`.

## Capabilities

### New Capabilities

- `platform-catalog`: the game registry (manifest shape, registration point, `GameCatalogService`), the `/games` catalogue page and its card states, the header entry point, and the i18n key contract for game titles and descriptions.

### Modified Capabilities

(none.) The catalogue is purely additive: no existing route, guard, redirect target, component, or i18n key changes. `web-shell`'s route contract already requires every non-shell route to be lazy, which `/games` satisfies without amendment.

## Impact

- **New files**: `src/app/games/{game-manifest.ts,index.ts,game-catalog.service.ts}`, `src/app/games/gomoku/manifest.ts`, seven planned-game manifests, `src/app/platform/catalog/{catalog.ts,catalog.html}` + spec files.
- **Modified files**: `src/app/app.routes.ts` (one lazy route), `src/app/shell/header/header.html` (one link), `public/i18n/{en,zh-CN}.json`.
- **Backend**: none. The catalogue is static client-side data; no endpoint, no DB, no migration.
- **Bundle**: one new lazy chunk, small (a static list and a card template). No new dependency. Note the pre-existing initial-bundle budget warning (513 kB against a 500 kB budget) is untouched by this change — the catalogue is lazy.
- **Tests**: `GameCatalogService` filtering and lookup; catalogue component rendering (card count, available card navigates, planned card is not a link, Chinese-only badge appears for an idiom game under `en`).
