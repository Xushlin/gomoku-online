## Context

We already have all the data and a similar UI pattern. The profile page's `GamesList` component renders this same shape (opponent + result + reason + date, row click → replay) but with pagination at 10/page. The lobby card is the "compact preview" cousin: 5 rows max, no pagination, one extra "View all" footer link.

Sharp edges:

1. **`auth.user()?.id` may be null at first paint.** The lobby route is auth-guarded, so it's never null in practice — but defensively, the card waits until `userId` is set before firing the request.
2. **Don't reuse `GamesList` directly.** GamesList owns its own pagination state + footer; reusing it would push us into prop-drilling "is this preview mode?" flags. The shared row layout is small enough (3 lines of HTML) to copy.
3. **Empty state on the lobby is hopeful, not awkward.** First-time users have 0 finished games — copy should encourage them to play, not confuse them.

## Goals / Non-Goals

**Goals:**

- Show the last 5 finished games on the home page.
- Link out to full history (`/users/:meId`) and to each replay (`/replay/:roomId`).
- Reuse the `username-link` convention for opponent names.
- Match the loading / empty / error / data four-state of every other lobby card.

**Non-Goals:**

- Pagination (`GamesList` on the profile owns that).
- Realtime updates while on lobby (no game-end push to the lobby; revisiting the page after a game refetches naturally).
- Result-colour coding — keep parity with the existing `GamesList` row.
- Stats (win streaks, best opponents, etc.) — separate, backend-dependent.

## Decisions

### D1. Don't reuse `GamesList` — copy the row template

**Decision:** `MyRecentGamesCard` is its own component with its own template, fetching `users.getGames(userId, 1, 5)`. The row layout (vs / opponent username link / result label / reason / date / move count) is conceptually shared with `GamesList` but copied verbatim — ~10 lines of template.

**Rationale:**

- Adding a "compact mode" flag to `GamesList` would smear lobby concerns into a profile-page sub-component.
- The two cards have different footers (pagination vs "View all" link) and different page sizes — different surfaces.
- Copy cost is < 30 LOC; one-time and easy to keep in sync visually if the row design ever changes.

**Alternatives considered:**

- Extract the row into a shared `<game-row>` presentational component. Rejected: premature; only two consumers, copy is simpler.

### D2. No polling

**Decision:** Single fetch on construction. No interval, no `LobbyDataService` slice.

**Rationale:**

- Recent games change only when the user finishes one. Finishing happens inside `RoomPage`, which already navigates back to `/home` afterward (via the GameEndedDialog "Back to lobby" action). That navigation re-mounts the lobby, which re-mounts the card, which re-fetches.
- Polling here costs network for ~zero benefit; matches the leaderboard's "no polling" decision in `LobbyDataService`.

### D3. Slot order in the right column

**Decision:** Right column order becomes: My active rooms → Recent games → AI game → Find player → Leaderboard.

**Rationale:**

- Active first (live state), recent second (just-past state) — both are about "me right now".
- AI game is an action card; find-player is a search; leaderboard is broadest. Action cards group together below the personal cards.

### D4. Empty state copy

**Decision:** Empty state shows translated `lobby.recent-games.empty` ("No finished games yet — play one to start your history."). No CTA button to avoid dueling with AI-game / find-player buttons right below.

**Rationale:**

- Empty is informational, not error. A "play now" button would compete with the dedicated cards; redundant.

### D5. "View all" target is the user's own profile

**Decision:** "View all" footer link uses `[routerLink]="['/users', userId]"` and lands on the user's own profile. From there, the existing `GamesList` paginated component shows full history.

**Rationale:**

- Profile already has the games list; no new page needed.
- One source of truth for "all my games".

## Risks / Trade-offs

- **Risk: `getGames` 404 if user's id is somehow not in DB.** → Backend never deletes user rows; auth is JWT against a known user. Treat 404 the same as generic error (retry surfaces).
- **Risk: opponent username overflow at small viewport.** → Mitigation: `truncate` Tailwind utility; same approach as other cards.
- **Risk: card looks identical to my-active-rooms at a glance.** → Mitigation: distinct title (`Recent games` vs `My active rooms`), different row content (result label is unique to recent), different footer ("View all" link). Should be visually distinguishable. If user testing shows confusion, swap card icons in a follow-up.
- **Trade-off: copying the row template instead of sharing.** Accepted (D1 rationale).

## Migration Plan

- Net-additive: one new card, two i18n keys, lobby grid grows by one entry.
- No existing component or test changed except lobby's import + template (which doesn't have a spec asserting card count).
- Rollback = revert.
