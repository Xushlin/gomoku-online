## Why

The backend already exposes a full JWT-with-rotation auth surface: `POST /api/auth/{register,login,refresh,logout,change-password}`, 15-minute access token, 7-day refresh token with rotation, RFC 7807 error responses, idempotent logout. The web scaffold (`add-web-scaffold`) gave us the shell + theming + i18n but intentionally contains zero business pages and zero HTTP. Without sign-in, every subsequent web change (lobby, game board, replay, profile) has nothing to hang off — they all assume a logged-in user. This change is the smallest viable step that makes "who am I" a real, testable thing in the web client.

We also want to lock in the **cross-cutting HTTP plumbing** (interceptor, 401→refresh→retry, error mapping, auth state in a Signal-backed service) exactly once. The patterns established here will be reused verbatim by every subsequent web change, so getting the shape right is cheaper now than in four months.

## What Changes

- **Three lazy-loaded routes**: `/login`, `/register`, `/account/password`. Every one uses `loadComponent` — none are eager.
- **`AuthService`** — abstract DI-token pattern matching `ThemeService` / `LanguageService`:
  - `readonly accessToken: Signal<string | null>`
  - `readonly user: Signal<UserDto | null>`
  - `readonly isAuthenticated: Signal<boolean>` (computed)
  - Methods: `login(email, password)`, `register(email, username, password)`, `logout()`, `changePassword(currentPassword, newPassword)`, `refresh()`
  - Access token: **in memory only** (never persisted — XSS containment)
  - Refresh token: `localStorage['gomoku:refresh']` (backend returns it in body, not a cookie)
  - Login / register / refresh responses populate all three signals atomically
- **`authInterceptor`** (functional interceptor, `withInterceptors([authInterceptor])`):
  - Attaches `Authorization: Bearer <access>` to every request that has an access token *and* is not itself a call to `/api/auth/login` / `/api/auth/register` / `/api/auth/refresh`
  - On HTTP 401 from any **non-auth** endpoint: trigger one silent refresh (deduplicated — concurrent 401s share a single in-flight refresh), retry the original request exactly once, and if refresh fails clear auth state and redirect to `/login?returnUrl=<current>`
  - On HTTP 400 with `ProblemDetails.errors`: pass through unchanged (pages map field errors into their forms)
- **`authGuard` + `guestGuard`** (functional `CanMatchFn`):
  - `authGuard`: requires `isAuthenticated()`; otherwise redirect to `/login?returnUrl=...`
  - `guestGuard`: requires NOT authenticated; otherwise redirect to `/home`
  - `/login` and `/register` are `guestGuard`; `/account/password` is `authGuard`
- **Three page components** under `src/app/pages/auth/` — standalone, OnPush, Container components that talk to `AuthService`:
  - `login` — email + password; Reactive Forms; disabled during inflight
  - `register` — email + username + password; client-side validator matches backend's password policy (≥ 8, has letter, has digit)
  - `change-password` — currentPassword + newPassword + confirmPassword (client-only match check); on success, treat as logout + redirect to `/login` with a one-time "password changed, please log in again" flash message (backend has revoked all refresh tokens server-side)
- **Password policy** extracted as a reusable validator factory `passwordPolicyValidator()` in `src/app/pages/auth/validators/` so register + change-password share identical client-side rules aligned with `RegisterCommandValidator`.
- **ProblemDetails mapping**: a tiny helper `mapProblemDetailsToForm(form, problem)` that walks `errors: { field: [msg] }` and calls `form.get(field)?.setErrors({ server: msg })`, so backend field-level validation shows next to the right input.
- **i18n**: append `auth.*` keys to both `public/i18n/en.json` and `public/i18n/zh-CN.json`. New keys:
  - `auth.login.{title,submit,email-label,password-label,submit-loading,no-account-cta,errors.invalid-credentials}`
  - `auth.register.{title,submit,email-label,username-label,password-label,already-have-account-cta,errors.email-taken,errors.username-taken}`
  - `auth.change-password.{title,submit,current-label,new-label,confirm-label,success,errors.wrong-current,errors.mismatch}`
  - `auth.errors.{generic,network,password-min-length,password-missing-letter,password-missing-digit,email-invalid,username-invalid}`
  - `header.auth.{login,logout,account}`
