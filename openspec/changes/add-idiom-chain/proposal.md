## Why

成语接龙 is the platform's sixth game and the first match game that is **not played on a board**. `generalize-match-payload` opened the two seams it needs — a move can be a text, a game can have no dimensions — and left the game itself for here.

It is also the first game that gives the match kernel a genuine human-vs-human population. 一字棋 and 象棋 declare `SupportsHumanVsHuman == false`; gomoku has been the only ladder since the beginning.

## What Changes

### The rules

A move is one idiom. It is legal when all three hold:

1. the dictionary contains it;
2. its **first character** equals the **last character** of the previous idiom (any idiom is legal as the opening move);
3. nobody has played it earlier in this game.

The game never ends by rule. `Apply` returns `Ongoing` for every legal move — a chain has no terminal position. It ends the way the kernel already ends games it cannot decide: the player who cannot answer runs out of turn time, and `Room.TimeOutCurrentTurn` awards the win. **This is the first game whose rules never return a result**, and it costs nothing: the kernel already had two non-rule endings (resign, timeout) and `MoveApplication` was deliberately built without an `EndReason` for exactly that reason.

Two rules are deliberately *not* implemented, and both are decisions rather than omissions:

- **同音不算接上.** Matching by pronunciation (说 *shuō* → 硕 *shuò*) is the common house rule and it doubles the branching factor, which makes the game much easier and much harder to adjudicate — 多音字 mean one idiom can have several "last sounds", and the client cannot see any of them. Character identity is checkable by both sides from the text alone. Recorded as a rule choice; reversing it later is a change to one comparison.
- **No AI.** A dictionary lookup would make a bot trivial to write and nearly unbeatable, and this is the one game the platform added *because* it needs human opponents. Its absence is also what keeps the rating honest: with no bot to farm, `IsRated` rests on a real opponent pool.

`IsRated` is therefore `true`, and unlike the previous three games that is a **judgement**, so it needs its reason on the record: the game has a genuine human opponent pool and its outcomes track vocabulary, which is a skill. The invariant `IsRated ⇒ SupportsHumanVsHuman` permits it; the invariant never *required* it.

### The dictionary reaches the rules synchronously

`add-idiom-dictionary` already built the port for this game. `IIdiomRepository`'s first method says so in its own doc comment:

> 按成语原文精确查找 —— **成语接龙**用它判断"这是不是一条真成语"。

That method is implemented, tested, and has **no production caller**. It also cannot be the one this game uses: it is `async`, and `IGameRules.Apply` is synchronous, lives in the Domain, and is called from inside an aggregate method. Making `Apply` async to serve one game would make gomoku and 象棋 pay for a need they do not have, and would push a database round-trip inside `Room.PlayMove`.

So the rules take a Domain port, `IIdiomLexicon`, with one synchronous member: `Contains(word)`. Infrastructure implements it by loading the curated words into a `FrozenSet<string>` once — 30,895 strings, a few MB, O(1) lookups, and no I/O on the move path. The async repository keeps its other callers and its other methods; nothing is deleted, because 成语纵横's generator and a future 猜成语 still want the rich rows.

**The port was designed for the right game and the wrong call path.** That is worth writing down rather than quietly routing around: a port built for a consumer that does not exist yet is a prediction, and this one got the consumer right and the shape wrong.

### The single registry list becomes a function of what it needs

`BuiltInGameRules.All` is a static list of parameterless instances. 成语接龙's rules need a lexicon, which cannot be a static field without loading a dictionary during type initialisation.

The temptation is to register this game separately in DI and leave `All` alone. **That would be the same defect this repo has now fixed twice** — a hand-maintained list that a walking test believes is the registry. Both `IsRated ⇒ SupportsHumanVsHuman` and `enforce-human-vs-human`'s validator walk `All`, and a game outside it slips past both in silence.

So `All` becomes `All(IIdiomLexicon)`. Every caller must now supply what the platform needs to describe itself, which is the honest shape; tests pass a small in-memory lexicon.

## Impact

- Affected specs: `game-rules-registry` (the registry list takes its dependencies; a rules set may have no board), `idiom-dictionary` (the synchronous lexicon port alongside the async repository), plus a new `idiom-chain` capability.
- Affected code: `Gewu.Domain/Games/IdiomChain/`, `Gewu.Domain/Idioms/IIdiomLexicon.cs`, `BuiltInGameRules`, `Gewu.Infrastructure` (lexicon implementation + DI), and the test fixtures that build registries.
- **No migration.** The idioms are already in the database.
- **No UI and no hub path.** A textual move has no transport yet — `GomokuHub` exposes `MakeMove` and `MovePiece`, both positional. That is `add-web-idiom-chain`'s job, exactly as 象棋 was unreachable after `add-xiangqi` until its AI and UI landed.
- Acceptance criterion, inherited from `add-xiangqi` and `add-klotski`: **the match aggregate is not touched.** `git diff --name-only` contains no `Rooms/Room.cs`, `Rooms/Game.cs`, `Rooms/Move.cs`, or `ValueObjects/MoveIntent.cs`.
