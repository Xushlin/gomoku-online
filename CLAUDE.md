# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this project is

**格物 / Gewu** — a multi-platform online game hall. Planned games: idiom games (成语纵横 / 成语接龙 / 猜成语), 五子棋, 一字棋, 中国象棋, 华容道, 俄罗斯方块. 「格」 means grid cell, which is what they all have in common.

Nine games ship today, and between them they establish the three kernels every later game reuses:

- **斗地主 (doudizhu)** — the match kernel's **hardest** proof: three seats, a server-only deal, hands visible to one seat each, and a settlement in points rather than ELO. It cost **eleven** changes — six to stop the kernel assuming two players, one for the cards, one for the rules, one for per-seat visibility, and one for the UI — and the aggregate was never touched by any of them. Playable at `/g/doudizhu/lobby`.

- **五子棋 (gomoku)** — the *match* kernel: players register, create/join rooms, play real-time matches (via SignalR) with room chat, spectator chat, and urge-opponent shortcuts; ELO-based ranking with special icons for the top three; human-vs-AI with multiple difficulties; game-record storage and replay.
- **成语纵横 (idiom-crossword)** — the *puzzle* kernel: a level catalogue, server-authoritative attempts (the answer key never leaves the server), server-counted mistakes and hints, star scoring, and per-level best records.
- **华容道 (klotski)** — the puzzle kernel's **proof**, and the one that showed its authority model has two shapes. 成语纵横 is authoritative because it *withholds* the answer; 华容道 hides nothing and is authoritative because it *replays* every claimed move. Playable at `/g/klotski`.
- **一字棋 (tictactoe)** — the match kernel's **proof**, not an extension of it. Its entire rule set is `NInARowRules("tictactoe", 3, 3, 3)`; it contributed zero lines of win detection. Human-vs-AI only, and therefore **unrated**: with no human-vs-human mode its only opponents are bots, bot games are rated, so a ladder over it would rank Easy-bot grinding rather than skill. That is now enforced by the invariant `IsRated ⇒ SupportsHumanVsHuman` rather than left to a comment. See the `add-tictactoe` audit for what adding the game revealed about the registry.
- **俄罗斯方块 (tetris)** — the *score-attack* kernel, and the platform's **third authority model**. 成语纵横 *withholds* the answer; 华容道 *replays* every move; 俄罗斯方块 replays **placements, not keystrokes** — `(rotation, column)` per piece, no timing anywhere. It is also the only game whose client owns the entire rule set, and the only one with no registry behind it: score-attack has one game, and *a switch with one arm is a switch*. Playable at `/g/tetris`, with periodic ladders at `/g/tetris/scores`.
- **中国象棋 (xiangqi)** — the match kernel's **first genuinely different game**, and since `enable-xiangqi-human-play` a full human-vs-human rated game with spectators. Its move is `from → to` rather than a placement, its board is 10×9 with pieces on intersections, and `Stone.Black` is 红. 一字棋 could not prove any of the kernel's seams general, because it is gomoku in miniature; 象棋 could, and did — at each of the three layers the assumption had leaked into (rules, AI, board component). It shipped human-vs-AI-only and unrated for a structural reason — no human-vs-human mode — and both halves are now false: the deferral its own doc comment recorded («大厅泛化之后翻它») came due.

Games fall into three categories that deliberately do **not** share one aggregate — see the platform roadmap below:

| Category | Games | Realtime | Core concepts |
| --- | --- | --- | --- |
| Turn-based adversarial | 五子棋, 一字棋, 中国象棋, 成语接龙, 斗地主, 挖坑 | SignalR | room, N seats, turn order, move sequence, ELO, spectators, replay |
| ↳ what 斗地主 adds | — | — | **hidden per-seat state**, a **server-only setup** (the deal), **three** seats, and a settlement in points rather than ELO |
| Single-player levels | 成语纵横, 华容道, 猜成语 | none (REST) | level catalogue, progress, stars, hints, time leaderboard |
| Single-player score-attack | 俄罗斯方块 | none (submit at end) | run record, score validation, periodic leaderboard |

## Current phase

**Nine games ship**: 五子棋 (the original), 成语纵横 (the first puzzle game), 一字棋 (the change that priced what a second board game costs), 中国象棋 (the change that proved which match seams were actually general), 华容道 (the one that did the same to the puzzle kernel), 成语接龙 (the one with no board at all), 俄罗斯方块 (the only one whose client owns the whole rule set), 斗地主 (three seats, hidden hands, and the first game whose players do not all see the same room), and 挖坑 (the first game whose first mover comes from the deal, and the one that made the card UI shared rather than copied). Detail:

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

  Of that list, **everything except `add-web-doudizhu` is now done**. Everything up to and including **`add-doudizhu`** — `generalize-match-flow` shipped as three narrower changes (`add-match-setup`, `generalize-turn-flow`, `pass-setup-to-rules`) plus the audit `pass-state-to-fallback`, so it was five enabling changes rather than one. `add-doudizhu-visibility` and `add-web-doudizhu` followed, so **斗地主 is done** — eleven changes end to end, and the match aggregate was not touched by any of them. The transport is a question for the first of those, not an open gap: `SayWord(roomId, text)` builds `MakeMoveCommand(Text:)` and inspects no game key, so **the payload path already carries a bid or a play** — a method named for 成语接龙 is in fact the generic text path, and whether 斗地主 should call it under that name is part of the DTO work that deletes `SeatWire`.

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

- [x] **`add-doudizhu-visibility`** — 斗地主 was **unplayable, and not because it had no UI**: nothing could deliver "the 17 cards in your hand" to you. `Game.Setup` is server-side and reaches no DTO (deliberately, pinned by a reflection assertion), the third seat appeared in no field of `RoomStateDto`, and the kitty — public once the landlord is decided — sat inside `Setup` where nobody could see it.

  `IPerSeatViewRules.ViewFor(MatchState, int? seat)` is a separate interface for the same reason `IDealtGameRules` was: on the base interface, four games would each need a lying implementation, and **a lying implementation is something the next person cannot delete**. Its return value is opaque to the kernel — the same treatment the puzzle vertical already gives `LayoutJson`. `RoomView` grew from one dimension to two: *who is this snapshot for, where do they sit, and what is their private slice*. `RoomStateDto.Seats` finally has a reader, which is why `generalize-match-contract` was right to defer it.

  Measured over real HTTP with three accounts plus a spectator: three hands of **17, pairwise intersections 0/0/0, union 51** (the other three cards are the kitty, invisible to everyone during bidding), the spectator's hand empty while still seeing counts and phase, and `seats=[0,1,2]`.

  **The core assertion is "no seat sees anyone else's card", not "a seat sees its own"** — the second is green on an implementation that ships all three hands. So it compares card by card, with a negative control that an out-of-range seat number also yields an empty hand: a bad seat index must not become *someone else's cards*. Three mutations, all red — `ViewFor` ignoring the seat, `RoomView` always projecting the seatless view, `ToState` dropping the field. Three independently breakable segments whose failure all looks like the same bug, so they are asserted at two different layers.

  **The subgroups went from two to three, and that was forced.** Once a seat group exists, a seated player must not stay in `non-spectators` — they would receive two snapshots (one with their hand, one without) and *which one they see would depend on arrival order*, exactly what `fix-spectator-chat-leak` built "mutually exclusive and exhaustive" to prevent. So that group became `observers` (in the room, no seat, not spectating). `Clients.User(...)` was rejected: it reaches **every** connection of that user, including their tab open on a different room — an urge popping in the wrong tab is harmless, a room snapshot overwriting another room's state is not. And no fast path for games without hidden state: two-seat games now send four identical payloads instead of two, because a second code path is a second chance for a handler to forget to trim.

  The "remove every seat group on leave" bound is **derived from the registry**, not a constant. The first version was `const int MaxSeats = 4` whose own comment said "when a game with more seats lands, this number has to grow" — **that sentence is the reason the constant had to go**: forgetting to grow it raises nothing, and the symptom is a player still receiving snapshots after leaving the room.

  Two limits written down rather than glossed: **the broadcast fan-out has no end-to-end test** (projection is covered by unit tests and real HTTP, the group function is exhaustive by construction, but "three real SignalR connections each receive only their own" needs a `Gewu.Api.Tests` project this repo does not have — so a one-character typo in `ViewGroupName` turns nothing red today). And a mutation-restore step silently reverted two days of work: **python resolves `/tmp/x` to `D:\tmp\x` while msys bash means something else**, and the stale same-named backup there was two days old. What exposed it was not the `dotnet test` right after — **that run printed only the Domain line because the other two projects never compiled**. *"No failures in the output" and "all three projects ran" are not the same claim.*

