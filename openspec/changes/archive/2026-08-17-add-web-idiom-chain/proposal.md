## Why

成语接龙 has rules (`add-idiom-chain`), a transport (`add-idiom-chain-transport`), a working lobby (measured in `enforce-ai-availability` — `/g/idiom-chain/lobby` already renders complete), and **no way for a person to play it.** `room-page` renders nothing at all for a boardless game: `boardSizeFor` returns `null`, both arms of its `@if` fail, and the left column is empty.

This change makes it playable and flips the manifest to `available`.

## What Changes

### A third arm, not a registry

`room-page`'s two-way `@if` carries a comment that predicted this change and pre-approved the shape:

> the match family has exactly these two, and the only remaining match game (成语接龙) has no grid at all. A registry here would trade type-safe input and output bindings for dynamic components — a real guarantee for an extension that is not coming. **If a third shape ever appears, extracting one then costs the same.**

That prediction is now checkable rather than asserted, so this change checks it: a third `@else if` is six lines and keeps both bindings typed, while a registry needs dynamic component loading and gives up compile-time checking of `(wordSay)`. The comment holds and is updated to say the third shape arrived and the reasoning survived it.

`ChainBoard` lives at `src/app/games/idiom-chain/chain-board/` — beside `XiangqiBoard` under `games/`, not under `pages/rooms/` where the shared grid `Board` sits. That asymmetry is pre-existing; this change does not widen or fix it.

### The board judges no legality, and this is the *third* different answer to the same question

`add-web-xiangqi` kept the rules off the client; `add-web-klotski` put them on it, and stated the test that reconciles them:

> The test is not *should the client know the rules*, it is *would knowing them produce a second truth that can diverge?*

成语接龙 splits under that test, which is why it needs saying explicitly. Two of its three rules — *links onto the previous word*, *not already played* — are decidable from what the client already has on screen. The third — *is in the dictionary* — needs 30,895 words the client does not and should not have.

**So the client displays and does not decide.** It shows, prominently, the character the next word must begin with, because that character is the last character of a word already rendered — reading it out is presentation, not adjudication. It does not gate the submit button on it. Three reasons, in order of weight:

1. A partly-authoritative input is worse than a non-authoritative one. If two refusals are instant and the third takes a round trip, the field behaves inconsistently for reasons the player cannot see.
2. The client's history can be stale by one ply. Blocking a word because it does not link to what this client last rendered can refuse a word that is legal on the server.
3. Refusal is now *informative*: this change gives each rule its own error code, so being told why costs one round trip and no ambiguity.

### `invalid-move` splits into three, paying off `add-idiom-chain-transport`'s deferral

That change measured the server producing precise reasons and the client receiving one code, and recorded the debt here: **"not a word" / "doesn't link" / "already said" are three different corrections, and one code says none of them.**

Three named factories on `InvalidMoveException`, following `SelfCheck`'s established precedent — `idiom-not-found`, `idiom-does-not-link`, `idiom-already-used` — plus three rows in `hubErrorToKey` and three key pairs in i18n.

`SelfCheck` also exposed a hole this change has to close before widening it: **`DomainErrorCodeTests` walks exception *types*, so `self-check` has never been covered by its own uniqueness assertion.** Factory-produced codes are invisible to it. One code slipping past a walking test is a latent problem; four is the same problem with a bigger surface. The walk now also reflects over public static factory methods returning the exception type, so a new factory is covered without anyone remembering.

### The input must not cap at four characters

Measured against the shipped dictionary rather than assumed:

| length | count |
| --- | --- |
| 4 | 29,502 |
| everything else (3, 5–13, 15) | **1,393** |

`maxlength="4"` would make 1,393 legal idioms unenterable, including 「一不做，二不休」 and 「各人自扫门前雪，莫管他家瓦上霜」. Some entries contain a full-width comma, so no character-class filtering either. The input mirrors the one cap the server actually has — `Move.Text`'s `HasMaxLength(64)` — and nothing else.

The chain list must therefore lay out a 15-character entry at 375 px without horizontal overflow, and will be checked with one on screen. `generalize-lobby` recorded why that matters: a no-overflow check passes trivially on content that is not there.

## Impact

- New specs: `web-idiom-chain`.
- Affected specs: `web-game-board` (the third renderer, the error table), `room-and-gameplay` (the new codes), `idiom-chain` (each rule names its code), `platform-catalog` (the manifest flip).
- Affected code: `GameHubService` (+`sayWord`), `room-page` + `replay-page` (third arm), new `ChainBoard`, `InvalidMoveException` + `IdiomChainRules`, `hub-error.mapper`, `DomainErrorCodeTests`, the manifest, both locale files.
- **No lobby changes.** Already done and measured.
- The chain is played over `SayWord`, which was verified over a real SignalR connection in `add-idiom-chain-transport`; this change is the first thing that calls it.
