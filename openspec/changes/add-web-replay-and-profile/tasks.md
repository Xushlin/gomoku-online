## 1. Typed DTOs + API service

- [x] 1.1 Create `src/app/core/api/models/user-profile.model.ts` with `UserPublicProfileDto`, `UserGameSummaryDto`, `PagedResult<T>`. Field names + casing aligned with backend (System.Text.Json camelCase + JsonStringEnumConverter strings).
- [x] 1.2 Extend `src/app/core/api/models/room.model.ts` with `GameReplayDto` interface (roomId, name, host, black, white, startedAt, endedAt, result non-null, winnerUserId nullable, endReason non-null, moves readonly array). Reuse existing `MoveDto` / `UserSummary` / `GameResult` / `GameEndReason`.
- [x] 1.3 Create `src/app/core/api/users-api.service.ts` — abstract class `UsersApiService` (DI token) + `DefaultUsersApiService` impl. Methods `getProfile(id)`, `getGames(id, page, pageSize)`, `search(query, page, pageSize)`. Use `encodeURIComponent` on all id / query path + query string params. Match the abstract-class-as-DI-token pattern of `RoomsApiService`.
- [x] 1.4 Add `getReplay(roomId): Observable<GameReplayDto>` to abstract `RoomsApiService` and to `DefaultRoomsApiService` — `GET /api/rooms/{id}/replay`.
- [x] 1.5 Register `UsersApiService` in `app.config.ts` providers: `{ provide: UsersApiService, useClass: DefaultUsersApiService }`.
- [x] 1.6 `npm run build` green; typecheck passes.

## 2. UsersApiService spec + RoomsApiService spec extension

- [x] 2.1 Create `src/app/core/api/users-api.service.spec.ts` — three describe blocks mirroring rooms-api spec style:
  - `getProfile()` GETs `/api/users/{id}` with URL-encoded id.
  - `getGames()` GETs `/api/users/{id}/games?page=&pageSize=`.
  - `search()` GETs `/api/users?search=&page=&pageSize=` with encoded query.
- [x] 2.2 Extend `src/app/core/api/rooms-api.service.spec.ts` with a `getReplay()` test — encoded path + 200 + DTO shape.

## 3. Reusable username-link CSS class

- [x] 3.1 Add `.username-link { color: var(--color-primary); text-decoration: none } .username-link:hover { text-decoration: underline }` to `src/styles/global.css`.
- [x] 3.2 Verify the class lands in compiled `styles-*.css` (grep). Add a focus-visible ring fallback if the global `:focus-visible` rule doesn't already cover anchors.

## 4. ProfilePage shell + routing + header card

- [x] 4.1 Create `src/app/pages/users/profile-page/profile-page.{ts,html}` — standalone, OnPush, `:host { display: block }`. Inject `UsersApiService`, `ActivatedRoute`, `Router`, `LanguageService`. Read `id` from route param; on `ngOnInit` fire `getProfile(id)`.
- [x] 4.2 Update `app.routes.ts` to add `{ path: 'users/:id', canMatch: [authGuard], loadComponent: () => import('./pages/users/profile-page/profile-page').then(m => m.ProfilePage) }`.
- [x] 4.3 ProfilePage local signals: `profile = signal<UserPublicProfileDto | null>(null)`, `loadingProfile = signal(true)`, `notFound = signal(false)`, `loadError = signal(false)`. Wire 404 → `notFound`, network → `loadError` with retry.
- [x] 4.4 Header card template: username (large), rating (with `profile.rating-label`), W-L-D triple, win rate (`computed`: `gamesPlayed === 0 ? '—' : ((wins / (wins+losses+draws)) * 100).toFixed(1) + '%'`), joined-at via Angular `formatDate(profile().createdAt, 'longDate', language.current())`. Card uses token utilities only.
- [x] 4.5 Skeleton placeholder rendering when `loadingProfile()`.
- [x] 4.6 404 fallback: `profile.not-found` translation + back-to-lobby link replaces the card.
- [x] 4.7 Generic error fallback: `profile.errors.generic` + retry button (re-runs `getProfile`).

