## Why

`add-klotski` shipped 华容道's rules, an A\* solver and five levels. Nothing can open it — the catalogue still says 即将上线, and the only callers are tests and curl. This is the change that makes it a game.

## Scope

`/g/klotski` = level list → `/g/klotski/levels/:index` = play → solve → stars. Pure REST on the puzzle endpoints; no room, no hub, no ELO.

## What Changes

### The client judges slides itself — and *should*

This is the exact opposite of the call `add-web-xiangqi` made, and the reason is worth stating because the two look superficially alike.

象棋's board deliberately knows **no** rules: a TypeScript port would be a second source of truth that could silently disagree with the server. 华容道's client already has everything — the pieces, the board, and the one rule ("a block slides into adjacent empty cells") — because a client that could not judge a slide could not animate one. There is no second source of truth to create; the rule is one line and it is the same line on both sides.

So the board highlights legal destinations, and a solve is submitted **once**, at the end, as the whole move list. The server replays it. That is `add-klotski`'s design D3 arriving at the layer it was written for.

### `PuzzleApiService.parseLayout` stops being about crosswords

It is declared `parseLayout(layoutJson: string): CrosswordLayout | null` — one game's shape on the shared puzzle client. 华容道 is the game that exposes it, exactly as it exposed the same leak one layer down. It becomes generic: `parseLayout<T>(layoutJson): T | null`.

### Interaction

Click a piece → its legal one-cell destinations are marked → click one. Arrow keys move the selected piece; `Escape` deselects. Same two-step shape as the xiangqi board, for the same reason: it works with a mouse, a finger and a keyboard without any drag machinery.

### Move count is the score, so it is on screen

The player sees their move count and the level's target the moment it matters — after solving, in the result. Before solving, showing the target would turn a puzzle into a countdown.

## Non-goals

- No drag-and-drop. Click-to-slide covers mouse, touch and keyboard; drag adds a third input path with its own failure modes for no new capability.
- No per-move `check` round trip. `add-klotski` D6 already recorded why 华容道 never calls that endpoint.
- No animation beyond a CSS transition on piece position.

## Impact

- New capability `web-klotski`.
- `web-idiom-crossword`: one requirement modified — `parseLayout` is generic now. Behaviour unchanged.
- Backend: **zero changes**.
