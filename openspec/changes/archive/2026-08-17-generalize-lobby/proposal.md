## Why

`/home` is gomoku's lobby. It has been since there was only one game, and five games later it still lists gomoku rooms, creates gomoku rooms, and shows the gomoku ladder — with no way to reach the same affordances for any other game. 一字棋 and 象棋 get bespoke human-vs-AI pages instead; 成语接龙, which genuinely needs human-vs-human, has nowhere to go at all.

The roadmap has carried this for four changes as "parameterising `/home` means rewriting a normative path in five web specs". That framing was the obstacle, and it turned out to be mostly wrong. Of the nine specs mentioning `/home`, most only assert that it is the post-login landing page and the brand link's target — both of which stay true. The genuinely blocking statements are four, and two of them are things this change is *supposed* to delete:

- `platform-catalog`: 「`/home` 仍是登录后的落地页与五子棋大厅」
- `web-leaderboard`: 「**`/home` 的排行榜卡片 MUST NOT 改动**」, with the duplicate-entry side effect explicitly logged as temporary
- `web-shell`: a scenario still naming `src/app/pages/home/home.ts`, a path that has not existed since `add-web-lobby` renamed it
- `web-lobby` itself

The backend never needed changing: room commands have carried a required `GameKey` since `add-tictactoe`, and `require-room-game-key` just made every client call site name its game. **What is left is routes and page structure.**

## What Changes

### One page becomes two, along a line already in the data

The lobby's seven cards divide cleanly, and the division is not invented — it is which endpoint each one calls:

| Scope | Cards | Endpoint takes a game key? |
| --- | --- | --- |
| **A game** | active rooms, play-vs-AI, leaderboard | yes |
| **An account** | hero (online count), my active rooms, my recent games, find player | no |

So `/g/:gameKey/lobby` takes the first group and `/home` keeps the second. `my-active-rooms` and `my-recent-games` are deliberately cross-game — `GET /api/users/me/active-rooms` answers "which games am I in right now", and across games is the correct answer, which is why that endpoint never took a key.

`/home` also gains a compact strip of playable games from `GAME_REGISTRY`, so landing still leads somewhere in one click. It is not a second catalogue: `/games` lists all eight including planned ones with descriptions and content-locale badges; the strip is a launcher for the available ones.

### `/home` stays eager; the game lobby is lazy

`web-lobby` requires `/home` to be in the initial bundle, reasoning that the post-login landing page should not cost a chunk round-trip. That rationale survives — but it now applies to a much smaller page, and the heavy half moves out. The initial bundle is currently **over budget** (~535 kB against a 500 kB limit), so this should help rather than cost. The change records the measured before/after either way; "should help" is a prediction, and predictions get checked.

### The data service splits, the engine does not

`DefaultLobbyDataService` is a generic polling-slice engine with four slices bolted on. The engine — dedup, visibility gating, half-interval catch-up, teardown — is shared and stays exactly one implementation. Only the slice sets differ: `HomeDataService` gets `onlineCount` + `myRooms`, `LobbyDataService` gets `rooms` + `leaderboard` scoped by an injected `LOBBY_GAME_KEY`.

Splitting rather than passing a key to one four-slice service matters for a reason that would otherwise show up as a bug report: `/home` would keep polling `/api/rooms?gameKey=gomoku` every 15 seconds for a card it no longer renders.

### A game without a lobby says so

`/g/:gameKey/lobby` is reachable for any key. For one that is unknown, or whose `supportsHumanVsHuman` is `false` (一字棋, 象棋), the page renders an explanatory panel with a link onward — **not** a redirect. A redirect would hide a mistyped URL by silently showing something else. The capability comes from `GameCapabilitiesService`, so the page holds a skeleton until `loaded()`, the same gate `remove-manifest-board` established.

This is a display decision, and `enforce-human-vs-human` is what lets it be only that: the server refuses to create such a room regardless of what the client renders.

## Impact

- Affected specs: `web-lobby` (the big one — `/home`'s contract splits in two), `web-leaderboard` (the pinned card and its logged duplicate-entry side effect both resolve), `platform-catalog` (`/home` stops being 五子棋大厅; gomoku's `launchRoute` moves), `web-shell` (a scenario naming a path deleted several changes ago).
- Affected code: `app.routes.ts`, `pages/lobby/**`, `core/lobby/**`, `games/gomoku/manifest.ts`, both locale files.
- **Split out deliberately:** `room-page` navigates to `/home` from five places (leave, dissolve, room-dissolved event, 404, game-ended dialog), and after this change that is wrong for every game — you finish a match and land on a page with no trace of it. The fix is small, but its two normative homes in `web-game-board` are among the longest requirements in the repo, and a MODIFIED delta must reproduce a requirement whole. Doubling this change's spec surface for an improvement that blocks nothing is the wrong trade. It ships next, as `lobby-return-target`.
- **Backend: zero changes.** The acceptance criterion inherited from `add-xiangqi` and `add-klotski` — `git diff --name-only` contains no `backend/` file.
- Not in scope: folding `/g/tictactoe` and `/g/xiangqi` into the lobby route as AI-only lobbies. Tempting, and it would delete two bespoke pages, but they have their own specs and their own board components; that is a separate change with its own argument.
- **What this change does not prove.** Only gomoku will use the parameterised lobby when it lands. This repo has learned four times that a seam shaped by one implementation is not general — and this is a fifth opportunity. The mitigation is structural rather than hopeful: a lobby is a page parameterised by a string, not an interface a game implements, so there is no polymorphism to get wrong. But 成语接龙 is what will actually test it, and until then that should be stated rather than assumed away.
