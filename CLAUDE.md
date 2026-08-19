# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this project is

**格物 / Gewu** — a multi-platform online game hall. Planned games: idiom games (成语纵横 / 成语接龙 / 猜成语), 五子棋, 一字棋, 中国象棋, 华容道, 俄罗斯方块. 「格」 means grid cell, which is what they all have in common.

Seven games ship today, and between them they establish the three kernels every later game reuses:

- **五子棋 (gomoku)** — the *match* kernel: players register, create/join rooms, play real-time matches (via SignalR) with room chat, spectator chat, and urge-opponent shortcuts; ELO-based ranking with special icons for the top three; human-vs-AI with multiple difficulties; game-record storage and replay.
- **成语纵横 (idiom-crossword)** — the *puzzle* kernel: a level catalogue, server-authoritative attempts (the answer key never leaves the server), server-counted mistakes and hints, star scoring, and per-level best records.
- **华容道 (klotski)** — the puzzle kernel's **proof**, and the one that showed its authority model has two shapes. 成语纵横 is authoritative because it *withholds* the answer; 华容道 hides nothing and is authoritative because it *replays* every claimed move. Playable at `/g/klotski`.
- **一字棋 (tictactoe)** — the match kernel's **proof**, not an extension of it. Its entire rule set is `NInARowRules("tictactoe", 3, 3, 3)`; it contributed zero lines of win detection. Human-vs-AI only, and therefore **unrated**: with no human-vs-human mode its only opponents are bots, bot games are rated, so a ladder over it would rank Easy-bot grinding rather than skill. That is now enforced by the invariant `IsRated ⇒ SupportsHumanVsHuman` rather than left to a comment. See the `add-tictactoe` audit for what adding the game revealed about the registry.
- **俄罗斯方块 (tetris)** — the *score-attack* kernel, and the platform's **third authority model**. 成语纵横 *withholds* the answer; 华容道 *replays* every move; 俄罗斯方块 replays **placements, not keystrokes** — `(rotation, column)` per piece, no timing anywhere. It is also the only game whose client owns the entire rule set, and the only one with no registry behind it: score-attack has one game, and *a switch with one arm is a switch*. Playable at `/g/tetris`, with periodic ladders at `/g/tetris/scores`.
- **中国象棋 (xiangqi)** — the match kernel's **first genuinely different game**, and since `enable-xiangqi-human-play` a full human-vs-human rated game with spectators. Its move is `from → to` rather than a placement, its board is 10×9 with pieces on intersections, and `Stone.Black` is 红. 一字棋 could not prove any of the kernel's seams general, because it is gomoku in miniature; 象棋 could, and did — at each of the three layers the assumption had leaked into (rules, AI, board component). It shipped human-vs-AI-only and unrated for a structural reason — no human-vs-human mode — and both halves are now false: the deferral its own doc comment recorded («大厅泛化之后翻它») came due.

Games fall into three categories that deliberately do **not** share one aggregate — see the platform roadmap below:

| Category | Games | Realtime | Core concepts |
| --- | --- | --- | --- |
| Turn-based adversarial | 五子棋, 一字棋, 中国象棋, 成语接龙, 斗地主 | SignalR | room, N seats, turn order, move sequence, ELO, spectators, replay |
| ↳ what 斗地主 adds | — | — | **hidden per-seat state**, a **server-only setup** (the deal), **three** seats, and a settlement in points rather than ELO |
| Single-player levels | 成语纵横, 华容道, 猜成语 | none (REST) | level catalogue, progress, stars, hints, time leaderboard |
| Single-player score-attack | 俄罗斯方块 | none (submit at end) | run record, score validation, periodic leaderboard |

## Current phase

**Seven games ship**: 五子棋 (the original), 成语纵横 (the first puzzle game), 一字棋 (the change that priced what a second board game costs), 中国象棋 (the change that proved which match seams were actually general), 华容道 (the one that did the same to the puzzle kernel), 成语接龙 (the one with no board at all), and 俄罗斯方块 (the only one whose client owns the whole rule set). Detail:

- [x] 4-layer Clean Architecture solution skeleton (`backend/Gewu.slnx`)
- [x] OpenSpec initialized (`openspec/config.yaml`); each shipped change is archived under `openspec/changes/archive/<date>-<name>/`
- [x] **Backend MVP** — auth, rooms, gameplay, AI (Easy / Medium / Hard, with side-picker), ELO, replay, presence, observability, rate limiting. Live specs in `openspec/specs/`.
- [x] **Web client v1** (`frontend-web/`) — Angular 21, Tailwind v4, Material/CDK, Transloco (`zh-CN` + `en`). Auth pages, lobby, real-time game board, replay player, public profiles, find-player search, AI room creation with side-picker, sound effects (Wood + Chiptune + Minimal packs), board skins (Wood + Classic), themes (Material + System + Ink) × dark/light, presence dots.
- [x] GitHub Actions CI runs on every push and PR (`backend` + `web` jobs in parallel).
- [x] **`add-platform-catalog`** — `GameManifest` registry (`src/app/games/index.ts`) + the `/games` catalogue page. Adding a game to the catalogue = one manifest entry + two i18n keys.
- [x] **Idiom vertical** — `add-idiom-dictionary` (30,895 curated idioms committed at `backend/data/idioms.curated.json`), `add-puzzle-core` (`PuzzleLevel` / `PuzzleAttempt` / `PuzzleLevelProgress` + the `IPuzzleRules` registry, server-authoritative, answer key never leaves the server), `add-idiom-crossword` + `add-web-idiom-crossword` (成语纵横, 12 generated levels, playable at `/g/idiom-crossword`).
- [x] **`add-game-rules-registry`** — `IGameRules` / `IGameRulesRegistry` / `NInARowRules(key, rows, cols, winLength)`; `Board` is now rows×cols rather than square; `Room` carries a `GameKey`. Deliberately Domain-only at the time.
- [x] **`add-tictactoe`** + **`add-web-tictactoe-ai`** — 一字棋, playable at `/g/tictactoe`. The rules cost **zero lines**; everything else was registry debt the previous change had left above the Domain layer, and paying it cost about as much as the game itself (~310 lines vs ~332). Read `openspec/changes/archive/2026-08-14-add-tictactoe/tasks.md` §7 before starting 中国象棋 — it is the priced list of what a new board game actually costs. Also added: an AI registry (`IGameAiFactory` / `IGameAiRegistry`), `GameKey` on the three room DTOs, and a board whose dimensions are inputs rather than a constant.

- [x] **`add-game-capabilities`** — `IGameRules.SupportsHumanVsHuman` plus the invariant `IsRated ⇒ SupportsHumanVsHuman`, enforced in the `NInARowRules` constructor **and** by a test that walks the registry.

  The roadmap used to say "`add-per-game-rating` must delete `IGameRules.IsRated`". That was wrong, and the reason matters more than the correction. `IsRated` had two justifications: it protected the single shared rating pool, and 一字棋 ratings are meaningless. Per-game rating removes the first but not the second — 一字棋 has no human-vs-human mode, its only opponents are bots, and bot games *are* rated (the `ai-opponent` D7 anti-arbitrage rule), so a 一字棋 ladder would rank whoever farmed the Easy bot most. Splitting the pool does not fix that. The deeper problem was the field's *shape*: a hand-maintained boolean meaning "should this game be rated" is a **judgement**, and judgements expire silently. Constraining it by an invariant against a structural fact removes the need to remember. **A TODO in a comment is not a mechanism.**

- [x] **`add-per-game-rating`** — `UserGameStats(UserId, GameKey, …)` is now the single source of truth for ELO and win/loss records; `User` no longer carries `Rating` / `GamesPlayed` / `Wins` / `Losses` / `Draws` and keeps **no mirror field** (a mirror is a second source of truth whose drift shows up as the leaderboard and the profile page disagreeing, with nothing to catch it). The three ladders (ELO / puzzle time+stars / score) still stay separate on purpose — this change unifies nothing.

  Shipped as **expand → contract**, and that split is the reusable lesson. The roadmap used to claim the change "cannot be made smaller" because dropping the columns forces every reader to change in one commit. That was wrong: `AddUserGameStats` (create table + backfill, **columns untouched**) is pure addition, keeps the tree green, and lands the riskiest part — the migration — where it can be reviewed and tested alone. `DropUserRatingColumns` follows once the readers have moved. Ordering is guaranteed by migration timestamps, so **a future migration squash must not reorder them.**

  EF's generated `Down` for the drop was wrong in the same way `AddRoomGameKey`'s `defaultValue: ""` was: `AddColumn(defaultValue: 0)` would restore everyone at rating 0 while the real data sat unread in `UserGameStats`. It now carries the data back by hand, with a test — because nobody walks the rollback path until they need it. Read the archived `tasks.md` §8 before touching schema again; it also records that `DROP COLUMN` on SQLite is a non-atomic table rebuild.

- [x] **`add-web-per-game-rating`** — every rated game gets its own ladder at `/g/:gameKey/leaderboard`, and the profile page gained a game switcher. Backed by a new read-only `GET /api/games`, a projection of `IGameRulesRegistry` (which grew an `All` member to make projecting possible).

  The choice worth remembering is *why the endpoint exists* rather than a `rated: boolean` on the front-end `GameManifest`. The manifest already holds one deliberate copy of server data — the board dimensions — and that one is tolerable because a mismatch shows up as a visibly wrong number of cells and the server rejects out-of-range moves. A `rated` copy has neither safety net: its failure mode is *a permanently empty ladder*, which looks exactly like a new game nobody has played. **Whether a copy is acceptable depends not on how small it is but on whether being wrong would ever be noticed.** One catalogue test (tic-tac-toe must have no ladder link) is the executable form of that argument; if it disappears, the copy has crept back.

  Also settled here: `gamesPlayed === 0` renders "no games yet" rather than the server's initial `1200`, because rendering the payload verbatim reads as *a beginner who has played*. When 中国象棋 ships that will be true of nearly every user.

- [x] **`generalize-match-domain`** — the enabling refactor for 中国象棋. `Room.PlayMove` no longer knows how a board works: it checks room state, player identity, and turn, then hands everything else to `IGameRules.Apply(history, intent, side)`. Bounds, occupancy, and move legality all belong to the rules now. `Move` gained nullable `FromRow`/`FromCol`, so a move is `(from?) → to`. `WinLength`/`CreateBoard` moved down to `INInARowRules`. `GameEndReason.Connected5` → `Decided` (underlying value still `0`, so no data migration).

  Two roadmap items turned out to be **wrong on inspection, not merely stale**. It said "two seats + JSON move payloads": 象棋 needs neither. `BlackPlayerId`/`WhitePlayerId` *are* two seats — 红/黑 is a display-layer reading, not a structural change. And every xiangqi move is exactly `from → to` (no castling, en passant, or promotion), so two nullable columns cover both game families while staying queryable, EF-mapped, and strongly typed at replay. **JSON would have been paying for a requirement that does not exist and that 象棋 would not have created.**

  The reusable lesson is about testing, not design. Plans called for two optional parameters on the SignalR hub's `MakeMove`. **SignalR does not apply C# optional-parameter defaults** — a 3-arg invocation against a 5-param method is rejected outright, which would have broken every published client on its next move. Domain, Application, and Api unit tests all passed; none of them cross SignalR's argument binding. `AiSmoke` caught it, because it does not know the refactor exists. That is the whole argument for keeping an end-to-end smoke that speaks the real transport.

- [x] **`add-xiangqi`** — 中国象棋的**规则**. `XiangqiRules` owns its own piece model and 10×9 board entirely inside the rules; the aggregate never sees them.

  **`generalize-match-domain`'s acceptance criterion held: `Room`/`Game`/`Move` were not touched at all.** `XiangqiThroughRoomTests` proves it by playing real xiangqi through the real aggregate. The change is a new `Games/Xiangqi/` directory, two registration lines, and one DI line.

  In this game **`Stone.Black` is 红**. `Game` starts on `Stone.Black` and xiangqi is red-first, so reading Black as red costs zero Domain change — `Stone` has always meant "first mover / second mover", and 红/黑 is how the display paints it. That is the bet `generalize-match-domain` placed, now collected.

  Two implementation choices worth keeping: flying-generals is **not** a special case (it is exactly "the enemy general can capture down that file", so it folds into the same check test as self-check); and **stalemate is a loss**, unlike chess — the one rule most likely to be miscopied from a western-chess implementation.

  It also removed a **fake** test. `AllBuiltInRules()` claimed in its own comment to walk the registry so that "adding 中国象棋 is automatically covered" — but its data source was a hand-written `{ Gomoku, TicTacToe }`, so xiangqi would have slipped past the `IsRated ⇒ SupportsHumanVsHuman` invariant in silence. That is precisely the failure the comment predicted, about a mechanism the comment got wrong. `BuiltInGameRules.All` is now the single list, feeding both DI and the test.

- [x] **`add-xiangqi-ai`** — depth-limited alpha-beta, three difficulties, and 象棋's first actual opponent. It also had to free the **AI seam** from the same placement-shaped assumption the rules seam had.

  `IBoardGameAi.SelectMove(Board, Stone) → Position` took an n-in-a-row-specific `Board` and returned a bare placement. Its own comment claimed "it never used anything gomoku-specific" — **that was false**, and it was written when `add-tictactoe` renamed `IGomokuAi`. 一字棋 cannot prove that interface general, because it is also a placement game on a `Board`. **Renaming an interface does not generalize it, and neither does adding a second game from the same family.** The seam now mirrors the rules: `SelectMove(history, myStone) → MoveIntent`.

  The five existing AI implementations were **not touched**. They moved to a narrow `IPlacementAi` behind a `PlacementAiAdapter`, because behind them sits the exhaustive tic-tac-toe verification (every reachable position asserted to land on the game-theoretic value) — rewriting that to change a signature trades a proven thing for mechanical-change risk. Two test files needed one-line adaptations; the tasks entry claiming "zero changes to existing AI tests" was overstated and is corrected there.

  Capture-first move ordering is **not an optional optimization**: alpha-beta's pruning depends entirely on order, and it took Hard from ~1.7s to ~750ms per move. 750ms sits inside `AiMoveWorker`'s existing 800ms minimum think time — that is the number that matters, not the test runtime.

  Nothing claims any difficulty is unbeatable. 象棋 cannot be searched exhaustively, so that assertion is unverifiable here, and **an unverifiable claim is worse than none**. What is asserted instead: legality across 12 plies of self-play, taking a hanging piece, and — the sharpest one — that the opening has exactly **44** legal moves, a number that can be checked independently.

