## Context

Web scaffold + auth are live. `/home` renders a welcome card; the backend already ships the four endpoints a lobby needs (`presence/online-count`, `rooms`, `users/me/active-rooms`, `leaderboard`), plus create/join/leave/dissolve. This change replaces the placeholder with a real lobby — three things to get right:

1. **Data freshness without backend pushes.** Backend specs don't emit lobby-level SignalR events. Room-list deltas, online-count broadcasts, and leaderboard updates don't exist. So "real-time" has to be simulated with polling, and polling has to respect visibility (no wasted requests from backgrounded tabs) and honour explicit invalidation (the moment I create a room, the list should refresh without waiting for the interval).
2. **Card isolation.** If the leaderboard endpoint errors, the lobby still needs to render the room list. Each card owns its loading / empty / error slice. A retry in one card doesn't refetch others.
3. **The join-room dead end.** Clicking "Join" without `add-web-game-board` would either silently do nothing or send the user to a 404. Neither is a good baseline. The design ships a thin `/rooms/:id` placeholder that lets users `join → observe they're in the room → leave`, proving the full join flow end-to-end even before the board lands.

Everything else (layout, theme, i18n, guards, interceptor, services) is an application of patterns already locked by `add-web-scaffold` and `add-web-auth-pages`.

## Goals / Non-Goals

**Goals:**

- Replace `/home` placeholder with a lobby that feels live — sub-minute freshness on everything except the leaderboard.
- Establish the REST-client shape (`src/app/core/api/*.service.ts` as abstract DI tokens) so every later web change copies it.
- Make the lobby completely usable end-to-end: I can sign in, see rooms, create a room, see it appear in my active rooms, open it, and leave it — all without `add-web-game-board`.
- Each card fails independently. No card takes down the page.
- Polling pauses when the tab is hidden; resumes + immediate refetch on refocus.
- `LobbyDataService` is page-scoped, not app-scoped — destroying the Lobby component stops the timers.

**Non-Goals:**

- SignalR hub connection. The hub is per-room; lobby has nothing to subscribe to.
- Leaderboard pagination UI. Backend supports it; this change fetches page 1 only.
- Room search / filter / sort. Server returns a sensible default order.
- Real-time room-list deltas. Polling is the baseline; a lobby-wide push channel is a future backend change.
- A full game-board experience. Explicitly `add-web-game-board`'s job.
- Visual design polish beyond "uses tokens, looks ok at 375px and 1440px". No illustrations, no micro-animations.

## Decisions

### D1. Layout: four cards in a responsive grid

**Decision:** The `Lobby` component renders:

- A full-width **hero strip** (welcome + online count).
- Below, a `grid-cols-1 lg:grid-cols-3` grid:
  - **Active rooms card** — spans 2 columns on `lg` (`col-span-2`), full width below.
  - **Right column** (stacked) — `My active rooms` card on top, `Leaderboard` card below. Single column on mobile.

