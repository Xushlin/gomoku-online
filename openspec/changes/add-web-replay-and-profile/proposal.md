## Why

`add-web-game-board` shipped the live game and ended at the `GameEndedDialog` — but four user journeys behind it sit on already-shipped backend endpoints with no web UI:

- "I just lost — let me see how it ended": no replay. The `GameEndedDialog` only offers Back-to-lobby / Stay; clicking through history is impossible.
- "Who is this player?": every `Username` rendered in lobby cards / leaderboard / chat / room sidebar is a plain text node. No way to inspect anyone's rating, W-L-D, or recent games.
- "How did Alice climb to #1?": leaderboard shows top 10 players but you can't drill into their actual matches.
- "Find a friend to play": no search box anywhere; the only way onto someone's profile would be to bump into them in a room.

The backend already covers all four through endpoints landed by archived changes (`add-game-replay`, `add-public-profile-and-search`):

- `GET /api/rooms/{id}/replay` → `GameReplayDto` with full `Moves[]` and meta. 409 if Playing/Waiting.
- `GET /api/users/{id}/games?page=&pageSize=` → `PagedResult<UserGameSummaryDto>`, `EndedAt DESC`.
- `GET /api/users/{id:guid}` → `UserPublicProfileDto` (rating, W-L-D, joined-at).
- `GET /api/users?search=&page=&pageSize=` → `PagedResult<UserPublicProfileDto>`, prefix StartsWith, bots filtered.

This change wires those four into the web app via two new lazy routes (`/replay/:id`, `/users/:id`), one lobby card (Find player), and a sweep that converts every existing `Username` text node into a clickable link to `/users/:id`. The `Board` component built in `add-web-game-board` already has a `readonly` input — the replay player is its first real consumer.

## What Changes

- **New route `/replay/:id`** at `src/app/pages/replay/replay-page/`. Lazy `loadComponent`, `canMatch: [authGuard]`. Fetches `GET /api/rooms/{id}/replay` on init. Renders the existing `Board` component in `[readonly]="true"` mode plus a scrubber: ▶ play / ⏸ pause / ⏮ first / ⏪ prev / ⏩ next / ⏭ last, plus a slider tied to `currentPly` (0..moves.length). Auto-play steps at ~700 ms/move with a configurable speed selector (0.5×, 1×, 2×). 404 → translated "replay not available" + back-to-home; 409 → translated "game still in progress" with link to the live room. Title bar shows match meta (room name, Black vs White usernames as links, end reason, ended-at).
- **New route `/users/:id`** at `src/app/pages/users/profile-page/`. Lazy, `authGuard`. Header card: username, rating, W-L-D, win rate (computed: `wins/(wins+losses+draws)` rounded), joined-at relative date. Below: a paginated games list (`GET /api/users/:id/games`), each row showing opponent username (link), result-from-this-user's-perspective ("Won" / "Lost" / "Drew"), end reason, ended-at, move count. Each row clicks through to `/replay/:roomId`. Pagination: prev/next page buttons + "page N of M". 404 → "user not found" + back-to-home.
- **`UsersApiService`** — new abstract-class-as-DI-token pattern (`src/app/core/api/users-api.service.ts`). Methods:
  - `getProfile(userId): Observable<UserPublicProfileDto>`
  - `getGames(userId, page, pageSize): Observable<PagedResult<UserGameSummaryDto>>`
  - `search(query, page, pageSize): Observable<PagedResult<UserPublicProfileDto>>`
- **`RoomsApiService.getReplay(roomId)`** added to existing service (replay sits naturally with rooms; matches `getById` / `resign` etc.).
- **Typed DTOs** in `src/app/core/api/models/`:
  - `user-profile.model.ts` — `UserPublicProfileDto`, `UserGameSummaryDto`, `PagedResult<T>`.
  - `room.model.ts` — extend with `GameReplayDto` (= `RoomState` minus chat/spectators/status, plus guaranteed-non-null `endedAt` / `result` / `endReason`).
