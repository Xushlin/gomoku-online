## Why

`RoomsController` fills a missing game key with `?? GameKeys.Gomoku` on all three room endpoints. `CreateRoomRequest`'s doc comment states the reason:

> 这是本变更**唯一**的缺省值妥协……本变更不含 Web 客户端,**已发布的客户端**不会送这个字段,让它们从此建不出房是不可接受的回归。

**There are no published clients.** Nothing is deployed; the only client is `frontend-web/` in this repository, and it has never sent `gameKey` to `POST /api/rooms` or `GET /api/rooms` — not because it decided to default, but because the field was never plumbed through. The shim is not a compatibility layer. It is a hardcoded `"gomoku"` living on the server, where no reader of the client can see it.

That invisibility is the whole cost. The same decision — *this lobby is gomoku's* — is currently recorded three different ways along one page's data path:

| Call | How gomoku gets chosen | Visible in the client? |
| --- | --- | --- |
| `roomsApi.list()` | server default | **no** |
| `roomsApi.createAiRoom(name, diff, side)` | optional 4th arg, omitted | barely |
| `leaderboardApi.top(10, 'gomoku')` | explicit literal, with a comment | yes |

Only the third can be found by reading the front end. `generalize-lobby` has to replace all three with a route parameter, and it should start from a state where all three are the same shape and all three are legible.

There is a behavioural argument too, though it is the smaller one. When 成语接龙's lobby lists rooms with `?gameKey=idiom-chain` but its create button forgets the field, the player creates a gomoku room that never appears in the list they are looking at — a confusing "create did nothing" bug. A required parameter turns that into a 400 the first time it is run.

## What Changes

### `gameKey` becomes required on the three room endpoints

`POST /api/rooms`, `POST /api/rooms/ai`, and `GET /api/rooms` stop defaulting. A missing or empty key is a 400 from the existing validator, which already produces a field-named `ProblemDetails`.

Nothing else changes about the contract — the key was already required all the way down. `CreateRoomCommand`, `CreateAiRoomCommand` and `GetRoomListQuery` have carried a non-nullable `GameKey` since `add-tictactoe`, precisely so the Application layer would not guess. The shim was the one place still guessing, and it was guessing on behalf of nobody.

### The client says which game it means

`RoomsApiService.list(gameKey)` and `.create(name, gameKey)` gain a required parameter; `createAiRoom`'s optional `gameKey` becomes required. Every call site names its game. Today every one of them names `'gomoku'` — which is the point: **the hardcode moves from the server, where it is invisible, into the client, where the next change deletes it.**

`LobbyDataService` keeps its existing `GOMOKU_GAME_KEY` constant and now uses it for the rooms slice too, not just the leaderboard slice.

### `AiSmoke` is updated with it

`backend/smoke/AiSmoke` posts `{ name, difficulty }` and relies on the default. It is the only test in the repo that speaks the real transport, and CLAUDE.md credits it with catching the SignalR optional-parameter defect that every unit-test layer missed. Breaking it silently while removing a default is exactly the failure it exists to prevent, so it gets the field.

It is also already broken for other reasons and outside CI. This change does not adopt that problem — but it does record what was actually observed when run, rather than repeating the received description of it.

### What is deliberately left defaulting

Three other `?? GameKeys.Gomoku` defaults exist. Each was checked; each stays, for a different reason, and the reasons are the useful part.

`CreateAiRoomRequest.HumanSide ?? Stone.Black` — defaulting a side is a choice **within** the game the caller asked for; defaulting the game key changes **which game** you are playing. Omitting `humanSide` yields a reasonable reading of an incomplete request. Omitting `gameKey` yields a different product.

`UsersController` profile — **the default is used, on purpose.** `UsersApiService.getProfile(userId, gameKey?)` omits it on first paint, and its doc comment already says why: there, omission is *a meaningful value* ("show the server's default game"), whereas on the leaderboard page omission could only ever be a forgotten argument. Removing this default would break the profile page's first render. This is the one place where "nobody sends it" is false.

`LeaderboardController` — no front-end caller omits it, but `AiSmoke` does, and the failure mode differs in kind: a leaderboard is always rendered under a visible game name, so serving the wrong ladder is wrong *on screen*. A room created under the wrong key is wrong in the database and looks like nothing happened.

Being able to state a different reason for each is the test that the rule was applied rather than a pattern matched.

## Impact

- Affected specs: `room-and-gameplay` (the endpoint table's `gameKey?` and the backward-compatibility scenario), `web-lobby` (the `RoomsApiService` signatures).
- Affected code: `RoomsController`, `rooms-api.service.ts`, `lobby-data.service.ts`, `create-room-dialog.ts`, `create-ai-room-dialog.ts`, `game-capabilities.service.ts`, `AiSmoke/Program.cs`.
- **No behaviour change.** Every existing call site sends `'gomoku'`, which is what the server was substituting.
- Not in scope: the lobby itself. Routes, cards and page structure are `generalize-lobby`.