Cards are fixed order on mobile (hero → my rooms → active rooms → leaderboard) so the thing most likely to be relevant (the game I'm already in) is reachable without scroll past a 20-row room list.

**Rationale:** Putting "Active rooms" in the wider column matches the truth that it's the largest table; putting "My active rooms" at the top-right on desktop keeps the user's in-progress game one glance away on a standard 1440px monitor. Mobile order shifts "My active rooms" above "Active rooms" because the mobile user is more likely to be returning to a match than browsing.

**Alternatives considered:**

- **Tabs** (Rooms / My rooms / Leaderboard). Rejected: hides information behind clicks that the desktop user has screen space for; worse parallel visibility.
- **Masonry or drag-to-rearrange.** Over-engineered for an MVP lobby.

### D2. `LobbyDataService` — page-scoped Signal store with per-slice polling

**Decision:**

- `LobbyDataService` is provided in the `Lobby` component's `providers: [LobbyDataService]` array, not app-wide. Destroying the component destroys the service, which stops all timers.
- Four independent "slices": `onlineCount`, `rooms`, `myRooms`, `leaderboard`. Each slice exposes `{ data: Signal<T | null>, loading: Signal<boolean>, error: Signal<unknown | null> }` plus a `refresh()` method.
- Polling intervals: online 30 s, rooms 15 s, myRooms 30 s. Leaderboard: no polling (fetch once on `ensureLoaded()`).
- Polling gated by `document.visibilityState === 'visible'`. On `visibilitychange` → `visible`, immediately `refresh()` any slice whose last-successful-fetch is more than half the interval old.
- `refresh()` is a no-op if a fetch for that slice is already in flight (prevents stacked polls from double-firing under network hiccups).

**Rationale:**

- Per-slice state means a leaderboard 500 doesn't grey out the rooms list.
- Page-scoped lifecycle means `onDestroy` → `teardown()` → `clearInterval` → `visibilitychange` listener removed. No leaks.
- Visibility gating is the single most important "don't be rude to the backend" move. A user with the tab in the background for an hour shouldn't have hit the endpoint 240 times.
- Refresh-on-visible avoids the bad UX of "I just came back and the data is 30s old" while not going to the other extreme of firing on every focus event.

**Alternatives considered:**

- **One global `RxJS` interval with switchMap per slice.** Works but ties lifecycle to the Observable chain; harder to reason about. Signals + `setInterval` inside the service is simpler.
- **Server-Sent Events.** Backend doesn't emit any lobby channel; would require a backend change first.
- **App-scoped service.** Rejected: the lobby is one route. Keeping the service out of the app scope means when the user is on `/account/password` we're not polling for no reason.

### D3. Polling cadence — why these numbers

**Decision:** 15 s for rooms, 30 s for online / my rooms, never for leaderboard.

- **Rooms (15 s)**: this is the list that churns fastest (someone creating / dissolving). 15 s is a balance between "feels fresh" and "not spamming". A 5 s interval would matter in a busy game night but for current scale is wasteful.
- **Online count (30 s)**: cosmetic. Users don't make decisions based on whether online count is 47 or 48. 30 s is fine.
- **My active rooms (30 s)**: likely to change only when *I* act (leave / dissolve). The 15 s rooms poll happens to often catch the same change via implicit knowledge. 30 s is sufficient.
- **Leaderboard (no polling)**: rankings change on game completion; in practice you'd see 1-2 rank swaps per hour at MVP scale. Not worth the polling overhead.

All numbers are easily adjusted in a config; the point of the design is "polling is configurable, not constants buried in components."

**Rationale:** These numbers are defensible but not load-tested. They're conservative starting points; a later change can tune based on observed traffic. They're exposed as a `LOBBY_POLLING_CONFIG` InjectionToken so tests can override to zero / infinity / a custom value without mocking timers.

### D4. API client shape

**Decision:** Three new services, each an abstract class as DI token + a default concrete implementation, same pattern as `AuthService`:

```ts
export abstract class RoomsApiService {
  abstract list(): Observable<readonly RoomSummary[]>;
  abstract myActiveRooms(): Observable<readonly RoomSummary[]>;
  abstract create(name: string): Observable<RoomSummary>;
  abstract join(roomId: string): Observable<RoomState>;
  abstract leave(roomId: string): Observable<void>;
}

@Injectable()
export class DefaultRoomsApiService extends RoomsApiService { /* HttpClient calls */ }
```

DTOs at `src/app/core/api/models/`:

- `room.model.ts`: `RoomSummary`, `RoomStatus`, `UserSummary`, `RoomState` (full state; used by `/rooms/:id` placeholder and later by game board).
- `presence.model.ts`: `OnlineCount`.
- `leaderboard.model.ts`: `LeaderboardEntry`, `PagedResult<T>`.

Field names follow the wire (camelCase). The `RoomSummary` shape is locked to what the backend actually emits — an implementation task verifies against `backend/src/Gomoku.Api/Common/DTOs/RoomSummaryDto.cs` (or equivalent) before shipping, because the OpenSpec spec didn't pin the field names inline.

**Rationale:**

- Same DI pattern the rest of the codebase uses; tests can stub any service.
- DTOs live alongside the service that returns them; a later module (game-board) can import `RoomState` without importing the service.
- `Observable<readonly T[]>` because the service shouldn't be mutating its callers' array.

**Alternatives considered:**

- **Monolithic `ApiService`.** Rejected: 50+ endpoints by project end; SRP matters here.
- **Class per endpoint (`ListRoomsQuery` / `CreateRoomCommand`).** Matches backend CQRS style but creates dozens of files. Abstract-service-per-resource is the Angular idiom.

### D5. Join flow and the `/rooms/:id` placeholder

**Decision:** Clicking "Join" (on a Waiting room) or "Resume" (on My active rooms) or "Watch" (on a Playing room):

1. If the row is not already in "My active rooms" with the user as a player → call `POST /api/rooms/:id/join` (for spectate, `POST /api/rooms/:id/spectate`).
2. Regardless, `router.navigate(['/rooms', roomId])`.

`/rooms/:id` is a new lazy route owned (temporarily) by this change. The `RoomPlaceholder` component:

- On mount: `RoomsApi.getById(id)` → shows `{ name, host.username, my side / "spectator" }`.
- "Leave room" button → `POST /api/rooms/:id/leave` (or dissolve if host-of-waiting) → navigate `/home`.
- Bold notice at the top: `lobby.placeholder-coming-soon-banner` translated copy that says game board is coming. Placeholder is explicitly a stub.

**Rationale:**

- Without this, "Join" is either a dead button or a silent no-op. Both are worse than a throwaway page.
- Add-web-game-board will replace this component in-place. The route path and the join navigation stay; only the rendered component changes. No coupling.
- The placeholder still exercises the real endpoints, so a misfire (wrong URL, wrong auth) surfaces here, not during game-board work.

**Alternatives considered:**

- **Disable the Join button.** Rejected: you can't demo the lobby.
- **Modal preview of room state.** Rejected: half-built modal is worse than a full-page placeholder; navigation semantics will match the game-board page later.
- **Skip routing, just call join API then toast "joined".** Rejected: no way to leave; inconsistent with future flow.

### D6. Top-3 leaderboard icons are client-side

**Decision:** The server returns `rank: number` in each `LeaderboardEntry`. The client renders an icon based purely on `rank === 1 | 2 | 3` (gold, silver, bronze). Display strings come from translation keys (`lobby.leaderboard.tier-gold|silver|bronze` are accessibility labels for screen readers; visual icon comes from a small set of `<span>` with Unicode medal characters or — if a themed look is preferred — inline SVGs in a `leaderboard-icons.ts` data module that lives in the `themes/`-style exception to the grep rules).

**Rationale:**

- Zero server coupling: the backend can add / remove tier flags later without the client changing.
- Unicode `🥇 🥈 🥉` is a free default that renders on every platform, no asset loading.
- `aria-label` via translation keys keeps screen readers happy.

**Alternatives considered:**

- **Server-emitted tier flag.** Rejected: unnecessary backend change.
- **SVG sprite sheet.** Can be layered on later if Unicode medals don't theme well in dark mode (they don't invert — should be fine against the card background, verify manually).

