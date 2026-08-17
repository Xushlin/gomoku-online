## Why

`generalize-lobby` moved every game's lobby to `/g/:gameKey/lobby` and left `/home` as the platform home. It did not move the way out of a room. Leaving a match, resigning it, having the host dissolve it, or dismissing the game-over dialog all still navigate to `/home` — a page with no trace of the game you were just playing, and no way back into it except the games strip.

The button is labelled *back to lobby*. It stopped going to a lobby.

## What Changes

### The exit resolves from the room's own game

A small helper takes a game key and returns where "out of here" means:

```ts
gameEntryRoute(catalog, gameKey)   // manifest launchRoute, else '/home'
```

That is the whole rule, and it is simpler than the one this change was scoped around. `generalize-lobby` set gomoku's `launchRoute` to `/g/gomoku/lobby`, so **the manifest already answers the question** — no capability lookup, no `supportsHumanVsHuman` branch, and no loading gate, because `GameCatalogService` is a static import that never fails and is never empty.

It also gives the right answer for the games that have no lobby. Leaving an AI 象棋 room lands on `/g/xiangqi`, which is where you start another one. Those games' entry pages *are* their lobbies; they simply are not room lists.

### It is three call sites, not five

The roadmap note said five. Two of them fire precisely when the room could **not** be loaded — the 404 on initial load, and the "room not found" panel's link. There is no game key in either case, and `/home` is the only honest answer. Reaching for the room's game key there would mean reading a field of a room that does not exist.

So: room dissolved, leave/dissolve success, and the game-over dialog's primary button.

The replay page was in scope while writing this and is not any more. Its only exit link lives in the 404 branch — the success view has no "back" affordance at all — so the one link it has is already correct for the same reason the room's two are. **The plan claimed a link that does not exist**; the alternative to correcting it was inventing a button nobody asked for so the spec would come true.

### What the label still says

`game.ended.back-to-lobby` keeps its wording even though 象棋's target is an AI setup page rather than a room list. Renaming it would touch the i18n requirements in several web specs to fix something that is, from the player's side, accurate enough: that page is where you go to start another game of this kind. Recorded rather than quietly accepted.

## Impact

- Affected specs: `web-game-board` (the sidebar's leave paths and the game-ended dialog).
- Affected code: `games/game-entry-route.ts` (new), `pages/rooms/room-page/room-page.ts`.
- **Backend: zero changes.** No new endpoint, no new field — the room DTO has carried `gameKey` since `add-tictactoe`.
- No i18n changes.
