## Why

The web app shows online count in the lobby hero ("13 players online") but never tells you *which* of those players is online when you land on a profile. The backend has had `GET /api/presence/users/{id}` since `add-presence-tracking` (returns `{ userId, isOnline }`) — we just never consumed it on the profile page. Adding a green/grey dot next to the username is the smallest, most visible polish: at a glance you know whether to start a conversation now or wait.

## What Changes

- **`PresenceApiService.getUserOnline(userId)`** added on the existing service. Returns `Observable<boolean>` (mapped from the wire `{ userId, isOnline }` to just the boolean for caller convenience — same pattern as the existing `getOnlineCount` mapping).
- **`UserPresenceWire`** type in `core/api/models/presence.model.ts` — `{ userId: string; isOnline: boolean }` — for parsing the wire response.
- **ProfilePage header card** gains a presence dot to the left of the username:
  - Green (`bg-success`, ~10 px round) when `isOnline === true`.
  - Grey (`bg-muted`) when `isOnline === false`.
  - Hidden until the request resolves (no flash). If the request fails, no dot is shown — failure is non-fatal, profile data still renders.
  - `aria-label` = translated `profile.online` / `profile.offline` for screen-reader users.
- **i18n** — new `profile.online` / `profile.offline` keys. en + zh-CN, parity zero drift.
- **Tests**:
  - `presence-api.service.spec.ts` — new test for `getUserOnline()`: GETs `/api/presence/users/{id}` and unwraps the `isOnline` field.
  - `profile-page.spec.ts` — extends existing tests: stubbed `PresenceApiService` returns `true` → dot has `bg-success`; returns `false` → dot has `bg-muted`; throws → no dot rendered.

Out of scope:
- Polling for live presence updates while on the profile page. A single fetch on mount is enough for v1; hot-reloading presence would need either polling or a presence subscription (neither is built). If users complain "I want to see them go online", revisit.
- Showing the dot on every username everywhere (lobby cards, leaderboard, sidebar, chat) — would need either bulk presence endpoint or N requests per page, both costly. Profile page is the highest-value single surface.
- "Last seen" timestamp — backend doesn't track it.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities

- `web-user-profile`: profile header card adds the presence dot. No behaviour change to the rest of the page or the games list.

## Impact

- **Modified files**:
  - `src/app/core/api/presence-api.service.ts` — add `getUserOnline(userId): Observable<boolean>`.
  - `src/app/core/api/models/presence.model.ts` — add `UserPresenceWire` interface.
  - `src/app/pages/users/profile-page/profile-page.{ts,html}` — fetch presence on mount, render dot.
  - `public/i18n/{en,zh-CN}.json` — `profile.online` / `profile.offline` keys.
- **Backend impact**: none.
- **Bundle**: trivial (~1 KB raw delta).
- **Enables**: a future bulk presence endpoint + leaderboard/lobby dot sweep, if user research justifies it.
