## 1. MyRecentGamesCard component

- [ ] 1.1 Create `src/app/pages/lobby/cards/my-recent-games/my-recent-games.{ts,html}` — standalone, OnPush, `:host { display: block; width: 100%; }`. Inject `AuthService`, `UsersApiService`, `Router`, `LanguageService`.
- [ ] 1.2 Local signals: `games = signal<readonly UserGameSummaryDto[]>([])`, `loading = signal(true)`, `error = signal(false)`. Computed `userId = computed(() => auth.user()?.id ?? null)`.
- [ ] 1.3 `effect` watches `userId()`: when non-null, fire `users.getGames(uid, 1, 5)` and set state. Effect cleanup unsubscribes prior in-flight subscription.
- [ ] 1.4 Helper methods: `opponentOf(g)`, `resultKey(g)` (returns `profile.result-win/-loss/-draw`), `reasonKey(g)` (returns `game.ended.reason-*`), `onRowClick(roomId)` → navigate `/replay/:roomId`.
- [ ] 1.5 Retry method: re-runs the effect (manually call the subscribe) and resets `loading` / `error` flags.

## 2. Card template

- [ ] 2.1 Card chrome matches sibling cards (`bg-surface text-text border-border rounded-card shadow-elevated`). Header with `lobby.recent-games.title`.
- [ ] 2.2 Loading state: 3 placeholder rows (`bg-border h-12 animate-pulse rounded`).
- [ ] 2.3 Error state: red text (`text-danger`) + retry button (`lobby.recent-games.error`).
- [ ] 2.4 Empty state: `text-muted` translated `lobby.recent-games.empty`.
- [ ] 2.5 Data state: `<ul class="flex flex-col gap-1 text-sm">` with up to 5 `<li>` rows. Each row mirrors the GamesList row layout (vs / opponent username link / result / reason / date / move count) — copied verbatim, not extracted.
- [ ] 2.6 Footer "View all" link → `[routerLink]="['/users', userId]"` (only rendered when data state and ≥ 1 item, OR always — implementation decides; prefer always-on if userId is present, since the profile shows full history regardless).

## 3. Lobby integration

- [ ] 3.1 Update `src/app/pages/lobby/lobby.ts` — import `MyRecentGamesCard`, add to `imports` array.
- [ ] 3.2 Update `src/app/pages/lobby/lobby.html` — slot `<app-my-recent-games-card />` in the right column between `<app-my-active-rooms-card />` and `<app-ai-game-card />`.

## 4. i18n

- [ ] 4.1 Add `lobby.recent-games.{title, view-all, empty, error}` to `public/i18n/en.json`.
- [ ] 4.2 Mirror in `zh-CN.json`.
- [ ] 4.3 Run parity flattener; confirm zero drift.

## 5. Tests

- [ ] 5.1 `my-recent-games.spec.ts`: stubbed `UsersApiService` + `AuthService` + `Router`. Assert:
  - On mount, `users.getGames` called with `(meId, 1, 5)`.
  - Row click navigates to `/replay/:roomId`.
  - Empty state: card shows `lobby.recent-games.empty` translated.
  - "View all" link's `routerLink` resolves to `/users/<meId>`.
  - Error state: when getGames fails, card shows error + retry; clicking retry refires the request.

## 6. Final verification

- [ ] 6.1 No hex / rgb in new files.
- [ ] 6.2 No CJK in new template.
- [ ] 6.3 `npm run lint` passes.
- [ ] 6.4 `npm run test:ci` passes (new tests included).
- [ ] 6.5 `npm run build` passes; lobby chunk grows ≤ 3 KB gzip.
- [ ] 6.6 `openspec validate add-web-recent-games-card` is clean.
