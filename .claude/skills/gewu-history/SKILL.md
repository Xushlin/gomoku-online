---
name: gewu-history
description: Index into JOURNAL.md — what each of the nine shipped games and each kernel seam actually cost, and which assumptions turned out false. Use before adding a game, changing IGameRules / Room / Game / Move, adding a per-game UI, touching the card table, or when a design choice here looks arbitrary and you want to know whether it was already decided and why.
metadata:
  author: gewu
  version: "1.0"
---

# 格物 / Gewu — engineering history

The record is [`JOURNAL.md`](../../../JOURNAL.md) at the repo root: one entry per shipped change, in merge order. **This file is the index, not a summary** — it tells you which entries apply to what you are about to do. Search the journal by the change name in backticks; every entry starts with its own name.

Reading the whole journal is not the intended use. Read the entries this index points at.

---

## Before adding a tenth game

Read, in this order: **`add-tictactoe`** (the first priced answer to "what does a second board game cost" — ~310 lines of registry debt vs ~332 for the game), then the entry for whichever existing game is *closest in shape* to yours, then **`add-wakeng`** (the most recent full example, and the one where the acceptance criterion was strongest).

The single most important inherited rule: **adding a game must not touch the match aggregate.** `git status backend/src/Gewu.Domain/Rooms/` is expected to be empty, and `Games/Abstractions/` should gain exactly one `GameKeys` constant. Four games have now met that bar. When a game *cannot* meet it, that is the signal to stop and write an enabling refactor first — see the seam list below.

The trap that has cost the most: **a second game from the same family proves nothing.** 一字棋 could not prove any match seam general because it is gomoku in miniature; 中国象棋 could, and every one of the three layers the assumption had leaked into needed changing. The same mistake was made four separate times with interfaces shaped by their only implementation — `IPuzzleRules` was "proven" by a fake modelled on 成语纵横, and both of its methods had to change when 华容道 arrived. Entries: **`generalize-puzzle-rules`**, **`add-xiangqi-ai`**, **`generalize-match-payload`**.

## Kernel seams — what each one is for, and what forced it

Every one of these exists because a specific game could not be added without it. **Never add a member to `IGameRules`** — five seams are separate interfaces for the same reason: on the base interface, the other games each need a lying implementation, and *a lying implementation is something the next person cannot delete*.

| Seam | Forced by | Journal entry |
| --- | --- | --- |
| `IGameRules.Apply(MatchState, intent, seat)` — rules own bounds, occupancy, legality | 中国象棋 (`from → to`) | `generalize-match-domain`, `pass-setup-to-rules` |
| `MoveIntent` positional **or** textual, exactly one | 成语接龙 (a move is an idiom) | `generalize-match-payload` |
| Seats as integers, `(seat + 1) % SeatCount` | 斗地主 (three seats) | `generalize-match-seats`, `add-room-seats` |
| `GameResult { Ongoing, Decided, Draw }` + winner seat | 斗地主 (seat 2 had no value to write) | `generalize-match-outcome` |
| `IDealtGameRules.CreateSetup(seed)` — server-only, reaches no DTO | 斗地主 (the deal) | `add-match-setup` |
| `MoveApplication.NextSeat` — rules name the next player | 斗地主 (the landlord leads) | `generalize-turn-flow` |
| `ITimeoutFallbackRules.MoveOnTimeout` — timeout **plays**, not forfeits | 斗地主 (no unique "opponent") | `generalize-turn-flow`, `pass-state-to-fallback` |
| `IFirstSeatRules.FirstSeat` — the deal decides who starts | 挖坑 (smallest ♣ leads) | `generalize-match-kickoff` |
| `IPerSeatViewRules.ViewFor(state, seat)` — hands visible to one seat | 斗地主 (hidden information) | `add-doudizhu-visibility` |
| `IPlayHintRules.LegalPlays(state, seat)` — "what can I play" | 挖坑, then 斗地主 | `add-wakeng-play-hints`, `add-doudizhu-play-hints` |

