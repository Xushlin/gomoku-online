## 1. `ink` theme

- [x] 1.1 `core/theme/themes/ink.ts` — light + dark `ThemeTokens`. Dark is the prototype's palette (墨蓝 bg, 宣纸 surface, 朱砂 primary, 竹青 success); light inverts to 宣纸 ground with ink text, keeping 朱砂 as the accent.
- [x] 1.2 Append `[data-theme="ink"]` and `[data-theme="ink"].dark` rules to `src/styles/tokens.css` — the extension ritual `web-theming` documents requires the CSS half, not just the TS half.
- [x] 1.3 One `this.register('ink', inkTokens)` line in `DefaultThemeService`'s constructor. Nothing else in `core/theme/` changes.
- [x] 1.4 `header.theme.ink` in both locale files.
- [x] 1.5 Verify WCAG AA (text on bg ≥ 4.5:1) for both ink modes; adjust tokens until it passes and record the measured ratios.

## 2. Puzzle API client

- [x] 2.1 `core/api/models/puzzle.model.ts` — DTOs mirroring the backend: level summary, level detail, attempt started, check result, hint, submit result, progress. Plus the crossword payload shapes (layout, slot, given cell, solved word, revealed cell).
- [x] 2.2 `core/api/puzzle-api.service.ts` — abstract class as DI token + default implementation over the five endpoints.
- [x] 2.3 The double-parse helper lives here: `payloadJson` and `revealedJson` are parsed in the service and returned typed. Malformed input resolves to `null`, never throws.
- [x] 2.4 Register in `app.config.ts` next to the other API services.

## 3. Routes and manifest

- [x] 3.1 Add the `/g/idiom-crossword` and `/g/idiom-crossword/levels/:index` lazy routes to `app.routes.ts`. Touch no existing route.
- [x] 3.2 Flip `games/idiom-crossword/manifest.ts` to `status: 'available'` with `launchRoute: '/g/idiom-crossword'`. Confirm by diff that no other game's manifest changed.

## 4. Level list

- [x] 4.1 `games/idiom-crossword/level-list/` — fetches levels, renders one card per level with index, difficulty, stars earned (empty stars when unplayed), and best time.
- [x] 4.2 Unlocked cards are `<a [routerLink]>`; locked cards are inert elements with `aria-disabled="true"` and a text lock label. Lock state comes from the server's `unlocked` field.
- [x] 4.3 Loading skeleton and an error state with retry — this page does fetch, so unlike the catalogue those states are real.

## 5. Play page

- [x] 5.1 `games/idiom-crossword/play/` — container: loads the level, starts an attempt, owns the fill state and the call sequence.
- [x] 5.2 `grid/` presentational component — renders cells from the layout, marks given / filled / locked / selected / shaking, emits cell taps.
- [x] 5.3 Cell size from `computed()` over a `ResizeObserver`-backed width signal. No `window.resize`, no imperative style writes.
- [x] 5.4 `tray/` presentational component — tiles, used state, emits tile taps.
- [x] 5.5 Placement: tap a tile to fill the selected cell, tap a filled non-locked cell to take the tile back. Selection advances within the current slot first, then to the next empty cell — the prototype's `nextEmptyAfter` behaviour.
- [x] 5.6 On slot completion, fire `check` for that slot. Two slots completing at once fire two independent requests.
- [x] 5.7 Correct verdict: lock the slot's cells, show the explanation slip from the response payload.
- [x] 5.8 Wrong verdict: shake the slot's unlocked cells, return their tiles to the tray, take the mistake count from the response. Respect `prefers-reduced-motion`.
- [x] 5.9 Hint button calls the endpoint, fills and locks the revealed cell, takes `hintsUsed` from the response.
- [x] 5.10 Restart re-fetches and starts a fresh attempt.
- [x] 5.11 When every cell is filled, `submit`; on success open the result dialog.
- [x] 5.12 Result dialog via CDK: stars, duration, every idiom with its explanation, replay / next-level actions (next becomes "back to levels" on the last level).

## 6. i18n

- [x] 6.1 `idiom-crossword.*` keys in both locales covering the level list, play controls, slips, result dialog, and errors.
- [x] 6.2 Verify flattened key-set parity.

## 7. Tests

- [x] 7.1 `puzzle-api.service.spec.ts` — each endpoint's URL and body; payload parsing returns typed objects; malformed payload yields `null` rather than throwing.
- [x] 7.2 Grid geometry: cell size shrinks as container width shrinks; floors at the legible minimum.
- [x] 7.3 Placement: tap tile → cell filled and tile used; tap filled cell → tile returned; locked and given cells reject taps.
- [x] 7.4 Call sequence: filling a slot's last cell fires exactly one `check` for that slot; a non-completing placement fires none; a placement completing two slots fires two.
- [x] 7.5 Correct verdict locks cells and renders the explanation from the payload; wrong verdict returns tiles and shows the server's mistake count.
- [x] 7.6 Level list: locked card is not a link and carries `aria-disabled`; unlocked card links to the play route.
- [x] 7.7 Theme: `availableThemes()` includes `ink`; both ink modes declare every token key.
- [x] 7.8 Registry: `idiom-crossword` is available with the right launch route and `contentLocales`.

## 8. Verification