- [x] **`add-web-doudizhu`** — 斗地主 is playable at `/g/doudizhu/lobby`. **Eight games ship.** Backend: **zero changes**. Verified by bidding and playing a card in a real browser against two real accounts.

  **`mySide` was the real generalization here.** The card table takes `mySeat: number | null`, because `'black' | 'white' | 'spectator'` has nothing to say about a third seat — and following that thread found the rest: `RoomPage.mySeat` reads `seats` (not `black`/`white`, where seat 2 simply does not appear and would be read as a spectator), `mySide` is *derived* from it rather than computed a second way, and `RoomSidebar` takes `mySeat` too — **seat 2 was previously denied the resign and leave buttons**, a consequence of `isPlayer = mySide !== 'spectator'`.

  Measured in the browser, not deduced: clicking **Bid 3** then selecting ♣6 and **Play (1)** produced `moves [(1,0,'bid:3'), (2,0,'play:M')]`, hand 17 → 20 (the kitty absorbed) with `kitty` turning from `null` into three cards, and **two tokens reading two different hand lengths (19 / 17) while the public counts and the table matched exactly** — per-seat trimming holding on the wire.

  **Two defects only a browser could find.** A drawn game still rendered two disabled action buttons — *a button you cannot press is a question on screen: is it me, or is it broken?* And with seat 2 to move, the sidebar said 「白方走棋」 in a game with no white side. The second is the sharper one: that string **was correct for all four two-seat games** and wrong for the third seat — the same shape as `SeatWire`, this time in copy. The fix keys off `seats.length`, not the game key: the seat list is already in the snapshot, and taking an async dependency for a number you already hold turns a synchronous fact into a loading state.

  The board judges no legality, and this is the fourth different answer to that question — 象棋 doesn't judge, 华容道 does, 成语接龙 **splits**, and 斗地主 doesn't judge *at all* except the one rule that needs no rules ("nothing on the table, so you cannot pass"). Combination recognition plus beat comparison live on the server and are the only judge; a second copy would diverge, and divergence reads to a player as a bug.

  The 375 px check was run with **19 cards on screen** (`overflow: 0`) — `generalize-lobby`'s lesson in its exact original form, since an empty hand passes trivially.

  One thing deliberately not done: **renaming `SayWord`.** It is in fact the generic text-payload path — the server only builds `MakeMoveCommand(Text:)` and never looks at the game key — but its name was coined for 成语接龙. No third hub method was added (one payload with two entrances is two validation paths to keep in step), and no drive-by rename either, since that touches server, contract and spec. **Trigger: the day a third text-payload game lands**, when a name coined for one game becomes misleading in three places.

