## Context

The backend's auth surface is stable — five endpoints (`register`, `login`, `refresh`, `logout`, `change-password`), JWT HS256 15-min access + 7-day rotating refresh, RFC 7807 error responses. The web scaffold wires up the shell and cross-cutting services (`ThemeService`, `LanguageService`) but has zero HTTP and zero user-facing business pages. This change does just enough to establish "who am I" in the web client and the HTTP plumbing that every subsequent page will lean on.

Three design decisions are load-bearing for every later web change:

1. **Where do tokens live, and for how long.** Every SPA has to answer this; getting it wrong means either terrible UX (re-login on every page load) or avoidable XSS exposure.
2. **What happens on 401.** Either the whole app goes to `/login` every 15 minutes, or we build a refresh-and-retry loop. Every later change assumes the second answer.
3. **How server validation errors get from `ProblemDetails.errors` to the form field.** Doing this by hand in every page is the path of inconsistent UX; centralising it once means register, change-password, and every future form look the same.

Getting these right now is cheaper than discovering disagreements when building the lobby.

## Goals / Non-Goals

**Goals:**

- Establish a typed, Signal-backed `AuthService` that holds `{ accessToken, user, isAuthenticated }` with the exact same abstract-class-as-DI-token shape as `ThemeService` / `LanguageService`.
- Ship three fully-functional, lazy-loaded pages (`login`, `register`, `change-password`) that round-trip cleanly against the real backend.
- Make HTTP 401 handling *invisible* to callers above the interceptor: a stale access token causes zero UX disruption as long as the refresh token is still valid.
- Give business pages a repeatable pattern for displaying `ProblemDetails.errors` as form-field errors.
- Keep the XSS attack surface small: access token never on disk, all user-visible copy routed through translations, never via `innerHTML`.
- Cover the state machine, interceptor refresh dance, and guards with Vitest so regressions are caught locally.

**Non-Goals:**

- Proactive token refresh (timer-based, pre-expiry) — we'll add it if 401-spike latency becomes user-visible; it isn't for 15-minute tokens with rotation.
- Remember-me toggle or per-device session management — refresh is always persisted.
- Password reset via email — backend doesn't implement it.
- CSRF protection — we use Bearer tokens, not cookies; irrelevant.
- OAuth / social login.
- Account deletion / email-change / avatar — handled by later profile work.
- SSR / hydration — the scaffold is browser-only; adding SSR is a separate concern.

## Decisions

### D1. Token storage: access in-memory, refresh in `localStorage['gomoku:refresh']`

**Decision:**

- Access token lives exclusively in the `accessToken` Signal inside `DefaultAuthService`. It is never written to any web storage.
- Refresh token is written to `localStorage['gomoku:refresh']` on login/register/refresh success, and deleted on logout and on refresh failure.
- On app bootstrap, the `DefaultAuthService` reads `localStorage['gomoku:refresh']`; if present, it fires a single `POST /api/auth/refresh` via `provideAppInitializer` *before* the first render so the UI boots with the user signed in rather than flashing the login page.

**Rationale:**

- XSS threat model: an attacker who runs arbitrary JS in our origin can already read `localStorage` *and* in-memory globals via closures. The thing in-memory *actually* buys is durability — a page refresh wipes the access token, forcing a refresh round-trip, which gives the backend a natural "touch point" to detect anomalies (e.g., refresh token invalidated server-side means the page-refreshed client gets kicked to `/login` instantly).
- `sessionStorage` was considered for the refresh token. Rejected: we want cross-tab sign-in persistence, and `sessionStorage` is per-tab.
- HttpOnly cookie was considered — and preferred in banking / PII contexts. Rejected here because (a) the backend returns refresh token in response body, not a cookie, and changing the backend for this is out of scope for a frontend change, (b) CSRF protection would then be required, adding complexity, (c) Gomoku's threat model does not warrant the extra ceremony.
- `localStorage` key namespace matches the existing `gomoku:` prefix (`gomoku:theme`, `gomoku:dark`, `gomoku:lang`).

