# add-tictactoe — design notes

## D1. Why a game nobody wants to play is the right next change

一字棋 has no audience. It is solved, it is trivial, and no adult plays it twice. That is precisely what makes it the correct second board game.

The value here is not the game — it is the **measurement**. `add-game-rules-registry` shipped an abstraction with one implementation, which proves nothing: every abstraction fits its only instance. The registry's real claim is "a new board game costs one class plus one registration", and the only way to find out is to add one.

Choosing a *hard* second game (中国象棋) would conflate two failure modes: "the registry is wrong" and "象棋 is genuinely complicated". 一字棋 has zero inherent complexity, so **every** line this change writes outside `NInARowRules("tictactoe", 3, 3, 3)` is registry debt, exposed and priced. The audit result is in the proposal: three leaks (room creation, lobby filtering, the AI layer), and one genuine success (`EasyAi` needed no change at all).

Consequence worth stating: if 一字棋 turns out to cost 800 lines, that is not an argument against shipping it. It is the change telling us what 象棋 would have cost silently.

## D2. Why `IsRated` is a flag and not a proper design

The correct model is per-game ratings — `UserGameStats(UserId, GameKey, Rating, Wins, …)`. That is `add-per-game-rating`, roadmap step 2, and it lands next.

Doing it *first*, before 一字棋 exists, would repeat the exact mistake this change is here to correct: designing a multi-game abstraction against a single game. A per-game rating table with one game key in it is as untested as a rules registry with one entry.

So the order is: add the second game, let it be unrated, then generalize the ladder with two real consumers. `IsRated` is the temporary lie that makes the ordering possible.

Guard against it becoming permanent:

- It carries a doc comment naming `add-per-game-rating` as the change that removes it.
- The spec requirement says the same, normatively.
- `add-per-game-rating`'s tasks will include deleting it. Recorded here because the change that must delete a flag is never the change that added it.

**Rejected alternative** — rate 一字棋 into the shared pool. This corrupts the one leaderboard that exists, and does so invisibly: a 五子棋 player's rank would move because someone else played 一字棋 against a bot. Also directly contradicts `add-ai-opponent` D7, whose anti-arbitrage argument assumes bot games are *worth* rating. Against a perfect 一字棋 bot they are worth nothing — every game is a draw.

**Rejected alternative** — no bot for 一字棋. Removes the arbitrage question but leaves the game unplayable solo, and the AI registry is exactly the piece this change most needs to exercise.

## D3. `gameKey` defaults to `"gomoku"` — the one place a default beats explicitness

Everywhere else this codebase prefers required parameters over defaults. Here the default is right, narrowly:

- `POST /api/rooms` body: `gameKey` optional → `"gomoku"`.
- `GET /api/rooms` query: `gameKey` optional → `"gomoku"`.

Without it, this backend-only change breaks the shipped web client, and `add-web-tictactoe` becomes a prerequisite rather than a follow-up. The two changes would have to merge, and the 400-line PR convention dies.

The default lives **only at the HTTP boundary**. `CreateRoomCommand.GameKey` and `GetRoomListQuery.GameKey` are required non-nullable — the Application layer never guesses which game it is being asked about. That keeps the compromise in one file per endpoint, where it is visible.

`add-web-tictactoe` makes the web client always send the key explicitly. The defaults stay afterwards for API clients, but nothing in-repo relies on them.

## D4. Unknown game key: 400 on create, empty list on query, 404 on move

Three endpoints, three different answers, and the difference is not arbitrary:

| Path | Unknown key | Why |
| --- | --- | --- |
| `POST /api/rooms` | **400** | No room exists yet. The caller sent a game that is not on this platform — a malformed request, caught by FluentValidation before any aggregate is loaded. |
| `GET /api/rooms?gameKey=…` | **empty list** | A lobby asking "what rooms exist for X" is correctly answered "none". Erroring a collection endpoint makes callers write a special case for a condition indistinguishable from an empty result. |
| `MakeMove` | **404** | The room *does* exist and names a game this build cannot resolve — corrupt data or a downgraded build. Already specified by `room-and-gameplay`; unchanged here. |

## D5. Reusing `EasyAi`, rewriting the other two

`EasyAi` already iterates `board.Rows` / `board.Cols` and picks a uniform random empty cell. Nothing about it is gomoku-specific, so 一字棋 registers the same instance. No new class, no new tests beyond one asserting it produces legal 3×3 moves.

`MediumAi` and `HardAi` are not reusable, and the reason is worth being precise about — it is not merely that they contain the constants `7` and `5`:

- `MediumAi:19` — `BoardCenter = 7`. Parameterisable in principle (`Rows / 2`).
- `HardAi:341` — `if (length >= 5) return TerminalWin`. Also parameterisable.
- But `HardAi`'s **candidate generation** restricts the search to cells within 2 of an existing stone, and its evaluation function scores open-three / open-four shapes. Both are gomoku *strategy*, not gomoku *parameters*. On a 3×3 board the neighbourhood restriction is a no-op and the shape vocabulary is meaningless.

Generalising them would mean inventing a parameterised pattern-scoring language that serves exactly two games, one of which does not need scoring at all. So: two small purpose-built classes instead.

`TicTacToeHardAi` is exhaustive minimax over the full tree — 5,478 reachable states, no depth limit, no α-β required for speed (though it is trivial to include), no evaluation function. Perfect play falls out of completeness rather than out of tuning, which makes it testable as a *property*: **the Hard bot never loses**, from any position, playing either side. That is a far stronger test than anything `HardAi` can assert about itself, and it is the payoff for picking a solved game.

Known consequence: a player cannot beat Hard. That is 一字棋, not a defect. Easy and Medium remain winnable, and the difficulty picker already exists.

## D6. Bot accounts are shared across games

`BotAccountIds` has three fixed Guids (Easy / Medium / Hard) seeded as users. 一字棋 reuses them rather than seeding three more.

Rationale: a bot account is an *identity*, not a strategy. Which algorithm it runs is resolved per-room from `(GameKey, Difficulty)` via the AI registry. Adding a game should not add rows to the users table — that would be the same "adding a game edits shared state" smell this change exists to remove.

This works today only because 一字棋 is unrated, so the shared bot accounts' ratings stay pure 五子棋 numbers. `add-per-game-rating` makes it work properly: the bot gets a `UserGameStats` row per game key, like everyone else.
