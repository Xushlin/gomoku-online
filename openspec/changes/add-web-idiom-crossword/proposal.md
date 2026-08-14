## Why

The backend can be played but nobody can play it. `add-idiom-crossword` registered the rules and seeded 12 levels, so the puzzle endpoints answer for `idiom-crossword` — but the only way to reach them today is curl. The catalogue still shows the game as `planned`.

This is also the change that spends the platform work: `/g/:gameKey` gets its first real route, the game registry gets its second `available` entry, and the puzzle REST contract gets its first client. If those abstractions were right, this change is mostly game code.

## What Changes

### `ink` theme — 活字印刷

The prototype's palette (墨蓝底 / 宣纸字块 / 朱砂印章 / 竹青完成色) becomes the platform's third theme, registered the same way `material` and `system` are: one tokens file plus one `register` line in `ThemeService`'s constructor. Light and dark sets both defined.

It is a **platform theme, not a game skin**. The prototype is dark-only ink; a light 宣纸 variant is the honest counterpart, and making it a theme means a player who likes it keeps it across gomoku too.

### Puzzle API client

`core/api/puzzle-api.service.ts` — abstract class as DI token, default implementation over the five existing endpoints. Plus `models/puzzle.model.ts` mirroring the DTOs.

Includes the helper the payload design forces: `payloadJson` is a JSON string *inside* a JSON response, so the client parses twice. That double parse is wrapped once, in the service, rather than repeated at every call site.

### The game (`games/idiom-crossword/`)

- **Level list** at `/g/idiom-crossword` — one card per level with stars earned, best time, and lock state. Locked levels are inert, matching the catalogue's rule about controls that go nowhere.
- **Play page** at `/g/idiom-crossword/levels/:index` — the grid, the tile tray, the slip that shows an idiom's explanation when it is solved, hint and restart controls, and the completion dialog with stars.
- Grid geometry comes from a **computed signal** over a container-width signal, not the prototype's `window.resize` listener plus manual `--cell` writes.
- Every dialog is CDK. The prototype's hand-rolled `#overlay` div gets focus trap, ESC, and ARIA for free.

### Server-authoritative flow, unchanged from the backend's design

The client holds no answers. It calls `check` when a slot fills, `hint` when the player asks, `submit` when the grid is full, and takes `mistakes` / `hintsUsed` / stars from the responses rather than counting locally.

### Registry and routes

- `/g/:gameKey/...` route namespace — first use. Gomoku stays at `/home` (see `add-platform-catalog`).
- `idiom-crossword` manifest flips to `available` with `launchRoute: '/g/idiom-crossword'`.

### Out of scope

- **Leaderboards** — still `add-puzzle-leaderboard`.
- **Sound.** The prototype is silent; `SoundService` exists and a 活字 pack would be nice, but it is a separate change with its own asset work.
- **Moving gomoku to `/g/gomoku`** — still `generalize-match-contract`.

## Capabilities

### New Capabilities

- `web-idiom-crossword`: the two routes and their shell, grid rendering and geometry, tray interaction, the server-authoritative call sequence, the explanation slip, hint and restart, the completion dialog, and the i18n key contract.

### Modified Capabilities

- `web-theming`: a third registered theme (`ink`), and its label in the theme switcher.
- `platform-catalog`: `idiom-crossword` becomes `available` with a launch route — the registry's first status flip, which is the mechanism that change promised would cost one field.

## Impact

- **New**: `core/theme/themes/ink.ts`, `core/api/puzzle-api.service.ts`, `core/api/models/puzzle.model.ts`, `games/idiom-crossword/` (level-list, play page, grid, tray, slip, result dialog), spec files for each.
- **Modified**: `core/theme/theme.service.ts` (one register line), `games/idiom-crossword/manifest.ts` (status + route), `app.routes.ts` (the `/g/:gameKey` branch), `public/i18n/{en,zh-CN}.json`.
- **Backend**: none. Every endpoint this change calls already exists and is tested.
- **Bundle**: one new lazy chunk for the game. The ink tokens add a few hundred bytes to the theme registry in the root bundle, consistent with how `material` and `system` already ship.
- **Tests**: the API service's double-parse helper, grid geometry, tray placement and retrieval, the call sequence on slot completion, the locked-level rule, stars rendering, and theme registration.