- [x] **`add-web-xiangqi`** — 象棋 is playable at `/g/xiangqi`. Backend: **zero changes**.

  The front end had the same placement-shaped assumption the Domain and the AI seam had already shed, and for the same reason it went unnoticed: gomoku and 一字棋 are one family, so the shared `Board` component had never been asked by another. Two things followed.

  **The client must hold the opening setup.** A gomoku board *is* its move history — every ply places a stone of a known colour. A 象棋 ply is `from → to`, which says nothing about where anything started, so the board can only be derived from a known initial position. That copy is accepted on the repo's existing test: being wrong paints the whole board wrong on move zero, the most visible failure there is, and the server rejects any move that only looks legal on a wrong board. The rejected alternative — have the server send a board projection — would put back into the match kernel exactly what `generalize-match-domain` took out, and would be pure cost for gomoku.

  **The board judges no legality.** It does only what needs no rules: you can pick up only your own piece, and it is read-only off-turn. A TypeScript port of the rules would be a second source of truth whose divergence reads to the player as a bug and which nothing here would detect. The price is real — you discover illegality by being refused — and it is the cheaper price.

  Two defects were found **in the browser, not by reading the code**, and both are the kind that unit tests structurally cannot see:

  1. Illegal moves surfaced as "Something went wrong. Please try again." `hubErrorToKey`'s keyword table was written for gomoku, where `invalid-move` is near-unreachable because you can only click an empty cell. In 象棋 refusal is the *normal* channel through which a player learns the rules, so the generic fallback reads as a broken app. Fixed, plus a dedicated message for self-check / flying-generals — "that move is not legal" does not tell a player what they missed.
  2. Piece colours were `var(--xq-red, #b3261e)` with **nothing defining the variable** — a literal wearing a token's clothes, inert under every skin and both colour modes. Now `pieces: { bg, red, black }` is part of `BoardSkinTokens`, so a new skin *cannot* omit them (a test fixture failed to compile the moment the field was added — that is the mechanism working). The constraint recorded on the token: a skin picks the **shade**, never the **hue**. A xiangqi board whose red side is not red is broken in every theme.

- [x] **`generalize-puzzle-rules`** — the enabling refactor for 华容道, and the **fourth** time this repo has met the same mistake: an interface shaped by its only implementation, believed general because a second implementation from the same family never contradicted it.

  puzzle-core's spec claimed the seam was proven — 「新增一个游戏 MUST 只需要一个实现 + 一处注册」 — and had a test for it. But that test registered a **fake shaped like 成语纵横**, and a fake cannot contradict the assumption it was written under. Two things were inexpressible: `Validate(solution, submission)` had no layout (the crossword's answer is *positional* and self-describing; 华容道's is a *path*, checkable only against its starting point), and `Score(hints, mistakes, duration)` could not see the moves — even though the spec text itself already said 「华容道计步数」. **Foreseen, written down, and then not provided for.**

  Every method now takes both halves of the level plus its own payload, and `Score` takes a `PuzzleScoreInput`. Passing the submission to scoring is **not** a hole in 「不采信客户端自述」: *a number the client gives you is untrustworthy; a number the server had to reconstruct before it would accept anything is a server-observed fact.* `Validate` replays every move before `Score` ever sees it. That sentence is in the spec, along with the limit it implies — implementations must not read any field of the submission that replay did not confirm.

- [x] **`add-klotski`** — 华容道's rules, solver and levels. Backend only; no UI yet.

  **Its authority comes from re-execution, not from withholding.** 成语纵横 keeps the answer server-side so a client cannot fake a solve. 华容道 hides nothing — pieces, board, exit and sliding rules are all public and all on the client, because a client that could not judge a slide could not animate one. The server instead replays every claimed move from the level's own start and refuses to score what it cannot reproduce. Same platform rule, opposite mechanism. `SolutionJson` therefore holds `{ minMoves }` and nothing else.

  **`minMoves` is computed, never quoted.** A\* with an admissible heuristic, run offline by the generator and re-derived by a test. The classic 横刀立马 comes out at **116** single-cell moves; that it matches the published single-cell figure (the familiar "81" counts a straight run as one move) is an *after-the-fact corroboration, not the source*. Order matters — same discipline as `add-xiangqi-ai` refusing to call any difficulty unbeatable.

  Hints are **searched from the player's reported position**, not read off a stored path: three moves off that path, a stored suggestion is neither optimal nor necessarily legal. The HTTP smoke followed nothing but hints and reached the exit in exactly `minMoves` moves — end-to-end proof that each hint really was on a shortest path.

  `Mistakes` is structurally always 0 here (nothing increments it unless a game calls `check`, and this one never needs to), so the formula ignores it — the first use of the platform rule `generalize-puzzle-rules` added for exactly this.

  **The inherited acceptance criterion held: `git diff --name-only` contains no puzzle-core file.** This time a real second game checked it, not a fake modelled on the first.

- [x] **`add-web-klotski`** — 华容道 is playable at `/g/klotski`. Backend: **zero changes**.

  Its board judges its own slides, which is the **opposite** of what `add-web-xiangqi` decided one game earlier — and both are right for the same reason. 象棋's move rules live only on the server, so a TypeScript port would *create* a second source of truth that could silently disagree. 华容道's rule already has to live on the client — "a block slides into an adjacent empty cell" is what drawing a drag requires — so there is nothing to create, and the server replays the whole path anyway.

  > The test is not *should the client know the rules*, it is *would knowing them produce a second truth that can diverge*.

  So no per-move `check`, legal destinations highlighted, and one submission at the end. The level's minimum stays hidden until the puzzle is solved: it is the divisor the server scores with, and on screen during play it turns a puzzle into a countdown.

  It also found the same leak one layer up from the one `generalize-puzzle-rules` fixed: `PuzzleApiService.parseLayout` returned `CrosswordLayout`, `PuzzleHint.revealed` was a `CrosswordRevealedCell`, `PuzzleCheckResult.solved` a `CrosswordSolvedWord` — one game's shape on the *platform's* client, holding only because one game used it. Now generic.

- [x] **`remove-manifest-board`** — `GameManifest.board` is gone; `boardSizeFor` reads the descriptor `GET /api/games` already returns.

  The copy had been tolerated on this repo's own test — *whether a copy is acceptable depends not on how small it is but on whether being wrong would ever be noticed*. `add-web-xiangqi` quietly voided that: it added `{ rows: 10, cols: 9 }` for a game whose board component hardcodes its own 10×9, so **nothing read it**, and a wrong value there would have been noticed by nobody. The field survived only because a test demanded every playable match game declare one. The field and that invariant died together.

  The one real cost is that the size is now asynchronous, so both pages hold their skeleton until `capabilities.loaded()`. `DEFAULT_BOARD` narrowed with it: it is for *a game key this client has never heard of*, **not** for *the descriptor has not arrived* — conflating those is how you end up painting a fallback over a size the client is about to learn.

  A note on evidence, kept in the archived tasks: the gate is proven by a unit test with a `loaded() === false` stub. The browser run only *corroborates* it — sampling started after page load, so it shows no flash at observable resolution rather than proving the first frame. **"I did not see it" and "it does not happen" are not the same claim.**

- [x] **`add-hub-error-codes`** — domain errors now carry a stable kebab-case `Code`, a hub filter turns them into `HubException(code)`, and the client maps codes instead of prose.

  The deferred note called `hubErrorToKey`'s prose matching "fragile". It was worse: **it did not work outside Development.** A hub method throwing a plain exception only has its message delivered when `EnableDetailedErrors` is on, and that is `IsDevelopment()`. Measured — same illegal 象棋 move, same build, same database, only the environment variable changed:

  | | before | after |
  | --- | --- | --- |
  | Development | *That move isn't allowed.* | *That move isn't allowed.* |
  | **Production** | ***Something went wrong.*** | ***That move isn't allowed.*** |

  So the message-quality fix `add-web-xiangqi` shipped was switched off in the only environment that will ever have players. Nothing is deployed, which is the only reason it had not bitten.

  The code lives on a `DomainException` base rather than in an Api-layer lookup table, because a table would have been the **third** place enumerating these exceptions — and a table is a list someone must remember to extend, while a constructor parameter is one the compiler demands.

  **A mechanism claim of mine was wrong here, and being wrong had a cost.** The proposal said `HubException` messages arrive verbatim; SignalR wraps them (`…on the server. HubException: invalid-move`, byte-identical in both environments). The first mapper compared the whole string, so the server was already sending codes while the UI still showed the generic message — a fix that looked done and was not. It was caught by reading the wire frame over long-polling instead of guessing again. *A mechanism description that sounds right and one that has been measured differ only when it matters.*

- [x] **`enforce-human-vs-human`** — `IGameRules.SupportsHumanVsHuman` was declared in the Domain, published to clients in `GET /api/games`, used to justify why 一字棋 and 象棋 are unrated, and **enforced nowhere**. Measured, not deduced: `POST /api/rooms { gameKey: "xiangqi" }` returned **201**, a second human joined with **200**, and the result was a live `status: "Playing"` 象棋 match between two real accounts — in a game whose descriptor, in the same API, says `supportsHumanVsHuman: false`.

  The damage is not the stray endpoint. `add-game-capabilities` built the invariant `IsRated ⇒ SupportsHumanVsHuman` precisely so that "一字棋 is unrated" would stop being a judgement and become a consequence of a **structural fact** — "its only opponents are bots". That fact was true of the web UI and false of the API. The rating conclusion still held, which is exactly what made it invisible: **a load-bearing premise can be false while the thing it holds up still stands.** An unenforced structural fact is just another judgement, and judgements expire silently — the whole reason that invariant exists.

  Why no test caught it is the sharper half. `CreateRoomGameKeyValidationTests` asserted **both** halves of the hole: that `tictactoe` passes human-room validation (the hole, asserted as correct), and that `"xiangqi"` fails as `// 规划中,尚未登记` (false since `add-xiangqi`). It stayed green because it ran against `GomokuRules.Registry`, a fixture hand-written as `{ Gomoku, TicTacToe }` whose comment claimed it was 「与生产 DI 一致」. So **every** `Gewu.Application.Tests` case resolving a game key ran in a world with no 象棋 in it.

  That is the same defect `add-xiangqi` already fixed once — it deleted a test whose comment claimed to walk the registry while its data source was hand-written, and created `BuiltInGameRules.All` so it could not recur. **It recurred immediately, in the fixture next door, because the fix never pointed that fixture at the new single list.** Building the mechanism is not the same as adopting it. The fixture now derives from `All`, and a test asserts the two key sets are equal — that one guards the future, the derivation only fixed the present.

  Enforcement is a second `IRuleBuilder` extension on the human-room path only; human-vs-AI is what these games *do* support, and blocking it there would exile them. It stays silent when the key resolves to nothing, so an unregistered key still reports one error rather than two. Create-time only: blocking `JoinRoom` too would add a registry dependency to serve a population of zero.

- [x] **`require-room-game-key`** — `gameKey` is now mandatory on `POST /api/rooms`, `POST /api/rooms/ai` and `GET /api/rooms`. Zero behaviour change; the point is *where the decision is written down*.

  `RoomsController` filled a missing key with `?? GameKeys.Gomoku`, justified in `CreateRoomRequest`'s own doc comment as compatibility for 「已发布的客户端」 — **published clients, of which there are zero.** The web app had never sent the field to those endpoints, not as a choice but because it was never plumbed. So the shim was not a compatibility layer; it was a hardcoded game living on the server, where no reader of the client could find it — and it is why the lobby stayed gomoku-only without anyone deciding that it should.

  The same decision had been recorded three different ways along one page's data path: an invisible server default (`list`), a half-visible optional argument (`createAiRoom`), and an explicit literal with a comment (the leaderboard slice). Only the third could be found by reading the front end. All three are now the same shape, and `games/gomoku/game-key.ts` exists at last — **五子棋 was the one game whose key had never been written down on the client side**, because the server kept supplying it.

  Three sibling defaults were checked and each **stays, for a different reason** — being able to say a different reason for each is the test that a rule was applied rather than a pattern matched. `HumanSide` defaults because choosing a side completes an incomplete request *within* the named game, whereas defaulting the key changes which game you are playing. The leaderboard defaults because a ladder always renders under a visible game name, so a wrong one is wrong *on screen*. And the profile default **is genuinely used**: `getProfile(userId, gameKey?)` omits it on first paint, where omission is a meaningful value ("the server's default game") rather than a forgotten argument — removing it would have broken that render. That one is the counter-example to this whole change's thesis, and it was found by checking rather than by assuming the pattern generalised.

- [x] **`generalize-lobby`** — `/home` is the platform home; `/g/:gameKey/lobby` is one game's lobby. Adding a game's lobby is now zero lobby code. **Backend: zero changes.**

  The roadmap called this blocked for four changes on "rewriting `/home` as a normative path in five web specs". That was mostly wrong on inspection: of the nine specs naming `/home`, most only assert it is the landing page and the brand link's target, both still true. Two of the four real blockers were statements this change exists to delete — `platform-catalog`'s 「`/home` 仍是…五子棋大厅」 and `web-leaderboard`'s 「`/home` 的排行榜卡片 MUST NOT 改动」, the latter with its duplicate-entry side effect logged as temporary. **An obstacle written down four changes ago is a claim, not a measurement.**

  The split line was not invented — it is which endpoint each card calls. Three take a game key (active rooms, play-vs-AI, ladder) and go to the game lobby; four do not (hero, my active rooms, my recent games, find player) and stay on `/home`. `myActiveRooms` is deliberately cross-game, which is why that endpoint never took a key.

  Two services, one engine: `HomeDataService` and `LobbyDataService` hold different slice sets and share one `SliceEngine`. Parameterising a single four-slice service instead would have left `/home` polling `/api/rooms` every 15s for a card it no longer renders — **a slice nobody renders but everybody pays for is a defect that only ever appears in the network panel.**

  The bundle prediction was checked rather than asserted: **537.05 → 500.35 kB** raw (137.44 → 130.82 transfer). The long-standing over-budget warning is not gone, but went from 37 kB over to **350 bytes** over.

  Two things worth keeping. **CDK dialogs get the root injector by default**, so `LOBBY_GAME_KEY` was invisible to them until both openers passed `injector: this.injector` — the kind of wiring a unit test catches only once you have thought to write it. And `app.routes.spec`'s "every manifest has a route" assertion compared route strings, which a parameterised route fails; left alone it would have pushed the next game towards a literal route just to keep the test quiet.

  Found **in the browser, and only with data on screen**: at 375 px the room row overflowed 8 px, because Angular's default `preserveWhitespaces: false` strips the whitespace between adjacent inline spans and leaves the line with no break opportunity. Pre-existing, untouched markup — invisible because *every previous 375 px check ran against an empty room list*. **A "no horizontal scroll" check passes trivially on an empty list.**

