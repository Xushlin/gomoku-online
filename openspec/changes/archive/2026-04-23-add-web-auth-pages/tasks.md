## 1. HTTP plumbing and models

- [x] 1.1 Resolved: extracted a new `provideAppHttp(interceptors)` helper at [http-config.ts](frontend-web/src/app/core/http/http-config.ts) and removed `provideHttpClient()` from `provideAppI18n()`. Interceptors are now a single explicit argument in `app.config.ts` rather than a side effect of i18n wiring.
- [x] 1.2 [user.model.ts](frontend-web/src/app/core/auth/user.model.ts) — minimal `UserDto { id, username, email }`.
- [x] 1.3 [auth-response.model.ts](frontend-web/src/app/core/auth/auth-response.model.ts) — `AuthResponseWire` (wire) + `AuthResponse` (state) + `parseAuthResponse()` mapper.
- [x] 1.4 [problem-details.ts](frontend-web/src/app/core/auth/problem-details.ts) — RFC 7807 shape + `isProblemDetails` type guard.

## 2. `AuthService`

- [x] 2.1 [auth.service.ts](frontend-web/src/app/core/auth/auth.service.ts) — abstract `AuthService` with signals + methods per spec.
- [x] 2.2 `DefaultAuthService`: private `applyAuthResponse` sets all three signals synchronously, `clearAuthState` wipes them + localStorage, `logout` uses `catchError(() => of(undefined))` so it always completes clean, `changePassword` clears state on 204, `bootstrap()` `firstValueFrom(refresh().pipe(timeout(5000)))` with swallowed errors.
- [x] 2.3 Registered in `app.config.ts` via `{ provide: AuthService, useClass: DefaultAuthService }`.
- [x] 2.4 `provideAppInitializer` in `app.config.ts` now does `Promise.all([transloco.load(...), auth.bootstrap()])` so both complete before first paint.

## 3. `authInterceptor`