**Alternatives considered:**

- **Refresh token in IndexedDB with opaque handle.** Rejected: over-engineered for the threat model; also readable by same-origin script.
- **Access token in sessionStorage.** Rejected: breaks cross-tab sign-in, and page refreshes wouldn't hit the refresh endpoint (losing that implicit re-validation).

### D2. 401 → silent refresh → retry, deduplicated

**Decision:** A functional HTTP interceptor wraps every outgoing request except calls to `/api/auth/login`, `/api/auth/register`, `/api/auth/refresh`, and `/api/auth/logout`. On 401 from an intercepted request:

1. If there is no refresh-in-flight, start one (`POST /api/auth/refresh` with the stored refresh token). Store the `Observable<AuthResponse>` in a module-level subject so all concurrent 401s subscribe to the same refresh.
2. When the refresh completes: update `AuthService` state, retry the original request exactly once, with the new access token in its `Authorization` header.
3. If the retry itself 401s: clear auth state, navigate to `/login?returnUrl=<current>`, and let the error propagate to the caller (so UI can show a friendly "your session expired, please log in again" toast).
4. If the refresh itself 401s (refresh token invalid/expired/revoked): same as (3).

**Concurrency invariant:** under N concurrent 401s triggered by N requests, there is **exactly one** `POST /api/auth/refresh` call on the wire.

**Rationale:**

- Reactive-on-401 is the simplest refresh strategy and the one the backend design already accommodates. Proactive timers add failure modes (drift, tab sleep, missed wakeups) for marginal benefit.
- The deduplication prevents "thundering herd" — when the page loads and 5 data-fetching widgets each send a request with a stale token, we don't want 5 refresh calls racing and rotating each other invalid.

**Alternatives considered:**

- **Pre-expiry timer + proactive refresh.** Rejected for scaffold simplicity. Can be layered on later as an internal implementation detail of `DefaultAuthService` without changing any caller.
- **401 → logout (no retry).** Rejected: unacceptable UX; every 15 minutes every user gets booted.
- **Let pages handle 401 themselves.** Rejected: repeats the refresh dance in every page.

### D3. Auth state shape and atomicity

**Decision:** `AuthService` exposes exactly three signals plus one computed:

```ts
abstract class AuthService {
  abstract readonly accessToken: Signal<string | null>;
  abstract readonly user: Signal<UserDto | null>;
  abstract readonly accessTokenExpiresAt: Signal<Date | null>;
  abstract readonly isAuthenticated: Signal<boolean>; // computed(() => accessToken() !== null && user() !== null)

  abstract login(email: string, password: string): Observable<void>;
  abstract register(email: string, username: string, password: string): Observable<void>;
  abstract logout(): Observable<void>;
  abstract changePassword(currentPassword: string, newPassword: string): Observable<void>;
  abstract refresh(): Observable<void>; // used by interceptor; rarely called by UI
}
```

`DefaultAuthService` updates all signals in the same microtask on every successful auth response, so consumers never observe partially-populated state (e.g. `accessToken` set but `user` still null).

Methods return `Observable<void>` rather than `Observable<AuthResponse>` — the service *is* the state. Callers await completion, not a payload.

**Rationale:**

- Signals > `BehaviorSubject` per CLAUDE.md.
- `Observable<void>` avoids leaking HTTP types to callers; if the page needs the user, it reads `auth.user()`.
- Atomic signal updates matter because `computed(isAuthenticated)` will fire twice otherwise, causing a flicker of "logged in but no user".

**Alternatives considered:**

- **Single `authState: Signal<AuthState>`.** Considered but rejected; three separate signals let the header bind to `user.username` without re-rendering on `accessTokenExpiresAt` changes.
- **Promise-based API.** Rejected: rest of the codebase is Observable-native (HttpClient, effects).

