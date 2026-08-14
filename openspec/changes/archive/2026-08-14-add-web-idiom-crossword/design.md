## Context

The reference is `chengyu-crossword.html`, a complete working prototype: tap a tile, it lands in the selected cell; fill an idiom and it locks with a 释义 slip; two hints; stars from mistakes plus hints; six levels behind dots.

Everything in it is client-side, including the answers (`grid[k].target`). `add-puzzle-core` moved that boundary — the client now gets a layout and a tray and must ask the server whether a word is right. This change is therefore *not* a port: it keeps the prototype's feel and inverts where the truth lives.

What the platform already supplies: lazy `/g/:gameKey` routes waiting for a first user, a game registry with `idiom-crossword` sitting at `planned`, `ThemeService` taking a tokens file plus one line, CDK for dialogs, and Transloco with enforced key parity.

## Goals / Non-Goals

**Goals:**

- The prototype's interaction feel, with the server as the only source of truth about correctness.
- Playable at 375 px, keyboard-reachable, and correct under both themes × light/dark.
- No answer ever reaches the client, and no score is computed locally.
- Adding this game touches no other game's files.

**Non-Goals:**

- Leaderboards, sound, offline play, gomoku's route move.
- Reproducing the prototype's exact CSS. It hardcodes colours, which the project forbids; the palette moves into theme tokens instead.

## Decisions

### D1: `ink` is a theme, not a board skin

Board skins (`wood`, `classic`, `midnight`) style the gomoku board specifically. The prototype's ink palette is a whole-page look — background, paper, seal red, jade for solved — so it belongs at the theme layer, where a player who chooses it keeps it everywhere.

That forces a decision the prototype never faced: it is dark-only, but a theme must define light and dark. The light set is the honest inversion — 宣纸 ground with ink text, seal red preserved as the accent, because 朱砂 on paper is the older of the two looks anyway.

*Alternative considered:* ship it as a crossword-only stylesheet. Rejected — it would put literal colours back in a component, which the project's rules forbid for good reason, and it would not survive the next game that wants the same look.

### D2: The client never counts anything that scores

`mistakes`, `hintsUsed`, and stars are read from responses, never derived locally. The client *does* track which cells it has filled — that is presentation state — but the moment a slot is full it asks the server.

This is the whole point of `add-puzzle-core`, and the failure mode it prevents is subtle: a client that counts its own mistakes will drift from the server's count the first time a request is retried, and then the star preview disagrees with the awarded stars. Reading the authoritative number is both safer and simpler.

### D3: Grid geometry is a computed signal, not a resize listener

The prototype listens to `window.resize` and writes `--cell` imperatively. Here a `ResizeObserver` on the board container feeds a width signal, and cell size is `computed()` from width, column count, and gap.

Reasons: it reacts to *container* changes (sidebar, orientation) rather than only window ones; it cannot leak a listener across route changes; and it keeps the value in the same reactive graph as everything else, so the template needs no manual repaint. The prototype's `renderAll()` on every resize also rebuilt the whole DOM — Angular's tracked `@for` does not.

### D4: One request per completed slot, not per keystroke

`check` fires when a slot's cells are all filled, which is what the prototype does locally. Placing a tile that does not complete anything costs nothing.

Worst case is a player filling the last cell of two crossing slots at once — two requests. Both are cheap indexed reads. The alternative, batching checks until submit, would lose the per-idiom feedback that makes the game feel alive.

### D5: A wrong slot shakes and clears, and the server has already counted it

On an incorrect verdict the cells shake (the prototype's animation, respecting `prefers-reduced-motion`) and the tiles return to the tray. The mistake count in the UI comes from the response, so the shake and the counter can never disagree.

Deliberately **not** decided: whether to tell the player *which* character is wrong. The server does not say, and inventing a client-side guess would be both wrong and a hint the game did not intend to give.

### D6: Locked levels are inert, like planned catalogue cards

Same rule, same reason: a focusable control that goes nowhere is worse for keyboard and screen-reader users than an element that never claims to be interactive. Lock state comes from the server's `unlocked` field, not from a local computation over stars.

### D7: The double parse is wrapped once

`payloadJson` is a JSON string inside a JSON response — the price of a platform that does not understand game payloads. `PuzzleApiService` parses it and returns typed objects, so no component ever sees the raw string. Malformed payloads resolve to `null` rather than throwing, because a broken slip should not take down a solved puzzle.

## Risks / Trade-offs

- **[Latency between filling a slot and seeing it lock]** → Cells show a pending state while the check is in flight; on a slow link the player keeps playing elsewhere. Requests are not queued behind each other.
- **[A dropped `check` loses a mistake the server recorded]** → The response is authoritative and the next response corrects the display, so drift is self-healing rather than accumulating.
- **[Ink theme's light set is invented, not designed]** → It is a first pass and easy to retune: one tokens file, no component changes.
- **[Big grids at 375 px]** → Level 11 is 12×10. Cell size floors at a legible minimum and the board scrolls inside its own container rather than pushing the page wide.
- **[Reload mid-attempt abandons it]** → Accepted, and it only costs that player's own time (`add-puzzle-core` D6). Resuming an attempt is a later change if playtesting asks for it.

## Migration Plan

Additive. New routes under `/g/idiom-crossword`, one manifest status flip, one theme registration. No backend change, no migration, no existing route touched. Rollback is reverting the commit; the backend keeps working and the catalogue card returns to `planned`.

## Open Questions

- **Should the tray group identical characters?** Level 11 has 44 tiles and duplicates are common. Grouping with a count is tidier; showing every tile matches the prototype. Starting with the prototype's behaviour.
- **Does the ink theme need its own board skin for gomoku?** Choosing `ink` today leaves the gomoku board on whatever skin was selected. Not obviously wrong; worth a look once someone plays both.
