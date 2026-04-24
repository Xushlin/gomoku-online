import {
  HttpErrorResponse,
  type HttpEvent,
  type HttpHandlerFn,
  type HttpInterceptorFn,
  type HttpRequest,
} from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, Observable, ReplaySubject, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

/**
 * URL prefixes that MUST NOT carry an Authorization header and MUST NOT be
 * retried after a silent refresh. These are the endpoints where a token is
 * either irrelevant or is itself the credential.
 */
const NO_AUTH_PREFIXES = ['/api/auth/login', '/api/auth/register', '/api/auth/refresh'] as const;

function isNoAuthRequest(url: string): boolean {
  return NO_AUTH_PREFIXES.some((prefix) => url.startsWith(prefix));
}

/**
 * Module-level dedup subject for concurrent refreshes.
 * - null while no refresh is in flight.
 * - A ReplaySubject during refresh: emits the new access token on success,
 *   or errors with the refresh failure. Late subscribers still get the result
 *   (ReplaySubject(1)).
 *
 * This is shared across all instantiations of the interceptor in a browser
 * session; there is only one, matching the single SPA window.
 */
let refreshInFlight$: ReplaySubject<string> | null = null;

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (isNoAuthRequest(req.url)) {
    // Never attach a bearer, never attempt refresh-on-401 — the caller owns
    // the credential lifecycle for these endpoints.
    return next(req);
  }

  const token = auth.accessToken();
  const authed = token ? attachBearer(req, token) : req;

  return next(authed).pipe(
    catchError((err: unknown) => {
      if (!(err instanceof HttpErrorResponse) || err.status !== 401) {
        return throwError(() => err);
      }
      return handle401(authed, next, auth, router);
    }),
  );
};

function attachBearer(req: HttpRequest<unknown>, token: string): HttpRequest<unknown> {
  return req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
}

function handle401(
  original: HttpRequest<unknown>,
  next: HttpHandlerFn,
  auth: AuthService,
  router: Router,
): Observable<HttpEvent<unknown>> {
  return getOrStartRefresh(auth).pipe(
    switchMap((newToken) => next(attachBearer(original, newToken))),
    catchError((refreshErr: unknown) => {
      // Refresh itself failed — session is gone. Kick to login; propagate the
      // ORIGINAL 401 to the caller (they asked for data, not a refresh).
      navigateToLogin(router);
      return throwError(() => refreshErr);
    }),
  );
}

function getOrStartRefresh(auth: AuthService): Observable<string> {
  if (refreshInFlight$ !== null) {
    return refreshInFlight$.asObservable();
  }
  const subject = new ReplaySubject<string>(1);
  refreshInFlight$ = subject;

  auth.refresh().subscribe({
    next: () => {
      const token = auth.accessToken();
      if (token) {
        subject.next(token);
        subject.complete();
      } else {
        subject.error(new Error('Refresh succeeded but no access token was set.'));
      }
      refreshInFlight$ = null;
    },
    error: (err: unknown) => {
      subject.error(err);
      refreshInFlight$ = null;
    },
  });

  return subject.asObservable();
}

function navigateToLogin(router: Router): void {
  const returnUrl = typeof window !== 'undefined' ? window.location.pathname + window.location.search : '/';
  const isAlreadyOnLogin = returnUrl.startsWith('/login');
  if (isAlreadyOnLogin) {
    void router.navigateByUrl('/login');
    return;
  }
  void router.navigate(['/login'], { queryParams: { returnUrl } });
}

/**
 * Test-only hook for resetting the module-level dedup subject between
 * test cases. Not exported from the barrel; tests import directly.
 */
export function __resetAuthInterceptorStateForTests(): void {
  refreshInFlight$ = null;
  // Emptying-out observable state: consumers that still hold a reference
  // will not leak, since we just null the module ref; GC handles the rest.
}

/** Expose the refresh-in-flight for test assertions. */
export function __isRefreshInFlightForTests(): boolean {
  return refreshInFlight$ !== null;
}
