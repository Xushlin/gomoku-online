import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { AuthService, DefaultAuthService } from './auth.service';
import type { AuthResponseWire } from './auth-response.model';

function futureExpiry(): string {
  return new Date(Date.now() + 15 * 60 * 1000).toISOString();
}

function wireAuth(overrides: Partial<AuthResponseWire> = {}): AuthResponseWire {
  return {
    accessToken: 'access-abc',
    refreshToken: 'refresh-xyz',
    accessTokenExpiresAt: futureExpiry(),
    user: { id: 'u-1', username: 'alice', email: 'alice@example.com' },
    ...overrides,
  };
}

function setup() {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: AuthService, useClass: DefaultAuthService },
    ],
  });
  const auth = TestBed.inject(AuthService);
  const http = TestBed.inject(HttpTestingController);
  return { auth, http };
}

describe('DefaultAuthService', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('login() populates all three signals and persists refresh token', () => {
    const { auth, http } = setup();

    let emitted = false;
    auth.login('alice@example.com', 'Password1').subscribe({ next: () => (emitted = true) });

    const req = http.expectOne('/api/auth/login');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ email: 'alice@example.com', password: 'Password1' });
    req.flush(wireAuth());

    expect(emitted).toBe(true);
    expect(auth.accessToken()).toBe('access-abc');
    expect(auth.user()?.username).toBe('alice');
    expect(auth.accessTokenExpiresAt()).toBeInstanceOf(Date);
    expect(auth.isAuthenticated()).toBe(true);
    expect(localStorage.getItem('gomoku:refresh')).toBe('refresh-xyz');

    http.verify();
  });

  it('register() populates state the same way as login()', () => {
    const { auth, http } = setup();

    auth.register('bob@example.com', 'bob', 'Password1').subscribe();
    http.expectOne('/api/auth/register').flush(
      wireAuth({ user: { id: 'u-2', username: 'bob', email: 'bob@example.com' } }),
    );

    expect(auth.user()?.username).toBe('bob');
    expect(auth.isAuthenticated()).toBe(true);
    http.verify();
  });

  it('logout() clears state and storage even when HTTP errors', () => {
    const { auth, http } = setup();
    auth.login('alice@example.com', 'Password1').subscribe();
    http.expectOne('/api/auth/login').flush(wireAuth());

    let completed = false;
    auth.logout().subscribe({ complete: () => (completed = true) });
    http.expectOne('/api/auth/logout').error(new ProgressEvent('error'));

    expect(completed).toBe(true);
    expect(auth.accessToken()).toBeNull();
    expect(auth.user()).toBeNull();
    expect(auth.isAuthenticated()).toBe(false);
    expect(localStorage.getItem('gomoku:refresh')).toBeNull();
    http.verify();
  });

  it('logout() without a stored refresh token skips HTTP and clears locally', () => {
    const { auth, http } = setup();

    let completed = false;
    auth.logout().subscribe({ complete: () => (completed = true) });

    expect(completed).toBe(true);
    http.expectNone('/api/auth/logout');
    expect(auth.isAuthenticated()).toBe(false);
    http.verify();
  });

  it('changePassword() 204 clears all state so next session must re-login', () => {
    const { auth, http } = setup();
    auth.login('alice@example.com', 'Password1').subscribe();
    http.expectOne('/api/auth/login').flush(wireAuth());

    auth.changePassword('Password1', 'NewPassword2').subscribe();
    const req = http.expectOne('/api/auth/change-password');
    expect(req.request.body).toEqual({ currentPassword: 'Password1', newPassword: 'NewPassword2' });
    req.flush(null, { status: 204, statusText: 'No Content' });

    expect(auth.isAuthenticated()).toBe(false);
    expect(localStorage.getItem('gomoku:refresh')).toBeNull();
    http.verify();
  });

  it('bootstrap() without a stored refresh resolves immediately unauthenticated', async () => {
    const { auth, http } = setup();

    await auth.bootstrap();

    expect(auth.isAuthenticated()).toBe(false);
    http.verify();
  });

  it('bootstrap() with a valid refresh populates state before resolve', async () => {
    const { auth, http } = setup();
    localStorage.setItem('gomoku:refresh', 'refresh-original');

    const bootstrapPromise = auth.bootstrap();
    const req = http.expectOne('/api/auth/refresh');
    expect(req.request.body).toEqual({ refreshToken: 'refresh-original' });
    req.flush(wireAuth({ accessToken: 'access-new', refreshToken: 'refresh-new' }));

    await bootstrapPromise;
    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.accessToken()).toBe('access-new');
    expect(localStorage.getItem('gomoku:refresh')).toBe('refresh-new');
    http.verify();
  });

  it('bootstrap() with an invalid refresh clears storage and resolves unauthenticated', async () => {
    const { auth, http } = setup();
    localStorage.setItem('gomoku:refresh', 'refresh-stale');

    const bootstrapPromise = auth.bootstrap();
    http.expectOne('/api/auth/refresh').flush(
      { type: 'InvalidRefreshTokenException', status: 401 },
      { status: 401, statusText: 'Unauthorized' },
    );

    await bootstrapPromise;
    expect(auth.isAuthenticated()).toBe(false);
    expect(localStorage.getItem('gomoku:refresh')).toBeNull();
    http.verify();
  });
});
