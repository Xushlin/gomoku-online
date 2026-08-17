## Why

`POST /api/rooms/ai { gameKey: "idiom-chain", humanSide: "White" }` returns **201**, and 65 seconds later the caller has **+46 ELO and a recorded win, having played nothing.**

Measured on a scratch database, not deduced:

```
rating before: 1162 | games: 2
created, waiting for the bot turn to time out...
  t+65s: status=Finished result=WhiteWin reason=TurnTimeout
rating after:  1208 | games: 4 | wins: 2
```

成语接龙 has no AI — deliberately, and the reason is on the record in `IdiomChainRules`: a dictionary lookup makes a near-unbeatable bot trivial, bot games are rated, so a ladder over a bot-playable 接龙 would rank whoever farmed the bot hardest. Nothing enforces that. The AI-room endpoint creates the room anyway, seats a bot that cannot exist, and the room enters `Playing` with a bot on move.

From there two things happen, and the second is the actual defect:

1. `AiMoveWorker` throws `RoomNotFoundException: Room '…' declares game 'idiom-chain', which has no AI.` **on every poll — every 1500 ms, for the life of the process, once per orphaned room.**
2. `TurnTimeoutWorker` reaches the bot's turn at 60 s and ends the game against the side that could not move. 成语接龙 `IsRated == true`, so ELO is awarded. The exploit is a loop: create, wait a minute, collect.

**This is the exact mirror of `enforce-human-vs-human`.** That change found `SupportsHumanVsHuman` declared, published, load-bearing, and enforced nowhere. This is the same hole on the other side of the same endpoint pair — and it is worse, because that one produced a match nobody asked for while this one pays out rating.

The sharper part is that the condition was **foreseen and then filed under the wrong failure**. `ExecuteBotMoveCommandHandler` carries this comment beside the throw:

> AI 与规则各有一份注册表,两处都可能解析不出来,且都映射成同一个 404。… 一个棋种可以先有规则(人人对战)、后有 AI。这里的失败模式相同:房间指向一个本构建不认识的棋种。

The middle sentence describes 成语接龙 precisely — rules first, AI later or never. The last sentence then declares the failure mode identical to an unknown game key. **It is not.** An unknown key means a corrupt room that should never have been written. "Rules but no AI" is a supported, intended, currently-true state of the platform, and the create endpoint manufactures rooms in it on request. Treating a reachable state as data corruption is how it ends up handled by a background worker logging forever.

Why no test caught it: `CreateRoomGameKeyValidationTests` asserts that every registry entry is accepted or refused a *human* room according to `SupportsHumanVsHuman`, and separately that AI rooms are allowed for `tictactoe` and `xiangqi`. There is no walk of the **AI** registry at all, because until 成语接龙 every registered game had an AI. **A rule that has never had a counter-example is indistinguishable from a rule nothing checks.**

This must land before `add-web-idiom-chain`. The lobby renders `<app-ai-game-card />` unconditionally, so flipping the manifest to `available` would put a "play the computer" button on the exploit.

## What Changes

### The AI registry is the authority, not a new flag

`CreateAiRoomCommandValidator` gains `MustHaveAnAi(IGameAiRegistry)`, a second `IRuleBuilder` extension beside `MustSupportHumanVsHuman`, on the **AI room path only**.

The rejected alternative is a `SupportsAi` boolean on `IGameRules`. It is rejected for the reason this repo already wrote down when it constrained `IsRated`: **a hand-maintained boolean restating a structural fact is a judgement, and judgements expire silently.** `IGameAiRegistry.For(key)` already answers this question and cannot drift from itself — register an AI for 成语接龙 tomorrow and the check flips with nothing to remember, no field to update, and no second place to forget.

It stays silent when the key resolves to no rules, same as its sibling: that case is `MustBeARegisteredGameKey`'s to report, and one field reporting one problem twice reads as two problems.

Human-room creation is untouched. 成语接龙 supports human-vs-human and that is how it is meant to be played.

### `GET /api/games` publishes `supportsAi`

`GameDescriptorDto` gains `bool SupportsAi`, projected from `IGameAiRegistry.For(gameKey) is not null` — the same registry the validator consults, so client and server cannot disagree.

This is what lets the lobby hide the card rather than render a button that 400s. It is a projection of a live registry, not a copy of a decision: the failure mode of a stale copy here would be *a visible button that always fails*, which is exactly the class of copy `add-web-per-game-rating` refused.

### The lobby stops offering a game the platform cannot play

`<app-ai-game-card />` renders only when the descriptor says `supportsAi`. `game-lobby.ts` already gates the leaderboard card on `descriptor()?.isRated` — this is the same shape, one card over.

A lobby whose every card is hidden is a real state (`supportsHumanVsHuman: false` already produces the `ai-only` notice), so the existing `unavailable` computation is extended rather than duplicated: a game with neither mode is not reachable today and is not invented here.

## Impact

- Affected specs: `ai-opponent` (the AI-room validator), `game-rules-registry` (the descriptor), `web-lobby` (the card).
- Affected code: `GameKeyValidation`, `CreateAiRoomCommandValidator`, `GameDescriptorDto` + its query handler, `GameDescriptor` model + `game-lobby.html`/`.ts` on the web.
- **No Domain changes.** Both registries already know everything this needs.
- Existing AI rooms for gomoku / 一字棋 / 象棋 are unaffected — all three have factories.
- The walking test gains its missing half: the AI path is asserted against `IGameAiRegistry` across the whole registry, with both outcomes required to occur, so the next game without an AI is covered by a test that already exists.