Two seams were deliberately **not** built: a JSON move column (rejected with a named trigger that has still not fired — `Move.Text`'s 64 chars fit a 20-card play at one char each), and a score-attack registry (one game; *a switch with one arm is a switch*). Entries: **`generalize-match-domain`**, **`add-tetris`**.

## Should the client know the rules?

This has **four different answers** in this repo and all four are right. The test is not *should the client know* but **would knowing them create a second truth that can diverge**:

- **中国象棋 — no.** Move rules live only on the server; a TS port would create a divergence a player reads as a bug. You learn illegality by being refused. (`add-web-xiangqi`)
- **华容道 — yes.** "A block slides into an adjacent empty cell" is what drawing a drag *requires*, so there is nothing to create — and the server replays the whole path anyway. (`add-web-klotski`)
- **成语接龙 — it splits.** Two of three rules are decidable from what is on screen; the third needs 30,895 words the client should not carry. So it displays the required first character and gates nothing. (`add-web-idiom-chain`)
- **斗地主 / 挖坑 — no, except the one rule that needs no rules** ("nothing on the table, so you cannot pass"). (`add-web-doudizhu`, `add-web-wakeng`)
- **俄罗斯方块 — the client owns everything**, and the server's straight-drop replay model *dictates a game rule*: no tucking under an overhang, because the server would replay it two rows higher. (`add-web-tetris`)

## Rating, and why two games are unrated

`IsRated` is constrained by the invariant `IsRated ⇒ SupportsHumanVsHuman`, enforced in the constructor and by a registry walk — not by judgement, because **judgements expire silently**. The two unrated games have *different* reasons, and being able to state a different reason for each is the test that a rule was applied rather than a pattern matched:

- **一字棋** — no human-vs-human mode, so its only opponents are bots, and bot games are rated; a ladder would rank Easy-bot grinding. (`add-game-capabilities`)
- **斗地主 / 挖坑** — ELO is a two-player model and these settle in per-player points, so a ladder over them is a *different* ladder. Keeps `IsRated ⇒ SeatCount == 2` intact without an exception. (`add-doudizhu-cards`, `add-wakeng`)

Twice a structural fact was declared and **enforced nowhere**, and both times the conclusion it held up still stood — which is what made it invisible: `POST /api/rooms` accepted 象棋 while its own descriptor said `supportsHumanVsHuman: false`, and `POST /api/rooms/ai` seated a bot for 成语接龙 and paid out **+46 ELO** for playing nothing. Entries: **`enforce-human-vs-human`**, **`enforce-ai-availability`**. Both were caused by inferring one half of the endpoint pair from the other.

## The card table

Shared and **parameterised**, not copied — `frontend-web/src/app/games/cards/`. `CardTableConfig` is a **required** input, so the compiler lists every call site; a default would make "forgot to pass it" and "deliberately used 斗地主's" identical, and the symptom is only visible on screen.

What is shared is a **fact** (the one-char encoding, the four suit paths, the three-seat geometry, "the current trick starts at the last `play:`"); what is per-game is what can legitimately **diverge** (the seat-view shape, and the ranking — 挖坑 is `3 > 2 > A > … > 4`). Entries: **`hoist-card-model`** for the rule, **`add-web-wakeng`** for applying it.

**The encoding order is not any game's ranking.** That coincidence held for 斗地主 and cost three separate defects when 挖坑 arrived — a server comment, a timeout fallback playing the player's *best* card, and the client rendering the hand strongest-first. Entries: `hoist-card-model`, `add-wakeng`, `add-web-wakeng`.

## Where the seat count keeps being wrong

"How many players are in this room" was hardcoded to two in **five** places, and the first four fixes were at the wrong layer — the room page (twice), the lobby row, the "my rooms" label, and the sidebar. The fifth hid *inside the branch added to fix it*, because **`seats.length` answers "how many are seated", not "how many seats exist"** — correct while a game is in progress, wrong for a waiting room. Entries: **`fix-lobby-seats`**, **`publish-seat-count`**, **`fix-three-seat-membership`** (where the same miscount let a seated player register as a spectator of their own game).

## Migrations

Read **`add-per-game-rating`** before touching schema. EF's generated `Down` has been wrong **four** times, always the same way: `AddColumn(defaultValue: 0)` or `""` restores plausible garbage instead of carrying the data back. Two migrations do it by hand now, one refuses via a `CHECK`-constrained scratch table whose *name is the error message*. Also: `DROP COLUMN` on SQLite is a non-atomic table rebuild; `--idempotent` is unsupported on SQLite and writes no file; explicit `.IsRequired()` outranks CLR nullability, so a type change can generate a clean migration that rejects the first row at runtime.

Expand → contract, with a **named intermediate** migration, is the shape — it is the only point at which "did the data move correctly" is observable, and 16 tests stop there.

## Theming, and the token layer

Two layers, and — checked, because the obvious guess is wrong — **they shipped on the same day** (2026-04-24, adjacent commits). `board-skins.css` got a rich vocabulary from the start: **26 tokens**, including image-valued ones (`--board-bg-image`, `--felt-edge`, `--card-face-edge`). The shell around it got **11** flat ones. So the gap was never drift — **it was designed in on day one**, which is why nobody ever noticed it as a regression. `extend-theme-tokens` pushed the board's vocabulary outward and moved components from spelling out visual values (`bg-surface rounded-card shadow-elevated`) to naming a **role** (`panel`), because a colour token cannot hold a gradient.

Before touching this, read **`extend-theme-tokens`** for three things that will otherwise cost a day: `--shadow-elevated` was a **dead token** for the theme system's whole life (Tailwind v4 inlines `@theme` values into `--tw-shadow` at build time, so `[data-theme]` never reached it); the neutral value of a decoration token is **not** guessable from its name (`transparent` and `none` were both wrong, both invisibly); and the role list must come from a **co-occurrence walk of real class attributes**, not from design-time imagination — the first guess invented two roles that did not exist and missed the most common one.

Adding a theme is still a one-file change, and that promise is checked rather than asserted: `add-qq-game-theme`'s acceptance criterion is that its diff contains **no component file at all**.
