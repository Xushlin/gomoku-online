## Context

Single-page, single-call addition. The hard parts (backend endpoint, profile page scaffolding) are already there. This is mostly a "thread one fetch into one component, render one dot" change.

Sharp edges (small):

1. **Failure is non-fatal.** If `getUserOnline` fails (offline, server hiccup), the profile must still render. Render the dot only when we have a successful boolean answer; otherwise omit it. Don't show "loading dot" placeholder — too much UI noise for a minor visual.
2. **Race with profile fetch.** Both fire in parallel on mount; profile may resolve first (page paints) and the dot pops in shortly after. That's fine — the dot is supplementary information, not load-blocking.
3. **Bots are users too.** Backend's `IsUserOnlineQuery` doesn't special-case bot accounts — they appear "offline" because their connection tracker entry doesn't exist (bots play server-side, no SignalR connection). On a bot's profile, the dot will be grey. Accepted; tests don't assert on bot.

## Goals / Non-Goals

**Goals:**

- Show presence dot on profile page (one user, one fetch).
- Tolerate failure silently (no dot, no error toast).
- Accessible — dot has aria-label "Online" / "Offline".

**Non-Goals:**

- Polling for live updates.
- Dots on every other username surface (would need bulk endpoint).
- "Last seen at" timestamps.

## Decisions

### D1. Map wire to boolean at the service boundary

**Decision:** `getUserOnline()` returns `Observable<boolean>` (not `Observable<UserPresenceWire>`). The service unwraps the `.isOnline` field via `.pipe(map(...))`, same as `getOnlineCount()` unwraps `.count`.

**Rationale:** Caller only cares about the boolean. Service hides the wire shape.

### D2. Single fetch on mount; no polling

**Decision:** Profile page fetches `getUserOnline(userId)` once when the route activates. No interval.

**Rationale:** Visiting a profile is a "snapshot" interaction. Polling adds work for tiny benefit. If the user navigates away and back, a fresh fetch happens.

### D3. Hide the dot on failure

**Decision:** A `presence = signal<boolean | null>(null)` — `null` means "not yet known or failed". Template renders the dot only when `presence() !== null`.

**Rationale:** A "loading" state for the dot is overkill for one boolean. Showing the dot only when known keeps the UI quiet.

### D4. Dot styling: 10 px, two colours, semantic class

**Decision:** A 10×10 px circle, `bg-success` (online) or `bg-muted` (offline), with `aria-label` set to `profile.online` / `profile.offline`. Inline before the username heading.

**Rationale:** Tiny enough to read as a status indicator, not as a stone or button. Tokens map to theme so it follows light/dark.

## Risks / Trade-offs

- **Risk: presence dot stale.** Without polling, the user may sit on a profile for minutes with a green dot that's gone offline. → Accepted v1 limitation; "snapshot at page load".
- **Risk: bots show as offline always.** → Accepted; bot accounts aren't in the connection tracker. Documented in Context.
- **Trade-off: no dot on lobby / chat / sidebar.** → Accepted; bulk endpoint missing. Single-page surface for v1.

## Migration Plan

- Net-additive. No existing tests break (profile spec is augmented, not replaced). Rollback = revert.
