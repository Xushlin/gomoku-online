/// <reference types="vite/client" />
import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { appConfig } from './app.config';

/**
 * Every abstract API token the app declares must actually be provided, and every
 * provided one must resolve.
 *
 * This file exists because the first half was **not** true and nothing noticed.
 * `add-web-tetris` added `ScoreRunsApiService` as an abstract DI token with a
 * `@Injectable({ providedIn: 'root' })` implementation — which registers the
 * *implementation*, not the token — and forgot the `{ provide, useClass }` line.
 * All 563 unit tests passed, because a component spec supplies its own stub for
 * exactly the service under test. It only showed up in a browser, as
 * `NG0201: No provider found for ScoreRunsApiService`, and the symptom was a route
 * that silently refused to render.
 *
 * The file list is **derived** via Vite's `import.meta.glob`, not hand-written: a
 * hand-written list is the defect this project has fixed three times on the backend
 * — a list someone must remember to extend, sitting next to a walking test that
 * believes it is complete.
 *
 * **The comparison is by name, and that is not laziness.** The first version of this
 * spec injected the globbed class objects directly and every token failed, including
 * the six that work in production. The reason: the glob's module ids carry the `.ts`
 * extension while `app.config.ts` imports them without it, so the two resolve to
 * separate module instances — two distinct classes with the same name, and therefore
 * two distinct DI tokens. Names are stable across that; class identity is not.
 */
describe('appConfig', () => {
  // Must stay literal — `import.meta.glob` is a Vite transform, and the
  // triple-slash reference above is what types it without touching tsconfig.
  const modules: Record<string, Record<string, unknown>> = import.meta.glob(
    './core/api/*-api.service.ts',
    { eager: true },
  );

  /**
   * Abstract API tokens by name: an exported class ending in `ApiService` that does
   * not start with `Default`. The `Default*` classes are implementations, reached
   * through their token rather than directly.
   */
  const declaredNames = Object.values(modules)
    .flatMap((mod) => Object.keys(mod).filter((name) => isTokenName(name, mod)))
    .sort();

  /** The `{ provide, useClass }` entries for API tokens, taken from the real config. */
  const apiProviders = flatten(appConfig.providers).filter((p) => {
    const token = (p as { provide?: unknown }).provide;
    return typeof token === 'function' && token.name.endsWith('ApiService');
  });

  const providedTokens = apiProviders.map(
    (p) => (p as { provide: abstract new () => unknown }).provide,
  );

  it('finds API tokens on both sides', () => {
    // Without this, a broken glob or a renamed convention would leave the suite
    // checking nothing — the exact shape of the bug it was written for.
    expect(declaredNames.length, 'declared').toBeGreaterThanOrEqual(6);
    expect(providedTokens.length, 'provided').toBeGreaterThanOrEqual(6);
    expect(declaredNames).toContain('ScoreRunsApiService');
  });

  it('provides every abstract API token declared under core/api', () => {
    const providedNames = providedTokens.map((t) => t.name).sort();

    expect(providedNames).toEqual(declaredNames);
  });

  it.each(providedTokens.map((t) => [t.name, t] as const))('resolves %s', (name, token) => {
    // Only the providers under test, not the whole config. Booting `appConfig`
    // wholesale also runs its app-initializer — which loads i18n over HTTP and
    // leaves an `EmptyError: no elements in sequence` rejection on teardown, so the
    // suite reported 573 passing tests *and* exit code 1. A narrower injector is
    // both quieter and a sharper statement of what is being checked.
    TestBed.configureTestingModule({ providers: [provideHttpClient(), ...apiProviders] });

    expect(TestBed.inject(token), name).toBeInstanceOf(token);
  });
});

function isTokenName(name: string, mod: Record<string, unknown>): boolean {
  return (
    typeof mod[name] === 'function' && name.endsWith('ApiService') && !name.startsWith('Default')
  );
}

/** Providers nest arbitrarily deep; `EnvironmentProviders` are opaque and skipped. */
function flatten(providers: readonly unknown[]): unknown[] {
  return providers.flatMap((p) => (Array.isArray(p) ? flatten(p) : [p]));
}
