import type { EnvironmentProviders } from '@angular/core';
import { provideHttpClient, withInterceptors, type HttpInterceptorFn } from '@angular/common/http';

/**
 * Single source of truth for app-wide HttpClient configuration.
 *
 * Previously HttpClient was provided as a side-effect of `provideAppI18n()`.
 * Extracting it means (a) one place to wire interceptors, (b) `app.config.ts`
 * stays readable: auth → http → i18n, each helper in charge of exactly its
 * concern.
 */
export function provideAppHttp(interceptors: HttpInterceptorFn[] = []): EnvironmentProviders {
  return provideHttpClient(withInterceptors(interceptors));
}