- **New lobby card "Find player"** (`src/app/pages/lobby/cards/find-player/`) — input box debounced 250 ms; on input, calls `users.search(prefix, 1, 5)`. Dropdown shows top 5 matches; click → navigate to `/users/:id`. Empty input shows nothing. ≥3 chars before searching to avoid noise; below that, helper text "type 3+ characters". Loading / error / empty states like every other lobby card.
- **Username-click linking sweep**: every place that currently renders a username as plain text becomes a `<a [routerLink]="['/users', user.id]">`:
  - `cards/active-rooms/` — host, black, white seat names
  - `cards/my-active-rooms/` — same
  - `cards/leaderboard/` — top-10 player names
  - `pages/rooms/room-page/sidebar/` — host, black, white
  - `pages/rooms/room-page/chat/chat-panel/` — sender username
- **Game-ended dialog** gets a "View replay" secondary button (between Stay and Back-to-lobby) → navigates `/replay/:roomId` with the current room id. Only shown after `GameEnded` has fired (which, in the dialog's case, is always).
- **Header search** — explicitly out of scope (header is busy; lobby card is enough for v1; can add later).
- **i18n** — new `replay.*` and `profile.*` trees in `public/i18n/en.json` + `zh-CN.json`. New `lobby.find-player.*` subtree. New `game.ended.view-replay`. Parity check stays at zero drift.
- **Tests** (Vitest):
  - `users-api.service.spec.ts` — happy-path GETs for profile / games / search with proper query encoding.
  - `rooms-api.service.spec.ts` — extend with `getReplay()` test.
  - `replay-page.spec.ts` — fetches on init, renders board read-only, prev/next/play/pause/seek update `currentPly`, end states (404, 409).
  - `profile-page.spec.ts` — fetches profile + first page of games, renders header card, pagination next/prev increments query param, row click navigates to /replay/:id.
  - `find-player.spec.ts` — debounced input, ≥3 char threshold, dropdown renders results, click navigates.
  - Update existing card / sidebar / chat-panel specs only as needed (router-link stub).

Out of scope:
- "Recent games" widget on the lobby home (separate change if asked for).
- Annotation / commenting on replay positions.
- Game export / shareable links.
- Header-located search input.
- "Online status" dot on profile (presence integration is separate).
- Sort options on the games list (default `EndedAt DESC` is enough for v1).

## Capabilities

### New Capabilities

- `web-replay`: `/replay/:id` route, the move-scrubber UX, replay rehydration via REST, board read-only consumer of the existing component.
- `web-user-profile`: `/users/:id` route, public profile card, paginated games list, the lobby Find-player card + search service, username-click linking convention.

### Modified Capabilities

- `web-game-board`: `GameEndedDialog` gains a "View replay" button. Username-text occurrences in `RoomSidebar` and `ChatPanel` become links to `/users/:id`. No game-loop semantics change.
- `web-lobby`: Adds the "Find player" card to the dashboard layout. Existing card-grid + polling cadence unchanged. Username text in active-rooms / my-active-rooms / leaderboard becomes links.

## Impact

- **New folders / files**:
  - `src/app/core/api/users-api.service.ts` + spec.
  - `src/app/core/api/models/user-profile.model.ts` (`UserPublicProfileDto`, `UserGameSummaryDto`, `PagedResult<T>`).
  - `src/app/pages/replay/replay-page/replay-page.{ts,html}` + spec.
  - `src/app/pages/users/profile-page/profile-page.{ts,html}` + spec, plus `games-list/` sub-component.
  - `src/app/pages/lobby/cards/find-player/find-player.{ts,html}` + spec.
- **Modified files**:
  - `src/app/app.config.ts` — provide `UsersApiService`.
  - `src/app/app.routes.ts` — add `/replay/:id` and `/users/:id` lazy routes (both `canMatch: [authGuard]`).
  - `src/app/core/api/rooms-api.service.ts` — `getReplay(roomId): Observable<GameReplayDto>`.
  - `src/app/core/api/models/room.model.ts` — add `GameReplayDto` interface.
  - `src/app/pages/lobby/lobby.html` — slot the new card into the grid.
  - 5 places where usernames render today → `routerLink` swap.
  - `src/app/pages/rooms/room-page/dialogs/game-ended-dialog.{ts,html}` — extra button.
  - `public/i18n/en.json` + `public/i18n/zh-CN.json` — new key trees.
- **Backend impact**: none — all four endpoints already in production specs.
- **Bundle**: two new lazy chunks (`replay-page`, `profile-page`); each well under the 200 KB gzip ceiling. Lobby chunk grows by one card (~2 KB gzip).
- **Enables**: a future "share replay link" change can deep-link to `/replay/:id?ply=N`; the present design's `currentPly` signal is route-bindable later.
