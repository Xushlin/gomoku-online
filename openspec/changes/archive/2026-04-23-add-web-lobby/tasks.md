## 1. Confirm backend DTO shapes

- [x] 1.1 Verified via backend source: `Gomoku.Application/Common/DTOs/RoomDtos.cs` + `RoomMapping.ToSummary`. `RoomSummary` fields match proposal. Serialiser: System.Text.Json default camelCase, enums as strings (`JsonStringEnumConverter`).
- [x] 1.2 `LeaderboardEntryDto` + `PagedResult<T>` match proposal. Serialised in `/backend/src/Gomoku.Application/Common/DTOs/LeaderboardEntryDto.cs` + `PagedResult.cs`.
- [x] 1.3 `RoomStateDto` top-level fields verified; pinned `spectators`, `game`, `chatMessages`, `createdAt` on the TS type. Nested `GameSnapshotDto` / `MoveDto` / `ChatMessageDto` left as `unknown` for `add-web-game-board` to pin.
- [x] 1.4 No field-name drift; no backend change needed.

## 2. API models and services

- [x] 2.1 [room.model.ts](frontend-web/src/app/core/api/models/room.model.ts) — `RoomStatus`, `UserSummary`, `RoomSummary`, `RoomState`; nested DTO shapes deliberately `unknown`.
- [x] 2.2 [presence.model.ts](frontend-web/src/app/core/api/models/presence.model.ts) — `OnlineCountWire`.
- [x] 2.3 [leaderboard.model.ts](frontend-web/src/app/core/api/models/leaderboard.model.ts) — `LeaderboardEntry`, `PagedResult<T>`.
- [x] 2.4 [presence-api.service.ts](frontend-web/src/app/core/api/presence-api.service.ts) — abstract + `DefaultPresenceApiService`; `getOnlineCount()` unwraps `{ count }` → `number`.
- [x] 2.5 [rooms-api.service.ts](frontend-web/src/app/core/api/rooms-api.service.ts) — `list`, `myActiveRooms`, `getById`, `create`, `join`, `leave`, `spectate`. `getById` URL-encodes the ID.
- [x] 2.6 [leaderboard-api.service.ts](frontend-web/src/app/core/api/leaderboard-api.service.ts) — `getPage(page, pageSize)` + `top(count)` wrapper.
- [x] 2.7 All three registered in `app.config.ts` via `useClass`.

## 3. `LobbyDataService` and polling config

- [x] 3.1 [lobby-polling.config.ts](frontend-web/src/app/core/lobby/lobby-polling.config.ts) — `LOBBY_POLLING_CONFIG` InjectionToken with `providedIn: 'root'` factory default `{ onlineCountMs: 30_000, roomsMs: 15_000, myRoomsMs: 30_000 }`.
- [x] 3.2 [lobby-data.service.ts](frontend-web/src/app/core/lobby/lobby-data.service.ts) — abstract `LobbyDataService` + `DefaultLobbyDataService implements OnDestroy`. Each slice holds `data` / `loading` / `error` signals + `inFlight` subscription + `lastSuccessAt` timestamp + `refresh()` method. `performFetch` guards on `inFlight` (dedup). Intervals started per slice in the constructor (leaderboard has `intervalMs: null`, no interval). `visibilitychange` listener auto-refreshes stale slices on return-to-visible. `ngOnDestroy` clears all intervals + removes listener.
- [x] 3.3 `LobbyDataService` is provided **in the Lobby component's `providers: [...]`** — page-scoped, not app-wide. When Lobby unmounts the service is destroyed and timers stop.

## 4. Lobby page — container + layout

- [x] 4.1 Renamed `src/app/pages/home/` → `src/app/pages/lobby/`. `Home` → `Lobby`, selector `app-lobby`.
- [x] 4.2 Old `home.spec.ts` removed (superseded by card specs + `lobby-data.service.spec.ts`).
- [x] 4.3 `app.routes.ts` — `/home` → `{ component: Lobby, canMatch: [authGuard] }`.
- [x] 4.4 [lobby.ts](frontend-web/src/app/pages/lobby/lobby.ts) — 27 LOC orchestrator, `providers: [{ provide: LobbyDataService, useClass: DefaultLobbyDataService }]`.
- [x] 4.5 [lobby.html](frontend-web/src/app/pages/lobby/lobby.html) — hero full width, `grid-cols-1 lg:grid-cols-3` below. Mobile order: hero → my-rooms → active-rooms → leaderboard (via `order-*` utilities).