### D4. Reactive Forms + shared password validator

**Decision:**

- All three pages use `@angular/forms` Reactive Forms via `FormBuilder`.
- A single `passwordPolicyValidator(): ValidatorFn` in `src/app/pages/auth/shared/password-policy.validator.ts` encodes the backend's rules:
  - Length ≥ 8 → error key `minlength`
  - At least one letter (`/[A-Za-z]/`) → error key `missingLetter`
  - At least one digit (`/\d/`) → error key `missingDigit`
- Register's `password` FormControl uses `[Validators.required, passwordPolicyValidator()]`.
- Change-password's `newPassword` FormControl uses the same.
- Login's `password` only requires `Validators.required` — no point pre-validating a stored password against current rules.
- Confirm-password on change-password uses a **form-level** validator `matchFieldsValidator('newPassword', 'confirmPassword')` that sets `{ mismatch: true }` on the confirm control, not the group.

**Rationale:**

- Single source of truth for client-side rules, so register + change-password cannot drift from each other.
- Backend stays the authority on rejection — client-side validation is purely for UX latency.
- Form-level validator for confirm-matching is idiomatic Angular and keeps the error on the right field.

**Alternatives considered:**

- **Template-driven forms.** Rejected: worse validation ergonomics, worse testability, and the rest of the codebase will be Reactive Forms for consistency.
- **Inline per-field regex in each component.** Rejected — three copies of the same regex is three places to drift.

### D5. ProblemDetails → form errors

**Decision:**

A small helper `mapProblemDetailsToForm(form: FormGroup, problem: ProblemDetails): boolean`:

- Iterates `problem.errors` (`{ fieldName: string[] }`), normalises `fieldName` to camelCase (backend sometimes sends `"Email"` → form control name is `email`), and on each match calls `form.get(controlName)?.setErrors({ server: messages[0] })`.
- If no field matched, returns `false` so the caller can surface the top-level `problem.detail` as a form-group-level error.

Field error rendering in templates reads the control's first error key in priority order (`required` → built-in validators → `server`) and translates:

```html
@if (form.controls.email.errors?.['required']) {
  <p class="text-danger">{{ 'auth.errors.required' | transloco }}</p>
} @else if (form.controls.email.errors?.['email']) {
  <p class="text-danger">{{ 'auth.errors.email-invalid' | transloco }}</p>
} @else if (form.controls.email.errors?.['server']) {
  <p class="text-danger">{{ form.controls.email.errors!['server'] }}</p>
}
```