## 5. ProfilePage games list + pagination

- [x] 5.1 Create `src/app/pages/users/profile-page/games-list/games-list.{ts,html}` — standalone, OnPush, `:host { display: block }`. Inputs: `userId: InputSignal<string>`, `currentUserId: InputSignal<string | null>` (the logged-in user's id, used to compute "from-this-user's-perspective" result label).
- [x] 5.2 Local signals: `page = signal(1)`, `pageSize = 10`, `games = signal<readonly UserGameSummaryDto[]>([])`, `total = signal(0)`, `loadingGames = signal(false)`. `effect` watches `[userId, page]` and re-fires `getGames(userId(), page(), 10)`.
- [x] 5.3 Compute `totalPages = computed(() => Math.max(1, Math.ceil(total() / pageSize)))`. Disable Prev when `page() === 1`, Next when `page() >= totalPages()`.
- [x] 5.4 Per-row template: row is `<button>` (`(click)="onRowClick(game.roomId)"`); inside, opponent username via shared `.username-link` rule (`(click)="$event.stopPropagation()"`). The result label switches via `computed` per row: profile user is `winnerUserId` → result-win; profile user is loser → result-loss; draw → result-draw. Reason via existing `game.ended.reason-*`. ended-at via `formatDate`. moveCount as plain number.
- [x] 5.5 `onRowClick(roomId)` → `router.navigateByUrl('/replay/' + roomId)`.
- [x] 5.6 Empty state — `profile.games-empty` translation, Prev/Next disabled.
- [x] 5.7 ProfilePage wires `<app-games-list [userId]="id" [currentUserId]="auth.user()?.id ?? null">` below the header card.

## 6. ReplayPage shell + routing

- [x] 6.1 Create `src/app/pages/replay/replay-page/replay-page.{ts,html}` — standalone, OnPush, `:host { display: block }`. Inject `RoomsApiService`, `ActivatedRoute`, `Router`.
- [x] 6.2 Update `app.routes.ts` to add `{ path: 'replay/:id', canMatch: [authGuard], loadComponent: () => import('./pages/replay/replay-page/replay-page').then(m => m.ReplayPage) }`.
- [x] 6.3 Local signals: `replay = signal<GameReplayDto | null>(null)`, `currentPly = signal<number>(0)`, `playing = signal<boolean>(false)`, `speed = signal<number>(1)` (allowed values 0.5 / 1 / 2), `loading = signal(true)`, `notFound = signal(false)`, `notFinished = signal(false)`, `loadError = signal(false)`.
- [x] 6.4 `ngOnInit`: read route id, call `rooms.getReplay(id)` → on success set `replay`, reset `currentPly` to 0; on 404 set `notFound`; on 409 set `notFinished`; else set `loadError`.
- [x] 6.5 `boardState = computed<RoomState | null>` synthesises a `RoomState`-shaped partial from `replay()` + `currentPly()` per design D3 (status='Finished', moves sliced).
- [x] 6.6 Title-bar template: room name, "黑方:" + black username link, "白方:" + white username link (both `.username-link` to `/users/:id`), reason badge (`game.ended.reason-*`), ended-at via `formatDate`.
- [x] 6.7 404 / 409 / generic-error fallbacks (per spec). 409 fallback includes a `[routerLink]="['/rooms', id]"` link to live room.

## 7. Replay scrubber

- [x] 7.1 Buttons in template: ⏮ first / ⏪ prev / ▶/⏸ play-pause / ⏩ next / ⏭ last + speed buttons (0.5×, 1×, 2×).
- [x] 7.2 Slider `<input type="range" min="0" [attr.max]="replay()?.moves.length ?? 0" step="1" [value]="currentPly()" (input)="onSeek($event)">`.
- [x] 7.3 Methods: `step(delta)` clamps `currentPly` to `[0, moves.length]` and pauses when reaching `moves.length`; `first()`, `last()`, `togglePlay()`, `setSpeed(s)`, `onSeek(e)` (force pause on user seek).
- [x] 7.4 Auto-play `effect`: when `playing()` is true, schedules `setInterval(() => step(+1), 700 / speed())`; teardown clears interval. Effect re-runs when `playing` or `speed` changes.
- [x] 7.5 At `currentPly === moves.length`, auto pause and main button label flips to `replay.scrubber.replay`; clicking restarts from 0 (`currentPly.set(0); playing.set(true)`).
- [x] 7.6 Disabled boundaries: ⏮ / ⏪ disabled at 0; ⏭ / ⏩ disabled at `moves.length`. Slider always enabled when `replay()` is non-null.

## 8. Find-player lobby card

- [x] 8.1 Create `src/app/pages/lobby/cards/find-player/find-player.{ts,html}` — standalone, OnPush, `:host { display: block }`. Inject `UsersApiService`, `Router`.
- [x] 8.2 Reactive form `inputCtrl = new FormControl('', { nonNullable: true })`. `query = toSignal(inputCtrl.valueChanges.pipe(debounceTime(250), distinctUntilChanged(), map(v => v.trim())), { initialValue: '' })`. `results = signal<readonly UserPublicProfileDto[]>([])`. `loading = signal(false)`. `error = signal(false)`.
- [x] 8.3 `effect` watches `query()`: if `length < 3`, clear results + return. Otherwise subscribe to `users.search(q, 1, 5)` → set results / error. Effect cleanup unsubscribes prior in-flight request.
- [x] 8.4 Template: input + helper `.hint-too-short` (when `query().length > 0 && < 3`) + dropdown list (when `results().length > 0`) + "no results" (when query is searchable but empty results) + error banner (when `error()`). Each result button shows `username (rating)`; click → `router.navigateByUrl('/users/' + r.id)`, then `inputCtrl.reset('')`.
- [x] 8.5 Slot the new card into `pages/lobby/lobby.html` so it lives in the dashboard grid alongside the other 4 cards. Adjust grid layout to accommodate 5 cards (lg-3 columns + extra row for the find-player card OR drop find-player into the my-rooms column).

## 9. GameEndedDialog gains View-replay button

- [x] 9.1 Update `dialogs/game-ended-dialog.ts` `GameEndedDialogData` interface to include `roomId: string`. Update RoomPage's `openGameEndedDialog` to pass `roomId: this.roomId!`.
- [x] 9.2 Update dialog template — three buttons in this order: `[ Stay ]  [ View replay ]  [ Back to lobby ]`. View-replay calls `dialogRef.close('replay')`.
- [x] 9.3 Update `GameEndedDialogResult` union: `'home' | 'stay' | 'replay'`. RoomPage's `closed` subscription handles `'replay'` → `router.navigateByUrl('/replay/' + this.roomId)`.
- [x] 9.4 Update existing dialog spec — primary/secondary/tertiary buttons by index need updating (3 buttons now, not 2).

## 10. Username-link sweep across existing components

- [x] 10.1 `pages/lobby/cards/active-rooms/active-rooms.html` — host / black / white seat names → `<a class="username-link" [routerLink]="..." (click)="$event.stopPropagation()">`.
- [x] 10.2 `pages/lobby/cards/my-active-rooms/my-active-rooms.html` — host + opponent username (the seat that is NOT the current user) → link.
- [x] 10.3 `pages/lobby/cards/leaderboard/leaderboard.html` — Top-10 player names → link.
- [x] 10.4 `pages/rooms/room-page/sidebar/sidebar.html` — host + black + white seat names → link.
- [x] 10.5 `pages/rooms/room-page/chat/chat-panel/chat-panel.html` — sender username → link with stopPropagation.
- [x] 10.6 Run grep across these 5 templates for plain `{{ user.username }}` text and confirm all are wrapped (or intentionally left, e.g. self-display contexts, with a comment).

## 11. i18n keys (en + zh-CN)

- [x] 11.1 Add `replay.*` subtree to `public/i18n/en.json` (title-prefix, errors.{not-found, still-in-progress, generic}, retry, back-to-lobby, scrubber.{first, prev, next, last, play, pause, replay, speed-label, end-of-game}).
- [x] 11.2 Add `profile.*` subtree (rating-label, wins-label, losses-label, draws-label, win-rate-label, joined-label, games-title, games-empty, result-win, result-loss, result-draw, page-indicator, prev-page, next-page, not-found, back-to-lobby, errors.generic).
- [x] 11.3 Add `lobby.find-player.*` subtree (title, placeholder, hint-too-short, no-results, error, search-button-aria).
- [x] 11.4 Add `game.ended.view-replay` (single key inserted into existing tree).
- [x] 11.5 Replicate every key into `zh-CN.json` with simplified Chinese values. Run the parity flattener (Node one-liner from earlier i18n changes); confirm zero drift.

## 12. Tests

- [x] 12.1 `users-api.service.spec.ts` per task 2.1.
- [x] 12.2 `rooms-api.service.spec.ts` extension per task 2.2.
- [x] 12.3 `pages/users/profile-page/profile-page.spec.ts` (smoke): mount with stubbed `UsersApiService` + `Router` + `ActivatedRoute`; verify getProfile + getGames called with route id; verify 404 fallback shows `profile.not-found`; verify retry path.
- [x] 12.4 `pages/users/profile-page/games-list/games-list.spec.ts`: row click navigates `/replay/:id`; opponent username link click does NOT navigate to replay (stopPropagation); page state advances on Next; empty state shows `profile.games-empty`; result label switches per perspective.
- [x] 12.5 `pages/replay/replay-page/replay-page.spec.ts`: 404 / 409 / success branches; scrubber prev/next clamps to bounds; toggling play creates+clears interval (use `vi.useFakeTimers()`); reaching end auto-pauses; speed change re-creates interval at new rate.
- [x] 12.6 `pages/lobby/cards/find-player/find-player.spec.ts`: <3-char input doesn't fire request; ≥3-char input fires after debounce; click result navigates; error banner on failure.
- [x] 12.7 `dialogs/game-ended-dialog.spec.ts` update — three buttons; View-replay closes with 'replay'; existing primary/secondary tests still pass with re-indexed selectors.
- [x] 12.8 Update existing card / sidebar / chat-panel specs only as needed: `RouterTestingHarness` or stubbed `Router` to swallow navigations from new `routerLink`s; ensure no spec asserts on raw text-as-text now that text-is-anchor.
- [x] 12.9 `npm run test:ci` — green.

## 13. Cross-cutting polish + grep sweeps

- [x] 13.1 No hex / rgb / hsl in new pages outside tokens / skins.
- [x] 13.2 No hardcoded Tailwind palette in new pages.
- [x] 13.3 No CJK in new templates (`pages/replay/`, `pages/users/`, `pages/lobby/cards/find-player/`).
- [x] 13.4 No `inject(HttpClient)` in new code outside `core/api/users-api.service.ts`.
- [x] 13.5 No `bypassSecurityTrust*`; no `innerHTML`.
- [x] 13.6 LOC budgets: ProfilePage < 230, ReplayPage < 250, GamesList < 150, FindPlayer < 150 — verify via `wc -l`.

## 14. Final verification

- [x] 14.1 `npm run lint` passes.
- [x] 14.2 `npm run test:ci` passes.
- [x] 14.3 `npm run build` passes. Initial chunk gzip remains ≤ 250 KB. New `replay-page` and `profile-page` lazy chunks present, each ≤ 200 KB gzip. Find-player adds modestly to lobby chunk (< 5 KB gzip).
- [x] 14.4 `openspec validate add-web-replay-and-profile` is clean.
