## Context

The previous web change (`add-web-game-board`) deliberately punted four user journeys to this one: replay scrubbing, public profile, paginated games list, and player search. The backend changes (`add-game-replay`, `add-public-profile-and-search`) shipped the four endpoints months ago — there is no protocol design needed; the work is purely web-app composition.

Two pieces from the prior change make this design easier than it would have been:

1. The `Board` component already accepts `[readonly]="true"` and consumes `RoomState | null`. The replay player just feeds it a synthesized "RoomState-shaped" object whose `game.moves` is sliced to the first `currentPly` moves. No new rendering primitives.
2. The username conventions (`UserSummary.id` everywhere) mean the linking sweep is a routerLink swap, not a refactor.

Sharp edges:

1. **`GameReplayDto` is shaped slightly differently from `RoomState`.** Both have `Black` / `White` / `StartedAt` / `EndedAt` / `Result` / `EndReason` / `Moves`, but the replay DTO has no `chatMessages`, no `status`, no `host`. The Board component only reads `game.moves` and `game.currentTurn` and `status === 'Playing'` (for disable check). Replay always passes `status='Finished'` so disabled is permanent. We synthesize a partial `RoomState`-shaped object purely for the Board input — we don't expose this shape elsewhere.
2. **Auto-play timing**: setting `setInterval` and toggling pause cleanly across destroys is a small lifecycle puzzle. We solve with a `playing` signal, an `effect` that creates / clears the interval based on it, and explicit `clearInterval` in `ngOnDestroy`.
3. **Find-player debouncing must not fire on transient input states**: typing "alice" issues 5 requests if not debounced. We use `toSignal(controlValueChanges.pipe(debounceTime(250), distinctUntilChanged(), filter(len ≥ 3)))` plus a manual cancel of in-flight subscriptions when the input changes again.
4. **Pagination state in URL or component-local?** Component-local for simplicity: `page = signal(1)` with prev/next buttons. Reload resets to page 1. URL-bound pagination (e.g. `?page=3`) is a nice-to-have but not necessary for v1; the games list is browse-only, not deep-linkable per page.
5. **Username link sweep — careful not to rewrap link text inside link text.** Lobby cards' "Join"/"Watch" buttons enclose row layouts; we make ONLY the username `<span>` into an anchor, not the whole row.

## Goals / Non-Goals

**Goals:**

- Replay endpoint becomes usable: open a finished game, scrub through all moves with auto-play.
- Any username text in the app becomes a click-through to that user's profile.
- A user-search input lives in the lobby and supports name-prefix find-as-you-type.
- Reuse `Board[readonly]` for replay (no parallel rendering layer).
- All translatable, all CDK, all token-themed, all 375 px usable, all OnPush.

**Non-Goals:**

- Annotated replay positions / move comments.
- Replay export / shareable link with deep `?ply=N` (route-bindable later, but not now).
- Header search input — explicitly deferred; Lobby card is enough for v1.
- Sort / filter on the games list (default `EndedAt DESC` ships).
- Online-status dot on profile (separate change involving `presence` domain).
- "Recently played with" on profile (could be a future feature; data is in `GET /api/users/:id/games`).

## Decisions

### D1. Replay route at `/replay/:id`, not nested under `/rooms/:id`

**Decision:** Top-level route `/replay/:id` rather than `/rooms/:id/replay`.

**Rationale:**