Server messages are passed through as plain text (Angular's interpolation auto-escapes). The backend's messages are English; translating them would require either backend cooperation or a `code → key` mapping — out of scope now. Known error *types* (e.g. 401 `InvalidCredentialsException`) are translated to localised strings in the page; free-form field messages are not.

**Rationale:**

- Keeps the mapping in one place.
- Accepts that server messages are (for now) English-only — the top-level form messages ("Invalid credentials", "Email already taken") are translated; the field-level passthrough is a narrow exception.
- Angular's auto-escaping handles XSS for interpolated strings.

**Alternatives considered:**

- **Translate every server message via an error-code map.** Rejected — backend currently returns free text, and adding a code system is a backend change. Can be layered on later.
- **Render server messages via `innerHTML`.** Hard rejected — XSS footgun.

### D6. Route guards as `CanMatchFn`

**Decision:** Both `authGuard` and `guestGuard` are functional `CanMatchFn`, not `CanActivateFn`:

```ts
export const authGuard: CanMatchFn = (_route, segments) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (auth.isAuthenticated()) return true;
  const returnUrl = '/' + segments.map(s => s.path).join('/');
  return router.createUrlTree(['/login'], { queryParams: { returnUrl } });
};
```

`guestGuard` is the mirror image — redirects to `/home` if already authenticated.

**Rationale:**

- `CanMatchFn` runs *before* lazy-loaded chunks are fetched; `CanActivateFn` runs after. For auth-gated routes, `CanMatchFn` means we don't even download the `/account/password` chunk for anonymous users.
- Functional guards keep boilerplate to one line per guard and are easy to test.

**Alternatives considered:**

- **Class-based guard.** Rejected: more ceremony, tests need TestBed, and it's not idiomatic in Angular 17+.

### D7. Bootstrap refresh before first render

**Decision:** In `app.config.ts`, `provideAppInitializer` now awaits a combined promise: the existing i18n preload *and* an auth-bootstrap step:

```ts
provideAppInitializer(() => {
  const auth = inject(AuthService);
  const transloco = inject(TranslocoService);
  const language = inject(LanguageService);
  return Promise.all([
    firstValueFrom(transloco.load(language.current())),
    auth.bootstrap(), // returns Promise<void>
  ]).then(() => undefined);
});
```

`DefaultAuthService.bootstrap()` behaviour:

- If `localStorage['gomoku:refresh']` is empty → resolve immediately, state stays unauthenticated.
- If present → `POST /api/auth/refresh`; on success, populate state; on failure (including network), clear storage and resolve unauthenticated. **Do not propagate the error** — we don't want a refresh failure to block the app from booting.

**Rationale:**

- User who reloaded the page expects to see the home/lobby, not a login page. Refreshing on bootstrap eliminates the flash.
- Bounded wait: if the network is slow, initialization hangs — same tradeoff Transloco's preload already accepts.
- Silently swallowing errors on bootstrap is deliberate; any error means "re-auth required" which is already the unauthenticated path.

**Alternatives considered:**

- **Don't bootstrap-refresh; rely on the first 401 on any request.** Rejected: the home page doesn't make any authenticated requests (by design), so a logged-in user who reloads sees `login` in the header until they navigate somewhere. Bad UX.
- **Parallel: start the app *and* fire the refresh.** Considered. Rejected because the half-second of "anonymous flash" is worse than the half-second longer boot.

### D8. Header auth slot

**Decision:** The existing `Header` component gets a fourth region at the right edge: either a "Log in" link (anonymous) or `{ username, logout button }` (authenticated). Wiring is by injecting `AuthService` into `Header` and reading its signals.

Logout button click:

1. Call `auth.logout()` — hits `POST /api/auth/logout` with the current refresh token, then clears state. The service tolerates network errors on logout (the local state is cleared regardless; 204 is idempotent on the server).
2. Router navigate to `/home`.

**Rationale:**

- Keeps the shell spec from needing modification (no new requirement in `web-shell`), because auth presentation is a `web-auth` concern that plugs into an existing region.
- Single source of auth UI means no conflicting "sign out" buttons appear elsewhere.

**Alternatives considered:**

- **Make Header dumb and let a new "AuthHeaderSlot" component provide auth UI.** Considered but rejected as over-engineered for a single slot. Can be extracted later if the header grows.

### D9. Testing strategy

**Decision:** Three test files under `src/app/core/auth/`:

- `auth.service.spec.ts` — state transitions, bootstrap paths, token storage writes, atomic updates, logout clears everything.
- `auth.interceptor.spec.ts` — the 401 refresh-and-retry dance, including concurrent-request deduplication (assert only one `refresh` HTTP call when 3 requests fire and all get 401).
- `auth.guards.spec.ts` — both guards return true / UrlTree under each auth state.

Plus `password-policy.validator.spec.ts` for the validator.

Pages (`login`, `register`, `change-password`) get one smoke test each: form submits, service called with expected args, field-level ProblemDetails mapped correctly. Router navigation and deeper UI behaviours are manual.

Mocking strategy: `HttpClient` via `HttpTestingController`; `AuthService` via an object stub conforming to the abstract class; `Router` via a `createSpyObj`-style fake with `navigateByUrl` / `createUrlTree`.

**Rationale:**

- The interceptor is the trickiest piece and gets the most test surface.
- Pages are thin containers — deep component-harness tests aren't worth the flake.

### D10. What we deliberately don't build

- **Remember-me toggle.** Not in scope. Refresh token always persists.
- **Lockout after N failed logins.** Backend handles rate-limiting (see archived `add-rate-limiting`); client just shows the 429.
- **Server-side translated error messages.** Out of scope — client translates type-level errors (login failed, email taken) and renders field messages verbatim.
- **Pre-expiry refresh.** Out of scope; reactive-on-401 suffices.
- **CSRF tokens.** N/A for Bearer auth.
- **Multi-factor auth.** Not implemented backend-side.

## Risks / Trade-offs

- **Risk: `localStorage` refresh token visible to any JS that runs in the origin.** → Mitigation: Angular's template-only rendering + absence of `innerHTML` keeps XSS surface small; backend rotation limits stolen-token replay; never log the refresh token on the client.
- **Risk: `provideAppInitializer` waiting on a network refresh delays first paint.** → Mitigation: `DefaultAuthService.bootstrap()` has an internal 5-second timeout; if the refresh doesn't complete, we proceed as unauthenticated and let the UI load. The user sees a brief "anonymous" state and can re-login, same as if refresh had failed outright.
- **Risk: Concurrent 401s trigger concurrent refreshes → rotation invalidates in-flight calls.** → Mitigation: the dedup subject in the interceptor. Explicitly tested.
- **Risk: Refresh fires during logout.** → Mitigation: `logout()` sets an "is-logging-out" flag; the interceptor's refresh path short-circuits when set, so a tail-end 401 during logout doesn't revive the session.
- **Risk: Backend returns a 401 for a non-auth reason (e.g., malformed JWT due to a code bug), and we silently refresh forever.** → Mitigation: the interceptor retries **exactly once** per request. If retry also 401s, we stop and surface the failure.
- **Risk: Change-password success followed immediately by a stale 401 retry using the old refresh token (which the backend has just revoked in `RevokeAllRefreshTokens`).** → Mitigation: on change-password success we unconditionally clear state and redirect to `/login` — the old refresh token never gets a chance to be used.
- **Trade-off: No proactive refresh.** → Accepted. Real users will see an extra ~50ms on the first request after the token expires (one refresh + one retry). Not worth the added complexity to shave it.
- **Trade-off: Server error messages rendered verbatim (English only for now).** → Accepted. Type-level errors (login failed) are translated; field-level messages are not. Can be layered on via a code→key map in a future change.

## Migration Plan

- First change to add HTTP. `provideHttpClient()` is already in the scaffold (inside `provideAppI18n()`) — this change adds `withInterceptors([authInterceptor])` to it.
- Refresh token migration: no prior deployment → no data to migrate. The `localStorage['gomoku:refresh']` key is brand-new.
- Rollback: revert the change; `localStorage['gomoku:refresh']` left behind is harmless (old builds don't read it). The home page still renders without auth.

## Open Questions

- **Q: Should we unify `provideHttpClient()` into a single place?** Right now it's inside `provideAppI18n()`. This change adds `withInterceptors([authInterceptor])` to it — either we move the HTTP provider out of the i18n helper or we let the i18n helper take an interceptors array. Leaning towards extracting a new `provideAppHttp()` helper and calling both from `app.config.ts`. Decide during implementation.
- **Q: Should `refresh()` on the `AuthService` API be exposed publicly or hidden?** The interceptor needs it, but nobody else should call it. Lean: keep it public (simpler DI than a separate `RefreshPort` interface) and document the contract.
- **Q: What does the "flash message" on change-password-success redirect look like?** A query parameter on `/login` (e.g. `/login?flash=password-changed`) is simple and survives the redirect without introducing a message bus. Lean: this.
