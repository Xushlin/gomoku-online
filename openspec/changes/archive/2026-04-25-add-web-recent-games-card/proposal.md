## Why

Lobby today shows what I'm *currently* doing (My active rooms = Waiting/Playing) but not what I *just finished*. After every game ends and I bounce back to lobby, my last results disappear behind a "View profile → Games" detour. That's the home page that should celebrate (or commiserate) my last few moves. The data is one call away — `GET /api/users/{id}/games?page=1&pageSize=5` already paginated by `EndedAt DESC`, shipped via `add-public-profile-and-search` and the `UsersApiService` we built in `add-web-replay-and-profile`.

This change adds a "Recent games" lobby card showing the 5 most recent finished games for the logged-in user, each row clickable into the replay player.

## What Changes

- **New `MyRecentGamesCard`** at `src/app/pages/lobby/cards/my-recent-games/my-recent-games.{ts,html}` + spec.
  - Inject `AuthService`, `UsersApiService`, `Router`. Read `userId = auth.user()?.id` (always set inside lobby — `home` is `canMatch: [authGuard]`).
  - On construction, call `users.getGames(userId, 1, 5)`. No polling — refetch only on explicit user action / route navigation. Most users won't finish a game while on lobby, and if they do, navigating into the room and back triggers a fresh load.
  - Render up to 5 rows: opponent username (link to `/users/:opp.id`, `.username-link` + stopPropagation), result-from-this-user's-perspective ("Won" / "Lost" / "Drew"), end reason translated, ended-at via `formatDate`. Whole row is a button → `/replay/:roomId`.
  - "View all" footer link → `/users/:userId` (the logged-in user's own profile).
  - Standard four-state UI: loading skeleton (3 placeholder rows) / empty (translated `lobby.recent-games.empty` + helpful copy) / error (translated message + retry button) / data.
- **Slot into lobby** — between `MyActiveRoomsCard` and `AiGameCard` in the right column. Order on the right column becomes: my-active-rooms → recent-games → ai-game → find-player → leaderboard.
- **i18n** — new `lobby.recent-games.{title, view-all, empty, error}` subtree. en + zh-CN, parity zero drift.
- **Tests** (Vitest):
  - `my-recent-games.spec.ts`: fetches `users.getGames(meId, 1, 5)` on mount; renders rows; row click navigates to `/replay/:roomId`; opponent username click goes to `/users/:opp.id` (stopPropagation); empty state shows `lobby.recent-games.empty`; "View all" link points to `/users/:meId`.

Out of scope:
- Polling / realtime update (low value — recent games change only when user finishes one, and the navigation pattern naturally refreshes).
- Filter / sort options — five most recent, end of story.
- Result-color coding (green/red/grey) — keep parity with the `GamesList` row that already shipped (which doesn't colour-code either).
- "Best win" or "longest streak" stats — separate concern, would need backend support.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities

- `web-lobby`: adds the recent-games card to the dashboard right column. No polling cadence change. No modification to existing cards.

## Impact

- **New folders / files**:
  - `src/app/pages/lobby/cards/my-recent-games/my-recent-games.{ts,html}` + spec
- **Modified files**:
  - `src/app/pages/lobby/lobby.{ts,html}` — import + slot the new card.
  - `public/i18n/{en,zh-CN}.json` — `lobby.recent-games.*` subtree.
- **Backend impact**: none — `GET /api/users/:id/games` and `UsersApiService.getGames` already exist.
- **Bundle**: tiny — one new card component. Lobby chunk grows ≈ 2 KB gzip.
