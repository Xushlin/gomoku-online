## Why

`GameManifest.board` is a front-end copy of the server's board dimensions. It was accepted on this repo's stated test — *whether a copy is acceptable depends not on how small it is but on whether being wrong would ever be noticed* — because a wrong value paints a visibly wrong number of cells and the server rejects out-of-range moves anyway.

**`add-web-xiangqi` broke that argument.** It added `board: { rows: 10, cols: 9 }` and then rendered 象棋 with a component that hardcodes its own 10×9, because a board of intersections is not a parameterisation of a grid of cells. So for 象棋 the field is **read by nothing**: a wrong value there would be noticed by nobody, ever. It survives only because `board-size.spec.ts` requires every playable match game to declare one.

The field's own doc comment says to delete it when dimensions reach the wire. They did, in `add-web-per-game-rating`: `GET /api/games` returns `rows` / `cols` per game, and `GameCapabilitiesService` already caches them. The stated precondition has been met for two changes now.

## What Changes

`GameManifest.board` is deleted, along with the invariant that kept it alive.

`boardSizeFor(catalog, gameKey)` becomes `boardSizeFor(capabilities, gameKey)`, reading the descriptor `GET /api/games` already returns. The room page and the replay page ask the capabilities service instead of the manifest.

### The one real cost: the size is now asynchronous

The manifest is a static import; the descriptor arrives over HTTP. A board rendered before the descriptor lands would be 15×15 and then jump.

It does not jump, because the pages **wait**: both already show a skeleton while their own data loads, and that gate now also covers `capabilities.loaded()`. A player never sees a wrong board — they see the skeleton for however much longer the descriptor takes, which on a warm session is zero because the catalogue page has usually already fetched it.

`DEFAULT_BOARD` stays for the case it was always for: a game key this client has never heard of. Falling back beats a blank page, and the server still rejects out-of-range moves.

### 象棋 keeps hardcoding 10×9, and that is now honest

`XiangqiBoard` does not take dimensions and should not: its layout, its river and its palaces are not a function of two integers. Before this change that fact sat next to a manifest entry claiming otherwise. Now there is no entry to contradict it.

## Non-goals

- No change to `GET /api/games`, to any DTO, or to the backend.
- `GameCatalogService` stays synchronous and static. It says *what games exist and how to reach them*; the server says *what they can do*. This change moves one field across that line rather than merging the two services — see `add-web-per-game-rating` for why they are separate.

## Impact

- `platform-catalog`: the `GameManifest` requirement loses the `board` field.
- `web-tictactoe`: "房间页按棋种决定棋盘尺寸" now resolves through the capabilities service.
- `web-replay`: same resolution change.
- Backend: **zero changes**.
