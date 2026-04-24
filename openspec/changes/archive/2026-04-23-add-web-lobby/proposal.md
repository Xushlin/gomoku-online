## Why

The web shell is live, auth works end-to-end — but `/home` is still a one-line "Welcome to Gomoku" placeholder. The backend already ships everything a lobby needs: `/api/presence/online-count`, `/api/rooms`, `/api/users/me/active-rooms`, `/api/leaderboard`, plus room create / join / dissolve. There is no reason to keep the placeholder; turning it into a real lobby is the unblocking step for `add-web-game-board` (you can't test "enter a room" without a room list to enter from).

This change also locks in the **REST-client pattern** (services under `src/app/core/api/`) that every subsequent web change will reuse. Getting the shape right once — typed DTOs, error handling through the existing auth interceptor, polling strategy — means lobby-adjacent features (profile, replay) copy a working template.

One important correction to my original wording: **SignalR is not wired in this change.** The backend specs show a single `/hubs/gomoku` hub scoped per-room — no lobby-level pushes (no room-list deltas, no online-count broadcast, no leaderboard stream). Every lobby concern is REST-only. Hub connection is correctly deferred to `add-web-game-board`, matching CLAUDE.md's "首次订阅时才连" rule and avoiding a connection the lobby doesn't need.

## What Changes

- **Replace the home placeholder with a real lobby** at `/home`. The route path stays `/home` so external links remain valid; the component is renamed `Home` → `Lobby` at `src/app/pages/lobby/`. The route remains eager (the lobby is the post-login landing page; shipping it in the main chunk avoids an extra round-trip at the moment of highest user impatience).
- **Four cards** that each own their own loading / empty / error state so a failure in one doesn't break the others:
  - **Hero** — translated greeting with `user().username` + live online-user count.
  - **Active rooms** — `GET /api/rooms` result, showing name, host, black/white seats, spectator count, status badge. Each row has a "Join" button for rooms in status `Waiting`; "Watch" button for status `Playing`. "Create room" primary button at card header opens a CDK Dialog.
  - **My active rooms** — `GET /api/users/me/active-rooms`. Shows rooms I'm currently playing/waiting in, with a "Resume" button per row. Client derives "I'm Black / I'm White" by comparing `user().id` to `room.black?.id` / `room.white?.id`.
  - **Leaderboard** — `GET /api/leaderboard?page=1&pageSize=10`. Top 10, with rank-based icons for the top three (`🥇 / 🥈 / 🥉` — or a themed SVG set if design pushback; icons are data-driven from `rank ≤ 3`, not from any server-side flag because backend doesn't emit one).
- **Polling strategy** (pull-only since backend emits no lobby pushes):
  - Online count: 30 s interval, paused when `document.visibilityState !== 'visible'`, re-fetched on focus.
  - Rooms list: 15 s interval, same visibility gating, + explicit refetch after own create.
  - My active rooms: 30 s interval, same rules.
  - Leaderboard: fetched once on mount (rankings change slowly; poll cadence would be waste).
- **`LobbyDataService`** (Signal-backed, one per route; not app-scoped) owns all four polling timers and exposes `onlineCount()`, `rooms()`, `myRooms()`, `leaderboard()` Signals plus `loading` / `error` state per slice. Cards are presentational; they inject the service via `providers` on the Lobby container so the service's lifecycle is tied to the page.
- **API clients** under `src/app/core/api/`:
  - `PresenceApiService` — `getOnlineCount()`.
  - `RoomsApiService` — `list()`, `create(name)`, `join(id)`, `myActiveRooms()`, `leave(id)`. (Full room operations will grow in `add-web-game-board`; this change ships what the lobby needs.)
  - `LeaderboardApiService` — `top(count)` thin wrapper over `getPage(page, pageSize)` for future pagination.
  - Each is an abstract-class-as-DI-token matching the `AuthService` / `ThemeService` pattern.
- **Typed DTOs** under `src/app/core/api/models/`:
  - `room.model.ts` — `RoomSummary`, `RoomStatus` (`'Waiting' | 'Playing' | 'Finished'`), `UserSummary`.
  - `presence.model.ts` — `OnlineCount`.
  - `leaderboard.model.ts` — `LeaderboardEntry`, `PagedResult<T>`.
- **Create-room dialog** via `@angular/cdk/dialog` — takes a name (3–50 chars, non-whitespace, matches backend validator), submit button disabled during inflight, maps 400 `ProblemDetails.errors["Name"]` to the form field using the existing `mapProblemDetailsToForm`. On success closes the dialog and triggers a rooms refetch.
- **Join flow**: clicking Join / Watch / Resume navigates to `/rooms/:id`. This route doesn't exist yet — `add-web-game-board` will own it. To avoid a dead-end, this change ships a thin `RoomPlaceholder` component at `/rooms/:id` that (a) calls `POST /api/rooms/:id/join` on mount if status was `Waiting`, (b) shows room name + host + your side + a "Leave room" button that calls the leave endpoint and navigates back to `/home`. It's explicitly a stub that `add-web-game-board` will replace — marked with a big comment + a task in that change's proposal. Without it the lobby's core "join" action is non-testable.
- **Header `/home` brand link**: already routed; no changes.
- **Auth gating**: the lobby route is already behind the scaffold's general routing; we explicitly add `canMatch: [authGuard]` to the `/home` route so anonymous users are redirected to `/login` rather than seeing an empty/crashing page. `/login` and `/register` stay under `guestGuard` — an authenticated user is redirected to `/home` (already the case).
- **Tests** (Vitest):
  - `LobbyDataService`: polling starts on first subscribe, respects visibility, refreshes on focus, slice error doesn't poison other slices.
  - `PresenceApiService` / `RoomsApiService` / `LeaderboardApiService`: each GET hits the right URL; create-room POSTs the right body; DTO parsing handles the wire shape.
  - Lobby integration: mounting the page fires the initial fetches; clicking "Create room" opens the dialog; dialog success refetches rooms.
  - Leaderboard rank → icon mapping.
- **i18n**: new `lobby.*` key tree — card titles, empty states, error retry CTAs, create-room dialog, status labels (`waiting`, `playing`), side labels (`black`, `white`, `spectator`). Both `en` and `zh-CN` updated; parity check stays at zero drift.

Out of scope (deferred):
- Any real-time room-list deltas (backend doesn't emit them; adding a lobby hub is a separate backend change).
- Leaderboard pagination UI — this change fetches page 1 only; full paged view is a follow-up.
- Full room/game-board experience — `add-web-game-board`.
- Profile and game-record pages — `add-web-replay-and-profile`.
- Room search / filter / sort — out of scope; rooms list is rendered in server's default order.

## Capabilities

### New Capabilities
- `web-lobby`: the lobby page contract — layout + cards + polling/visibility strategy, API service shapes for rooms/presence/leaderboard, the create-room dialog contract, the auth guard on `/home`, the thin `/rooms/:id` placeholder (marked as stub for `add-web-game-board`), i18n key structure for lobby copy, error / empty / loading state requirements per card.

### Modified Capabilities
<!-- None at spec level. web-shell's `home` route contract was "renders a placeholder showing i18n+theme working" which is superseded by this change's lobby, but the *requirements* in web-shell (lazy-load rule, 375px responsive, a11y baseline) are unchanged. The home placeholder was always understood to be a temporary tenant of the /home route. If review prefers to treat this as a modification to web-shell's home-route requirement we can add a delta, but my read is that's over-bookkeeping. -->

## Impact

- **New folders**:
  - `src/app/pages/lobby/` — `lobby.ts`, `lobby.html`, children `cards/hero/`, `cards/active-rooms/`, `cards/my-active-rooms/`, `cards/leaderboard/`, `dialogs/create-room-dialog/`.
  - `src/app/pages/rooms/` — thin `RoomPlaceholder` (gets replaced by game-board).
  - `src/app/core/api/` — `presence-api.service.ts`, `rooms-api.service.ts`, `leaderboard-api.service.ts`, `models/{room,presence,leaderboard}.model.ts`.
  - `src/app/core/lobby/` — `lobby-data.service.ts` + its spec.
- **Renamed**: `src/app/pages/home/` → `src/app/pages/lobby/`; `Home` class → `Lobby` (selector `app-lobby`). Route path `/home` unchanged.
- **Modified files**:
  - `src/app/app.routes.ts` — `/home` now `{ path: 'home', component: Lobby, canMatch: [authGuard] }`; add thin `/rooms/:id` lazy route.
  - `src/app/app.config.ts` — provide the three API services via abstract-class tokens.
  - `public/i18n/en.json` + `public/i18n/zh-CN.json` — append `lobby.*` keys (~30 keys each).
  - `src/app/pages/home/home.spec.ts` — retired along with the old Home; replaced by `lobby.spec.ts`.
- **No new dependencies** — everything uses Angular HttpClient (already wired), CDK Dialog (already installed in scaffold).
- **No backend impact** — every endpoint used is already shipped and specced.
- **Bundle size note**: the lobby is eager (per scaffold contract). Adding it grows the main chunk; if the post-change main chunk exceeds 250 KB gzipped, flag in the PR (initial scaffold was 98 KB gzip with auth; lobby's four cards + dialog + three services should land around ~140–160 KB gzip worst case). The 500 KB budget has plenty of headroom.
- **Follow-ups enabled**:
  - `add-web-game-board` replaces the `/rooms/:id` placeholder with the real board + SignalR wiring.
  - `add-web-replay-and-profile` can link from leaderboard entries to profile pages (by `entry.userId`).
  - A future change can add a lobby-wide SignalR hub for room-list deltas if polling proves insufficient.