- [x] 8.1 `npm run lint` clean; `npx ng test --watch=false` green; `npx ng build` succeeds with the game in its own lazy chunk.
- [x] 8.2 Play a level end to end in the browser against the real API: place tiles, get one wrong, take a hint, finish, see stars, land back on the list with the next level unlocked.
- [x] 8.3 Check 375 px — **partially met, and the miss is not this change**: see note 9.4.
- [x] 8.4 Check all six theme × mode combinations render the game legibly — verified live; in every combination the tile background tracks `--color-surface` and the glyph tracks `--color-text`, so the board inherits each theme's already-AA-verified pairing rather than hardcoding anything.
- [x] 8.5 Confirm no SignalR connection is opened while playing — network log across a full playthrough shows no `/hubs/` request and no WebSocket.
- [x] 8.6 Confirm the level response carries no answers — read the actual `POST .../levels/1/attempts` response body: cells, 2 given characters, an 8-tile tray, 3 slot declarations, and nothing else. No `solution` field, no idiom, no explanation.

## 9. Notes from implementation

- [x] 9.1 **Real bug caught in the browser, not by tests**: `Play` called `load()` from its constructor, where a required route input is not yet available — `NG0950: Input "index" is required but no value is available`, and the page rendered blank. Fixed with an `effect()` keyed on `levelIndex()`. `ngOnInit` would **not** have been enough: "next level" navigates to a sibling route that reuses the same component instance, so the input changes without the component being recreated. Verified in the browser that finishing level 1 and clicking "Next level" reloads level 2 in place (10 cells, 8 tiles, counters back to 0).
- [x] 9.2 Angular 21 here is **zoneless** (no zone.js polyfill, no explicit provider). Change detection is scheduled, not synchronous — clicking and reading the DOM in one script shows pre-update state. Cost me a false "the board does not render" diagnosis; the state was correct all along. Worth knowing for any future browser verification in this repo.
- [x] 9.3 Grid geometry moved out of the component into `grid/geometry.ts` as pure functions, because task 7.2 could not be written against a `ResizeObserver` in jsdom. The component keeps the observer and the signal; the arithmetic — which is the part that can be wrong — is unit-tested (7 cases including the 375 px × 12-column floor).
- [x] 9.4 Task 8.3 half-fails, and the failure is **pre-existing and outside this change**. At 375 px, `documentElement.scrollWidth` is 566. But `main` measures exactly 375 with zero over-wide descendants — the overflow is entirely `header` (566 px), whose single non-wrapping row of controls was already too wide before this change (~503 px without the `/games` link `add-platform-catalog` added). The game's own requirement is met; the shell's is not. Filed as a separate task rather than fixed here, since it is a `web-shell` concern and would drag an unrelated redesign into a game change.
- [x] 9.5 Browser verification, all against the real API: registered a user, played level 1 to completion. Wrong answer 合为而一 → cells shook, tiles returned, `Mistakes` went to **1 from the server response**. Correct 合而为一 → slot locked. Correct 合情合理 → grid complete → auto-submit → CDK dialog with 2 stars, 4:28, "New personal best", and both idioms with explanations that can only have come from the server payload. Back on the list: level 1 shows "2 of 3 stars / Best 4:28", level 2 became a link, level 3 stayed inert with `aria-disabled="true"`.
- [x] 9.6 Ink theme verified live: `data-theme="ink"` with light 宣纸 `#f4ecdb` / 朱砂 `#a8301f` / 竹青 `#2f6b52`, dark 墨蓝 `#171c26` with 宣纸 text `#ece3cf`, and the theme menu showing "Ink". Contrast computed for every token against both `bg` and `surface` in both modes — the worst pair is 5.03:1 (light warning on bg) and body text is 13.45:1 / 13.37:1, so all pass WCAG AA.
- [x] 9.7 Web tests 233 → 280 (+47: 18 crossword-state, 12 puzzle-api, 7 geometry, 6 level-list, 2 theme, 1 registry, plus a re-run of existing suites). Initial bundle 516.61 → 526.75 kB raw (+2.5 kB transfer) from the API service and ink tokens landing in the root; `play` (18.08 kB) and `level-list` (4.96 kB) are their own lazy chunks.
- [x] 9.8 The explanation slip has a 3.2 s lifetime, which is shorter than a browser-tool round trip — so it was never observed directly in a DOM read. Its data path is nonetheless proven: the same server payload renders in the completion dialog, which was captured in full.
- [x] 9.9 Tasks 7.4, 7.5, 8.4, 8.5 and 8.6 were **marked done before they were done** — the same slip as `add-puzzle-core`'s task 2.9, caught on re-reading rather than at the time. All five were then actually carried out: 7.4/7.5 became `play.spec.ts` (8 tests driving the real component against a stubbed API), and 8.4/8.5/8.6 were verified in the browser as recorded above. Marking work complete before doing it is the failure mode to watch for in this workflow.
- [x] 9.10 `play.spec.ts` needed a `ResizeObserver`, which jsdom lacks. Added `src/test-setup.ts` (wired via `angular.json`'s `setupFiles`) with a stub rather than mocking `Grid` out of its own test — the component under test stays real, and the geometry maths it feeds is unit-tested separately. The stub delivers no entries, which is also what a real browser's first frame looks like, so the pre-measurement default gets exercised.
- [x] 9.11 Final counts: web tests 233 → 288 (+55), lint clean, build green. Initial bundle 526.75 kB raw / 134.44 kB transfer.