- **Header extension**: wire an auth slot into the existing `Header` — when authenticated, show `{{ user.username }}` + a logout button; when anonymous, show a Login link (routing to `/login`). This is a small additive edit to the existing shell header, not a new header component.
- **Tests** (Vitest):
  - `AuthService` state transitions: unauthenticated → login → authenticated; change-password → back to unauthenticated
  - `authInterceptor`:
    - Attaches `Authorization` header when token present + request is non-auth
    - Does NOT attach to `/api/auth/login|register|refresh` requests
    - On 401 → calls refresh exactly once even under concurrent 401s → retries original
    - On refresh failure → clears state + navigates to `/login`
  - `authGuard` / `guestGuard`: redirect behavior given each auth state
  - `passwordPolicyValidator`: accepts valid, rejects each failure mode

Out of scope (deferred):
- Proactive refresh-before-expiry timer (reactive-on-401 is enough for scaffold)
- Remember-me toggle (refresh token persistence is unconditional for now)
- OAuth / social login
- CSRF (backend uses Bearer tokens, not cookie auth → no CSRF concern here)
- Password-reset-via-email flow (backend doesn't implement it yet)

## Capabilities

### New Capabilities
- `web-auth`: Web-client authentication — `AuthService` contract and state shape, token storage policy (access in-memory, refresh in `localStorage`), auth / guest route guards, `authInterceptor` request attachment + 401 refresh-and-retry + concurrent-refresh deduplication, login / register / change-password page contracts, ProblemDetails → form-error mapping, password-policy validator aligned with the backend's `RegisterCommandValidator`, i18n key structure for auth UI, header auth slot (anon vs. signed-in).

### Modified Capabilities
<!-- None. web-shell's header gets an additive slot but the existing requirements (controls for language/theme/dark) remain unchanged. web-i18n just gains more keys — its rules about translation discipline are unchanged. -->

## Impact

- **New folders**:
  - `src/app/core/auth/` — `auth.service.ts`, `auth.interceptor.ts`, `auth.guards.ts`, `auth-response.model.ts`, `problem-details.ts`, `user.model.ts`
  - `src/app/pages/auth/login/`, `src/app/pages/auth/register/`, `src/app/pages/auth/change-password/`
  - `src/app/pages/auth/shared/` — `password-policy.validator.ts`, `problem-details.mapper.ts`, optionally a small `AuthCard` presentational component reused by all three pages
- **Modified files**:
  - `src/app/app.config.ts` — add `withInterceptors([authInterceptor])` to the HTTP providers, add `{ provide: AuthService, useClass: DefaultAuthService }`
  - `src/app/app.routes.ts` — add three lazy routes with guards
  - `src/app/shell/header/header.ts` + `.html` — add auth slot (login link or username + logout button)
  - `public/i18n/en.json` + `public/i18n/zh-CN.json` — append `auth.*` and `header.auth.*` keys
- **New dependencies**: none. Angular 21 gives us `HttpClient`, `ReactiveFormsModule`, `provideRouter` + `CanMatchFn`, `provideHttpClient(withInterceptors(...))` — we already have `@angular/common/http` transitively via HttpClient use in the scaffold.
- **Backend impact**: none — backend auth surface is stable and already shipped.
- **Security baseline**: documented here for future changes:
  - Access token never touches disk
  - Refresh token in `localStorage` under `gomoku:refresh` — accepted because (a) rotation limits replay window, (b) Angular's auto-escaping + template-only HTML shrinks XSS surface, (c) the scaffold already locked in CSP-friendly practices
  - Logout MUST call `POST /api/auth/logout` to revoke server-side before wiping client state, even on change-password-triggered logouts (except when the change-password 204 itself already invalidates all tokens — in that case, skip the /logout call and just wipe)
  - Errors returned from the server MUST NOT be rendered via any mechanism that interprets HTML (plain text-binding only)
- **Follow-ups enabled**:
  - `add-web-lobby` — authenticated routes can rely on the guard + interceptor
  - `add-web-game-board` — SignalR connection can pick up `accessToken()` signal and include it as bearer in the hub handshake
  - `add-web-replay-and-profile` — profile page can show the `user()` signal's username / email directly