### D7. Polling config as InjectionToken

**Decision:** Export `LOBBY_POLLING_CONFIG = new InjectionToken<LobbyPollingConfig>('...')` with a default value and let tests / future features override:

```ts
export interface LobbyPollingConfig {
  readonly onlineCountMs: number;   // default 30_000
  readonly roomsMs: number;         // default 15_000
  readonly myRoomsMs: number;       // default 30_000
}
```

**Rationale:** Tests override to `0` or very large values to inspect "does the service poll" vs "does the service NOT poll when hidden" without timer magic. Production can override via an env-driven provider later if we want environment-specific cadences.

### D8. What the placeholder component does NOT do

- Does **not** open a SignalR connection. That's `add-web-game-board`.
- Does **not** show the board. A prominent banner says "Game board coming soon."
- Does **not** allow sending chat or urge. Those SignalR-only actions have no placeholder.
- **Does** allow leave (real REST call). Otherwise the lobby's join button is a trap.

### D9. i18n and accessibility

- `lobby.*` tree in both `en.json` and `zh-CN.json`. Zero-drift parity enforced by the same node-eval check used in `add-web-auth-pages`.
- All interactive controls have `aria-label` via translation (`lobby.rooms.join`, `lobby.rooms.create`, `lobby.leaderboard.tier-gold`, etc.).
- Rank icons are `aria-hidden="true"`; the adjacent translated tier label carries the semantic meaning.
- Empty states and error states per card have text and a retry button. No silent loading — every card shows a skeleton / spinner state when its `loading` signal is true, so there is no layout jump as data arrives.

