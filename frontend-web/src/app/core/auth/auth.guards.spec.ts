import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, type CanMatchFn, type UrlSegment } from '@angular/router';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from './auth.service';
import { authGuard, guestGuard } from './auth.guards';

class StubAuthService implements Partial<AuthService> {
  readonly isAuthenticated = signal<boolean>(false);
}

function runGuard(guard: CanMatchFn, path: string[]): unknown {
  const segments: UrlSegment[] = path.map(
    (p) => ({ path: p, parameters: {} }) as unknown as UrlSegment,
  );
  return TestBed.runInInjectionContext(() =>
    guard(
      { path: path.join('/') } as never,
      segments,
    ),
  );
}

describe('auth guards', () => {
  let auth: StubAuthService;
  let router: { createUrlTree: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    auth = new StubAuthService();
    router = { createUrlTree: vi.fn((commands, extras) => ({ commands, extras })) };
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: auth },
        { provide: Router, useValue: router },
      ],
    });
  });

  it('authGuard: returns true when authenticated', () => {
    auth.isAuthenticated.set(true);
    expect(runGuard(authGuard, ['account', 'password'])).toBe(true);
    expect(router.createUrlTree).not.toHaveBeenCalled();
  });

  it('authGuard: returns UrlTree to /login?returnUrl=<matched> when anonymous', () => {
    auth.isAuthenticated.set(false);
    const result = runGuard(authGuard, ['account', 'password']);
    expect(router.createUrlTree).toHaveBeenCalledWith(
      ['/login'],
      { queryParams: { returnUrl: '/account/password' } },
    );
    expect(result).toBeTruthy();
  });

  it('guestGuard: returns true when anonymous', () => {
    auth.isAuthenticated.set(false);
    expect(runGuard(guestGuard, ['login'])).toBe(true);
    expect(router.createUrlTree).not.toHaveBeenCalled();
  });

  it('guestGuard: returns UrlTree to /home when authenticated', () => {
    auth.isAuthenticated.set(true);
    const result = runGuard(guestGuard, ['login']);
    expect(router.createUrlTree).toHaveBeenCalledWith(['/home']);
    expect(result).toBeTruthy();
  });
});
