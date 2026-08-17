## Why

`IGameRules.SupportsHumanVsHuman` is declared in the Domain, published to clients in `GameDescriptorDto`, and used to justify why 一字棋 and 中国象棋 are unrated. **Nothing on the server enforces it.**

Against the real production registry, today:

```
POST /api/rooms  { "name": "two humans", "gameKey": "xiangqi" }   → 201 Created
POST /api/rooms  { "name": "two humans", "gameKey": "tictactoe" } → 201 Created
```

Both games declare `SupportsHumanVsHuman == false`. Both rooms accept a second human via `POST /api/rooms/{id}/join` and play a full rated-ineligible match. `CreateRoomCommandValidator` checks one thing about the key — that `IGameRulesRegistry.For(key)` resolves — and `Room.Create` checks nothing.

### Why this matters beyond a stray endpoint

`add-game-capabilities` introduced the invariant `IsRated ⇒ SupportsHumanVsHuman` specifically so that "一字棋 is unrated" would stop being a judgement someone has to remember and become a consequence of a structural fact. `BuiltInGameRules.TicTacToe`'s own doc comment states the fact it rests on:

> 平台没有为一字棋提供人人对战入口(它只有 `/g/tictactoe` 这一个人机页面),于是它唯一的对手是机器人

That is true of the **web UI** and false of the **API**. The invariant is anchored to a claim the server does not uphold. The rating conclusion happens to still be correct, which is exactly what makes it easy to miss: a load-bearing premise can be false while the thing it holds up still stands.

And `game-rules-registry`'s own definition of the field is written in platform terms, not UI terms — 「平台是否提供人人对战入口」. By its own words the answer for 象棋 is currently *yes*.

### Why no test caught it

`CreateRoomGameKeyValidationTests` asserts both halves of the hole:

- Line 31 asserts `GameKeys.TicTacToe` **passes** human-room validation — the hole, asserted as correct behaviour.
- Line 39 asserts `"xiangqi"` **fails**, annotated `// 规划中,尚未登记` — false since `add-xiangqi` shipped three changes ago.

It stays green because it runs against `GomokuRules.Registry`, a test fixture hand-written as `{ Gomoku, TicTacToe }` whose doc comment claims it is 「与生产 DI 一致的注册表」. Production DI registers `BuiltInGameRules.All`, which has three entries.

**This is the same defect `add-xiangqi` already fixed once, in a second file the fix did not reach.** That change deleted a test whose comment claimed to walk the registry while its data source was a hand-written `{ Gomoku, TicTacToe }`, and created `BuiltInGameRules.All` as the single list so it could not recur. It recurred immediately, in the fixture next door, because the fixture was never pointed at the new single list.

### Why now rather than with the lobby

Lobby generalization (`/g/:gameKey/lobby`) is the next roadmap item, and it is what turns this from an API-only hole into a **button**. A generalized lobby reads `SupportsHumanVsHuman` to decide whether to render "create room" — and would be the only thing standing between a player and a 象棋 human-vs-human room. Client-side capability checks are not enforcement. Fixing the server first means the lobby's use of the flag is a display decision backed by a server rule, not a substitute for one.

## What Changes

### The create-room path enforces the capability

`CreateRoomCommandValidator` gains a second rule on `GameKey`: the resolved rules MUST have `SupportsHumanVsHuman == true`. 400, same as an unregistered key, and for the same reason the existing rule gives — the room does not exist yet, so this is a bad request, not a missing resource.

`CreateAiRoomCommandValidator` does **not** get this rule. Human-vs-AI is precisely what these games do support; blocking it there would ban 一字棋 and 象棋 from the platform entirely.

The check reads the registry, like the existing one. It is expressed as a second `IRuleBuilder` extension next to `MustBeARegisteredGameKey`, so both create paths keep sharing one definition of "what a game key must be", and the two rules compose per-path.

### The test fixture stops lying

`GomokuRules.Registry` is rebuilt from `BuiltInGameRules.All` — the same single list DI uses — instead of enumerating two entries by hand. Its doc comment's claim that it matches production DI becomes true rather than aspirational.

This is not cosmetic. It is the mechanism: every `Gewu.Application.Tests` case that resolves a game key has been running against a registry with no 象棋 in it. `GomokuRules.GomokuOnly` stays hand-written — that one is *supposed* to be a partial registry, and it says so.

### A test that cannot be fooled the same way

`SupportsHumanVsHuman` enforcement is asserted by **walking `BuiltInGameRules.All`** rather than by listing games: for every registered rule, `CreateRoomCommandValidator` accepts its key iff the rule supports human-vs-human. A fourth game gets covered by existing.

Plus a `[Fact]` asserting the walk found at least one game of each kind — a walk that silently covers nothing is the failure mode this repo has now paid for twice, and the counter-assertion is what makes the third time impossible.

### Stale spec scenarios get corrected

Three normative scenarios use `xiangqi` as the example of an *unregistered* key. After this change, `POST /api/rooms { gameKey: "xiangqi" }` returns 400 again — for an entirely different reason. **A scenario that passes for the wrong reason is worse than one that fails**, because the next reader takes it as evidence for a claim it never tested. They are rewritten against a key that is genuinely not on the platform.

## Impact

- Affected specs: `room-and-gameplay` (create-path validation, REST endpoint scenarios), `game-rules-registry` (the capability is enforced, not merely declared).
- Affected code: `Gewu.Application/Common/Validation/GameKeyValidation.cs`, `CreateRoomCommandValidator`, `tests/Gewu.Application.Tests/GomokuRules.cs`, `CreateRoomGameKeyValidationTests`.
- **No web changes.** The lobby only ever creates gomoku rooms, so no client behaviour changes.
- **No migration.** No production data exists, and dev databases would at worst hold a 一字棋 human room somebody made by hand; nothing reads the capability at play time.
- Not in scope: the `?? GameKeys.Gomoku` compatibility shim in `RoomsController` and the lobby's game-scoping. Both belong to `generalize-lobby`, and both are about *which* game the client asks for — a separate question from *what the server permits*.
