## Context

`Room.Create` hard-codes "host = black"; that's been the seating convention since `add-domain-core` shipped. AI rooms inherit this — every human-vs-bot game opens with the human on Black. We want both seatings.

The cheapest path through the aggregate is **swap after creation**, not "create with explicit side". `Room.Create` keeps its current contract; the AI handler does the swap as a follow-up step when needed. That keeps:

- The host invariant ("host always = the room creator") intact and simple.
- All existing call sites (`CreateRoomCommand` for human-vs-human) untouched.
- The new operation tightly constrained: `SwapPlayers` is only valid in the narrow "game just started, no moves yet" window.

Sharp edges:

1. **Game.CurrentTurn after swap.** When humans-vs-bot starts, `JoinAsPlayer` transitions to Playing and `Game.CurrentTurn = Black`. After `SwapPlayers`, the *id* on `BlackPlayerId` is now the bot, but `CurrentTurn` is still Black — i.e., it's the bot's turn. Bot worker picks it up. Correct, and no extra logic.
2. **`Game.Moves.Count == 0` invariant.** The swap is only legal if no moves have been recorded. After the bot has moved once, swapping would mean the existing move's `stone='Black'` would point to the wrong player. The aggregate enforces this via the precondition check.
3. **AI worker race.** Theoretically the bot polling loop could pick up a fresh AI room and play move 1 in the same instant we're about to swap. In practice the worker polls at `MinThinkTimeMs = 800ms` and the swap happens inside the same `SaveChangesAsync` call that creates the room — same transaction, atomic. Worker can't see the room until commit. Safe.
4. **Wire format default.** A naïve C# default like `HumanSide = Stone.Black` works on the command record but the API DTO's wire schema must explicitly tolerate missing field (some clients don't know about the new field yet). Using `Stone HumanSide = Stone.Black` on the request record + `JsonSerializerOptions` defaults handles it: missing field deserialises to the C# default, which is `Stone.Empty` (the first enum member) — bug. Solution: make the field nullable on the DTO (`Stone? HumanSide`) and treat null as Black in the controller before sending the command.

## Goals / Non-Goals

**Goals:**

- Enable picking either side in the AI dialog with sensible defaults.
- Backward-compat for any client that doesn't yet know about the field (web is the only client today, but the contract should allow third parties).
- Minimal domain surface — one new method, narrow precondition.

**Non-Goals:**

- "Random" side option.
- Letting human-vs-human room creators pick their side (different concern; the host always being black is a UX assumption we don't want to disturb here).
- Allowing side swap mid-game.
- Letting the AI worker re-evaluate side mid-game (it follows whichever id is on BlackPlayerId / WhitePlayerId — swap before move 1 transparently changes which side it plays).

## Decisions

### D1. Swap after create, don't parameterise create

**Decision:** Add `Room.SwapPlayers(DateTime now)`. The handler does:

```csharp
Room.Create(...)            // host on Black, white empty
room.JoinAsPlayer(bot.Id, now);  // bot on White, status = Playing
if (HumanSide == Stone.White) room.SwapPlayers(now);
```

**Rationale:**

- `Room.Create` semantics ("host on black") stay. Only AI rooms (one call site) need a different seating.
- Swap is constrained to the post-create / pre-first-move window, so the safety net is small.
- The alternative (`Room.Create(name, hostId, hostSide, now)`) ripples to all callers and to `JoinAsPlayer` (which currently assumes white is the seat to be filled).

**Alternatives considered:**

- Parameterise `Room.Create` with `hostSide`. Rejected: bigger blast radius, more tests to revise, and 99 % of rooms (human-vs-human) don't care.

### D2. `HumanSide` not `HostSide`

**Decision:** Field name on the command/DTO is `HumanSide`, not `HostSide`.

**Rationale:**

- The user mental model is "I want to play White against the AI", not "the host plays White".
- `HostSide` would invite questions ("can the host be the bot?"). The validator already rejects bot-as-host; `HumanSide` keeps the language unambiguous.

### D3. Wire format: nullable `Stone? HumanSide`, default Black at controller

**Decision:** API DTO field is `Stone? HumanSide` (nullable). When deserialiser sees missing/null, it stays null. Controller normalises to `Stone.Black` before constructing the command record.

**Rationale:**

- Avoids the `Stone.Empty` (first enum value) trap that would happen with a non-nullable default on a record.
- Keeps the contract explicit: "absent = legacy default", "Black" or "White" = explicit choice.

### D4. Web dialog: Black / White button group, default Black

**Decision:** Mirror the existing Easy / Medium / Hard difficulty group exactly. Two buttons (Black / White), `role="radiogroup"`, default value `Black`.

**Rationale:**

- The dialog already has a button group convention. Two more buttons sit naturally.

### D5. Web service: optional third arg

**Decision:** `createAiRoom(name, difficulty, humanSide?: BotSide)`. When omitted, body has 2 fields (current shape). When provided, body has 3.

**Rationale:**

- Allows existing test stubs that only pass `(name, difficulty)` to keep working.
- The dialog always passes the third arg from now on, but unit tests of the service don't have to.

## Risks / Trade-offs

- **Risk: bot worker race during swap.** Mitigated by atomicity of the same-transaction create + join + swap path (D2 from Context).
- **Risk: a future change wants to swap players mid-game (e.g., handicap rebalancing).** SwapPlayers' invariant rejects that. If it ever becomes legitimate, lift the invariant with a separate change.
- **Risk: third-party clients that don't update their DTO get the old default forever.** Acceptable — the field's semantics ("default Black if absent") is exactly that.
- **Trade-off: dialog grows by one button group.** Accepted; AI dialog already has difficulty + name input, two more buttons fit.

## Migration Plan

- Net-additive at every layer. No schema migration. No existing test or call site changes meaning *unless* it asserts on the absent-field shape; that's allowed to keep working since the field is optional.
- Rollback = revert. Existing AI rooms unaffected (creation path only).