- [x] **`lobby-return-target`** — leaving a game returns to that game, not to `/home`. `gameEntryRoute(catalog, gameKey)` reads the manifest's `launchRoute`, falling back to `/home`.

  The rule came out simpler than planned. Because `generalize-lobby` set gomoku's `launchRoute` to its lobby, **the manifest already answers the question** — no `supportsHumanVsHuman` branch, and no loading gate, because the catalogue is a static import while `GameCapabilitiesService` is not. Games with no lobby land on their own AI page, which is where you start another one.

  Two things I had wrong, both corrected by looking rather than reasoning. The roadmap said **five** call sites; it is **three** — the other two fire when the room could not be loaded, so there is no game key to read and `/home` is the only honest answer. And the 404 navigation is not on the initial-load path at all: initial load renders the not-found panel, and only `rehydrate()` (reconnect, room gone) navigates. The first test aimed at the wrong path and failed; **it was right to fail.**

  The replay page was in scope and is not: its only exit link lives in the 404 branch and was already correct. The alternative to dropping it was inventing a button nobody asked for so a spec I had written would come true.

- [x] **`generalize-match-payload`** — the third enabling refactor, and the one that collects a bet `generalize-match-domain` placed in writing. That change rejected a JSON move column with a reason and a trigger: *象棋 would not have created the requirement… **真出现不规则走子时再加列***. 成语接龙 creates it — its move is an idiom, not a square. **A deferral that names its own trigger is the good kind.**

  Two assumptions came out, not one. `MoveIntent.To` was non-nullable, so every move had to land somewhere; and `Rows`/`Cols` sat on the **base** `IGameRules`, so every game had to claim a board. Storing `(0,0)` for an idiom was never an option — the kernel's own rule, in bold on `MoveIntent`, forbids using a legal value to mean "not applicable". Returning `0,0` for the board was worse than untidy: the descriptor publishes those numbers and the web treats `rows <= 0` as *unknown*, substituting 15×15, so a word game would have been described to every client as a gomoku grid.

  So a move is now positional **or** textual, exactly one, enforced in the constructor and shared by all three carriers; and `IBoardGameRules` holds the dimensions, which `INInARowRules` extends and a chain game will not implement. `boardSizeFor` gained a third case — **no board** is not the same answer as **unknown key**, and neither is the same as *the descriptor has not arrived*.

  **The migration is where the interesting failures were, both of them silent.** First: changing the CLR type to `int?` produced a migration that only added the `Text` column, because `MoveConfiguration` still said `.IsRequired()` — **explicit configuration outranks CLR nullability**, so the type change compiled, the migration generated cleanly, and the database would have rejected the first textual move at runtime. Second: EF's generated `Down` wrote `defaultValue: 0` and dropped `Text`, turning every idiom into a move at square (0,0) with its content gone — the same defect `add-per-game-rating`'s `Down` was fixed for. It now refuses via a `CHECK`-constrained scratch table whose **name is the error message**, with a test asserting both rollback paths.

  What this does **not** do is add the game. The seam is shaped against a rule set that is written down and not yet implemented — exactly how `generalize-match-domain` was shaped against 象棋 and `generalize-puzzle-rules` against 华容道. Both held; neither was *proven* until the game landed. `add-idiom-chain` is the only thing that can check this one.

- [x] **`add-idiom-chain`** — 成语接龙's rules. Backend only; no hub path and no UI yet, the same shape `add-xiangqi` shipped in.

  **The inherited acceptance criterion held: the match aggregate was not touched.** `IdiomChainThroughRoomTests` plays a real chain through the real `Room`, and it proves more than 象棋's equivalent did — 象棋 showed a *slide* payload could cross the aggregate; this shows a game with **no board, no coordinates, and rules that never decide a winner** crosses the same one. `MoveApplication` was built without an `EndReason` on the argument that "how it ended" has three kinds and rules are only one; this is the first game where rules are *never* the one.

  **The registry list becoming a function paid off immediately.** 成语接龙's rules need a dictionary, so `BuiltInGameRules.All` could not stay a static list. The tempting alternative — register this game separately in DI — is the defect this repo has fixed twice: a hand-written list a walking test believes is the registry. Making it `All(lexicon)` instead meant the `IsRated ⇒ SupportsHumanVsHuman` walk and the create-room capability walk covered the new game **without one assertion changing**.

  A port was corrected here rather than routed around. `add-idiom-dictionary` built `IIdiomRepository.FindByWordAsync` *for this game* — its doc comment says so — and it is implemented, tested, and still has no production caller. It also cannot be the one this game uses: it is `async`, while `IGameRules.Apply` is synchronous, in the Domain, and called from inside an aggregate method. **The port picked the right consumer and the wrong call path.** A synchronous `IIdiomLexicon` now sits beside it; nothing was deleted.

  Two rule decisions, both recorded with reasons rather than left implicit. **同音不算接上** — matching by sound doubles the branching factor and moves adjudication to something the client cannot see (多音字 give one idiom several "last sounds"), while a character is checkable by both sides from the text alone. And **no AI** — a dictionary lookup makes a near-unbeatable bot trivial, bot games are rated, so a ladder would rank bot-farming; `IsRated` stands precisely because there is no bot to farm. That makes `IsRated` this game's one genuine *judgement*, so its reason is on the record: a real human opponent pool, and outcomes that track vocabulary.

  Verified live, not deduced: `GET /api/games` reports `idiom-chain` with `rows: null, cols: null` — the boardless branch `generalize-match-payload` opened, reached by a real game for the first time — and `POST /api/rooms { gameKey: "idiom-chain" }` returns **201** while `xiangqi` still returns 400. Also checked that the lexicon holds **30,895** words: an empty dictionary would have made every step above look identical and rejected every idiom.

- [x] **`add-idiom-chain-transport`** — 成语接龙 is now *playable*, with no UI: `SayWord(Guid roomId, string word)` on the hub, `string? Text` on `MakeMoveCommand`, `Row`/`Col` narrowed to `int?`.

  **A third hub method rather than a fourth parameter**, and this change re-measured the reason instead of inheriting it. `generalize-match-domain` recorded `InvalidDataException: Invocation provides N argument(s) but target expects M` after `AiSmoke` hit it; quoting a measurement and taking one differ only when the answer has changed, so it was taken again — and it returned something the original note did not have: **an extra argument is refused too** (`provides 3 … expects 2`). So adding a parameter to a live hub method breaks in *both* directions — old clients cannot send fewer, and new clients cannot send more ahead of the server. Three methods is not caution, it is the only shape that rolls forward.

  Verified over two real long-polling connections against a **Production** host — `EnableDetailedErrors` is off there, which is the environment `add-hub-error-codes` existed to fix. A four-ply chain succeeded; an unlinked word, a non-idiom, and a `MakeMove(7,7)` aimed at a boardless game each came back `HubException: invalid-move`; gomoku's `MakeMove` was untouched and its AI answered. In one `Moves` table, from one run: four rows with `Text` set and **all four coordinates `NULL`**, beside two gomoku rows with `Row=7, Col=7` and `Text=NULL`. `generalize-match-payload`'s migration had never before been exercised by both payload kinds at once.

  **The command is a third encoding of "positional or textual", and that is a trade, not an oversight.** Carrying a `MoveIntent` on the command would delete the encoding — but `Position`'s constructor rejects negative coordinates, so building the intent in the hub would move that rejection out of `MakeMoveCommandValidator`, turning a documented **400 with a named field** into a throw before the command exists. `web-game-board` and `add-hub-error-codes` both pin that path. Changing it is defensible; doing it silently inside a feature change is not. A test pins the resulting division of labour: the validator deliberately does **not** check "exactly one payload" — that invariant has exactly one home, `MoveIntent`'s constructor, and re-implementing it in the validator would make a fourth copy.

  Two things found by running it that no unit test sees. Argument-count refusals happen in SignalR's binding layer, **before** `DomainErrorHubFilter` — so they carry no error code, and **under this repo's shipped logging config they are recorded nowhere**: `appsettings.json` pins `Microsoft.AspNetCore` to `Warning`, and SignalR logs a binding failure below that. The precise claim is that nothing was written at or above the configured level, not that the framework emits nothing — but the configured level is what an operator would actually have. Domain refusals from the same method *do* surface (`[ERR] Failed to invoke hub method 'SayWord'`), which is what makes the gap easy to miss. Pre-existing, and it means a client/server signature mismatch is invisible from both ends at once. And the server's own refusal messages are precise (「'风和日丽' must start with '止'」, 「is not an idiom in the dictionary」) while the client receives a single `invalid-move`. 象棋 can live with that — a player looks at the board and works it out. 接龙 cannot: **"not a word" / "doesn't link" / "already said" are three different corrections**, and one code says none of them. That is a granularity requirement 成语接龙 creates, so it belongs to the change that renders it.

- [x] **`enforce-ai-availability`** — `POST /api/rooms/ai { gameKey: "idiom-chain", humanSide: White }` returned **201**, and 65 seconds later the caller had **+46 ELO and a recorded win, having played nothing.** Measured, not deduced; repeatable in a loop.

  成语接龙 has no AI on purpose — a dictionary lookup makes a near-unbeatable bot trivial, bot games are rated, so a bot-playable chain would rank whoever farmed it. Nothing enforced that. The endpoint seated a bot that could not exist, `AiMoveWorker` threw `RoomNotFoundException … has no AI` **every 1500 ms forever**, and `TurnTimeoutWorker` then handed the human a rated win against it.

  **This is `enforce-human-vs-human` on the other side of the same endpoint pair, and worse — that one produced a match nobody asked for; this one pays out rating.** The condition was *foreseen*: `ExecuteBotMoveCommandHandler`'s comment says 「一个棋种可以先有规则(人人对战)、后有 AI」 and then declares the failure identical to "the room points at a game this build does not know". It is not. An unknown key is a corrupt room; **rules-without-AI is a supported, currently-true platform state that the create endpoint manufactures on request.** Filing a reachable state under data corruption is how it ends up handled by a worker logging into the void.

  The check reads `IGameAiRegistry`, not a new `SupportsAi` on `IGameRules` — same reasoning that constrained `IsRated`: a hand-maintained boolean restating a structural fact is a judgement, and judgements expire silently. Register an AI tomorrow and the validator flips with nothing to remember. `GET /api/games` publishes `supportsAi` from the *same* registry, so the button the client draws and the request the server accepts cannot disagree; a test walks every key asserting exactly that.

  **The same fixture defect recurred a third time, seven lines below where it was last fixed.** `GomokuRules.AiRegistry` was hand-written as `{ Gomoku, TicTacToe }` under the comment 「与生产 DI 一致」 while production has registered three since `add-xiangqi-ai` — so all of `Gewu.Application.Tests` ran in a world with no xiangqi AI. `add-xiangqi` built `BuiltInGameRules.All` to end this; `enforce-human-vs-human` adopted it for the rules fixture and **did not look at the AI fixture next door**. This time it nearly cost more than a stale test: the new walking assertion, written against that fixture, would have been a green test asserting xiangqi has no AI. `BuiltInGameAis.All` now feeds DI and the fixture, with a test pinning the two key sets equal.

  The `web-lobby` spec had deferred the card-gating with a named trigger — 「留到第一个"有人人对战、但没有 AI"的棋种出现那天」 — and **the deferral was right while its risk assessment was wrong**: it reasoned entirely about the card, and `POST /api/rooms/ai` never looked at whether a lobby existed. A conclusion true of the web UI and false of the API, exactly as before.

  Measured on the way past, outside this change's scope: **`/g/idiom-chain/lobby` already renders completely** — title, room list, create-room, ladder — while the manifest still says `planned`. The lobby seam's second consumer costs **zero lobby code**.