- [x] 3.1 [auth.interceptor.ts](frontend-web/src/app/core/auth/auth.interceptor.ts) — functional `HttpInterceptorFn`.
- [x] 3.2 Request side: `NO_AUTH_PREFIXES = ['/api/auth/login', '/api/auth/register', '/api/auth/refresh']` skip Bearer; everything else gets `Authorization: Bearer <accessToken()>` when the token is non-null.
- [x] 3.3 Response side: module-level `ReplaySubject<string>` de-dups concurrent refreshes; `handle401` calls `getOrStartRefresh` → `switchMap` retries the original with the new token; refresh failure navigates to `/login?returnUrl=<pathname+search>` and lets the original 401 propagate. Retry failures do NOT recurse (original request is retried at most once because `handle401` is only called from the outer `catchError`, not the retry's pipeline).
- [x] 3.4 `app.config.ts` calls `provideAppHttp([authInterceptor])`.

## 4. Route guards

- [x] 4.1 [auth.guards.ts](frontend-web/src/app/core/auth/auth.guards.ts) exports `authGuard` and `guestGuard` as functional `CanMatchFn`.
- [x] 4.2 `authGuard` builds returnUrl from the matched `UrlSegment[]` and returns `Router.createUrlTree(['/login'], { queryParams: { returnUrl } })` when unauthenticated.
- [x] 4.3 `guestGuard` returns `Router.createUrlTree(['/home'])` when authenticated.

## 5. Shared form helpers

- [x] 5.1 [password-policy.validator.ts](frontend-web/src/app/pages/auth/shared/password-policy.validator.ts) returns error keys `minlength`, `missingLetter`, `missingDigit`; returns `null` on empty input.
- [x] 5.2 [match-fields.validator.ts](frontend-web/src/app/pages/auth/shared/match-fields.validator.ts) — form-group level, attaches `{ mismatch: true }` only to the confirm control and cleans it up when values realign.
- [x] 5.3 [problem-details.mapper.ts](frontend-web/src/app/pages/auth/shared/problem-details.mapper.ts) — normalises first-char lowercase, preserves existing errors, sets `{ server: msg }`, `markAsTouched`, returns bool for caller.

## 6. i18n keys

- [x] 6.1 + 6.2 [en.json](frontend-web/public/i18n/en.json) + [zh-CN.json](frontend-web/public/i18n/zh-CN.json) both extended with full `auth.*` + `header.auth.*` tree (51 keys total each).
- [x] 6.3 Parity check run with a one-shot `node -e` flattener — zero drift between en and zh-CN.

## 7. Pages

- [x] 7.1 [login.ts](frontend-web/src/app/pages/auth/login/login.ts) + [login.html](frontend-web/src/app/pages/auth/login/login.html) — handles `?flash=password-changed` info banner, 401→invalid-credentials, 403→account-inactive, 400 ProblemDetails→field mapping, network error banner.
- [x] 7.2 [register.ts](frontend-web/src/app/pages/auth/register/register.ts) + [register.html](frontend-web/src/app/pages/auth/register/register.html) — maps 409 EmailAlreadyExistsException / UsernameAlreadyExistsException to the right field via `serverKey` (translatable); other 400s fall through to `mapProblemDetailsToForm`.
- [x] 7.3 [change-password.ts](frontend-web/src/app/pages/auth/change-password/change-password.ts) + [change-password.html](frontend-web/src/app/pages/auth/change-password/change-password.html) — form-level `matchFieldsValidator`, 401→wrong-current `serverKey`, 204→navigate to `/login?flash=password-changed` (AuthService already cleared state).
- [x] 7.4 Extracted [auth-card.ts](frontend-web/src/app/pages/auth/shared/auth-card.ts) — presentational wrapper all three pages render into; centred, `max-w-sm → sm:max-w-md`, all-token-utility styling.
- [x] 7.5 Submit buttons bound to a `submitting = signal(false)` per page; `[disabled]="submitting()"` in templates.

## 8. Routes + header

- [x] 8.1 [app.routes.ts](frontend-web/src/app/app.routes.ts) — three lazy routes (`login`, `register`, `account/password`) with guards; `/home` + wildcard redirects remain.
- [x] 8.2 [header.ts](frontend-web/src/app/shell/header/header.ts) now injects `AuthService` + `Router`, exposes `auth.user()`, `auth.isAuthenticated()`, and a `logout()` method that chains `auth.logout().subscribe(...)` then navigates `/home` (error path still navigates `/home` — local state already cleared).
- [x] 8.3 [header.html](frontend-web/src/app/shell/header/header.html) — `@if (auth.isAuthenticated())` block shows `{{ user.username }}` (auto-escaped) + logout button, else a primary-coloured `routerLink="/login"` CTA. All labels via `| transloco`, all styling via token utilities. The brand mark `Gomoku` is a routerLink back to `/home`.
- [x] 8.4 Verified 375 px build earlier in the scaffold step still holds with the new slot — `Language:` / `Theme:` / `Dark:` prefixes collapse below `sm:`, `username` hidden below `sm:` too (logout button remains). No horizontal scroll observed.

## 9. Tests

- [x] 9.1 [auth.service.spec.ts](frontend-web/src/app/core/auth/auth.service.spec.ts) — 8 tests covering login/register signal population, logout error tolerance, logout short-circuit when no refresh token, change-password state clear, bootstrap with no / valid / invalid refresh token.
- [x] 9.2 [auth.interceptor.spec.ts](frontend-web/src/app/core/auth/auth.interceptor.spec.ts) — 7 tests. Key assertions: Authorization attached/omitted correctly, 401→single refresh→retry, **3 concurrent 401s share exactly one refresh call**, refresh failure → `Router.navigate(['/login'], { queryParams: { returnUrl: ... } })`, retry-401 does not loop. Tests import `__resetAuthInterceptorStateForTests` to clean module-level state between cases.
- [x] 9.3 [auth.guards.spec.ts](frontend-web/src/app/core/auth/auth.guards.spec.ts) — 4 tests covering each cell of the 2×2 auth × guard matrix.
- [x] 9.4 [password-policy.validator.spec.ts](frontend-web/src/app/pages/auth/shared/password-policy.validator.spec.ts) — 6 tests covering each failure mode and empty-input passthrough.
- [x] 9.5 Smoke specs per page: [login.spec.ts](frontend-web/src/app/pages/auth/login/login.spec.ts), [register.spec.ts](frontend-web/src/app/pages/auth/register/register.spec.ts), [change-password.spec.ts](frontend-web/src/app/pages/auth/change-password/change-password.spec.ts) — assert service calls with expected args and field-level error mapping for key error paths.
- [x] 9.6 `npm run test:ci` — **10 files, 44 tests, all passing**.

## 10. Cross-cutting polish

- [x] 10.1 `grep -rnE '#[0-9a-fA-F]{3,8}\b|\brgb\(|\bhsl\(' src/app src/styles/tailwind.css src/styles/global.css` (excluding `core/theme/themes/` data files) returns zero matches.
- [x] 10.2 `grep -rnE 'bg-(gray|white|black|…)|text-(white|black|…)' src/app` — zero matches.
- [x] 10.3 `grep -rnP '[\x{4e00}-\x{9fff}]' src/app` — only hits are in `home.spec.ts` translation fixtures (same as scaffold baseline).
- [x] 10.4 `grep -rnE 'console\.(log|debug|info).*(access|refresh)Token' src/app/core/auth` — zero matches.
- [x] 10.5 `grep -rn 'bypassSecurityTrust' src/app` — zero matches.
- [x] 10.6 `grep -rn 'inject(HttpClient)' src/app` excluding `core/auth/` and `core/i18n/transloco-loader.ts` — zero matches; pages only talk to `AuthService`.
- [x] 10.7 Page class bodies: login 98 LOC, register 95 LOC, change-password 81 LOC — all well under the 150 LOC ceiling.

## 11. Manual verification (DevTools + real backend)

- [ ] 11.1 Register a fresh user via `/register` against live backend — expect `/home`, localStorage `gomoku:refresh` set, header shows username.
- [ ] 11.2 Hard reload — bootstrap refresh kicks in, user stays signed in.
- [ ] 11.3 Click logout — localStorage cleared, header flips to "Log in", navigate to `/home`.
- [ ] 11.4 Login with wrong password — translated banner, form not cleared, button re-enabled.
- [ ] 11.5 Login with correct password — navigate to `returnUrl` when present else `/home`.
- [ ] 11.6 Change password flow: `/account/password` → success → redirect to `/login?flash=password-changed` with banner; old password fails, new password works.
- [ ] 11.7 Force expired access token via DevTools (overwrite `authService.accessToken` in Angular DevTools or wait 15 min) — interceptor silently refreshes and retries.
- [ ] 11.8 Revoke refresh on backend then trigger a protected call — redirect to `/login?returnUrl=...`, state cleared.
- [ ] 11.9 Visual sweep: all three auth pages in material/system × light/dark × en/zh-CN = 8 combinations look correct.

## 12. Final verification

- [x] 12.1 `npm run lint` passes.
- [x] 12.2 `npm run test:ci` passes (44/44 tests in 10 files).
- [x] 12.3 `npm run build` passes. Initial chunk **98 KB gzip** (scaffold was 91.5 KB; auth plumbing adds ~6.5 KB). Three lazy chunks emitted: `login` 1.67 KB, `register` 1.84 KB, `change-password` 1.73 KB — each well under the 200 KB lazy-chunk ceiling.
- [x] 12.4 `openspec validate add-web-auth-pages` — clean.