- [x] **`add-doudizhu-table-visuals`** — 斗地主的牌桌变成了一张桌子,发牌与出牌有了动作。Backend: **zero changes**.

  用户的话是「界面有点粗糙,太简陋了」,并给了一张 QQ 游戏斗地主的参照图。那张图里真正起作用的四件事**都不需要新的服务端数据**:一张绿呢桌子、三个座位环绕(下家在右 —— 出牌逆时针,俯视时下方的逆时针下一位就在右)、对家的手牌是一叠**牌背**(与服务端逐张裁剪过的事实一致 —— 客户端手上本来就没有那些牌)、以及牌从扇形正中散到手里 / 出的牌从出牌人的方位飞到桌心。

  **整张牌的位图没有用,而这是这个仓库已经判过的同一个案子。** 用户提供的素材包里有 54 张 420×600 的牌面,而它们既不跟 app 主题也不跟棋盘皮肤 —— 硬规则是组件里不许写死颜色,`add-web-xiangqi` 给象棋棋子的答案是 `BoardSkinTokens.pieces`,连约束一起继承:**皮肤挑的是深浅,不是色相**。所以纸面 / 边框 / 角标 / 牌背 / 桌面进 token(三个皮肤各补一份,**漏了编译不过** —— 那份 `bamboo` fixture 在加字段那一刻就红了,和 `pieces` 加进来那次是同一处),而**花色的色相是这个游戏的身份**(♥ 必须是红的),所以只用素材包里的**四个花色图**,一张 2 KB 上下。顺带解决一个老问题:`♥` 在部分平台会被渲染成彩色 emoji,而一张图不会。

  **动画由「牌的身份」驱动,而不是由计时器。** 模板用 `track card.code`,所以一张牌的 DOM 节点只在这张牌**第一次出现**时被创建,而 `animation` 写在牌上就恰好在那一刻放一次 —— 之后重排、别人出牌、快照刷新都不会重播(CSS 动画不因节点移动而重启)。于是没有信号要清、没有 `setTimeout` 要取消、也没有「动画放过了吗」这个状态。**同一个事实同时驱动 DOM 与动画,就没有第二个东西需要被记得去重置**;抢到地主后进手的三张底牌因此**也**会飞进来,而那不需要额外一行代码。

  **三处「计划是错的」,每一处都是量出来的,而三处的形状相同:一个我以为已知的机制。**

  1. 样式表按 `.board-*` / `.xq-*` 的先例放在全局 —— 而全局样式**首屏就要下载**:初始包 474.16 → **484.83 kB**,480 kB 的预算当场报警。搬进组件(`room-page` 那个 lazy chunk)后 479.66 kB。中间还走错一步:为了压进 `anyComponentStyle: 4kB`,把 token 颜色写成 `text-[color:var(--x)]` 之类的 arbitrary utility —— **那些 utility 会进首屏的 Tailwind 样式表**,479.53 → 480.38 kB,预算又红。最后的分工是:尺寸间距用现成 utility(它们早就在首屏包里),皮肤 token 留在组件 CSS。
  2. 发牌的横向散开量量出来是 **0**。`--ddz-step` 里有个 `100%`,而**百分比是在用它的地方解算的**:在 `margin-left` 里它对着容器,而在 `transform: translate()` 里它对着**元素自己**,`(34px - 34px) / 16 = 0`。牌只往下掉不散开,**而动画照样在放**,所以看起来像「设计成这样」。
  3. 花色图的路径原本写在 CSS 里(CSS 才是绘制权威),而这份**测试构建没有 .png 的 loader**:绝对路径 `Could not resolve`,相对路径 `No loader is configured for ".png"` —— 两次都是整个测试构建失败。路径因此由组件绑成 `--ddz-pip`,而「它指着一个真存在的文件」由一条走遍**全部 54 个编码**的测试钉住(**惰性** `import.meta.glob` 只取键名、不加载模块 —— 同一个 loader 限制下唯一还能证明文件在磁盘上的办法)。代价写在源码里:那个绑定若被清洗掉,花色会**静静地不见**,所以有一条断言读 inline style 里的 `url(`。

  **「装着扇形的容器不能是 shrink-to-fit」这一条踩了三次** —— 右侧座位的 `flex-end`、改成 `center`、桌心的 `items-center`。同一个机制:`100%` 在宽度尚未定下来的容器里解算成 0,步长先变**负**(牌背反向叠,`scrollWidth 18 > clientWidth 0`),加了下限之后又被压到 2px(17 张牌挤成 50px 的一条)。

  **顺带修的两处,都只有在屏幕上才看得见。** 侧栏在三座位房间只列黑白两个人 —— **2 号座位上的人在自己的房间里根本不出现**,而这是 `add-web-doudizhu` 修过的「白方走棋」**同一个缺陷的第二处**,当时那条测试是绿的:它问的是另一个问题。以及 375 px 下「10」被后一张牌切成「1(」。

  **新增一个 lint 期的检查,并作废了一份抄在规格里的名单。** `frontend-web/scripts/check-styles.mjs` 挂在 `npm run lint` 上,断言每个皮肤块定义的变量集与默认皮肤一致、样式表里没写死花色路径、发牌 keyframe 里没引用带百分比的变量。它**不在 vitest 里**,是试过三条路之后的结论(`?raw` 的默认导出是 `[]`、`import.meta.glob` 带 `query` 同上、`node:fs` 在 spec 的 tsconfig 里没有类型)。而 `web-board-skins` 里那条抄了 11 个变量名的 requirement 同时作废:它自 `add-web-xiangqi` 起就漏了 `--xq-*` 三个,**而那条 Scenario 从来没有被实现过** —— 没有任何测试读过 `board-skins.css`。第一版检查还红在**我自己写的注释**上(classic 块里一句「NOT `--color-surface`: this skin…」),所以它先剥注释 —— `generalize-match-seats` 的源码级断言记的是同一条。

  **一次变异什么也没证明,而它长得像证明了。** 侧栏那条第一次用 `@if (false)` 变异,得到的是**模板编译错误**:exit 1,而没有一条测试跑起来。改成 `seats.length > 5` 才真正让 2 条测试变红。**一个构建失败的变异不是红测试** —— 与本文件已记的「`--no-build` 会跑磁盘上碰巧存在的那份二进制」是同一族:失败与「没在测」长得一样。

  **动画的证据全部来自 headless CDP,而这纠正了本文件里的一句话。** 之前记的是「Browser pane 不显示时读到的 DOM 属性是旧的」;更准确的说法是**时间线根本不走** —— `document.timeline.currentTime` 冻在 0,所有动画 `running@0`、`opacity: 0`,牌停在 `from` 关键帧上(而这反倒让我能直接读出那一帧的散开量,于是发现它是 0)。`chrome --headless --screenshot` 也不行:它只能靠 `--virtual-time-budget`,而 SignalR 的长轮询一直挂着,虚拟时间到点时页面还停在骨架屏上。用 CDP 真实时间采样量到的是:t=432ms 时 17 个动画全 `running`、第一张牌在 `x=216, y=-108, opacity 0`;t=882ms 时 15 个还在跑、第一张已就位;t=2482ms 时**一个都不剩**;而 `--force-prefers-reduced-motion` 下同一组采样**每次都是 0 个动画**。375 px 的检查是在**满屏内容**下做的(20 张牌 / 两家各 17 张牌背 / 桌上一手牌),**没有任何元素** `scrollWidth > clientWidth` —— 这一条是必要的:三次溢出里有两次在页面级 `scrollWidth - clientWidth === 0` 下完全看不见。

- [x] **`draw-card-suits`** — 花色从素材 PNG 换成自绘的 SVG path。用户的判断是「换成自己的更好」,而按下去之后**三样为迁就位图搭的脚手架一起消失了**。

  `fill="currentColor"` 让花色跟着牌面的 `color`,也就是 `--card-red` / `--card-black` —— **皮肤重新拿回了「深浅」那一半**,而 `add-web-xiangqi` 定的约束仍然成立。量过,因为这是换路线的全部意义:同一张 ♥ 在 wood 下是 `rgb(198,40,40)`、midnight 下是 `rgb(217,59,57)`;同一张 ♠ 是 `rgb(43,43,43)` / `rgb(29,36,48)`。**一张 PNG 在两个皮肤下只会是同一个值。** 一起消失的还有:`--ddz-pip` 那条 style 绑定、那条用惰性 `import.meta.glob` 证明文件在磁盘上的测试、以及那条防止绑定被清洗掉后花色**静静不见**的断言。**能被删掉的机制才是最好的机制。**

  四条 path 是手写之后**画出来看**的:梅花第一版没有梗(像一株三叶草),黑桃取了肩部更饱满的那版。**一条 path 写得对不对,只有画出来才知道。**

  **顺带修一个我自己造的回归,而它只有截图看得见。** 上一个 change 为了压 4 kB 的组件样式预算,我删掉了 `:host { width: 100% }`,理由写的是「宽度由父级 flex 给」——**那是错的**:房间页的容器是 `flex-col items-center`,于是整张桌子按内容收窄(felt 从 ~730px 变成 **~430px**),而牌宽是 `8.6vw` 跟视口不跟容器。**上一个 change 的所有断言都是绿的** —— jsdom 没有排版引擎。这是「shrink-to-fit 咬到这张牌桌」的**第四次**,现在被 `check-styles.mjs` 钉住并变异验过。

- [x] **`add-card-sounds`** — 发牌与出牌有了声音,而**内置 sound pack 改成了按需加载**,因为前者把 480 kB 的预算顶穿了。

  斗地主此前**每一个**动作 —— 叫分、不要、出牌 —— 都在放 `move-place`(一颗棋子落在木头上),而发牌一声不响。现在出牌是 `card-play`,而**叫分与不要刻意留在 `move-place` 上**:不看屏幕也听得出别人是出了牌还是过了牌。**这正是分成两个事件的理由,不是副产品。**

  量到的音频图 —— 一个 `AudioContext`,唯一的变量是发生了什么事,而页面是打开着的、另两家这时才入座:发牌 `buffer 5 / filter 5 / osc 0`(wood 的五连 tick,没有别的事件长这样)、叫分 `1/1/0`、**出牌 `1/1/1`**、不要 `1/1/0`。**没有人听过它们** —— 这里量的是「三件事建出的图互不相同且各是设计的那一张」,不是「好听」,`add-xiangqi-ai` 那条规则同样适用。

  **发牌只在牌到手的那一刻响一次,而动画在每次刷新都重播。** 两者由同一个事实驱动(手牌出现),但声音的触发条件更严:**重播一个动画是装饰,重播一个声音是在报告一件没有发生的事。** 第一版的哨兵被 effect 的第一次运行吃掉(构造时 `state()` 是 null),于是第一份真快照成了「0 → 17」的跳变,打开一局进行中的牌局也会响 —— 三条断言当场变红。

  **`unhandledSoundEvent` 那个机制当场生效**:两个事件加进 `SOUND_EVENTS`、三个 pack 一行不改时,`tsc` 报三处 `Argument of type '"card-deal" | "card-play"' is not assignable to parameter of type 'never'`。而 `web-sound` 里那份把九个事件名逐个抄进代码块的 requirement 同时作废 —— 换的是它的**形状**(清单在源码里,规格说规则),因为 `web-game-board` 抄过一整个源文件、过期了四次。

  **预算这件事按上一条记录预测的方式发生了,而出路也是那条记录写下的。** 加两个事件之后初始包 **481.23 kB**。量法是把三个 pack 换成空实现再构建:**472.54 kB** —— 所以三个 pack 是**首屏的 8.69 kB**,而它们在用户第一次与页面交互之前一声都发不出来。`BUILT_IN_PACKS` 于是变成 `PACK_LOADERS`(动态 import),构造时不 await 地预热当前 pack,而万一 `play()` 先到就**排队而不丢**(「这一局的第一手是静的」是一个只在会话第一次出现、之后永远复现不出来的缺陷),`AudioContext` 仍同步构造 —— autoplay 策略要的是用户手势那一帧。结果 **473.29 kB,余量 6.71 kB**,比这一整轮开始前(474.16)还宽。

  一条顺带钉住的:「已知 pack」= 有实现**或**有 loader。只查已加载实现的那一版会让持久化的选择在**每次启动**时失效 —— 启动那刻内置 pack 一个都还没加载。

  **「一个会崩的变异不是变异」,这一轮里第二次。** 哨兵那条第一次把 `if (!state) return` 换成空块,那让 effect 在 `state` 为 null 时**抛异常** —— 一条崩溃路径,不是「另一种合理实现」,所以它活了下来。写成第一版真正的样子之后,3 条测试变红。(第一次是 `@if (false)` 变成模板编译错误:exit 1,而没有一条测试跑起来。)



- [x] **`fix-lobby-seats`** — **大厅把每一个房间都当成两个人的。** 量出来的:房间行写死「黑方 / 白方」两个标签,`sideKey()` 只比 `black` / `white` —— 于是三座位房间里 2 号座位上的人在大厅的房间行里**根本不出现**,并在「我的对局」里被标成「你是观战」,**在他自己的对局里**。根因在服务端:`RoomSummaryDto` 只有那两个字段,而它们是 0 号与 1 号座位的派生读法,上面两处没有别的数据可读。它加 `Seats`,与 `RoomStateDto` 同类型同形状;那两个字段保留(那边加 `Seats` 时也没删)。

  **这是同一个缺陷的第三与第四处,而前两次修错了层。** `add-web-doudizhu` 修掉「白方走棋」、`add-doudizhu-table-visuals` 修掉侧栏只列两个人 —— 两次都修在**房间页**,而大厅读的是**另一个 DTO**,所以那两次对它一行影响都没有。`AiSmoke` 里那段写着「add-doudizhu-visibility 付这笔账」的注释因此一直绿着:它看的正是这份摘要。**「我刚修过这个缺陷」和「这个缺陷修完了」之间,差一个「还有谁在读同一件事」。**

  **行上不出现颜色词,而这不是一处审美选择:`board-seats.ts` 自己的文档就是禁令** —— 「Only the board family may call it. A game with more than two seats has no colours to map, which is why nothing outside `games/` and the board components uses it.」 大厅不是棋盘。`seat-empty` 占位符随之消失:`seats` 只含**在座的**座位,而「一共有几个座位」不在这个 DTO 里;而「还有空位」这件事同一行上已经说了两遍(Waiting 徽章 + Join 按钮)。

  **这两个组件此前一条测试都没有,而那就是缺陷能活到今天的直接原因** —— 没有任何东西问过「一桌三个人时这一行画了几个人」。两个 spec 文件因此是新建的,而核心断言是**遍历**(1/2/3 个座位各走一遍,玩家链接数等于 `seats.length`):举例式的「斗地主房间画三个人」在一个把第三个人硬编码进去的实现上同样是绿的。同一个形状的第三次 —— `guard-long-content-wrapping` 发现五条聊天测试没有一条渲染过消息,`add-web-doudizhu` 发现那条侧栏测试问的是另一个问题。

  **三件只有在浏览器里、而且只有用最长数据才看得见的事。**

  1. **一处既有缺陷:`hero-card` 的 `<h1>` 在 375 px 下横向溢出 1 px。** 用户名上限是 20 个字符,而一个 20 字符的名字里没有一个换行机会 —— 335 px 的文字挤在 293 px 的盒子里。加一个 `break-words`。**它此前每一次 375 px 检查都白过**,因为那些检查用的是 alice / bob。这是 `guard-long-content-wrapping` 那一族漏掉的第三处,而**用户名的上限就是那个「最长内容」的定义** —— 随便编一个长串反而不诚实:那种串根本注册不进来。
  2. **名字之间的分隔符是量出来才加的。** 第一版只靠外层 flex 的 `gap-x-1`(4 px),而两个 20 字符的名字挨在一起读不开。改成 `·` —— 它本来就是这一行的分隔符、语言中立,**不是**一个显示字符串。第一版曾经写过 `、`,那是中文标点,而模板里不许写死显示字符串。
  3. **第五处在屏幕上被确认,而它修不进这个变更。** 侧栏那条修复的判据是 `seats.length > 2`,注释写着「座位表就在这份快照里,不必去问注册表要 `seatCount`」。那句话回答的是**另一个问题** —— **`seats.length` 不是「这个棋种有几个座位」,是「有几个座位被坐上了」**。于是一个**等待中**的三座位房间,侧栏原文是 `Black: Baa11… White: Caa11…`。它需要 `IGameRules.SeatCount`,而 `GET /api/games` 今天不发它。留给 `publish-seat-count`:**把一个契约字段悄悄塞进一个缺陷修复里,是这个仓库自己判过的坏做法。**

  **一个坑,记下来因为下一个人也会扑一次:一个 Playing 的三座位斗地主房间会在约一分钟内自己消失。** 超时兜底连叫三次 `bid:0` → 流局 → `Finished`,而房间列表过滤掉 `Finished`。于是「在大厅里看三个人名」第一次量了个空。**它不是缺陷,是斗地主的规则在起作用** —— 而在它上面做「三个人名不溢出」的检查,会得到一条空转通过的断言,`generalize-lobby` 那条「空列表上『无横向滚动』是白过的」在这里换了一种方式复现。

  归档时收拾了**自己造的三处规格漂移**,而分清哪些是自己造的才是关键:`active-rooms` 那条覆盖清单与它的两个 Scenario、`/home` 卡片那句「我是 Black/White/spectator」、以及 `web-user-profile` 里那句「host / black / white」都归本变更;而「`my-active-rooms`:每行的 host / 对手座位」是**既有**漂移 —— 那张卡的模板一个 username 都不渲染,这条覆盖要求从写下来那天起就没有实现。它带标注留下,好过悄悄删掉一条本该被实现的要求。**MODIFIED 是整体替换,所以一条没被 delta 覆盖到的 requirement 会静静保留旧句子** —— 这是「按 requirement 数、不按文件数」那条账的另一面。

  验证:后端 **1444** 绿(新增 5)、前端 **841** 绿(新增 14)、lint 干净、初始包 473.32 kB。两处变异都红(行退回读 `black`/`white` → 3 红;`sideKey` 忽略 `seats` → 2 红)。浏览器 375 px:三个 **20 字符**的名字在同一行,页面级溢出 0 且**没有任何元素** `scrollWidth > clientWidth`;`/home` 上 2 号座位读到「You are seated」,整页溢出 0。**截图没有** —— Browser pane 未显示时页面不合成帧,所以布局量到了(`scrollWidth`/`clientWidth` 是布局,headless 下照样跑)、**外观没看过**。

- [x] **`publish-seat-count`** — **`seats.length` 不是「这个棋种有几个座位」,是「有几个座位被坐上了」。** 侧栏拿它当前者用,于是一个**等待中**的三座位房间渲染的是「黑方 / 白方」—— 在浏览器里量到的,原文是 `Black: Baa11… White: Caa11…`。

  **这是同一个缺陷的第五处,而它藏在为修它而加的那个分支里。** `add-doudizhu-table-visuals` 加 `moreThanTwoSeats()` 时写着「座位表就在这份快照里,不必去问注册表要 `seatCount`」—— 那句话回答的是**另一个问题**:对局进行中三个座位都坐满,所以它当时是对的;而房间在坐满**之前**也要渲染,那时它是错的。**一个在稳态下正确的代理量,会在过渡态里说谎。**

  `GameDescriptorDto` 因此加 `int SeatCount`,投影自 `IGameRules.SeatCount`。**非空**,而那正是它与 `Rows` / `Cols` 的区别:每个有 `IGameRules` 的棋种都有座位数,不存在「不适用」;而成语接龙真的没有盘面。前端读它、**不存副本** —— 那正是 `remove-manifest-board` 删掉的东西。`GameCapabilitiesService` 一行不改(`of()` 已经返回整个描述符),而**异步的账已经付过了**:`RoomPage.loading()` 里本来就含 `!capabilities.loaded()`。座位数已知之后,泛化那一支还能把**空座位**画出来 —— 在它之前,一个还差一个人的三座位房间看不出自己还差一个。

  **颜色那一支留着,而这是同一个问题的第二个答案。** 两座位棋盘棋种的侧栏继续说「黑方 / 白方」:你正看着一张摆着黑白子的棋盘,而「谁是黑方」是座位号给不出的信息。大厅行的答案**相反**(`fix-lobby-seats`),因为它是跨棋种的列表、不是棋盘。**同一个问题,两个层次,两个答案** —— 而能为每一个说出**不同的**理由,才说明这是在应用规则,不是在套模板。

  **一条只比「DTO 与规则一致」的遍历守不住「投影写死 2」** —— 它比的是 `rules.SeatCount`,所以只在注册表里**真有**一个座位数不是 2 的棋种时才会红。所以另加一条钉**样本**的:取值集合里 MUST 同时有 2 与大于 2 的值,与 `enable-xiangqi-human-play` 记的「一条只走一边的遍历会全绿地什么都不验」同源。而 `The_dto_does_not_carry_WinLength` 按它自己的注释红了(它断言**整个**属性集合,注释写着「加字段时它会红,那正是想要的:对外契约多一个字段该是一次有意的决定」)—— 那是本变更唯一一条**预告过**的红灯。

  **AiSmoke 里那段注释连着两版都在描述另一个 DTO。** 它先说「`add-doudizhu-visibility` 付这笔账」(那个变更改的是 `RoomStateDto`),`add-wakeng` 改成「触发条件是 `add-web-wakeng`」(而付账的是 `fix-lobby-seats`)。两条断言现在是更窄、更诚实的一对:`White` **仍然只是 1 号座位**,而座位列表自己在 `Seats` 里 —— 两句话同时成立,才说明那个字段是加上去的、不是把旧字段改了意思。**一条描述另一个 DTO 的注释,会在自己这个 DTO 被修好之后继续错着。**

  验证:后端 **1445** 绿、前端 **844** 绿、lint 干净、初始包 473.32 kB。两处变异都红(判据退回 `seats.length` → 2 红,正是那两条「等待中的三座位房间」;投影写死 2 → 2 红)。真 HTTP:`AiSmoke` **51** 条全过,`GET /api/games` 报 wakeng / doudizhu **3**、gomoku **2**。新增断言里「描述符还没到达时它不编造座位」是负控制 —— 少了它,一个把 `null` 当 3 处理的实现会让另外几条全绿。

  一个小坑:**`grep -c` 找不到东西时返回 1**,把 `&&` 链掐断了,于是那次 smoke 根本没跑而我去 tail 一个不存在的文件。与「管道会吃掉你想量的退出码」同族:**一个用来验证的命令,自己的退出码也会说话。**

  (顺带一条工具坑,记下来因为它差点让本条记录整段丢掉:**在这台机器上 `python - <<EOF` 的 stdin 默认不是 UTF-8**,含中文的脚本会直接 `SyntaxError: Non-UTF-8 code`。而它失败之后 `git commit` 照样跑了 —— 于是提交信息里写着「CLAUDE.md 记的是……」,而 CLAUDE.md 一个字都没改。**一个失败的步骤后面跟一个成功的步骤,和两个成功的步骤,长得一模一样。**)

- [x] **`add-web-wakeng`** — 挖坑可玩了,`/g/wakeng/lobby`。**九个棋种 ship。** 后端**零改动**。

  **这一步真正做的事是把 `hoist-card-model` 那把尺子用在界面上,逐件问「共享的是事实,还是形状」。** 判据一字不改:**按「是不是同一件事」分,不按「代码长得像不像」分。**

  - **共享**(搬到 `games/cards/`):牌的一字符编码(服务端 `Card.Alphabet` 的那份副本)、四个花色的 SVG path、三座位环绕的方位、「当前一轮是从最后一手 `play:` 起到末尾」—— 挖坑用**同一批值**、同一个下标,那是一个事实。
  - **各一份**:`seatView` 的形状(`landlord` / `baseScore` 对 `digger` / `bid` / `firstBidder`)、大小顺序。**「它们可以分歧」正是「这不是一个事实」的检验。**

  **牌桌组件共享 + 参数化,而不是复制,理由是具体的:** 那 374 行 CSS 里的扇形公式**被 shrink-to-fit 咬过四次**,而它的不变量由 `check-styles.mjs` 按文件名钉着 —— 复制它就是复制一个已经出过四次错的公式,**而那是一份真的会分叉的第二真源**。两个游戏的差别是四个数和几个标签:那是**参数**,不是分歧,与 `NInARowRules(key, rows, cols, winLength)` 让一字棋贡献零行判胜是同一个形状。于是 `room-page` 那个 `@if` **不因挖坑增加一支** —— 两个棋种渲染同一个组件,只是配置不同。

  **「搬家没有改行为」有一条可执行形式:既有的 844 条断言一条都没改**(只改 import 路径),而 `config` 是**必填输入**,所以编译器把每一处调用点列了出来 —— 一个默认值会让「忘了传」和「故意用斗地主那份」长得一样,而后者的症状是底牌少一张、手牌顺序反着、首叫者标记不见,三样都只在屏幕上看得见。

  **`compareForDisplay` 不是凑数的配置项,是一处真缺陷的预防 —— 同一个巧合第三次咬人。** 服务端送来的 `myHand` 是 `Card.Encode` 的输出,也就是**编码顺序**(3、4、…、K、A、2)。斗地主的大小恰好就是它,所以按原样渲染是对的;**挖坑是 `3 > 2 > A > … > 4`**,按原样渲染会把**最强的那张放在最左边**、第二张是最弱的 4。前两次是 `hoist-card-model` 改掉服务端 `CardRank` 那句「数值就是大小顺序」、`add-wakeng` 修掉超时兜底照抄的 `HandOf(seat)[0]`;这一次在客户端,而 `PlayingCard.rank` 的文档一并改对。**一句只被一个实现验证过的话,会在每一层各错一次。**

  三处变异全红,而它们各只杀一条测试 —— 那是刻意的:每一条都构造成「配置换成斗地主那份就会红」。`compareForDisplay` → 排序那条;`kittySize` 3 → 底牌那条;`showsFirstBidder` false → 标记那条。

  **五件计划之外的事,而其中三件是「我的测量本身错了」。**

  1. **我自己建的那个 lint 期检查抓住了这次搬家。** `check-styles.mjs` 按**文件名**钉 `card-table.css`,于是 `npm run lint` 当场炸。**按文件名钉正是它有用的原因**:一次搬家若让那些不变量静静失效,谁都不会发现。
  2. **首叫者标记第一版让组件样式预算红了 90 字节。** 我加的 `.ddz-card--mini` 写了 `width` / `height` / `font-size`,而 `.ddz-card` 本来就从 `--ddz-w` 推出这三样 —— 那个类**整个删掉**,改成内联绑一个 `--ddz-w`(底牌那一支本来就在内联绑 `--ddz-gaps`),**新增 CSS 零行**。`add-doudizhu-table-visuals` 记过:为绕开这个预算去用 Tailwind arbitrary utility,会把字节挪进**首屏**样式表,更糟。
  3. **一条断言我先写错了,而改对之后比原来更强。** 「标记显示 3 号座位」—— 而测试用的 transloco **没有翻译**,`{{seat}}` 根本不插值。改成断言**哪一张牌**在:服务端点名 ♣4,而手里另有一张 3,于是它证明的是「画的是被点名的那张」,不是「从手牌里随便挑了一张」。
  4. **暗色我第一次量错了属性。** `--felt-bg` / `--card-face` 是**渐变**,所以落在 `background-image` 上,而我读的是 `backgroundColor` —— 读到 `rgba(0,0,0,0)`,看起来像「暗色下没有底色」。改读 `backgroundImage` 之后两个模式逐值不同(felt `#2f7a4a` → `#1f5334`,牌面 `#fffdf6` → `#f6f1e2`)。**一个量错了属性的测量,和一个真的缺陷,长得一模一样。**
  5. **点牌出牌这条交互在这个 pane 里验不了。** Browser pane 不显示时页面不合成帧,zoneless 的变更检测不同步跑,所以点完一张牌再读 DOM 读到的是 `Play (0)` 与 `disabled`。那是本文件已记过两次的已知限制,不是缺陷;那条路径的权威是单测(`emits the selected cards in ascending order`),而载荷本身在 `add-wakeng` 的真 SignalR 探针里走通过。**说清楚哪一半没量,比说「都验过了」诚实。**

  浏览器(375 px,**满屏内容**:18 张手牌 + 32 张牌背 + 4 张底牌 + 桌上一手):手牌读作 `4,4 … A,3`(最弱在左、3 在右)、叫分按钮说 **Dig 1/2/3 / No dig**、底牌 **4** 张牌背、首叫者标记带 ♣4、侧栏列出 **Seat 1 / 2 / 3** 三个人(`publish-seat-count` 的效果)。页面级溢出 **0**,而唯三个 `scrollWidth > clientWidth` 的元素**实测**是 `text-overflow: ellipsis` 的 `truncate`(20 字符用户名),按设计如此 —— **「有元素溢出」和「布局坏了」不是一回事,而分清它们要读 computed style,不能只看数字。**

  一个坑:**一局挖坑会自己往前走**(超时兜底 60 秒一手),所以我第一次打开房间时叫分已经结束、底牌已经公开。「叫分阶段长什么样」要**新开一局立刻看** —— 与 `fix-lobby-seats` 记的「Playing 的三座位房间约一分钟自己消失」同源:**一个会自己演化的系统,观察它要挑时机。**

- [x] **底牌泄漏 + placeholder 写死棋种**(纯修复,无提案)—— 两处用户在屏幕上抓到的缺陷,而第一处是**同一行代码在两个牌类棋种里各错了一遍**。

  `ViewFor` 判「底牌该不该公开」用的是 `Digger is null` / `Landlord is null`,而那两个字段在**有人叫过一次分**的那一刻就非空 —— 它们的含义是「**当前最高叫分者**」,不是「已经定下的挖坑者 / 地主」。于是首家一叫,四张底牌就对**所有人**公开,而后面两家正是靠看不见它才要下判断。判据只能是**阶段**:`WakengTable` / `DoudizhuTable` 只在叫分真正结束时才把 `Phase` 推出 `Bidding`。规格里本来就写着「叫分阶段 MUST 为 null」,所以这是纯粹的合规修复。

  **没有一条既有测试抓住它,而原因是两个游戏的那条测试有同一个盲点**:都用「一步都没走」验隐藏、用 `bid:3` 验公开 —— 而 `bid:3` **立刻**结束叫分,于是「有人叫过分、但叫分还没结束」那一格**从来没有被走到**。两条新断言各带**前提检查**(先断言 `Digger`/`Landlord` 真的非空、阶段真的还在叫分),否则新测试会重复原来那个盲点。

  同一张截图里还有两处同源的错:阶段写着「叫分」而旁边并排写「挖坑者:1 号座位」(**用同一个词说两件事**),以及「自由出牌」在叫分阶段就显示。都按阶段收起。

  placeholder 那条:大厅泛化之后 `/g/:gameKey/lobby` 是**一个棋种**的大厅,而对话框写死「我的五子棋房」。它本来就注入了 `LOBBY_GAME_KEY`(建房要它),所以改成插值即可。**规格只列了键名、不列内容,而没有任何测试断言过那句文案** —— 那就是它活下来的原因。新断言**自带翻译**:共享的 mount 用空 `langs`,两个棋种会渲染出同一个键,验不出插值有没有发生,而要验的正是插值。

- [x] **`add-wakeng-play-hints`** — 「要不起自动过牌」与「提示按钮」,而**它们是同一个函数的两个消费者**。

  `WakengFollows.For(hand, onTable)` 给出这手牌在当前局面下全部合法的出法,于是 **要不起 = 这个列表为空**,**提示 = 在列表里轮换**。写成两套逻辑会造出一个能自相矛盾的组合 —— 提示说「你可以出这手」,而自动过牌已经替你过了。**一个事实两个读者,不是两个事实**,而一条断言逐座位比对 `canFollow == (候选非空)` 把它们钉在一起。同 `add-tetris` 里「开局校验」与「重放」读同一个 `ScoreAttackGames`。

  **判「能不能出」的只能是服务端。** 牌型识别与压牌比大小是这一局唯一的判据;客户端再写一遍就是一份会悄悄分叉的第二真源,而分叉在玩家眼里是「这游戏有 bug」。客户端**不算**「我要不起」,它只是**照服务端算出来的事实行动** —— 与 `generalize-puzzle-rules` 那句「客户端给你的数不可采信,而服务端自己算出来的数是一个服务端观测到的事实」同源。

  **两个出口的形状不同,而那是刻意的:** `canFollow` 是一个**布尔**、进 `seatView`(每次快照都要用);候选列表走**按需**的 `GET /api/rooms/{id}/hints`(几十项,塞进每次广播就是给每个人的每一帧付一次钱)。端点只回答**调用者自己**那一份 —— 一个能查别人候选的端点等于把别人的手牌算出来给你。围观者与非玩家拿到空列表而不是 403:提示是**可有可无的便利**,而「这里没有可提示的东西」的正确反应是按钮不出现,不是一条错误路径。

  `canFollow` 的定义写死成「**假如此刻轮到你**,你出得起吗」,与轮次**无关** —— `ViewFor` 收的 `MatchState` 里根本没有当前回合。一个「有时是 false 只因为还没轮到你」的字段会让客户端在错的时候自动过牌。

  **自动过牌发的是一手真的 `pass`**:进走子历史、别人看得见「不要」、走与真人完全相同的路径。**不是「跳过这个座位」**——「连续两家过牌清桌」数的就是 `pass`,而一个不记 `pass` 的跳过会让同一件事有两种记法;服务端替走则需要一个新接缝,那是为一个便利动内核。

  **只做挖坑。** 斗地主的「压得住」要算炸弹、四带二、飞机带翅膀,而且炸弹跨型压,候选空间大一个量级 —— 挖坑没有炸弹,每一手合法牌都是「k 组等大的牌,k>1 时连续」,所以枚举是几十行。触发条件:斗地主也要这两个功能的那天,而那时该问的是「这两个棋种的『出得起』真是同一种东西吗」——今天的答案是**不是**。

  **变异测试逼出两处,而两处都是「测试测的是别的东西」。**

  1. **「一个回合最多过一次」那处变异第一次活了下来。** 同步的两次 `detectChanges` 里,第二次是被 `submittingMove` 挡住的 —— 那条测试测的是**另一个守卫**。两次之间 `await` 之后,哨兵才是唯一挡着的东西,它才红。
  2. **房间页那条测试的 `seatView` 我用字符串拼,留了个没被替换的 `%s`** → JSON 解不出来 → `parseView` 返回 `null` → **「不该自动过」那条因此也是绿的**,通过的理由是「解析失败」而不是「出得起」。改成 `JSON.stringify`,没有可拼错的东西。**一条负向断言在「什么都没发生」时同样是绿的,所以它需要一个能证明局面真的成立的前提。**

  两处计划外:`card-table.spec.ts` 用 `actionButtons(fixture).at(-1)` 拿「不要」按钮,而加一个「提示」按钮就把它指到了新按钮上 ——**一个按位置找元素的夹具,会在任何一次加按钮时静静指错**,改成按 `data-testid` 取。以及我先在牌桌里写了一份 `shouldAutoPass`、又在房间页写了一份 —— **同一个决定两个家**,删掉牌桌那份:牌桌只在用户点击时发动作,而「替他过牌」是页面的决定。

  后端 **1466** 绿、前端 **862** 绿、lint 干净、初始包 473.62 kB。**这两个功能没有在浏览器里跑过** —— 它们要一局真牌局走到「有人出了一手你压不住的牌」那一格,而那要三个账号配合;说清楚哪一半没量,比说「都验过了」诚实。

**挖坑 (wakeng) 已经 ship** —— 四个变更（`generalize-match-kickoff` / `hoist-card-model` / `add-wakeng-cards` / `add-wakeng`）把规则送进内核，`add-web-wakeng` 给它界面，而中途插了两笔它逆出来的债（`fix-lobby-seats` / `publish-seat-count`）。它是第一个**规则由用户以 URL 给出**、而不是由我拟的棋种；四处原文没说或自相矛盾的地方由用户定下（**A 不能进顺子**、**三家都不挖则首叫者兜底 1 倍**、**基数默认 1**、**首叫者亮的那张牌公开进 `seatView`**），详细记在 `add-wakeng-cards` 那一条里。

它与斗地主的差别不是「多几个牌型」,而是**几乎每一条都不同**:3 最大而不是王最大、**没有炸弹**、三条四条**不能带牌**、2 和 3 不能进顺子。所以它复用的是 `Card`(编码、花色、点数)与洗牌,而**大小与牌型是一整套自己的** —— 这与「一字棋复用 `NInARowRules`」是相反的一端:同一个家族里,牌可以共享,规则不行。

四处原文没说或自相矛盾的地方由用户定了下来,记在这里因为它们是**判断,不是推导**:

1. **A 不能进顺子。** 原文只排除了 3 和 2,却又说「因此连到 K 的顺子是最大的」—— 而 A 在它自己的大小表里比 K 大,所以那个「因此」只有在 A 也不能进顺子时才成立。用户确认按后者。
2. **三家都说不挖时,第一家挖,兜底 1 倍。** 原文没写。不是重新发牌。
3. **基数默认 1**,将来做成房间设置。
4. **首叫者亮的那张牌公开进 `seatView`。** 按规则它本来就是明示的,而服务端算得出 —— 客户端不该自己猜。

- [x] **`generalize-match-kickoff`** — 挖坑的第一个使能改动,而它找到了**内核第五处「对到目前为止的每个棋种都成立、于是被写死」的假设**:`Game` 的构造函数里是 `CurrentTurn = FirstSeat`(常量 0)。前四处是两个座位、颜色命名的胜负、开局设置、以及下一手是谁。

  五个现有棋种的先手都是**约定**(谁坐 0 号谁先);挖坑不是,它的先手是**发牌**决定的。`IFirstSeatRules.FirstSeat(MatchState) → int`,默认仍是 0,越界当场抛 —— 存下来会造出一局**谁都动不了**的棋,而它要到几十秒后由超时兜底才暴露,那时报的是超时,不是「首手座位是 99」。

  **一个绕过它的聪明办法被否掉并写进了规格**:把发牌旋转成「最小 ♣ 总在 0 号」在统计上等价、在体验上不等价 —— 那样同一个人每一局都先叫。写进规格是为了下一个人不必重新发现它是个坏主意。

  又是一个**单独的接口**而不是给 `IGameRules` 加成员,理由与 `IDealtGameRules` / `IPerSeatViewRules` 当初分出来时逐字相同。这已经是这个模式的第四次,而它每次都省掉五个骗人的实现。

  **变异三处、两个方向都红**:忽略 seam 7 红、默认改成 1 号 2 红、去掉范围校验 4 红。中间那一条是 `generalize-turn-flow` 给 `NextSeat` 留下的教训 —— **一个带默认含义的东西,只钉一边会让「默认被当成必选」悄悄通过**。两条注册表走查也刻意是两条:「没有人实现这个接口」与「于是每一局都从 0 号开始」是两件事,而只有后者会被那个「默认改成 1 号」的变异抓到。

  1304 后端测试绿(新增 10),**五个现有棋种一行不动**。

- [x] **`hoist-card-model`** — 一张牌不属于任何一个棋种。`Card` 从 `Games/Doudizhu/` 搬到 `Games/Cards/`,洗牌提成 `CardShuffle`,新增 `Card.SuitedDeck`(52 张,`FullDeck` 的子集而不是另一份构造)。**行为零改动**,由斗地主既有的 `The_encoded_deal_is_pinned` 保证 —— 那条测试把一个种子发出的整副牌写死成一个字符串,搬家之后仍然绿,而那就是「一个字节都没变」的可执行形式。没有它,这次重构只能是「看起来等价」。

  搬家的理由不是整洁:让挖坑 `using Gewu.Domain.Games.Doudizhu` 会让**一个棋种的命名空间成为另一个棋种的承重结构**,而下一个读代码的人有理由问「删掉斗地主会不会弄坏挖坑」。

  **`TetrisPieceSequence` 刻意不动。** 洗牌提出来是因为挖坑会让 Fisher–Yates 加 xorshift32 变成第三份副本;但俄罗斯方块那一份的存在理由是**客户端必须用 TypeScript 实现同一个算法**,而那份 TS 已经与它逐项对齐过(三个整袋、21 个方块)。让它去依赖一个叫 `CardShuffle` 的东西,是把「方块序列」说成「洗牌」。**共享要按「是不是同一件事」分,而不是按「代码长得像不像」分。**

  **一句只被一个实现验证过的注释,在第二个实现出现时才显出它是个巧合。** `CardRank` 的注释写的是「**数值就是大小顺序**,所以比大小是整数比较」—— 那只对斗地主成立。挖坑是 `3 > 2 > A > … > 4`,3 最大而不是最小。数值不能改(它是编码下标的来源,也就是持久化格式),所以改的是注释,并补了一条断言。

- [x] **`add-wakeng-cards`** — 挖坑的纯逻辑:大小、八种牌型、压牌、发牌、首叫权、计分。照 `add-doudizhu-cards` 的形状,**`git status` 里没有一个 `M`,只有 `A`**。

  **挖坑的牌型模型比斗地主简单得多,而那不是巧合:它没有带牌、也没有炸弹。** 于是每一手合法牌都是同一句话 —— **k 组等大的牌,k > 1 时点数连续**:k=1 得到单/对/三头/四头,k≥3 得到顺子/连对/飞机/火箭,而 **k=2 不是任何牌型**(那是「连牌 3 组起」的直接后果,不是特例)。一条规则覆盖八种;斗地主那边要单独处理三带一、四带二、飞机带翅膀,还要判「翅膀不能拆炸弹」。**同一个家族里,牌可以共享,规则不行** —— 而这次是规则更简单的那一边。

  「强弱」与「连续位置」刻意是两个函数:它们在 4–K 上给出同一个数,合起来能省几行,但强弱覆盖 13 个点数、连续性只覆盖 10 个,合并会让「A 算第 11 位」这种错悄悄成立。

  **变异八处,七处一次就红,一处活了下来 —— 而那一处是这次最值得留下的东西。** 「四头不是炸弹」那条测试**在四头真变成炸弹时照样是绿的**:去掉「同型」之后 `Beats` 还剩「同张数 + 更大」,而四头与它想压的东西**张数几乎从不相同**(4 对 3、4 对 2、4 对 1),于是每一条断言都因为**别的理由**通过。唯一能区分的形状是**同张数、不同牌型、而且压的那一手更大** —— `KKKK` 对 `4567`。**这是「断言的结论对、判据不对」的又一例**,而读那段测试的名字与断言,它看起来正是在说「四头不是炸弹」。

  首叫权那三条断言也是这个形状的正面例子:「比它更小的每一张梅花都在底牌里」「三个座位都当过首叫」「200 个种子里多数首叫牌是 ♣4」—— **从大往小扫也能通过前两条,但过不了第三条**。

  还有一次踩坑值得留着:测试 helper 里 `Parse("2c")` 写成 `(CardRank)int.Parse("2")`,而 `CardRank.Two` 的值是 **15** —— 那个枚举的数值是编码顺序。我在上一条 change 里刚写下这句话,几分钟后自己踩了进去。那个 helper 现在有一句 `Enum.IsDefined`:**它不是防御性编程,它是那次踩坑的可执行形式。**

  1357 后端测试绿(新增 45)。
- [x] **`add-wakeng`** — 挖坑的**规则接内核**。后端;没有 UI。**验收标准继承 `add-doudizhu` 并成立:`Gewu.Domain/Rooms/` 零改动**,`Games/Abstractions/` 只多一行 `GameKeys` 常量,五个现有棋种一行不动,DI 一行不动(它从 `BuiltInGameRules.All` 派生)。`WakengThroughRoomTests` 用真 `Room` 打一整局真挖坑。

  **它是 `generalize-match-kickoff` 那个 seam 的第一个真实现** —— 先手由**发牌**决定,而不是「谁坐 0 号」。斗地主证明了三个座位、隐藏信息、规则指名下一手能过同一个聚合;这一条加上「开局那一刻由发牌决定谁先动」。量到的:三个真账号坐满一个房间,`currentSeat == firstBidder == **2**`。**那个「不是 0」是这条证据的全部** —— 内核的默认值就是 0,所以首叫者恰好是 0 的那一局什么也证明不了。同一条前提在两处单测里是显式断言的,不是碰运气。

  **首出权归首叫者,不归挖坑者** —— 与斗地主相反(那边地主先出),而这是原文的话(「首叫权和首出权」)。两条叫分结束路径都**显式**指名那个座位,因为三家各叫一次时自然轮转恰好也落在他身上,而那是 **3 个座位 × 3 次叫分的巧合**;有人叫 3 时它会给错人。变异验过:改用自然轮转 7 条红。

  **三家都不挖时首叫者兜底 1 倍,于是挖坑没有流局。** 斗地主在同一条路径上是和局。这一条不是靠一个例子钉的,而是**穷举**:三家每一种合法叫分组合都走一遍,`GameResult.Draw` 一次都不出现 —— 一个照抄斗地主流局分支的实现在那里 9 条红。

  **`WakengMove` 是另一个类型,而不是复用 `DoudizhuMove`。** 两者语法一模一样 —— 而它们**产出不同的字符串、喂给不同的规则**,没有一段代码同时读两者:共享的只有形状。**形状相同不等于事实相同**,而「它们可以分歧」(挖坑哪天要 `bid:4`,斗地主一行不动)正是这条的检验。`Card` 当初必须提出去是因为挖坑**真的在用同一批值**(同样 52 张、同样的字母表、同一个 `DecodeMany`),那是一个事实;这不是。同一条判据 `hoist-card-model` 拒绝把 `TetrisPieceSequence` 并进 `CardShuffle` 时刚用过。

  唯一真正必要的那一小块提成了 `Games/Cards/CardPlay`:「畸形的牌是一次领域拒绝而不是 `FormatException`」。它修的是一条**量过的**缺陷(`play:!!!` 以未映射异常冒出去变成 **500**,客户端看到「服务器出错了」而实际是它自己发错了),而**一个需要被记得的 `catch` 会在第三个解析器那里被忘掉**。它 MUST 留在 move 层、MUST NOT 下沉到 `Card.DecodeMany` —— 两个 `Deal.Decode` 也调它,而它们**要的正是** `FormatException`:一份坏掉的发牌是**损坏的记录**,不是一步非法的棋。两个调用方要两种异常,所以映射只能在上面这一层,而这一条有测试(两个游戏各走一遍,加一条阳性对照)。

  **一条真缺陷,而它是「一个只被一个实现验证过的巧合」的第二次咬人。** 超时兜底照抄斗地主写成 `HandOf(seat)[0]`,注释还写着「手牌按大小升序」。手牌按 `Card` 的自然序排,而那是**编码**顺序(3、4、…、K、A、2)—— 恰好就是斗地主的大小顺序。挖坑是 `3 > 2 > A > … > 4`,于是**手上有 3 的时候 `[0]` 是最强那张**:托管会替人把最好的牌打掉。`hoist-card-model` 刚修过 `CardRank` 那句「数值就是大小顺序」,同一个巧合在**上面一层**又成立了一次。那条断言**带前提**:那手牌里必须有 3 或 2,否则编码序的第一张恰好也是最弱的,而两种实现给出同一个答案。变异验过:1 条红,而它是唯一那条。

  **六条走查按它们自己的注释改,第七条没有预告。** 前六条都在注释里写着「挖坑落地那天这条会红」;`The_unrated_games_are_tictactoe_and_doudizhu` 只是一份写死的名单,是收尾任务里那句「全库搜一遍别的硬编码棋种计数」翻出来的。**一份没有预告的名单,和一份有预告的名单,过期时长得一样;区别只在谁会去找它。**

  `GameSetupMigrationTests` 那条要求「第二个棋种要设置的那天……这笔账变大,该重新估」。重估的结论是**那句话把账算错了**:要修就是**一个**新迁移,它守的是那一**列**,不是游戏 —— 一个棋种和五个棋种的修复成本一模一样。棋种数量跟踪的是**暴露面**,而暴露面在没有部署时买不到任何东西。**结论不变、理由换了**,而那条断言的用途也变了:它不再度量账有多大,而是一份「带守卫的迁移得把哪些棋种的数据搬回来」的清单。

  **一条从来没有被实现过的 Scenario,本仓库同一个缺陷的第四次。** `game-rules-registry` 自 `add-doudizhu-visibility` 起就写着「恰好一个内置棋种实现 `IPerSeatViewRules`」,而 `backend/tests/` 下**一次都没有出现过这个接口名** —— 用阳性对照量的(同样的搜法必须搜得到 `IDealtGameRules`,它在四个文件里)。它的两个邻居各有一条真断言,所以它读起来像也有。前三次是 `web-board-skins` 抄的 11 个变量名、`web-shell` 数 sound pack 的那条、`web-idiom-chain` 的 375 px 断言。**一条没有实现的 Scenario 与一条错的 Scenario 在归档时长得一模一样**,而 `openspec validate --strict` 两者都放行。

  **一个量出来的坑,留给下一个做变异测试的人:恢复步骤会骗过编译器。** `shutil.copy2` 保留 mtime,于是恢复后的源文件比 `obj/` 里的产物**更旧**,MSBuild 的增量判断认为无事发生 —— `dotnet build` 报 **0 errors、什么也没编**,接着的 `--no-build` 测的是**变异体**。它表现成两条测试莫名变红。这一次红的恰好是一眼能认出的那两条,三分钟就查到了;**一个更隐蔽的变异会长得像一个真缺陷**。与本文件已记的「`--no-build` 会跑磁盘上碰巧存在的那份二进制」同族,而更阴:**一次成功而什么都没做的构建,和一次真的构建,长得一模一样。** 两条只杀一条测试的变异因此带强制重编重量了一遍,结果不变。

  验证:**1439** 后端测试绿(新增 82)、827 前端测试绿、七处变异全红且恢复后工作树逐字节校验一致。真 HTTP:`AiSmoke` **45** 条全过(新增步骤 10;退出码是**不经管道**测的 —— 一个管道会吃掉你想量的那个退出码)。三个真账号 + 一个围观者:手牌 16/16/16、两两交集 **0/0/0**、并集 **48**(另 4 张在底牌里,叫分阶段谁都看不见)、围观者手牌为空而公开计数在、房间 DTO 里搜不到 `setup`。三条真 SignalR 长轮询连接:`SayWord` 载着 `bid:3` 与 `play:E` 走通,挖坑者 16 → **20** 且底牌 `RTrv` 转为公开,另一家看到 `[16,20,16]` 而**一张都没看到**;`MakeMove(7,7)` 与「出一张不在手上的牌」都回 `invalid-move`。

  顺带记下三笔:`IGameRules.SeatCount` 的注释「现有实现全部为 2」自 `add-doudizhu` 起就是假的(改掉了);`WakengScoring.Settle` 与 `DoudizhuScoring.Settle` 一样**仍然没有生产调用方**,触发条件不变(平台需要一条**点数榜**的那天);以及 **`RoomSummaryDto`(大厅列表用的那个)至今只有 `Black` / `White`,没有座位列表**,所以三座位房间的第三个人在**大厅的房间行里**不出现 —— 与 `add-doudizhu-table-visuals` 在侧栏修掉的是同一个缺陷的**第三处**,触发条件是 `add-web-wakeng` 要给一个三座位棋种画大厅。AiSmoke 里那段声称这笔债已经付掉的注释也一并改对了:`SeatWire` 确实删了、`RoomStateDto.Seats` 确实有了,而那两条断言**照样是绿的**,因为它们看的是另一个 DTO。


Discipline: **do not start a new game until the previous one is archived.** Eight games × (rules + AI + UI + i18n + tests) will otherwise all rot half-finished. And the rule is narrower than the failure it needs to prevent: `enable-xiangqi-human-play` was not a game, so nothing stopped it sitting unarchived for 36 commits with the live spec contradicting the code. **A merged PR whose change directory is still in `openspec/changes/` is the signal** — check that list, because strict validation will not.

Deferred follow-ups, each with a reason:

- **An exception message still names an env var the runtime ignores.** Starting the API in Production without a signing key throws `Jwt:SigningKey is empty in Production. Set environment variable GOMOKU_JWT__SIGNINGKEY.` — and `fix-spec-api-ops-env-prefix` **measured** that the `GOMOKU_` prefix does not work: only the unprefixed `Jwt__SigningKey` is read. So the message sends whoever hits it to a variable the process will not look at. Found while starting a scratch API for `add-doudizhu-table-visuals`' browser verification (three env-var attempts: `Jwt__Key` — wrong key name; `Jwt__SigningKey` with a non-base64 value — `FormatException`; then base64 — worked).

  That earlier change fixed the *spec* and left the *runtime's own error text* saying the old thing, which is the same defect one layer over: **a documented config key the runtime silently ignores is worse than no documentation**, and an exception message is documentation that arrives at exactly the moment someone needs it. It is a one-line fix in `Program.cs` plus the assertion that the message names `Jwt__SigningKey`; it is deferred only because it is backend and unrelated to a card table. **Trigger: the next backend change of any size.**

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

- **The `bundle initial exceeded maximum budget` warning is gone, and the budget is now 480 kB** — 504.65 kB → **470.37 kB**, against a threshold tightened from 500 kB so the win cannot quietly erode (transfer 132.15 → 124.70 kB). See `close-bundle-budget` and `tighten-bundle-budget`. Headroom has moved three times and the trail is the useful part: 9.63 kB after `tighten-bundle-budget`, then **0.34 kB** after `add-doudizhu-table-visuals` (four skin blocks × ten new `--card-*` / `--felt-*` variables, and `board-skins.css` is eager), and the very next change hit the wall exactly as that note predicted — `add-card-sounds` built at **481.23 kB**. The fix was the option that note named: **make it lazy.** Measured by stubbing the three sound packs and rebuilding — 481.23 → 472.54 kB, so the packs were **8.69 kB of first paint** for audio that cannot play before the first user gesture. They are `import()`ed on demand now, and the budget sits at **473.29 kB with 6.71 kB free**. The budget fired five times across those two changes and was never raised. **The pattern worth copying: when it fires, ask what is eager that does not need to be, and measure it by stubbing rather than by reasoning.**

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