## 5. Lobby cards

- [x] 5.1 [hero.ts](frontend-web/src/app/pages/lobby/cards/hero/hero.ts) + [hero.html](frontend-web/src/app/pages/lobby/cards/hero/hero.html) — welcome + online count with loading skeleton + error dash. 17 LOC.
- [x] 5.2 [active-rooms.ts](frontend-web/src/app/pages/lobby/cards/active-rooms/active-rooms.ts) + [active-rooms.html](frontend-web/src/app/pages/lobby/cards/active-rooms/active-rooms.html) — list + Create button + Join/Watch per row, 409-AlreadyInRoom still navigates. 85 LOC.
- [x] 5.3 [my-active-rooms.ts](frontend-web/src/app/pages/lobby/cards/my-active-rooms/my-active-rooms.ts) + [my-active-rooms.html](frontend-web/src/app/pages/lobby/cards/my-active-rooms/my-active-rooms.html) — side inferred from `user().id` vs `black.id`/`white.id`, Resume navigates directly (no extra join). 46 LOC.
- [x] 5.4 [leaderboard.ts](frontend-web/src/app/pages/lobby/cards/leaderboard/leaderboard.ts) + [leaderboard.html](frontend-web/src/app/pages/lobby/cards/leaderboard/leaderboard.html) — `tierIcon(rank)` returns 🥇/🥈/🥉/null; `tierKey(rank)` for the `sr-only` translated tier label. 50 LOC.
- [x] 5.5 All cards use token utilities only. Grep sweep §10.1–10.2 passes.

## 6. Create-room dialog

- [x] 6.1 [create-room-dialog.ts](frontend-web/src/app/pages/lobby/dialogs/create-room-dialog/create-room-dialog.ts) + [create-room-dialog.html](frontend-web/src/app/pages/lobby/dialogs/create-room-dialog/create-room-dialog.html) — CDK `DialogRef`, reactive form, submit disabled while in-flight.
- [x] 6.2 Submit handler: calls `RoomsApiService.create`, on 400 + `ProblemDetails.errors` runs `mapProblemDetailsToForm`, other errors → `bannerKey` signal with `lobby.create-room.errors.{generic,network}`.
- [x] 6.3 Active-rooms card's `openCreateDialog()` opens `CreateRoomDialog` and on truthy `closed` result triggers both `rooms.refresh()` + `myRooms.refresh()`.
- [x] 6.4 Dialog uses only token utility classes — grep sweep clean.

## 7. `/rooms/:id` placeholder

- [x] 7.1 [room-placeholder.ts](frontend-web/src/app/pages/rooms/room-placeholder/room-placeholder.ts) + [room-placeholder.html](frontend-web/src/app/pages/rooms/room-placeholder/room-placeholder.html) — standalone, OnPush, lazy.
- [x] 7.2 On mount reads route param, calls `getById`, derives `mySide` signal via `AuthService.user()`.
- [x] 7.3 Template renders coming-soon banner, name/host/side/status, Leave button.
- [x] 7.4 404 handling: swaps to an inline not-found state with a back-to-lobby link; no unhandled rejection.
- [x] 7.5 Lazy route added to `app.routes.ts` with `canMatch: [authGuard]`.

## 8. i18n keys

- [x] 8.1 [en.json](frontend-web/public/i18n/en.json) — full `lobby.*` tree added (hero/rooms/my-rooms/leaderboard/create-room/errors/placeholder).
- [x] 8.2 [zh-CN.json](frontend-web/public/i18n/zh-CN.json) — mirror keys in 简体中文.
- [x] 8.3 Parity: `node -e` flattener confirms **101 keys per locale, zero drift**.

## 9. Tests