- A finished room often gets cleaned up (room aggregate's `FinishedRoomRetentionMinutes`); the URL space `/rooms/:id` is "live room context", not "replay archive". A finished `/rooms/:id` page renders the live room in a frozen state already; replay is a different mode.
- Top-level keeps the route flat and the lazy chunk simple.
- Bookmarking / sharing replays in a future `add-replay-share` change is cleaner from a flat path.

**Alternatives considered:**

- `/rooms/:id/replay` as a sibling route (would need the live `/rooms/:id` to navigate explicitly to the replay sub-route on game-end). Rejected — the live room page is already complex; a sibling route increases its surface.

### D2. Replay state via component-local signals; no service

**Decision:** `ReplayPage` owns its own `replay = signal<GameReplayDto | null>`, `currentPly = signal<number>`, `playing = signal<boolean>`, `speed = signal<number>`. No `ReplayService` abstraction.

**Rationale:**

- One page, one piece of state, no cross-page sharing. A service is overkill.
- Signal-based state matches the codebase's idiom (Lobby cards, RoomPage all do this).

**Alternatives considered:**

- A `ReplayPlayerService` exposing playback signals + control methods. Rejected: zero current consumer outside the page; adds DI ceremony with no win.

### D3. Replay's Board input adapter

**Decision:** A `boardState = computed<RoomState | null>` derives from `replay()` + `currentPly()`. It returns a partial `RoomState` shape:

```ts
return {
  id: r.roomId,
  name: r.name,
  status: 'Finished',
  host: r.host,
  black: r.black,
  white: r.white,
  spectators: [],
  game: {
    id: 'replay',           // synthesized; never read by Board
    currentTurn: nextTurn,  // unused at status=Finished
    startedAt: r.startedAt,
    endedAt: r.endedAt,
    result: r.result,
    winnerUserId: r.winnerUserId,
    endReason: r.endReason,
    turnStartedAt: r.startedAt, // unused at status=Finished
    turnTimeoutSeconds: 0,
    moves: r.moves.slice(0, currentPly()),  // ← the scrubber's effect
  },
  chatMessages: [],
  createdAt: r.startedAt,
};
```

Board's `cellDisabled` returns true because `status !== 'Playing'`, so the read-only guarantee falls out. The last-move highlight automatically tracks `moves.at(-1)` of the slice — perfect for the scrubber.

**Rationale:**

- Avoids touching the Board component (no second mode beyond the existing `readonly` input).
- The shape is local to `ReplayPage`; the synthesis function is ~20 LOC.

**Alternatives considered:**

- Adding a separate `(moves)` input to the Board and refactoring `state` away. Rejected: explodes the surface area for one extra consumer.

### D4. Auto-play via effect-driven setInterval

**Decision:**

```ts
constructor() {
  effect(() => {
    if (!this.playing()) return;       // tracked
    const speed = this.speed();        // tracked
    const id = setInterval(() => this.step(+1), 700 / speed);
    return () => clearInterval(id);    // teardown when playing flips off OR speed changes
  });
}
```

`step(+1)` increments `currentPly` and pauses at the end (sets `playing.set(false)` if `currentPly === moves.length`).

**Rationale:**

- `effect`'s teardown semantics mean speed change → old interval cleared, new interval starts at the new rate. No imperative interval-bookkeeping.
- Pause = `playing.set(false)` naturally.

**Alternatives considered:**

- `requestAnimationFrame` loop with manual delta accumulation. Rejected: overkill for 700 ms ticks.
- RxJS `interval(...)` piped through a takeUntil. Rejected: signals already exist for the rest; mixing in more rx is noise.

### D5. `UsersApiService` is a sibling service, not part of `AuthService`

**Decision:** New `core/api/users-api.service.ts` (abstract DI token + default impl). `AuthService` still owns "the current user via `/me` and refresh flow"; `UsersApiService` owns "any other user's data".

**Rationale:**

- `AuthService` is the auth concern (logged-in identity + tokens). Profile / games / search are read-side data about *other* users — different boundary.
- Mirrors `RoomsApiService` / `LeaderboardApiService` / `PresenceApiService` pattern.

### D6. Find-player card lives in the lobby, not the header

**Decision:** New lobby card `find-player`. Header has no search input.

**Rationale:**

- Header is already crowded (4 controls + auth state). Adding a 5th — and a wide one (input field) — would push the layout past comfortable on tablet sizes.
- A lobby card is more discoverable in the user's primary "what should I do" view; the header is for global state, not nav.
- Future change can add a header `Cmd-K`-style search if user research demands it.

**Alternatives considered:**

- Header `<input>` with command-palette feel. Rejected: scope creep for v1; needs keyboard nav, focus management, etc.

### D7. Search debouncing: signal + RxJS hybrid

**Decision:**

```ts
private readonly inputCtrl = new FormControl('', { nonNullable: true });
private readonly query = toSignal(
  this.inputCtrl.valueChanges.pipe(
    debounceTime(250),
    distinctUntilChanged(),
    map((v) => v.trim()),
  ),
  { initialValue: '' },
);
private readonly results = signal<readonly UserPublicProfileDto[]>([]);

constructor() {
  effect(() => {
    const q = this.query();
    if (q.length < 3) { this.results.set([]); return; }
    const sub = this.users.search(q, 1, 5).subscribe({
      next: (r) => this.results.set(r.items),
      error: () => this.results.set([]),
    });
    return () => sub.unsubscribe();
  });
}
```

**Rationale:**

- Debounce belongs in RxJS (best ergonomics for time-based emission).
- The result list is a Signal so the template renders without `async` pipe noise.
- `effect`'s teardown auto-cancels any in-flight subscription when input changes faster than the network.

**Alternatives considered:**

- Pure RxJS with `switchMap`. Equivalent in correctness; more verbose template (`@if (results$ | async; as items)`).

### D8. Replay end-state UX

**Decision:** When the replay reaches `currentPly === moves.length`:

- The play button becomes "Replay from start" (clicking sets `currentPly.set(0)` and `playing.set(true)`).
- Auto-play stops (effect teardown when `playing.set(false)` ran).
- A small "End of game" badge appears in the title bar.

**Rationale:**

- Avoids the auto-play "stuck spinning" bug (running setInterval forever past the end).
- "Replay from start" is the only useful action at this state; no need for a separate button.

### D9. Username-link CSS rule, not per-template

**Decision:** Add a single CSS class (`.username-link`) in `global.css` styling username anchors consistently — primary color, no underline by default, underline on hover, focus-visible ring. Templates apply the class to every `routerLink`-wrapped username.

**Rationale:**

- Centralizes the rule (one source of truth for username-link visual).
- Five templates change but each just wraps in `<a class="username-link" [routerLink]="...">`.

**Alternatives considered:**

- Inline `class="text-primary hover:underline"` per template. Rejected: 5+ duplications; harder to evolve.

### D10. Games-list pagination: prev/next + page indicator, no jump-to-page

**Decision:** Two buttons (Prev / Next) + a "Page N of M" label. Disabled state when at boundary. Default page size 10. Selectable page size dropdown deferred.

**Rationale:**

- Most users browse the most recent few games; deep history is a rare use case.
- A jump-to-page or per-page picker doubles the UI; not worth it for v1.

### D11. View-replay button placement in `GameEndedDialog`

**Decision:** Three buttons: `[ Stay ]  [ View replay ]  [ Back to lobby ]`. View-replay is the "secondary primary"; Back-to-lobby keeps its primary visual treatment.

**Rationale:**

- After a loss, the user often wants to immediately review what happened — this is the natural moment to surface the replay link.
- Primary action stays "back to lobby" because that's the most common follow-up; replay is one click away but not the default.

**Alternatives considered:**

- Putting "View replay" only on the games-list rows on the profile page. Rejected: too many clicks in the most common flow ("just lost, want to see how").

## Risks / Trade-offs

- **Risk: replay's synthesized `RoomState` drift.** If `RoomState` shape changes upstream, the replay synthesizer needs to follow. → Mitigation: keep the synthesizer in `replay-page.ts` as a small `boardState` computed; a TypeScript compile error catches drift.
- **Risk: search hits the backend on every keystroke after debounce — could DoS at scale.** → Mitigation: 250 ms debounce + 3-char minimum + cap at 5 results. Backend is JWT-auth + already rate-limited (`add-rate-limiting`).
- **Risk: pagination + browser back-button confusion.** Page state isn't in URL, so going back from `/replay/:id` to `/users/:id` lands on page 1 not whichever page they came from. → Accepted for v1. Documented as a follow-up: bind `page` to `?page=` query param (small change later).
- **Risk: 409 (game-not-finished) on `/replay/:id` is surprising — only happens if user hand-types a URL for a live room.** → Mitigation: translated 409 banner + link "go to live room instead" so the recovery is obvious.
- **Risk: username-link sweep introduces unintended event swallows.** Wrapping a `<span>` in `<a>` inside an existing button row could break button click. → Mitigation: only the username text becomes the anchor (not the whole row). Each anchor has `(click)="$event.stopPropagation()"` so clicks don't bubble to a parent row click handler.
- **Risk: replay autoplay drift on slow CPU.** `setInterval` accumulates jitter. → Accepted for v1; the feature is a UX nicety, not a precision metronome. If users complain we switch to `requestAnimationFrame`.
- **Risk: bot users in profile.** Backend includes bots in `GET /api/users/:id` (so profile page works for "AI_Hard"), but excludes bots from search. → Accepted; this is the backend's intentional asymmetry. Documented in proposal.
- **Trade-off: no header search.** Less discoverable than a header bar. Accepted; revisit when usage shows demand.
- **Trade-off: `currentPly` is local state, not URL.** Sharing a specific position in a replay isn't possible. Accepted; deep-linking is a follow-up change.

## Migration Plan

- Both routes are net-new (`/replay/:id`, `/users/:id`); no existing route is modified.
- The username-link sweep changes 5 templates — purely additive (text → anchor); existing tests for those components don't assert on raw text vs anchor markup.
- `GameEndedDialog`'s third button is additive; existing dialog test (which targets primary/secondary by index) needs an update — covered in tasks.md.
- Rollback = revert the commit; no data migration, no backend dependency.

## Open Questions

- **Q1: Win-rate computation source of truth?** Backend's `UserPublicProfileDto` has `Wins / Losses / Draws / GamesPlayed`. Win rate is a frontend display concern: `wins / (wins + losses + draws)` rounded. Backend doesn't compute it. Web-side computation is fine. (Resolved.)
- **Q2: Profile page games list should it cross-link to *both* opponent profile and replay?** Yes — opponent username is the username-link convention; the row body (date / result / move count) clicks through to replay. We resolve by making the username an inline `<a>` and the rest of the row a button-like surface.
- **Q3: Should the replay player display chat?** No — backend's `GameReplayDto` doesn't include chat. We could fetch `GET /api/rooms/:id` separately for chatMessages, but the room may have been retention-cleaned. Defer "replay-with-chat" to a later change with a dedicated DTO change.
- **Q4: What about the leaderboard's "tier" badges (gold/silver/bronze for top 3)?** The leaderboard already shows them; profile pages don't show ranking position. Adding "rank: #4" to profile is a backend-data concern (no current endpoint returns rank). Defer.
