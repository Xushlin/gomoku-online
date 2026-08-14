## Context

`add-idiom-crossword` chose to give `Hint` a bare `alreadyRevealedCount` rather than the player's cursor, on the grounds that a cursor has to be reported by the client and trusted, and that "for a ranked game that is a trade in the wrong direction" (its design D5). The same decision recorded the cost as an occasional wasted hint and deferred it explicitly: "If playtesting says that matters, the fix is to pass the set of cells the player has *filled* (not the answers) and reveal the first unfilled one."

Playtesting said it matters, and the estimate of the cost was wrong. Both the player and the reveal order run top-to-bottom, so they stay in lockstep: by the time you are stuck, every cell a hint can reach is one you already solved. On level 5 the first useful hint would have been the 14th.

The original concern — trusting client input in a ranked game — was aimed at the wrong thing. What must not be trusted is anything that **scores**: mistakes, hints used, elapsed time. Those stay server-owned and are untouched here. Where the single revealed cell lands changes nothing about the score.

## Goals / Non-Goals

**Goals:**

- A hint reveals a cell the player actually wants.
- Nothing that scores moves from the server.
- The platform stays ignorant of game payload shape.

**Non-Goals:**

- Changing what a hint costs, or how stars are computed.
- Hint semantics for 华容道 / 猜成语 — they get the same opaque channel and decide for themselves.
- Any migration, any level regeneration.

## Decisions

### D1: Opaque `stateJson`, not a typed cursor

The obvious signature is `Hint(solution, layout, IReadOnlySet<string> filled, string? selected)`. Rejected: "filled cells" and "a selected cell" are crossword concepts. 华容道's hint context is a block layout; 猜成语 has no grid at all. Baking a grid cursor into the platform interface would make the next puzzle game work around it.

So the client sends an opaque string, exactly as it already does for `check` and `submit`, and the rules parse it. The platform keeps understanding nothing about game content — which is the property that let 成语纵横 register without touching a platform file.

### D2: Selected beats first-unfilled, and may point at a filled cell

The prototype reveals the *selected* cell, falling back to the first empty one. Matching that is the point of this change, so precedence is: valid selection → first unfilled → first cell.

A selection is honoured even when that cell already holds a character. That is deliberate: the cell you are staring at with a wrong tile in it is precisely the one you want resolved, and the client's `applyHint` already frees the wrong tile back to the tray before writing the correct character.

A selection that is not a real cell, or is pre-filled, is ignored rather than rejected — a stale cursor after a restart should degrade to a sensible hint, not an error.

### D3: All-filled falls back to the first non-pre-filled cell

If the client reports every cell filled, there is no unfilled cell to reveal. Rather than erroring, reveal the first non-pre-filled cell and let the correct character overwrite whatever is there.

This is reachable: a wrong full submission records a mistake and leaves the attempt open with the grid still full. At that moment overwriting a cell is exactly the help the player needs.

### D4: The hint endpoint's body is optional

`POST /api/puzzle-attempts/{id}/hint` gains an optional `stateJson`. An absent or unparseable body degrades to rule 2 (first unfilled — with nothing reported filled, that is the first cell), which is the old behaviour for a fresh grid.

Keeps the change additive on the wire and means a client that has not been updated still gets a hint rather than a 400.

### D5: Trusting the reported state is not a scoring hole

Worth stating plainly because the original design rejected exactly this. The client reports which of its own cells hold characters and where its cursor is. It cannot use that to learn anything — the response is one cell, the same one cost, and the answers never leave the server.

It *can* aim: a client claiming "nothing filled, selected = (6,2)" gets (6,2). That is the prototype's behaviour and the intended feature. `HintsUsed` still increments server-side, so aiming a hint costs the same star as stumbling into one.

## Risks / Trade-offs

- **[A stale `selected` after restart reveals a cell the player did not mean]** → Costs one hint on a fresh grid where any cell is about equally useful. Cheaper than validating cursor freshness across attempts.
- **[Bigger hint request body on a 12×10 level]** → ~120 short strings, a couple of KB, on an endpoint called at most a handful of times per level.
- **[Two puzzle games could parse `stateJson` differently]** → Intended. Opaqueness is what keeps the platform out of game content.

## Migration Plan

None. No schema, no level data, no stored state. `alreadyRevealedCount` disappears from an interface with one implementation and one caller.

Rollback is reverting the commit; hints return to reading order.

## Open Questions

- **Should a hint refuse to reveal a cell the player already has correct?** It would stop the last way to waste one, but it also means the server tells the client "that one is already right" — a free check, without the mistake count that `check` charges. Left alone deliberately.
