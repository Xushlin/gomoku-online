import { InjectionToken, inject } from '@angular/core';
import type { HttpInterceptorFn } from '@angular/common/http';

/**
 * Where the server is.
 *
 * **Empty by default, and that default is the whole point.** In a browser the app
 * is served from the same origin as the API, so `/api/rooms` is already correct
 * and prefixing it with `''` leaves it byte-for-byte unchanged. Every existing
 * test that asserts an exact URL is the executable form of that promise.
 *
 * A host that is *not* same-origin overrides it — the Electron shell loads the
 * page from the local filesystem, where there is no origin to be same as, so
 * every relative path would resolve to somewhere that does not exist. The symptom
 * is a 404 on login that looks exactly like the backend being down.
 *
 * No trailing slash: `'https://gewu.example'`, not `'https://gewu.example/'`.
 */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', {
  providedIn: 'root',
  factory: () => hostApiBaseUrl(),
});

/**
 * What the **host** says the server address is, or `''` when there is no host.
 *
 * A host is anything that loads this bundle from somewhere other than the server:
 * today the Electron shell (whose preload freezes `window.gewuHost` before Angular
 * bootstraps), tomorrow a mobile wrapper, or a static site served from a different
 * domain than its API.
 *
 * **Reading a global rather than taking a second Angular build is the whole point.**
 * One `dist/` serves both: a browser has no `gewuHost`, so this returns `''` and every
 * URL stays same-origin and byte-for-byte what it was.
 *
 * It is read synchronously and defensively — during injector construction, before
 * anything has had a chance to validate it. A host that sets a non-string is treated
 * as absent rather than allowed to concatenate `[object Object]` onto every request.
 */
export function hostApiBaseUrl(): string {
  const host = (globalThis as { gewuHost?: { apiBaseUrl?: unknown } }).gewuHost;
  const value = host?.apiBaseUrl;
  return typeof value === 'string' ? value : '';
}

/**
 * The path prefixes that belong to the **server**.
 *
 * `/i18n/` is deliberately absent, and that absence is load-bearing: the locale
 * files are assets of the app itself. In the desktop shell they sit next to the
 * bundle and must keep loading locally — sending them to the API server would
 * make the app fail to render text when the server is unreachable, which is a
 * strictly worse failure than "cannot log in".
 */
const SERVER_PREFIXES = ['/api/', '/hubs/'] as const;

/** Does this URL address the server (rather than an asset of the app)? */
export function isServerPath(url: string): boolean {
  return SERVER_PREFIXES.some((prefix) => url.startsWith(prefix));
}

/**
 * Join the base and a server-relative path.
 *
 * One implementation, two callers — the interceptor below and the SignalR hub,
 * which does not go through `HttpClient` and would otherwise need its own copy.
 * A second copy of "how do we build a server URL" is the kind that drifts into
 * *the realtime connection alone* pointing at the wrong host, and that failure
 * looks like "the game just does not update".
 */
export function serverUrl(base: string, path: string): string {
  return base && isServerPath(path) ? base + path : path;
}

/**
 * Prefixes server-bound requests with {@link API_BASE_URL}.
 *
 * An interceptor rather than a change in each of the nine services that call
 * `HttpClient`: it puts "which paths are the server's" in exactly one place, and
 * it keeps every service — and therefore every existing test — untouched.
 *
 * **It must run before `authInterceptor`**, which is why it is registered first:
 * that one decides whether to attach a token by looking at the path, and it is
 * written to read a path out of either form.
 */
export const apiBaseUrlInterceptor: HttpInterceptorFn = (req, next) => {
  const base = inject(API_BASE_URL);
  if (!base || !isServerPath(req.url)) {
    return next(req);
  }
  return next(req.clone({ url: base + req.url }));
};