- [x] **`add-web-idiom-chain`** — 成语接龙 is playable at `/g/idiom-chain/lobby`. **Six games ship.** Verified by playing a real four-ply game, two plies typed into the actual input box, against a real opponent on a second SignalR connection.

  **The board seam's third and final shape, and the two-way `@if`'s own prediction came true.** That comment said a registry would trade typed bindings for dynamic components and that "if a third shape ever appears, extracting one then costs the same". It does: one `@else if`, six lines, both bindings still type-checked. The conclusion is unchanged and is now measured rather than forecast.

  **`invalid-move` split into three codes**, paying the debt `add-idiom-chain-transport` logged. 象棋 can share one code because the player sees the board and works it out; 接龙 has no board, and its client deliberately judges nothing, so the server's refusal is the *only* channel through which a player learns the rules — and "not a word" / "doesn't link" / "already said" are three different corrections. Verified live: `idiom-does-not-link`, `idiom-not-found`, and `idiom-already-used` all arrive distinct. The third needed a genuine two-cycle from the dictionary (`一五一十 → 十不当一 → 一五一十`) so the repeat also *links*; a casually-built history tests the linking rule instead.

  **The board judges no legality, and that is this repo's third different answer to the same question.** `add-web-klotski` set the test — not *should the client know the rules* but *would knowing them produce a second truth that can diverge* — and 成语接龙 **splits** under it: two of three rules are decidable from what is on screen, the third needs 30,895 words the client should not carry. So it displays the required first character (that character is already rendered; reading it out is presentation) and gates nothing. A partly-authoritative input is worse than a non-authoritative one, and a stale-by-one-ply history would refuse legal words.

  **The input must not cap at four characters** — measured, not assumed: 29,502 idioms are four characters and **1,393 are not**, running 3 to 15, some containing a full-width comma. `maxlength` mirrors the one cap the server has (`Move.Text`'s 64) and nothing else. The 375 px check was then run with the dictionary's longest entry actually on screen (`overflow: 0`, row 310 = 310) — at ply zero the same check reads 0 and proves nothing, which is `generalize-lobby`'s lesson in its exact original form.

  **Splitting the codes immediately caught a test whose name lied.** `A_word_already_played_is_refused_even_though_it_links_on` used a history where the word did *not* link, so it was passing on the wrong rule — invisible while both rules threw one undifferentiated `InvalidMoveException`.

  Two things found that are **not** defects but change what browser evidence is worth. `StubHub` is a bare class, not `implements GameHubService`, so adding a hub method leaves it silently incomplete — `satisfies` cannot fix it without typing all twelve members, so the mechanism holding it is "every hub method has a test that calls it". And **when the Browser pane is not displayed the page produces no frames, so zoneless `requestAnimationFrame`-scheduled change detection does not run**: I read a stale `disabled` attribute and a stale input value and briefly took both for bugs. Conclusions about DOM-attribute *timing* from a non-compositing pane are worthless; the authority there is a unit test calling `detectChanges()`. *"I did not see it" and "it does not happen" differ in both directions.*

- [x] **`enable-xiangqi-human-play`** — 象棋 is a full human-vs-human rated game. **Its own doc comment predicted this change and named the trigger** — 「大厅泛化之后翻它,而计不计分是那时一个独立的、需要理由的决定」 — the third self-triggering deferral in this repo and the cleanest, because it also said what would still need deciding.

  The two flags have **different natures, so different justifications**. `SupportsHumanVsHuman` is a *deduction*: `enforce-human-vs-human` defined the field by behaviour, so once `POST /api/rooms` accepts a game the entry exists and the declaration must follow. `IsRated` is a *judgement*, and it is the one that comment reserved — 象棋's only reason for being unrated was "no opponent pool, so a ladder measures nothing", and this change destroys exactly that reason. 一字棋 deliberately does **not** follow, and it is now the sole `false` sample keeping several "both outcomes must occur" registry walks from degenerating into one-sided no-ops.

  **Spectators and spectator commentary were already kernel features** — two channels, asymmetric visibility, `/spectate`, notifier subgroups, the lobby's Watch button. Nothing was built here. What this change added is the *assertion* that they hold for every human-play game, including a source-level one: `JoinAsSpectator` / `LeaveAsSpectator` / `PostChatMessage` must not mention `GameKey`. Mutation-checked — inserting one branch turns it red.

  **The number of places that had pinned "象棋 has no human play" as correct was seven, not the four the proposal found.** Three lived in front-end specs and surfaced as red lights, not as search hits. *Counting by grep and counting by failing test are not the same count.*

  A comment of mine was falsified by its own mutation check: I wrote that before this change only gomoku had human play, so a "walk covers >1 game" assertion guarded the flip. Flipping 象棋 back left it green — 成语接龙 also has human play, so 象棋 was the third, not the second. That assertion guards against a degenerate walk and **cannot** guard against 象棋 being reverted; a second test names 象棋 explicitly, because a walk only covers what is in the set.

  **This change was archived 36 commits late, and the gap is the lesson.** The code merged in PR #54; the change directory rode along in the same PR and the archive step was simply skipped. For 36 commits the live spec tree said 「象棋今天**不计分**,因为它还没有对手」 with `SupportsHumanVsHuman == false` / `IsRated == false`, while `XiangqiRules` had both `true` — *the exact shape this change existed to destroy*, wearing its own conclusion. Nothing reported it; it surfaced from listing `openspec/changes/` while answering "what is left". And **`openspec validate --specs --strict` passed 37/37 both before and after**, so that number validates spec *shape*, never spec *truth* — it must stop being quoted as evidence the specs match the code.

  I predicted the late archive would need hand-merging, because `room-and-gameplay` and `in-room-chat` had moved 4 and 5 commits since #54 and a MODIFIED requirement replaces wholesale. Measured per requirement: **all four MODIFIED bodies were byte-identical to their state at #54** — those commits touched *other* requirements in the same files, so it was a clean apply. **Counting by file and counting by requirement are not the same count** — the same mistake this change already recorded as "counting by grep and counting by failing test are not the same count", in a new unit.

  **The verification found a pre-existing authorization defect and did not fix it.** `in-room-chat` says the Spectator channel is visible to spectators only. Writing is enforced; *reading* leaks on two of three paths — `GET /api/rooms/{id}` returns every message regardless of caller, and the `RoomState` broadcast pushes one DTO to the whole room group. Measured both, not inferred. **The one path that is correct (`ChatMessage` events, routed per channel) is precisely why the other two went unnoticed**: `ChatPanel` hides the spectator tab from players, so the screen looks right while the data sits in their client. Present since `in-room-chat`; 象棋 did not make it reachable, only visible, because reproducing it needs human-vs-human *and* spectators at once. Fixed separately as a pure spec-compliance bug — see the roadmap.

- [x] **`fix-spectator-chat-leak`** — the leak `enable-xiangqi-human-play` measured, closed on all three paths. Pure spec-compliance fix, no proposal.

  `in-room-chat` said the Spectator channel is spectators-only; the write side enforced it and **all three read sides did not**. The fix is three mechanisms, each chosen so the next person cannot forget it:

  - `ToState` takes a **required** `RoomView`. No default value — a default makes "forgot to say" and "deliberately gave everything" identical in the source, which is the shape the defect lived in. The compiler then listed all nine call sites, and three of them turned out to be projecting a snapshot used only for the broadcast: those lines deleted themselves.
  - `IRoomNotifier.RoomStateChangedAsync` takes the **aggregate, not a DTO**, and projects both views itself. Handing each handler the job of projecting twice is handing each handler a chance to forget.
  - `JoinRoom` derives the subgroup from the aggregate via `GetRoomRoleQuery`. `JoinSpectatorGroup` validates and is a silent no-op for non-spectators — "I am not a spectator yet" is not an error.

  **I got the grouping wrong first, in a way only the exhaustiveness question exposes.** I split on *player* vs spectator, which left a connection that had entered the room but not yet clicked Watch in *neither* subgroup — it would have received no `RoomState` at all. Groups must be **mutually exclusive and exhaustive**; the group is therefore `non-spectators`, and `RoomView.For` keys off `IsSpectator`, not `!IsPlayer`. That one change fixed both the missing broadcast and a REST/broadcast inconsistency at once.

  Verified by re-running the three probes that found it: players now see 1 message (room channel) where spectators see 3; a player calling `JoinSpectatorGroup` gets a silent no-op and 0 live spectator messages; and a real move produces **1 broadcast frame with 0 spectator messages for the player, 1 frame with 1 for the spectator**. That last pairing matters — the first attempt reported "0 frames, 0 messages", and **zero frames is not zero leakage, it is no measurement.** Mutation-checked: removing the trim turns three tests red. Frontend: zero changes.

  **The spectator's own screen was then verified separately, because the above proves only the server half.** Logged into the browser as a spectator of a live 象棋 game: two chat tabs where a player gets one, all 90 board points disabled, the other spectator's comment rendered, a comment posted *from the UI* landing in the database, and a third comment from the other spectator **appearing live without a reload**. Layout checked at 375 px with a 400-character unbroken string in the panel — nothing overflows, `overflow-wrap: break-word` holds. No defect found; the gap was in what had been looked at, not in the code. Worth keeping the shape of the gap, though: after the fix I had measured all three server paths and described the feature as verified, while the half the *user asked for* — spectators commenting, on screen — had only ever been exercised through the API.

Not yet done — platform roadmap:

1. **`add-tetris` — shipped.** `ScoreRun` + migration, `POST /api/score-runs`, `POST /api/score-runs/{id}/submit`, `GET /api/score-runs/leaderboard`, all verified over real HTTP. `add-web-tetris` followed and 俄罗斯方块 is now playable at `/g/tetris`. Kept here because the reasoning below is the record for the whole score-attack category.

   **The roadmap's own split was questioned and rejected.** It said `add-score-attack-core` first, then the game. `add-puzzle-core` did exactly that: it built `IPuzzleRules` with one implementation and a **fake shaped like 成语纵横** to "prove" the seam general — and a fake cannot contradict the assumption it was written under. 华容道 arrived and both `Validate` and `Score` had to change. The score category has **one** game planned, so building a registry now is the same bet at worse odds. `ScoreRun` is named generically because it is *data*, not a seam: a wrong data shape costs one column, a wrong seam costs every implementation. Two spec scenarios pin the absence of a registry.

   **Score-attack needed a third authority model, and the interesting part is which one was rejected.** 成语纵横 *withholds* the answer; 华容道 *replays* every move; 俄罗斯方块 replays **placements, not keystrokes**. Replaying keystrokes would require two simulations agreeing frame-for-frame on gravity, lock delay, soft-drop rate and the level curve — and one frame of disagreement rejects a *legitimate* game, which the player reads as being called a cheat. Placements involve no timing at all, each one is decidable, and the granularity matches 华容道's "one slide", which a real game has already proven. The limit is written into the spec rather than papered over: **replay guarantees the score matches the placements, not that a human produced them** — an offline solver can compute a good line from the server's seed, and `add-xiangqi-ai`'s rule applies (an unverifiable claim is worse than none).

   The piece sequence deliberately avoids `System.Random`: **its algorithm has changed between .NET versions**, and this sequence must be identical across runtimes *and* languages (the client is TypeScript). A generator that silently re-scores every historical run on a runtime upgrade is worse than no generator.

   **The Application layer had two consumers of one fact, and that is how it kept its promise of having no registry.** Starting a run asks "is this key playable"; submitting asks "how is this replayed". Two callers, one fact — and splitting it into two judgements is exactly the shape of `enforce-ai-availability`, where `POST /api/rooms/ai` accepted 成语接龙, the worker threw `has no AI` every 1500 ms forever, and the timeout worker then paid out **+46 ELO**. That fix made the validator read `IGameAiRegistry` rather than add a hand-maintained boolean; this is the same discipline at minimum size. `ScoreAttackGames.IsScoreAttackGame` and `Replay` recognise the same key, and one test walks five keys asserting the two verdicts are **equal one by one**. It is still not a registry — *a switch with one arm is a switch*.

   **The client's numbers are not "remembered to be ignored" — they have nowhere to go.** `SubmitScoreRunCommand` has no score / lines / level / duration field and `StartScoreRunCommand` has no seed field, so the assertion is reflective: the commands' public members must not intersect that set of names. A behavioural test would only prove *today's* handler ignores the field; **a field that does not exist has no tomorrow.** Measured over the wire, a submission carrying `score: 999999, lines: 999, level: 99, durationMs: 1, finishedAt: 2030-01-01` came back `score: 300, lines: 3, level: 1, durationMs: 979`.

   **The generator's "two implementations" got checked by two implementations for the first time.** The spec had claimed this was the one thing allowed a second copy *because* a test could align it item by item — but until now there was only one copy, so the claim itself was unverified. A Python implementation of xorshift32 + seven-bag + field + greedy (what the client will have to write) agrees with C# on the **first 21 pieces — three whole bags — for seed 20260818**, and end-to-end it is stronger: from the server's own seed it predicted 300 points across 46 placements, and the server's replay produced 300. One mismatched piece would almost certainly change the score and probably make a placement illegal.

   **A mechanism reason I wrote down was falsified by measuring it.** The leaderboard validator's comment said ASP.NET's enum binding accepts numbers, so `?window=99` would bind to `(ScoreWindow)99` and fall into `StartOf`'s catch-all as `all`. Measured on the same build: `0/1/2` → **200** (numbers *are* accepted), `3` / `-1` / `fortnight` / empty → **400 from the model binder**, names case-insensitive. So the binder validates against *defined* values and `99` never reaches `StartOf` — that `IsInEnum` rule is unreachable from this endpoint. The fix was not to delete it but to move the real defence: `StartOf` now **throws** on an undefined window instead of having a catch-all that silently means `all`. A catch-all there would return all history for a typo — the worst kind of success — and it bets correctness on callers always validating. `IsInEnum` stays with its reason rewritten: it guards the *query object*, which any caller can construct, and its only job is to make that failure a 400 with a field name.

   Also settled: the leaderboard shows **one row per player** (their best run in the window), a requirement the spec had omitted — score-attack inherently invites replaying, so "one row per run" fails by *necessity*, not by accident. The dedup is a correlated subquery rather than `GroupBy(...).Select(g => g.OrderBy(...).First())`, because the latter is not guaranteed to translate on SQLite and **silently degrading to client-side evaluation still returns the right answer** while moving filtering and paging into the process — which is why it is tested against real SQLite. And the 100 000-placement cap is a *resource* limit, not a score cap (scores are deliberately uncapped): 100 000 placements at 2 s each is 55 hours of continuous play.

   **Two self-corrections there, both forced by mutation testing, and the first one is the instructive half.** The scoring tests were pure constant arithmetic that never called the implementation — replacing the level factor with `1` left all 32 green. I had even written a justification for why it *couldn't* be tested ("constructing a four-line clear needs a solver"), and that justification was also wrong: verifying level scaling needs one line cleared at level 2, not four at once. **A reason for why something cannot be tested needs checking too.** The fix was to extract a public `ScoreForClear(cleared, linesBefore)` — which belonged in public anyway, since the formula is part of the external contract and the client displays per-clear scores. The second: the placement scaffolding had its own bug twice (out-of-bounds, then stack overflow), and both times the red test was the *scaffolding*, not the rules.

- [x] **`add-web-tetris`** — 俄罗斯方块 is playable at `/g/tetris`. **Seven games ship.** Verified by actually playing two runs in a browser and submitting them; the second scored 100 and the ladder shows it. Backend: **zero changes**.

  **The server's replay model dictated a game rule, and that is the whole design.** `TetrisField.LandingRow` drops a piece straight down its column; the server never learns how the player got there. So a piece **tucked under an overhang** is at a position no straight drop reaches — the server replays it two rows higher, or refuses the run. The symptom would be *the whole game rejected at the very last moment*, which is precisely the failure `add-tetris` rejected keystroke-replay to avoid. Pairing a tuck-permitting client with a straight-drop server just swaps timing divergence for geometric divergence.

  So the client maintains the invariant "this piece could have fallen straight down its column", and refuses any move that breaks it. The cost is on screen in one sentence of copy; what it buys is that **every recorded placement is replayable by construction** rather than by after-the-fact checking. Two alternatives were rejected: letting the client submit a row too (then the server no longer decides where pieces land, and "is this cell free" is nearly always true), and dropping gravity for a placement puzzle (trivially replayable, but the falling *is* the game). It also settles lock delay — there is none, because the argument for a grace period is "let the player slide it in at the last moment", and that slide is the one thing forbidden.

  **The running score is a preview; the recorded one is the server's**, and the result panel shows the server's. The guard is not "assert they agree" (that needs a server) but a client-only invariant: *the live score must equal replaying the placements the client itself wrote down*. That catches a wrong formula **and** a recorded placement that does not match where the piece landed — and the second is invisible on screen, because the screen draws the actual landing. Measured against the real backend: client `Score100 Lines1 Level1`, server `100 points, 1 line, level 1`.

  **Three self-corrections, and the first two matter more than the feature.**

  1. **My reachability tests did not test reachability.** Deleting the check outright left all 34 green — those games simply never attempted a tuck. Random play covers that rule only by luck. The fix is a deterministic case: seed 20260818 opens with S, whose hard drop leaves exactly one overhang, and the next piece is walked to the adjacent column and asked to move in. It carries a **positive control** too, or an implementation that refused everything would pass.
  2. **A piece could *spawn* into an unreachable position** — a real defect, found by that invariant and not by reading the code. L/J/T/S/Z have rotations whose top row is not full, so once the stack reaches the ceiling the piece fits at row 0 while a filled cell sits *above* one of its lower cells. Only reachable in the last plies of a losing game, i.e. where nobody looks. `spawn` now ends the run instead.
  3. **A mechanism comment of mine was falsified again.** I wrote that every `>>> 0` in the xorshift port was load-bearing; removing the first one left 34 green, because `<<`/`^`/`>>>` all work on the same 32 bits and only the *final* coercion feeds `%`. Removing that one turns 6 red. Comment rewritten to the true reason.

  **And one defect only a browser could find: `NG0201: No provider found for ScoreRunsApiService`.** The abstract class is the DI token; the implementation carried `providedIn: 'root'`, which registers the *implementation*; `app.config.ts` was missing the `{ provide, useClass }` line. **All 563 unit tests passed**, because a component spec supplies its own stub for exactly the service under test. The symptom was `/g/tetris` silently refusing to render. The fix includes `app.config.spec.ts`, which derives the token list with `import.meta.glob` rather than hand-writing it — and that spec was itself wrong twice: injecting the globbed classes failed *all seven* tokens (the glob's `.ts` module ids resolve to a second copy of each class, so a different DI token — names survive that, identities do not), and booting the whole `appConfig` left an unhandled rejection from the i18n initializer, so the suite reported **573 passing tests and exit code 1**.

  The score ladder is its own page at `/g/:gameKey/scores`, not the ELO one: different rows, different endpoint, and **two sets of columns are two components**. The catalogue gates its link on `category === 'score'` rather than a server flag — not a relapse into client-side copies, because `category` is declared in the manifest, already drives the grouping, and there is no server flag to read (tetris has no `IGameRules`, so `GET /api/games` never describes it).

  Also worth keeping: the 375 px check was run **with 44 blocks on the field** (`generalize-lobby`'s lesson — an empty field passes trivially), and no wall kicks, because a kick that helped would land the piece exactly where the server cannot reproduce it.

- [x] **`add-game-sounds`** — 俄罗斯方块 makes sound, and 象棋's captures stop sounding like quiet moves. Backend: **zero changes**.

  **The 象棋 half of the request had already shipped, and the interesting part is why nobody could tell.** Its move sound comes from `RoomPage`, which fires on `moves.length` changing and never asks which game it is — so `add-web-xiangqi` inherited落子/胜/负/平/催促 for free. What was missing was narrower and worse: **a capture and a quiet slide were the same sound**, in the one game where「他动了一步」and「他吃了我的車」are different news. The client already knew which: `positionAfter(moves)` is how it draws the board, so "was the destination occupied" is a fact it reads every frame. `lastMoveCaptured` just asks it. One branch on `isXiangqi()`, not a registry — *a switch with one arm is a switch*.

  **A mechanism this repo believed in was measured and found absent — in a comment that contradicted itself.** `sound.tokens.ts` said adding an event "requires editing this union (TS exhaustiveness then forces every registered pack to render it — **or fall through silently**)". Only the second half was true: a sixth member with all three packs untouched compiled at **exit 0**, because `play` returns `void` and a missing `case` just runs off the end. `web-sound` even had a Scenario asserting「**WHEN** TS 编译期」. That was survivable while five voices never changed; this change adds 4 events × 3 packs, where a forgotten arm reads as *"tetris makes no noise under the chiptune pack"* — findable only by a human auditioning each pack.

  Two derived mechanisms replaced it. `SOUND_EVENTS` is now the array and `SoundEventName` is derived from it, so the runtime list a walking test uses cannot fall behind the union; and every pack's `switch` ends in `default: return unhandledSoundEvent(event)` with `event: never`, so a missing arm **fails to compile naming the event**. Mutation-checked in both directions: before, exit 0; after, three errors at `wood.ts(54,36)`, `chiptune.ts(68,36)`, `minimal.ts(63,36)`.

  **The positive control is the part worth keeping.** The first probe ran `tsc -p tsconfig.json` — which is `"files": []` plus `references`, so `--listFilesOnly` counts **zero files**. The probe "passed" and so did the control; only the control's *passing* exposed that nothing was being compiled. *A tool that verifies a mechanism can itself be measuring nothing.*

  Tetris reuses two events and adds none of its own for them: a lock is `move-place` (it *is* "a move landed") and a top-out is `game-lose` (a score-attack run only ever ends by topping out, and the descending sting is exactly that). What genuinely had no analogue is a line clear — and a four-row clear gets its **own** event, because `LINE_SCORES`' 100-vs-800 gap "is the whole 'save up for a tetris' decision" and a sound that ignores it contradicts the scoreboard. The rule that decided the whole set: **sound reports what happened, not what you pressed** — no keypress makes a noise. One sound per gravity step by precedence `over > level-up > quad > clear > lock`, with level-up outranking a quad because it *changes the game* (gravity speeds up) while a quad is a reward already on screen.

  The engine was not touched. It is a pure state machine, so the component observes two `{locks, lines, level, over}` snapshots — `RoomPage`'s `previousMoveCount` pattern — and a pure `soundForStep` decides. That is also what makes a real four-row clear or level-up assertable at all, since reaching either through the UI takes a solver or ten cleared rows.

  **Three empty shells were closed on the way, and one of them was a real absence:** `wood.ts` and `chiptune.ts` had **no tests at all** (only `minimal` did, over a hand-written `ALL_EVENTS` — a fourth copy of the list this repo has fixed three times); `BUILT_IN_PACKS` now feeds DI *and* the walking spec, the `BuiltInGameRules.All` pattern's third application; and `room-page.spec.ts`'s `SoundService` stub — an untyped object literal, `useValue` being `any` — had been **missing `volume` and `setVolume` since the volume slider shipped**. `stubSoundService()` now `extends SoundService`. I first wrote that the sound feature had never had a behavioural test; that is wrong — `header.spec.ts` asserts the pack-switch audition, twice. The true claim is narrower and still the point: **no sound belonging to a game event had ever been asserted**, so nothing anywhere pinned 落子 / 胜 / 负 / 平 / 催促.

  Writing chiptune I made exactly the mistake the new "no two events build the same graph" assertion exists for: its `line-clear` was `game-win`'s notes played twice as fast. Distinguishable to a fingerprint, near-identical to an ear.

  **Two more copies of the pack list survived this change and were fixed straight after** — see `fix-spec-web-shell-pack-count`. `web-shell` enumerated the sound packs twice: a Scenario titled 「列出**全部**已注册 pack」 whose assertion read "lists `wood` and `chiptune`, two items", and an i18n requirement listing `label` / `wood` / `chiptune` with no `minimal`. Both wrong since the third pack shipped; the code was right both times. The instructive part is why they stayed wrong: **that Scenario had never been implemented** — no test had ever counted the menu items — and `minimal`'s translation key existed only because `i18n-parity.spec.ts` named it in *another* hand-written list. Two hand-written lists guarding one fact means the fourth pack is guarded by neither. Both are derived now, and both mutation-checked.

  **Browser evidence, and its limit.** Same room, same pack, same AudioContext, sole variable the destination: a capture (红炮 taking 黑马, `aria-label` confirming the horse became a cannon) builds `buf 1 / filter 1 / osc 1`; a soldier stepping to an empty point builds `buf 1 / filter 1 / osc 0`. On `/g/tetris`: starting a run creates **zero** AudioContexts, a lock builds wood's `move-place` graph under a real context in state `running`, a top-out builds a different one, and `ctxs` never exceeds 1. **A line clear could not be reached in the browser** — the pane does not composite, so zoneless change detection does not run synchronously and every DOM read after a keypress is stale, which is precisely what a play-agent needs; three driver attempts stacked 132 blocks 18 rows deep with *every row one cell short*. That claim rests on the unit test that drives a real clear through the component with a shadow game and a positive control. **"I did not see it" and "it does not happen" differ in both directions.**

- [x] **`generalize-match-seats`** + **`add-room-seats`** — the match kernel stopped assuming two players. Two changes, expand-then-contract, because 斗地主 needs three seats.

  `Game.CurrentTurn` was a `Stone` and rotation was `stone == Black ? White : Black` — **that one line was the entire two-player assumption**. It is now `(seat + 1) % rules.SeatCount`, `Move.Stone` became `Move.Seat`, and `Stone` sank into the board family behind `BoardSeats` (the same move `add-xiangqi` made for 红/黑: the name stays, its meaning drops to the layer where it is true). A source-level assertion — comment-blind, because the first version went red on my own explanatory prose — pins that `Stone` appears in no file under `Domain/Rooms/`.

  Then `Room`'s two player columns became a `RoomSeats` collection. `BlackPlayerId` / `WhitePlayerId` survive as **derived readers** over seat 0 and 1 — not a mirror, because there is only one store — and 87 call sites did not change. `JoinAsPlayer(userId, now, rules)` starts the game when `_seats.Count == rules.SeatCount`, so a three-seat room stays `Waiting` after the second player sits.

  **`const int 0` implicitly converts to any C# enum, and `static readonly int` does not.** The first version of `BoardSeats` used `const`, and `FirstSeat` silently compiled to `Stone.Empty` at ten sites; only two failed at runtime. Making the constants `static readonly` turned all ten into compile errors. Only `0` has that privilege — `SecondSeat = 1` was fine, which is exactly what makes it hard to spot.

  **EF's generated migrations were wrong twice, in two different ways.** For the seat rename it emitted *only* the column rename: the two value shifts (`Stone` 1/2 → seat 0/1, and `Games.CurrentTurn` likewise) were invisible to it because **no storage type changed**. Missing them would flip turn order and every historical move's side. For `AddRoomSeats` it emitted drop-before-create, which makes a backfill impossible, plus a `Down` with `defaultValue: Guid.Empty`. Both hand-written, both tested at a *named intermediate* migration — the only point where "did the data move correctly" is observable.

  **A mutation gap became a new test file.** Deleting all five `.Include("_seats")` calls left the whole Infrastructure suite green — no test had ever loaded a room and then read its seats, and the symptom would not be a missing field but every read path throwing (`BlackPlayerId` calls `Single()` on an empty collection). `RoomRepositorySeatsTests` exists because of that.

  `SeatWire` is the one piece of deliberate debt: seat ↔ `'Black'|'White'` at the DTO boundary only, so the front end needed zero changes. Its own doc names the trigger — **the first `SeatCount != 2` game lands, the DTOs gain a seat field, and the class is deleted.**

- [x] **`add-doudizhu-cards`** — 斗地主's pure half: cards, combo recognition, beating, dealing, scoring. No `IGameRules`, no registration, no migration; **not one existing file changed.**

  **A measurement saved an entire enabling change.** `Move.Text` caps at 64 characters and a play is at most 20 cards, so one character per card means **the existing text payload already fits**. `generalize-match-payload`'s recorded trigger (「真出现不规则走子时再加列」) does *not* fire: a hand of cards is ordinary textual content, the same kind as 成语接龙's idiom, not a new dimension. The encoding is a persistence format, so it is pinned to the byte (`3♣='A'`, `2♠='z'`, `@`, `#`) and deliberately avoids quotes, commas and backslashes — a stored format should not require its reader to work out how many layers of escaping happened.

  Shuffling does not use `System.Random`, same reason as tetris and harder: its algorithm has changed between .NET versions, and a deal must be identical on every runtime *and* in TypeScript, or a runtime upgrade silently re-deals every archived game.

  **Mutation testing taught two things.** `WingsAreLegal` — a guard I wrote in the airplane branch to enforce "wings must not split a bomb", *with its own test* — was **dead code**: a hand containing exactly one quad always returns from the 四带二 branch first and never reaches it. Deleted; the real enforcement point is documented and verified load-bearing. And my seed-0 comment was **factually wrong**: state 0 does not mean "never shuffled" (every swap targets index 0, cards do move, all 54 stay distinct); the real consequence is total entropy loss, so the assertion is now `FromSeed(0) == FromSeed(0x9E3779B9)`.

  **What is left for 斗地主, and why it is four more changes.** The card logic is provable on its own; wiring it into the kernel is not one PR. In order: **`generalize-match-outcome`** (done — the result enum was two-player), **`generalize-match-flow`** (a server-only setup value on `Game` that reaches no DTO; rules able to override `(seat+1) % N`, because the landlord leads regardless of who bid last; and a rules-supplied timeout fallback — without it `TurnTimeoutWorker` would throw every 1500 ms into the void, which is exactly `enforce-ai-availability`'s defect), **`add-doudizhu`** (the rules through the real `Room`), **`add-doudizhu-visibility`** (hands are visible to one seat only — `RoomView` currently splits player/spectator and needs a third, per-seat dimension), then **`add-web-doudizhu`**.

  Of that list, everything up to and including **`add-doudizhu` is now done** — `generalize-match-flow` shipped as three narrower changes (`add-match-setup`, `generalize-turn-flow`, `pass-setup-to-rules`) plus the audit `pass-state-to-fallback`, so it was five enabling changes rather than one. Two remain: `add-doudizhu-visibility`, then `add-web-doudizhu`. The transport is a question for the first of those, not an open gap: `SayWord(roomId, text)` builds `MakeMoveCommand(Text:)` and inspects no game key, so **the payload path already carries a bid or a play** — a method named for 成语接龙 is in fact the generic text path, and whether 斗地主 should call it under that name is part of the DTO work that deletes `SeatWire`.

  斗地主 will be **unrated**, and the reason is structural rather than a judgement: ELO is a two-player model and this game settles in per-player points, so a ladder over it is a *different* ladder — the same separation tetris's score ladder already has. That also keeps `IsRated ⇒ SeatCount == 2` intact rather than needing an exception.

  House rules are all in an executable shape: 四带二 **is not a bomb** (three independent assertions — `IsBombLike` false, beats nothing, loses to a smaller bomb, because getting all three wrong at once is the common implementation), 三带一 compares by the **triplet**, straights only compare within one length, and spring + anti-spring being simultaneously true **throws** rather than picking one.

- [x] **The last four kernel changes before 斗地主** — `generalize-match-outcome`, `add-match-setup`, `generalize-turn-flow`, `pass-setup-to-rules`. Together with the two seat changes above, six enabling changes; **no game shipped in any of them**, and the last one exists because I audited the third.

  **`generalize-match-outcome` — the colour-named results were two mirrors, not one gap.** `GameResult` was `Ongoing / BlackWin / WhiteWin / Draw` and 斗地主's seat 2 had no value to write. Before adding a third, I counted the 18 references: **every one** was `stone == Black ? BlackWin : WhiteWin`, and the stone was always **the mover**. `Board.PlaceStone(move)` was told `move.Stone` and answered with the same fact; `Game` held `Result ∈ {BlackWin, WhiteWin}` *and* `WinnerUserId`. So the enum is `{ Ongoing, Decided, Draw }` and the winner is a seat — **asking "where does this value come from" turned "add a `Seat2Win`" into "delete two"**, and the code got *shorter* (`HardAi.IsWinForStone` deleted outright, along with two branches its own comments called impossible).

  One place genuinely needed the mover: `TicTacToeHardAi.TerminalScore` gets a result from placing `toMove`, which may be either side — so `mover` is now an explicit parameter and the exhaustive verification passes unchanged.

  It also fixed a real client defect. `result === 'BlackWin' && mySide === 'black'` needs two mirrors, and **a spectator holds neither** — so every game told every spectator they had lost. `myOutcome(ended, myUserId)` is now one function, shared by the dialog title and the win/lose sound.

  **`add-match-setup` — a server-only setup on `Game`, and I shipped it with no reader.** `IDealtGameRules.CreateSetup(seed)` produces an opaque string the kernel stores and never interprets; the four existing games changed **zero lines** (separate interface, same reason `IBoardGameRules` was split off: a lying implementation is something the next person cannot delete). The kernel takes a **string, not a seed** — three reasons, and the decisive one is that the entropy source (`ISeedProvider`) already lives in Application where the dependency direction allows it, which keeps tests reproducible: a pinned setup string, not "whatever got dealt".

  `setup` is a **required nullable parameter with no default**, and the room checks *both* directions at kickoff. The second direction (a setup given to a game that has none) throws because that setup would be **stored and never read** — the most expensive kind of wrong state.

  **And that is exactly what the change itself shipped.** `Apply`'s signature was `(history, intent, seat)`, so no rule could read `Setup`; after the merge the only mention of `.Setup` in `src` was a comment. The spec had declared it deferred ("由规则读,将来的 `Apply`"), but **a declared deferral and an oversight have the same result**. `pass-setup-to-rules` closes it: `Apply(MatchState state, …)` where `MatchState` is `(Setup, History)`. Found by sitting down to write the game and asking how a rule reads a hand — **whether a field has a reader is a question neither the compiler nor the tests ask.**

  The record over a fourth parameter is a **readability** argument (two of four flat parameters are "this game's state", two are "this move"), *not* extensibility — this repo rejected that reasoning when it declined a JSON move column. And no implicit `IReadOnlyList<PlayedMove> → MatchState` conversion, though it would have saved all 26 mechanical call-site edits: it would make "does this game have a setup?" vanish at the call site, so a future test that forgets one gets "you don't hold that card" instead of a compile error.

  **`generalize-turn-flow` — whose turn, and what if they don't move.** `MoveApplication.NextSeat` lets rules override `(seat + 1) % N` (the landlord leads regardless of who bid last); `ITimeoutFallbackRules.MoveOnTimeout` makes a timeout **play a move** instead of forfeiting, because with three seats "the opponent" isn't unique and "the peasants won" doesn't fit one `WinnerUserId`.

  `NextSeat`'s `null` **does** carry a default meaning, which looks like a contradiction of `setup`'s no-default rule one change earlier. Same test, opposite answer: **would forgetting be noticed?** A forgotten `setup` is a dealt game with no cards, which detonates tens of seconds later; a forgotten `NextSeat` asks *the wrong player to move*, which the game's first test catches. Mutation testing pinned **both directions** — ignoring `NextSeat` goes red, and treating it as mandatory (not moving when unset) also goes red. **A field with a default meaning needs both directions nailed down**, or "the default got mistaken for mandatory" passes in silence.

  The fallback move **must go through `rules.Apply`**, so `PlayMove` and the timeout branch share one private `ApplyMove`. Not tidiness: the fallback can *end the game* (playing someone's last card wins), and two paths would make "Apply is the single entry point for legality and outcome" into two entry points. The seam boundary lands exactly between identity/turn checks (`PlayMove`-only — the fallback's seat comes from `CurrentTurn`) and rules/record/finish (shared).

  The `SeatCount == 2` guard on the forfeit path was **not relaxed** — a three-seat game with no fallback still fails loudly at timeout. And the "fallback must make progress" requirement is explicitly **not** a spin guard: each fallback waits a full timeout period, so the worst case is one move per period. No invented "consecutive fallback limit"; that number would be arbitrary and the thing it guards against does not exist.

  **Four spec-drift findings, and two were mine from hours earlier.** `game-rules-registry` still described `Apply(…, Stone side)` and a pre-`generalize-match-payload` `MoveIntent`; `elo-rating` carried a paragraph saying "this must be rewritten by `add-per-game-rating`" that **`add-per-game-rating` itself had written**; `web-game-board` copied a whole source file into a requirement and had gone stale three times over. Then in `generalize-turn-flow` I wrote a MODIFIED block for a requirement **name that does not exist** — MODIFIED matches by name, so it would have *added* a second requirement describing the same handler. Caught by grepping the live spec, not by the validator.

  **`openspec validate --specs --strict` was 38/38 green through all of it.** It validates spec *shape*, never spec *truth*, and "does this requirement exist" is truth.

  **Two archiving rules, learned by breaking both in one command.** Renaming a requirement inside a MODIFIED block fails at archive time (`not found`) — the fix is a `RENAMED` block, and **`add-tictactoe` and `add-web-tictactoe-ai` had both already recorded this trap.** Worse: I archived the other three first, so when the rename aborted, this change's *older* requirement bodies were poised to overwrite the newer ones. **Archive in merge order.** Verified afterwards by grepping the live specs for the newest text rather than trusting the 38/38.

- [x] **`pass-state-to-fallback`** — the sibling of the change above, and it exists because I audited that one. `pass-setup-to-rules` changed `Apply` so a rule could read the deal; `MoveOnTimeout` sat a few dozen lines away in the same file with the identical problem and was not looked at. 斗地主's fallback needs the hand ("lead the smallest single card"), and a hand lives in the deal, not the history. **Same shape as `enforce-ai-availability`'s "fixed the rules fixture, did not look at the AI fixture seven lines below" — "I just fixed this class of problem" is a reason to grep for siblings, not a reason to relax.**

- [x] **`add-doudizhu`** — 斗地主's **rules**, through the real `Room`. Backend only; no UI. **The inherited acceptance criterion held: the match aggregate was not touched** — `git status backend/src` was two kernel-adjacent files, `+4 / -1`, and `Rooms/` zero. `DoudizhuThroughRoomTests` plays a real game through the real aggregate, and it proves more than 象棋's or 成语接龙's equivalent: **three seats, hidden information, and rules that name the next player** all cross the same aggregate at once. DI needed no change (it already derives from `BuiltInGameRules.All`), and five registry-walk invariants covered the new game **without one assertion changing**.

  **Five assertions written as "the day a game does X, this goes red" all went red, and that is what they were for.** Four were resolved the way their own comments said. `Every_registered_game_has_two_seats_today` became "every **rated** game has two seats" *plus* a new "at least one game has more than two", because otherwise deleting 斗地主 would leave the first one green as an empty loop. `No_built_in_game_deals_a_setup_yet` and `No_built_in_game_falls_back_on_timeout_yet` became **"exactly one"** — "exactly" has teeth that "at least" does not: the second such game turns it red, which is precisely when to ask whether those two games' needs are really the same thing.

  **The fifth guarded a *reason*, and the reason is what expired.** `AddGameSetup`'s `Down` drops the column, justified by "the only reader of `Setup` is the rules of a game that does not exist before this migration". 斗地主 exists now, so that `Down` really would destroy data — and worse than "roll back to a build without 斗地主": roll back and forward again and the column returns all-`NULL`, the room still looks playable, and the rules throw on the next move. A merged migration is not edited, so the consequence was **demonstrated in a test** and left on the record, with a second assertion pinning the *scope* (exactly one game writes a non-`NULL` `Setup` today, so the "add a guarded migration" bill covers one game).

  **Two real defects, both surfaced by tests written for something else.** `Card.DecodeMany` throws `FormatException` — not a `DomainException` — so `play:!!!` would have left the hub as an **unmapped exception (500)** rather than a coded refusal, telling the client "server error" when the client is the thing at fault. And the duplicate-card check I had written in `Parse` was **dead code**: `DecodeMany` already rejects duplicates. **Same shape as `add-doudizhu-cards`' `WingsAreLegal`, found the same way** — the test written for it caught a different exception than expected.

  **A right conclusion resting on a wrong reason.** The class doc argued the `play:` tag is necessary because "a bare `pass` is a legal four-card hand". `pass` has two `s`, and duplicate cards are rejected, so that string *throws* — its safety is **luck** (that word happens to repeat a letter), not the tag. `cab` is the honest example, and the test now also asserts `pass` throws so nobody mistakes the luck for design. **A correct conclusion with a wrong reason sends the next person the wrong way.**

  **And an acceptance criterion of mine was stronger than this repo's actual practice.** I asserted `Rooms/` and `Games/Abstractions/` must not mention the game; it went red on `IGameRules.cs`, because `GameKeys` lives there and **every game adds a constant**. That criterion had contradicted every game since `add-xiangqi` — it had simply never been executed. It is now two honest halves (the aggregate names nothing; the abstractions carry exactly the one constant), each guarded by a non-empty-file-set check so a wrong path fails instead of passing vacuously.

  **The CI failure was one assertion, and the reason it was red is worth more than the fix.** `dotnet test Gewu.slnx` was 1266 green; `AiSmoke` failed on `Count(g => !g.IsRated) == 1` — a *second copy* of a fact I had already corrected one layer down the same day. The cause is not this repo's thrice-fixed "hand-written list posing as a registry": it is that **this copy lives where `dotnet test` cannot reach** — a console app run against a live server. Another argument for having wired it into CI, again supplied by the smoke itself. The fix is **not 1 → 2** (that just moves the mine one game forward): the roster stays in the Application test, where the two unrated games' *different* reasons are written down, and the smoke now pins the invariant `IsRated ⇒ SupportsHumanVsHuman` plus both-sides-non-empty, so it needs no edit when game #9 lands. Added alongside: 斗地主's four descriptor facts and a step 9 for the **other half** — `POST /api/rooms` → 201, `POST /api/rooms/ai` → 400. `enforce-human-vs-human` and `enforce-ai-availability` were both caused by inferring one half from the other, so both halves get measured.

  **My first local run of that smoke measured the day-before-yesterday's build.** `--no-build` against `bin/Release`, and `/hubs/match` answered 404 while `/hubs/gomoku` answered 401 — the pre-`rename-gomoku-to-gewu` binary. **`--no-build` measures whatever happens to be on disk**; CI is immune only because its `--no-build` follows a build in the same job. One layer deeper: rebuilding while the server still ran failed on a locked DLL (`MSB3021 … used by Gewu.Api`), and the next `--no-build` run would have happily used the un-replaced binary — a failed build followed by a successful run.

  **Mutation testing forced two fixes in the smoke itself.** Flipping 斗地主's two flags made room creation return 400, whose body is `ProblemDetails` — parsing it into the summary record threw, and the process died **without reporting the remaining assertions** (exit 127; CI still red, but the report is useless — the same broken reporting this file already records about the hardcoded column). Parsing only on 2xx turns that into 8 clean failures and exit 1. And `waiting?.White is null` **passed vacuously** when the room was never created: it was the only assertion still green after the create failed. `waiting is { White: null }` fixes it — **"the room was not created" and "that seat in the room is empty" must not be the same result.**

  **Archiving taught a sharper version of PR #88's rule.** Both changes MODIFY the same `ITimeoutFallbackRules` requirement, and `add-doudizhu`'s body was written *before* `pass-state-to-fallback` existed — so it carried the **old signature**. Merge order was #89 then #90, so archiving in merge order would have let the **later-merged** change overwrite the newer text with older text. Caught by comparing the two bodies requirement-by-requirement before archiving, and resolved by hand-merging (new signature and `MatchState` paragraphs from #89, "exactly one implementation" and 斗地主's concrete fallback from #90). **Archive in merge order is necessary, not sufficient: when two unarchived changes touch one requirement, one of them has to be merged by hand.** `openspec validate --strict` was green on both, before and after — it validates shape, never truth.

  One live-spec drift created by this change and fixed in the same PR: `web-xiangqi` had a scenario titled 「只有一字棋仍然没有排行榜入口」 justified as 「它是唯一不计分的对战棋种」. **Its executable form never changed** — the front-end test is stub-driven, reads `isRated`, and never counts games — so the code was right and only the sentence describing it was false. That is exactly the class `enable-xiangqi-human-play` let sit for 36 commits. Two other 「唯一」 lines in `xiangqi/spec.md` were checked and **both still hold**, for two *different* reasons — being able to say a different reason for each is the test that a rule was applied rather than a pattern matched.

  **Measured on the way past, and left for the next change:** `Room.IsPlayer` and `Leave` still read `BlackPlayerId || WhitePlayerId`, i.e. **seats 0 and 1 only**. In a three-seat room the third player therefore cannot leave (`NotInRoomException`) and **can register as a spectator of their own game**, which hands them the spectator view and the spectator channel — the invariant `fix-spectator-chat-leak` exists to hold. `PlayMove` is unaffected (it goes through `SeatOf`, which knows every seat). That is `add-doudizhu-visibility`'s work, and it is in its proposal.

- [x] **`fix-three-seat-membership`** — "is this user a player in this room" had **seven hand-written copies**, every one of them `BlackPlayerId || WhitePlayerId`, i.e. seats 0 and 1 only. 斗地主 made all seven wrong, and **1266 existing tests stayed green** because no test had ever walked those paths for a three-seat room.

  Measured over real HTTP with three real accounts: seat 2's `POST /leave` returned **404** — he could not leave the room he was in — and `POST /spectate` returned **204**, so **a player holding a seat became a spectator**. That hands him `IncludeSpectatorChat`, i.e. the entire spectator channel, which is exactly the invariant `fix-spectator-chat-leak` spent a whole change building. The same fix's write side was affected too: `PostChatMessage`'s `isPlayer` also counted to two, so **seat 2 could post into the spectator channel**. That change's own conclusion — 「写入侧一直是强制的」 — was true in a two-seat world. **A conclusion can be recorded while it holds and then quietly outlive the world it held in**; same shape as `enforce-human-vs-human`'s "true of the web UI, false of the API".

  `SeatOf`'s doc says it exists because "three places asking which seat wrote the same if/else". **That consolidation only did half the job**: "which seat" got one home while the *neighbouring* question — "is this a player at all" — stayed spread across seven. Mutation-checked one call site at a time: **all seven go red.**

  Urging is the one place where this was a **behaviour** generalization rather than a fix. `senderSeat == 0 ? WhitePlayerId : BlackPlayerId` is *equivalent* to "urge whoever is to move" for two seats (an existing guard already forbids urging on your own turn), so the four shipped games change by zero lines; for three seats the old form **always urged seat 0** and seat 2 could never be urged. The discriminating test needs a position where the player to move is neither seat 0 nor the sender — otherwise both formulas agree and the test proves nothing.

  Three dead `var state = room.ToState(…)` statements went with it. C# does not warn (CS0219 only covers constants), and they wear `RoomView.For(room, viewer)` — this repo's only expression of "who is this snapshot for" — so they read as load-bearing. **And `fix-spectator-chat-leak`'s commit message claims "those three lines deleted themselves".** They did not: the diff shows them dutifully given the new argument while the next line switched to the aggregate, which is what made them dead. **A required parameter makes the compiler list every call site; "make it compile" is not "decide what each call site is for."**

- [x] **`generalize-match-contract`** — the wire said `Stone`, and for a three-seat room it gave a **wrong answer, not an incomplete one**. Measured: three `bid:0` moves came back `Black / White / White` — the two farmers indistinguishable in the move log — and `currentTurn` read `White` across **two different players' turns**, so a countdown could not say who it was waiting for. `SeatWire.ToStone(seat)` was `seat == 0 ? Black : White`; its own doc had named the trigger (「第一个 `SeatCount != 2` 的棋种落地那天」) and this was it. `MoveDto.Seat`, `GameSnapshotDto.CurrentSeat`, `SeatWire` deleted; colour became a **display** reading in `games/board-seats.ts` (gomoku reads seat 0 as 黑, 象棋 as 红 — one number, two readings, both true only in the display layer).

  **The client's two-seat guess was deleted rather than generalized.** `handleMoveMade` computed `move.stone === 'Black' ? 'White' : 'Black'`; the client cannot compute the next seat (no seat list on the DTO, no `seatCount` in `GET /api/games`) and does not need to, because `RoomStateChangedAsync` is awaited before `MoveMadeAsync`. That ordering had sat in the spec for a long time and had **never been measured on the wire**, and nothing had depended on it. Now something does, so `AiSmoke` records the arrival order of the two frames and asserts the first one mentioning the ply is the `RoomState`. Mutation-checked by swapping the two `await`s: `✗ … (first was move:1)`, exit 1. **An argument of the form "the order makes this code unnecessary" has to carry the evidence for that order.**

  **Mutation testing found that the change's entire point was unasserted.** Making `seatStone` always return `'Black'` left **all 744 front-end tests green** — nothing checked that seat 1 renders as a white stone; the one test that touched colours only looked at seat 0, and it was really testing out-of-range plies. One added assertion (a `seat: 0` move and a `seat: 1` move must paint one black and one white, *and each not the other*) turns that mutation red. The other three mutations died on their own: swapping `seatOfSide` → 19 red, reordering the broadcast → smoke red, deleting `SeatWire` → the compiler.

  **The requirement that copies source code into itself went stale a fourth time** — and its own body already said 「一条把源码整段抄进来的 requirement,会在每一次那段源码变化时静静过期」. So this change replaced its *shape* instead of updating the copy: name the types that must exist and the **decisions** that must hold (seats not colours, colour lives in the display layer, `GameResult` carries no colour, `row`/`col` nullable), and leave per-field shape to TypeScript, which is the compiler's job and not the spec's. Three pieces of pre-existing spec drift were corrected in passing, because MODIFIED replaces wholesale and **copying a wrong sentence forward is signing it again**: `RoomStateChangedAsync`'s signature, `GameHubService`'s API list (missing `sayWord` / `reconnect`), and `Connected5` where `Decided` has been the name since `generalize-match-domain`.

  A method choice worth keeping: **the spec delta was generated by a script that extracts each requirement body from the live spec and patches it**, asserting every anchor exists. Retyping a long MODIFIED body by hand is how an unrelated sentence gets silently reverted — which had just happened for real while archiving `add-doudizhu`.

Discipline: **do not start a new game until the previous one is archived.** Seven games × (rules + AI + UI + i18n + tests) will otherwise all rot half-finished. And the rule is narrower than the failure it needs to prevent: `enable-xiangqi-human-play` was not a game, so nothing stopped it sitting unarchived for 36 commits with the live spec contradicting the code. **A merged PR whose change directory is still in `openspec/changes/` is the signal** — check that list, because strict validation will not.

Deferred follow-ups, each with a reason:

- **`squash-migration-baseline` — measured and declined.** Off the deferred list: decided against, with the numbers, so nobody re-litigates it.

  Its own justification only ever argued the *cost of doing it* ("cheap while the DB is still local-only"), never the *benefit*. Measured: the 14 migrations' entire SQL is **400 lines that apply in 259 ms** to a fresh SQLite file. A squashed baseline emits a subset of that DDL, so it saves on the order of **100 ms, once, per fresh database**. The 5.3 s that `dotnet ef database update` actually takes is `dotnet ef` host startup, which a squash does not touch at all.

  Against that: **16 tests across 3 files stop being expressible.** `UserGameStatsBackfillTests`, `MoveOriginMigrationTests` and `MoveTextPayloadMigrationTests` each call `IMigrator.MigrateAsync` with a *named intermediate* stop (`DropUserRatingColumns`, `AddMoveOrigin`), because "did the data move correctly" is only observable between expand and contract — after the contract half the source columns are gone and an assertion is just checking against itself. Squash and those stops cease to exist. Four spec requirements describing intermediate states go with them, including the two that encode bugs this repo actually got bitten by: `AddRoomGameKey`'s `defaultValue: ""` (every existing room's `GameKey` becomes an empty string, so no room resolves its rules) and `DropUserRatingColumns`'s `Down` with `defaultValue: 0` (everyone restored at rating 0).

  **Trading a 100 ms one-off for deleting the only tests that guard hand-written data migrations is a bad trade at any file count.** If a real reason appears — a provider change, a hundred migrations, an actual deployment — reopen it. The conditional 「若有人压缩迁移,这个先后同样 MUST NOT 颠倒」 constraints in `user-management` stay where they are for exactly that day.
- **`backend/smoke/AiSmoke` now runs in CI** — see `ci/run-ai-smoke`. It is in `Gewu.slnx`, its base URL comes from `SMOKE_BASE_URL`, and the `backend` job starts the API against a scratch SQLite file, waits on `/health`, runs the smoke, and fails on its exit code.

  **It was broken when it was picked up, and it broke in exactly the way its own note predicted.** `require-room-game-key` had run it green (17 passed); `generalize-match-payload` then made `GameDescriptor.Rows/Cols` nullable, and AiSmoke's DTO still declared non-nullable `int` — so it compiled fine and crashed at runtime on step 8 the moment a boardless game existed. Nobody noticed, because nothing ran it. **That is the strongest argument for wiring it in, and the smoke supplied it itself.**

  Now 21 assertions, extended to pin what the last few changes established: xiangqi rated and open to human play, tic-tac-toe the *only* unrated versus game, 成语接龙 reporting `rows: null` and `supportsAi: false`. Exit code mutation-checked — a deliberately failing assertion produces `EXIT=1`, so a red smoke really does turn the job red.

  A measurement mistake of mine on the way: I first read `EXIT=0` from a crashing run, because `$?` after `dotnet run … | tail -30` is *tail's* exit code. **A pipe eats the exit code you were trying to measure.**

- **Puzzle level artefacts are now stored compactly** — see `compact-puzzle-artefacts`. `PuzzleLevelSeeder` used `JsonElement.GetRawText()`, which returns the *source text slice*, so the committed artefact's indentation was copied verbatim into `LayoutJson` and then served on every level load. Measured on a real dev database: **58% of the stored bytes were whitespace**. Same-build A/B on the endpoint itself: 成语纵横 level 10 went **6,389 B → 2,321 B**.

  The artefact files stay indented, and that is the point: **that copy is for a human to review, the column is for a machine to send, and their correct formats differ.** The old bug was one function treating the two as the same thing.

  Two traps are recorded in the spec. `Utf8JsonWriter`'s default encoder escapes every non-ASCII character, so 成语 and 曹操 would become `\uXXXX` — *larger* than the whitespace saved, and unreadable in a DB browser; `UnsafeRelaxedJsonEscaping` is required, and the "unsafe" in its name only concerns HTML interpolation, which this JSON never sees. And **the size assertion is not decoration**: swap the encoder back and both "semantically identical" and "no insignificant whitespace" still pass — only size regresses. Mutation-checked, so a semantics-only suite would have gone green on that regression.

  Existing dev databases keep their pretty-printed rows, because the seeder is a no-op once levels exist. Not worth a data migration: a local DB can be deleted, and there is no deployed one.

- **The `bundle initial exceeded maximum budget` warning is gone, and the budget is now 480 kB** — 504.65 kB → **470.37 kB**, against a threshold tightened from 500 kB so the win cannot quietly erode (transfer 132.15 → 124.70 kB). See `close-bundle-budget` and `tighten-bundle-budget`. Headroom is **9.63 kB**, deliberately narrow: the whole point of the signal is that it fires before anyone has to go looking.

  The fix was one component. `find-player` — a single debounced text box on `/home` — was the **only eagerly-loaded consumer of `@angular/forms`**: the auth pages, the lobby dialogs and the chat panel are all behind lazy routes. One `FormControl` was therefore pulling **34 kB** of forms machinery into every first paint. Replacing it with a plain signal + `[value]`/`(input)` removed exactly that, and the deferred note's guess — "one small thing rather than an architectural change" — turned out right.

  **A claim of mine in this file was wrong, and measuring it is what found the fix.** I had written that `add-web-tetris` "did not make it worse: the whole game is in lazy chunks, so the initial bundle is unchanged". Built both commits: **502.17 kB before, 504.65 kB after** — it added 2.48 kB, because `DefaultScoreRunsApiService` is registered in `app.config.ts`, which is eager. Lazy *routes* do not make a service lazy if the provider list names it. The 350-byte figure from `generalize-lobby` was also stale by then.

  The budget was **not** raised — that would convert a live signal into silence. It was later *lowered* to 480 kB instead, which is the opposite move and the one that keeps the signal useful. Mutation-checked that the new threshold is live: set it to 460 kB and the build reports `Budget 460.00 kB was not met by 10.37 kB`. (A note on that check — the first grep for it used the pattern `not be met` while the message says `was not met`, so it read as "no warning" for a moment. **The tool that verifies a signal can be the thing that is broken.**)
- **Long-content wrapping is now guarded in two places** — see `guard-long-content-wrapping`. The chat panel and 成语接龙's word list each carry a `break-words` that nothing asserted; a style rewrite would have shipped a horizontally-scrolling room page at 375 px.

  It also turned out **only one of the two identical fragilities was even specified**: `web-idiom-chain` already required the 375 px assertion "with the longest entry present" — and that assertion did not exist. `in-room-chat` had neither. Both now have both.

  **The guard and the evidence prove different halves, and the spec says so.** jsdom has no layout engine and no Tailwind stylesheet, so `getComputedStyle` returns nothing useful and `scrollWidth` is always 0 — a unit test can only pin the class list. That catches the class being deleted; it cannot catch the stylesheet ceasing to define it. The browser half was measured: a real 500-character unbroken message posted through the UI over SignalR, and the dictionary's longest idiom (15 characters) played as an opening word — both `overflow-wrap: break-word`, both `scrollWidth === clientWidth === 375`, page overflow **0**. The assertion accepts any of `break-words` / `break-all` / `wrap-anywhere`, so a legitimate swap is not a false failure.

  A footnote on how empty that gap really was: the new chat assertion first failed with `NG0201: No provider found for ActivatedRoute`, because the sender's name is a `routerLink`. All five pre-existing tests in `chat-panel.spec.ts` covered the tabs and the input — **not one of them had ever rendered a message.**
- **`StubHub` is now bound to the contract** (`implements GameHubService`) — see `close-bundle-budget`. Adding a fifteenth abstract member fails to compile (`TS2720`), mutation-checked; the mechanism used to be the habit "every hub method has a test that calls it".

  Doing it turned up why it mattered. The blocker on record was "`makeRoomState` must return a real `RoomState`", and once typed, that helper had **no `gameKey` at all** — so every room test in the file ran against `undefined` and got `boardSizeFor`'s 15×15 fallback. That fallback is gomoku's own size, which is exactly why it never looked wrong. One test was leaning on the omission to mean "a server newer than this build"; `gameKey` is not optional on the wire, so `undefined` was never a state a server could produce. It now uses an unregistered key — same intent, real scenario.
- **The gomoku→gewu rename is done** — see `rename-gomoku-to-gewu`. localStorage keys, `MatchHub`, `/hubs/match`, `logs/gewu-.log`, and the JWT issuer/audience all carry the platform's name now. **The game's own name was not touched**: `gameKey: "gomoku"`, `GomokuRules`, `gomokuManifest`, `/g/gomoku/*` — 五子棋 *is* called gomoku.

  **No read-old/write-new shim, and the reason overturned my own premise.** I had written that renaming the storage keys "logs everyone out, so it needs a shim." Two things were wrong with that. The population being logged out is **zero** (no deployment, no production data — the same shape as `require-room-game-key`'s compatibility layer for "published clients", of which there were none). And more decisively: the JWT issuer/audience change lands in the same commit, so **every existing token dies anyway** — a shim would have faithfully preserved a refresh token that no longer validates. It would have been permanent dead code protecting nothing.

  **The first mechanical pass broke gomoku, and `git diff` caught it.** The bare pattern `gomoku:` matched a TypeScript object literal key (`gomoku: { rows: 15, cols: 15 }` — that one silently changed the board size), prose with a colon after the word, and an i18n test fixture. The fix was to match only quoted `'gomoku:<key>'` with all eight keys named explicitly, plus a `GameKeyNamingTests` guard asserting the game key is still `"gomoku"`. **A game key is a contract** — it is in room records, API paths, the client registry, and every persisted `Room` row. Renaming it is not a rename, it is a data migration.

  Deliberately untouched: the bot emails `easy@bot.gomoku.local` in three **already-merged migrations** (editing those is a hard rule here, and they are internal identifiers — a migration for zero benefit), and the GitHub repo name.

  The `GOMOKU_*` env-var prefix half of this note is **resolved** — see `fix-spec-api-ops-env-prefix`. It was measured rather than reasoned about: a Production instance given both `GOMOKU_Cors__AllowedOrigins__0` and `Cors__AllowedOrigins__1` accepts only the unprefixed one, with a third origin as the control. The spec now says so, and a new scenario pins the *negative* — the prefixed form does not work — because that was the behaviour previously promised, and reversing it into an assertion beats deleting it if you want to stop someone helpfully adding the prefix back. **A documented config key that the runtime silently ignores is worse than no documentation**: whoever follows the docs believes the allowlist changed, requests keep being refused, and nothing at the scene points at the word "prefix".

The connection-string key is `ConnectionStrings:Default`, **not** `:DefaultConnection`, so the env var that overrides it is `ConnectionStrings__Default` (verified working — pointing it at a scratch file is how the `add-per-game-rating` migration was smoke-tested against a copy of the dev database). An earlier note claiming that env var "does not take effect" was simply using the wrong key name.

Other platforms, unchanged from before:

- `frontend-desktop/` — empty. Electron wrap of the Angular app.
- `frontend-mobile/` — empty. Flutter + Material 3.

## Workflow — OpenSpec is mandatory

**Never write implementation code without an approved OpenSpec proposal.** This is a hard rule, not a preference.

1. **Propose** — for each new feature, create a change directory at `openspec/changes/<change-name>/` containing `proposal.md`, `tasks.md`, and `specs/`. Use `/opsx:propose` or `/openspec-propose`.
2. **Review** — the user reads the proposal and requests edits. Wait for explicit approval before touching code.
3. **Implement** — once approved, work through `tasks.md` item by item, checking off as you go. Use `/opsx:apply`.
4. **Archive** — when done, `openspec archive <change-name>` moves spec deltas from `changes/` into the live `openspec/specs/` tree and renames the change directory under `archive/`.

Pure bug fixes that bring code into compliance with an existing spec don't need a new proposal — fix the code, commit. Spec-level corrections that document already-shipped behaviour can ship as a tiny `fix-spec-<name>-drift` change.

## Tech stack

### Backend (`backend/`) — .NET 10

- ASP.NET Core Web API, target `net10.0` on every project (nullable + implicit usings enabled)
- **MediatR** for CQRS — every write is a `Command`, every read is a `Query`, one handler per file
- **EF Core** — SQLite for local dev, SQL Server when scaling
- **SignalR** for real-time play and chat
- **FluentValidation** for input validation, **Serilog** for logging, **JWT** for auth
- Tests: **xUnit** + **FluentAssertions** + **Moq**

The solution file is `.slnx` (XML), not `.sln`. The `dotnet` CLI handles it transparently; older tooling may not.

### Web (`frontend-web/`) — phase 1

- **Angular 21** + TypeScript strict mode
- **Tailwind CSS v4** + **Angular Material** + **`@angular/cdk`** (overlays / dialogs / a11y)
- **Transloco** for runtime i18n (initial locales: `zh-CN`, `en`; adding a new locale = drop one JSON file + register one line)
- `@microsoft/signalr` client (lazy-imported on first hub call to keep it out of the main bundle)
- State: **Angular Signals** first; NgRx only for genuinely complex flows
- Tests: **Vitest** (not Karma/Jasmine)

### Desktop (`frontend-desktop/`) — phase 2

Electron wrapping the Angular app.

### Mobile (`frontend-mobile/`) — phase 3

Flutter + Material Design 3, `signalr_netcore` client.

## Backend architecture

### Layer dependency direction (strict)

```
Domain  ← Application  ← Infrastructure
                       ← Api
```

- **`Gewu.Domain`** — entities, value objects, domain events. Zero outward dependencies.
- **`Gewu.Application`** — use cases (MediatR handlers), DTOs, interfaces for infrastructure concerns. Depends on `Domain` only.
- **`Gewu.Infrastructure`** — EF Core, persistence, external adapters. Implements `Application` interfaces.
- **`Gewu.Api`** — ASP.NET host, HTTP endpoints, SignalR hubs, DI composition root.

Preserve the direction when adding project references. **Never** have `Api` reference `Domain` directly; **never** put DB access outside `Infrastructure`.

### DDD aggregates

- **Room aggregate** (root: `Room`) contains `Game` + `Chat`. All mutations to game state and chat go through `Room`.
- **User aggregate** (root: `User`) is independent.
- Value objects (no identity, immutable): `Move`, `BoardPosition`, `Score`.
- Domain events (e.g. `MoveMade`, `GameEnded`) published via MediatR.

### CQRS conventions

- Commands: `CreateRoomCommand`, `MakeMoveCommand`, ...
- Queries: `GetRoomListQuery`, `GetLeaderboardQuery`, ...
- One handler per file; the file name matches the command/query.
- **SignalR hubs route messages only** — they dispatch to MediatR and push results back. No business logic in hubs.

### Hard rules

- **Domain and Application must not use `async void`, `.Result`, or `.Wait()`.** Use `async Task` / `await` end-to-end.
- Database access is **only** allowed in `Infrastructure`.
- Public methods need at least an XML `<summary>` doc comment.
- Interfaces use the `I` prefix (`IRoomRepository`).
- C# files: PascalCase.

### Required tests

Don't ship without unit tests for:
- Domain core logic — **win detection** and **ELO calculation** in particular.
- Every Application handler.
- Frontend: components and services with real logic (pure display components can skip).

`Gewu.Domain.Tests` and `Gewu.Application.Tests` exist. If an Api-level integration test project is added, name it `Gewu.Api.Tests` and register it in `Gewu.slnx`. The test csprojs declare `Xunit` as a global using — don't add `using Xunit;` in test files.

## Frontend conventions (Angular)

### Naming & structure

- Filenames: **kebab-case**. Classes: PascalCase.
- Use **standalone components** (Angular 17+ style). Don't create new NgModules unless unavoidable.
- Prefer **Signals** over `BehaviorSubject` for local state.
- When Tailwind class strings get long, extract via `@apply` into a custom utility.
- All HTTP lives in `core/api/`. Components must not call `HttpClient` directly.

### Design & UX (hard rules)

- **Dark mode must work from day one.** Mechanism: `ThemeService` (Signal) toggles a `.dark` class on `<html>`; colors come from CSS variables (`--color-bg`, `--color-primary`, etc.); Tailwind's `dark:` variant assists. Never hard-code dark colors in components (no `bg-gray-900` / `text-white`).
- **Responsive (mobile-first).** Every route MUST work at **375 px** width, then progressively enhance via `sm: / md: / lg: / xl:` up to 1440 px+. Tailwind's default breakpoints are sufficient — don't write CSS for one specific resolution.
- **Modern UX.** CSS transitions over JS animations. Visible `focus-visible` ring. Skeleton placeholders to avoid layout shift. Every interactive element keyboard-reachable. Respect `prefers-reduced-motion`. Loading / empty / error states all need real UI — no plain `"loading…"` text.

### Performance

- **Lazy loading is mandatory.** Every route outside the root shell (shell + login) uses `loadComponent` / `loadChildren`. Each lazy chunk stays **under 200 KB gzipped**; split if larger.
- `<img loading="lazy">` is the default; only above-the-fold first-paint images are exempt.
- The SignalR client is constructed inside a service and **only connects on first subscription** — never at app bootstrap. A user who never opens a game page should never establish a hub connection.

### Architecture — SOLID, easy to extend

- **Single responsibility.** A component does one thing. Container components (fetch data, dispatch events) and presentational components (pure rendering of inputs) are separate — don't mix.
- **Dependency inversion.** When a service has plausible alternative implementations (mock for tests / future API client / different state backend), use an **abstract class as the DI token**. Inject by token, not by concrete class.
- **Open/closed.** Adding a new theme / locale / difficulty MUST be a "drop one config file or one TS file" change — no edits to existing components.
- Composition over inheritance. Cross-cutting behavior goes in directives / pipes, not base classes.
- Components stay under 200 LOC. If larger, extract a service or store.

### Dialogs & overlays

Dialogs / popovers / overlays MUST use **Angular CDK** (`@angular/cdk/dialog` or `@angular/cdk/overlay`). Material's `MatDialog` is fine (it wraps CDK). **Never** hand-roll `<div>` + `*ngIf` modals — focus trap, ESC handling, backdrop, ARIA attributes are all required, and CDK gives them for free.

### i18n

- Use **Transloco** (or Angular i18n + ICU runtime switching).
- Initial locales: `zh-CN` (Simplified Chinese) + `en`. Files at `public/i18n/<locale>.json`. Flat keys / dotted paths (`room.join.button`).
- Templates **MUST NOT hard-code** Chinese or English display strings — always `{{ 'key.path' | transloco }}`. Date / number formatting goes through Angular's `formatDate` / `formatNumber` with a locale parameter.
- The active language is held by `LanguageService` (Signal), persisted to `localStorage`. Resolution order: `localStorage` → `navigator.language` → `en` fallback.
- Adding a new locale = drop a new `i18n/<locale>.json` + add one entry to `LanguageService.supported` + register Angular locale data in `core/i18n/register-locales.ts`. No other file changes.

### Theme switching (Material / System / future)

- Themes are kept in a registry: `ThemeService.register(name, tokens)`. `tokens` is a CSS variable bag (`--color-primary`, `--color-surface`, `--radius-card`, `--shadow-elevated`, …). Switching = setting `data-theme="<name>"` on `<html>` and persisting to `localStorage`.
- Two themes ship: `material` (Angular Material default palette + Material radii / shadows) and `system` (Apple / Fluent-ish minimal — smaller radii, lighter shadows).
- **Dark/Light is an orthogonal axis to the theme.** Each theme has light + dark token sets. `ThemeService` exposes two signals (`themeName` and `isDark`) that switch independently.
- Component styles MUST reference CSS variables, never literal colors. "This button uses theme-blue" = `var(--color-primary)`, not `#2962FF`.
- Adding a new theme = drop one tokens file + one `ThemeService.register(...)` call. No component changes.

The same registry pattern applies to **board skins** (`BoardSkinService`, currently `wood` + `classic`) and **sound packs** (`SoundService`, currently `wood` + `chiptune` + `minimal`, listed once in `core/sound/packs/index.ts`).

### Frontend tests

- Vitest covers: services / stores with logic, components with conditional branches, the i18n pipe, cross-cutting services like `ThemeService` / `LanguageService` / `SoundService`. Pure presentational components can skip.
- For side-effecting paths (dialogs, route guards, SignalR subscriptions), use TestBed + `ComponentHarness` integration tests.

## Common commands

From `backend/`:

```bash
dotnet build Gewu.slnx                              # build all
dotnet test  Gewu.slnx                              # run all tests
dotnet run   --project src/Gewu.Api                 # http://localhost:5145, https://localhost:7082

dotnet test tests/Gewu.Domain.Tests                 # single project
dotnet test tests/Gewu.Domain.Tests \
  --filter "FullyQualifiedName~WinDetectionTests.Diagonal"   # single test
```

### EF Core migrations

Install once: `dotnet tool install --global dotnet-ef`. Run from `backend/`:

```bash
# Add a migration (name in PascalCase, describes the intent)
dotnet ef migrations add AddUserAndRoom \
  --project src/Gewu.Infrastructure \
  --startup-project src/Gewu.Api \
  --output-dir Persistence/Migrations

# Apply to the configured DB
dotnet ef database update \
  --project src/Gewu.Infrastructure \
  --startup-project src/Gewu.Api

# Roll back the last migration (only before it has been merged / pushed)
dotnet ef migrations remove \
  --project src/Gewu.Infrastructure \
  --startup-project src/Gewu.Api

# Generate a SQL script for review.
#
# NOTE: **--idempotent does not work here.** EF throws
#   NotSupportedException: Generating idempotent scripts for migrations is not
#   currently supported for SQLite
# and writes no file at all. This block used to say `--idempotent`, and it had
# never once worked — measured, not assumed.
dotnet ef migrations script \
  --project src/Gewu.Infrastructure \
  --startup-project src/Gewu.Api \
  -o migrations.sql

# Just the delta between two migrations. This form *is* supported on SQLite.
dotnet ef migrations script AddMoveOrigin AddScoreRuns \
  --project src/Gewu.Infrastructure \
  --startup-project src/Gewu.Api \
  -o delta.sql
```

Rule: never edit a migration that has already been merged to `main` — add a new one instead.

### Frontend

```bash
npm install
npm start           # ng serve (with the dev proxy → :5145)
npm run build
npm test            # Vitest, watch mode
npm run test -- --run   # Vitest, single run (CI mode)
npm run lint
```

## Git — branches & commits

Use **GitHub Flow** plus **Conventional Commits**.

**Branches** — `main` is protected and always deployable. Branch off `main` for every change; PR back to `main`:

```
feat/<slug>      new feature       feat/room-chat
fix/<slug>       bug fix           fix/elo-draw-calc
refactor/<slug>  refactor          refactor/move-validator
docs/<slug>      docs only         docs/signalr-contract
chore/<slug>     build / misc      chore/upgrade-ef-core
test/<slug>      tests only        test/win-detection-edge
```

Slugs are kebab-case. Tie to an OpenSpec change name when one exists (e.g. `feat/add-domain-core`).

**Commits** — [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <subject>

[optional body — explain WHY, not WHAT]

[optional footer — BREAKING CHANGE: ... / Refs: #123]
```

Types: `feat` `fix` `docs` `style` `refactor` `perf` `test` `build` `ci` `chore` `revert`. Scope is the module (`domain`, `api`, `web`, `infra`, etc.). Subject is in the imperative mood, ≤ 72 chars, no trailing period. Breaking changes get a `!` after the type/scope **and** a `BREAKING CHANGE:` footer.

Examples:

```
feat(domain): add five-in-a-row win detection
fix(api): return 409 instead of 500 when room is full
refactor(web)!: replace BehaviorSubject with Signals in game store
```

A single commit can be in English or Chinese, but pick one — don't mix within a single commit. Don't commit failing tests.

## Code review

All merges to `main` go through a PR. Direct push is forbidden.

### PR requirements

- **Link to the OpenSpec change.** The PR description states the corresponding `openspec/changes/<name>/`. If there is no associated change, explain why one isn't needed (pure docs, build fix, spec-drift correction).
- **Size.** Each PR ideally stays under **400 lines of net change** (excluding auto-generated migration / lock files). Split when larger.
- **CI must be green.** `dotnet build` + `dotnet test` + the web `npm run lint` + `npm test -- --run` must all pass before requesting review.
- **At least 1 approval** to merge. PRs touching architecture, security, or DB schema benefit from 2 approvals.
- Merge style: **Squash merge**. Use Conventional Commits format and keep the PR number (`feat(domain): ... (#42)`).

### Author self-check (before opening a PR)

- [ ] Layer direction intact: `Domain` has no outward dependencies; `Application` only depends on `Domain`; DB access only in `Infrastructure`; `Api` does not directly reference `Domain`
- [ ] No `async void` / `.Result` / `.Wait()` in `Domain` or `Application`
- [ ] SignalR hub is purely a router; business logic lives in handlers
- [ ] Public methods have an XML `<summary>`; interfaces start with `I`
- [ ] Unit tests cover: win detection / ELO / new handlers / web services with logic
- [ ] No secrets / connection strings / sensitive `appsettings.*.json` values committed
- [ ] OpenSpec `tasks.md` is checked off; latest progress reflected in the PR description

### Reviewer focus (in priority order)

1. **Correctness and business logic** — win detection rules, ELO formula, forbidden moves, timeouts, reconnection. Are edge cases tested?
2. **Architecture and dependency direction** — any sneaky cross-layer calls? Are DB / HTTP details leaking into `Application` or `Domain`?
3. **Concurrency and async** — deadlock risk? `async` used correctly? SignalR group / connection lifecycle right?
4. **Security** — input validation (FluentValidation), authn/authz, SQL injection, XSS, JWT verification, spectators must not be able to send move commands.
5. **Test quality** — not just coverage; do tests assert *behavior* rather than *implementation*? Is mocking abused?
6. **Readability** — naming, function length, comments that explain *why* rather than *what*.
7. **Performance** — only raise this when there's a metric or profile to point at. Don't ask for optimization on a hunch.

### Comment etiquette

Prefix review comments so the author knows what's required vs. optional:

- `must:` — must change, otherwise no merge (correctness / security / architectural violation)
- `should:` — strongly suggested change; the author needs a reason to keep their version
- `nit:` — minor / style; the author's call
- `question:` — pure question, not a request for change
- `praise:` — when something's well-done, say so

Authors can decline `should` / `nit` items but should briefly explain why. Don't approve while a `must:` is unresolved.

## Shell

Windows host, bash shell. Use Unix syntax in commands (`/dev/null`, forward slashes), not `NUL` / backslashes.
