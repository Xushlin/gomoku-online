import type { EnvironmentProviders } from '@angular/core';
import { provideHttpClient, withInterceptors, type HttpInterceptorFn } from '@angular/common/http';
import { apiBaseUrlInterceptor } from '../api/api-base-url';
import { authInterceptor } from '../auth/auth.interceptor';

/**
 * Single source of truth for app-wide HttpClient configuration.
 *
 * Previously HttpClient was provided as a side-effect of `provideAppI18n()`.
 * Extracting it means (a) one place to wire interceptors, (b) `app.config.ts`
 * stays readable: auth → http → i18n, each helper in charge of exactly its
 * concern.
 */
/**
 * The interceptor chain the **application** runs, in order.
 *
 * Exported so a test can run the real chain without booting `appConfig` wholesale —
 * doing that starts the i18n app-initializer, which leaves an `EmptyError` rejection
 * on teardown and makes vitest report passing tests *and* an unhandled error
 * ("this might cause false positive tests"). `app.config.spec.ts` already carries
 * that scar in a comment; this constant is how the HTTP half gets tested without
 * re-opening it.
 *
 * Order matters: the base URL is applied first, so everything after it sees the
 * final address.
 */
export const APP_INTERCEPTORS: readonly HttpInterceptorFn[] = [
  apiBaseUrlInterceptor,
  authInterceptor,
];

export function provideAppHttp(interceptors: HttpInterceptorFn[] = []): EnvironmentProviders {
  return provideHttpClient(withInterceptors(interceptors));
}