- [x] 9.1 [rooms-api.service.spec.ts](frontend-web/src/app/core/api/rooms-api.service.spec.ts) — 7 tests: every method hits the expected URL / method / body, incl. `getById` URL-encoding.
- [x] 9.2 [presence-api.service.spec.ts](frontend-web/src/app/core/api/presence-api.service.spec.ts) — `{ count: 42 }` unwraps to `42`.
- [x] 9.3 [leaderboard-api.service.spec.ts](frontend-web/src/app/core/api/leaderboard-api.service.spec.ts) — `getPage` query params, `top(n)` wraps and returns `.items`.
- [x] 9.4 [lobby-data.service.spec.ts](frontend-web/src/app/core/lobby/lobby-data.service.spec.ts) — 5 tests: initial parallel fetches, signals populate on success, concurrent-refresh dedup (1 pending `/api/rooms` even after 3 `refresh()` calls), slice error isolation (leaderboard 500 doesn't touch others), visibility-hidden blocks auto-refresh.
- [x] 9.5 Skipped a dedicated `lobby.spec.ts` — `Lobby` is a 27 LOC pure orchestrator whose behaviour is wholly covered by the card specs + `lobby-data.service.spec.ts`. A component-level smoke test would assert only "renders 4 children", which is mechanical and adds no real safety. Revisit if Lobby gains orchestration logic.
- [x] 9.6 [leaderboard.spec.ts](frontend-web/src/app/pages/lobby/cards/leaderboard/leaderboard.spec.ts) — 🥇🥈🥉 icons for rank 1/2/3; `#4` / `#5` for lower ranks; `tierKey` + `tierIcon` functions return null outside top 3.
- [x] 9.7 [create-room-dialog.spec.ts](frontend-web/src/app/pages/lobby/dialogs/create-room-dialog/create-room-dialog.spec.ts) — valid submit calls `create()` + closes with room; short name blocks; whitespace-only triggers `pattern` error; 400 ProblemDetails field-mapping stamps `Name → server error`.
- [x] 9.8 [room-placeholder.spec.ts](frontend-web/src/app/pages/rooms/room-placeholder/room-placeholder.spec.ts) — renders room data; 404 triggers not-found state; Leave calls `rooms.leave` + `navigateByUrl('/home')`.
- [x] 9.9 `npm run test:ci` — **16 files, 67 tests, all passing**.

## 10. Cross-cutting polish

- [x] 10.1 Hex / rgb / hsl grep — zero hits outside theme data.
- [x] 10.2 Hardcoded Tailwind palette utilities grep — zero hits.
- [x] 10.3 CJK grep in `src/app` — zero hits (test-spec translation fixtures excluded, same rule as scaffold / auth).
- [x] 10.4 `inject(HttpClient)` grep — only in `core/api/*.service.ts`, `core/i18n/transloco-loader.ts`, `core/auth/auth.service.ts`, and test files (`auth.interceptor.spec.ts` which must inject HttpClient to trigger the interceptor). Components: zero.
- [x] 10.5 `bypassSecurityTrust*` grep — zero hits.
- [x] 10.6 Component LOC: `Lobby` 27, hero 17, active-rooms 85, my-rooms 46, leaderboard 50, dialog 80, placeholder 81 — all well under the 150 ceiling.

## 11. Manual verification (DevTools + real backend)

- [ ] 11.1 Sign in as Alice; land on `/home`; four cards populate; create "Alice's room" → list refreshes to include it + appears in "My active rooms" with 你执黑 / You are Black.
- [ ] 11.2 DevTools Network: 15 s rooms cadence, 30 s for online + myRooms, no leaderboard polling. Switch tabs for 2 min → no requests; switch back → immediate refresh for stale slices.
- [ ] 11.3 Second browser as Bob; create a room → Alice's tab shows it within 15 s; Alice clicks Join → `/rooms/:id` placeholder shows Bob as host, Alice as White, Leave → back on `/home`; Alice's "My active rooms" drops that room.
- [ ] 11.4 Log out + navigate to `/home` → redirected to `/login?returnUrl=/home`.
- [ ] 11.5 Kill backend; reload `/home` → all four cards show error + retry; click one retry → only that slice refreshes.
- [ ] 11.6 Visual sweep across material/system × light/dark × en/zh-CN (8 combinations) on `/home` and `/rooms/:id`.
- [ ] 11.7 375 px viewport: stacked layout, no horizontal scroll on `/home` or `/rooms/:id`.
- [ ] 11.8 Leaderboard medal icons (🥇 🥈 🥉) render cleanly in both light and dark themes; swap to SVG if they clash.

## 12. Final verification

- [x] 12.1 `npm run lint` passes.
- [x] 12.2 `npm run test:ci` passes (67/67 across 16 files).
- [x] 12.3 `npm run build` passes. Initial chunk **116 KB gzip** (well under 250 KB target; was 98 KB after auth, lobby adds ~18 KB). Four lazy chunks: login 1.69 KB, register 1.86 KB, change-password 1.75 KB, room-placeholder 1.50 KB — each well under 200 KB ceiling.
- [x] 12.4 `openspec validate add-web-lobby` — clean.