## Risks / Trade-offs

- **Risk: polling load at user growth.** Mitigation: visibility gating + `refresh()` no-op-if-in-flight keep the per-tab rate capped at ≤ 1 req / 15 s for rooms and ≤ 1 req / 30 s for the rest. At 100 concurrent active tabs that's ~10 rps for rooms, 7 rps for others — well within any backend. At 10k tabs revisit with a push channel.
- **Risk: placeholder `/rooms/:id` diverges from what game-board needs.** Mitigation: placeholder uses only `RoomsApi.getById()` + `leave()`; game-board adds behaviour, doesn't refactor the route. Replacement is a component swap, not a route redesign.
- **Risk: `RoomSummary` DTO field names guess wrong.** Mitigation: a task verifies against the actual backend source before shipping; TypeScript error surfaces mismatch at build time because handlers type the full chain.
- **Risk: `LobbyDataService` page-scoped — test ergonomics.** Mitigation: service is provided via DI at the component, so `TestBed.createComponent(Lobby)` gets its own instance automatically; no global state to reset between cases. Config is an `InjectionToken` so tests set polling to `0` to freeze time.
- **Risk: unicode medal icons look bad in one of four themes.** Mitigation: manual visual sweep in §11 of the tasks; if they clash, swap for an SVG set from `src/styles/themes/` — one-file change, no API shift.
- **Trade-off: eager-loaded lobby grows main chunk.** Accepted. Lobby is the post-login landing page — users always hit it — and eager means no extra round-trip at the moment of highest impatience. If main chunk exceeds 250 KB gzip, we revisit and split the leaderboard card (least-used) into a lazy island.
- **Trade-off: no room-list real-time.** Accepted. Users don't expect "room created on the other side of the world" to appear in < 15 s. If that assumption turns out wrong, we add a push channel.
- **Trade-off: `/rooms/:id` placeholder is throwaway code.** Accepted — ~30 LOC, removed cleanly when game-board lands. Preferable to a dead join button.

## Migration Plan

- Home placeholder → Lobby. Route path `/home` unchanged; external links keep working.
- Old `src/app/pages/home/` folder is moved to `src/app/pages/lobby/` and its component renamed. Nothing else imports the `Home` class so the rename is local. Tests under `pages/home/` are rewritten against `Lobby`.
- Auth guard added to `/home`: anonymous users who had bookmarked `/home` will now see `/login?returnUrl=/home`. Slight behaviour change, but the scaffold never made any claim that `/home` was anonymous-accessible — and the page now actually needs auth for its API calls.
- Rollback: revert the commit. The backend endpoints remain shipped; nothing outside this change depends on lobby code.

## Open Questions

- **Q1**: Do we want a "refresh" button per card in addition to automatic polling? Design says no (polling + visibility focus is enough), but user testing may prove otherwise. Decide when a real user looks at the page.
- **Q2**: Empty-state illustrations — Unicode emoji placeholder ("No rooms yet — create one to get started 🎯") or SVG? Leaning emoji for MVP; design later.
- **Q3**: Should clicking a leaderboard entry navigate to `/users/:id` (profile)? Deferred to `add-web-replay-and-profile` since that route doesn't exist yet. For now leaderboard rows are non-interactive text.
