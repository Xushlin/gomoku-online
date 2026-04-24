import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { EMPTY, Subject } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from './auth.service';
import { authInterceptor, __resetAuthInterceptorStateForTests } from './auth.interceptor';

class StubAuthService implements Partial<AuthService> {
  readonly accessToken = signal<string | null>(null);
  readonly user = signal<null>(null);
  readonly accessTokenExpiresAt = signal<Date | null>(null);
  readonly isAuthenticated = signal<boolean>(false);
  refresh = vi.fn();
}

function configureTestBed(stub: StubAuthService, routerSpy: { navigate: ReturnType<typeof vi.fn>; navigateByUrl: ReturnType<typeof vi.fn> }) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(withInterceptors([authInterceptor])),
      provideHttpClientTesting(),
      { provide: AuthService, useValue: stub },
      { provide: Router, useValue: routerSpy },
    ],
  });
}

function makeRouter() {
  return {
    navigate: vi.fn(() => Promise.resolve(true)),
    navigateByUrl: vi.fn(() => Promise.resolve(true)),
  };
}

describe('authInterceptor', () => {
  let stub: StubAuthService;
  let router: ReturnType<typeof makeRouter>;

  beforeEach(() => {
    __resetAuthInterceptorStateForTests();
    stub = new StubAuthService();
    router = makeRouter();
    configureTestBed(stub, router);
  });

  afterEach(() => {
    __resetAuthInterceptorStateForTests();
  });

  it('attaches Authorization: Bearer for non-auth URLs when token is set', () => {
    stub.accessToken.set('tok-123');

    const http = TestBed.inject(HttpClient);
    const ctrl = TestBed.inject(HttpTestingController);

    http.get('/api/rooms').subscribe();
    const req = ctrl.expectOne('/api/rooms');
    expect(req.request.headers.get('Authorization')).toBe('Bearer tok-123');
    req.flush([]);
    ctrl.verify();
  });

  it('does NOT attach Authorization on /api/auth/login even when a token is set', () => {
    stub.accessToken.set('tok-123');
    const http = TestBed.inject(HttpClient);
    const ctrl = TestBed.inject(HttpTestingController);

    http.post('/api/auth/login', { email: 'x', password: 'y' }).subscribe();
    const req = ctrl.expectOne('/api/auth/login');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
    ctrl.verify();
  });

  it('does NOT attach Authorization on /api/auth/register or /api/auth/refresh', () => {
    stub.accessToken.set('tok-123');
    const http = TestBed.inject(HttpClient);
    const ctrl = TestBed.inject(HttpTestingController);

    http.post('/api/auth/register', {}).subscribe();
    http.post('/api/auth/refresh', {}).subscribe();

    expect(ctrl.expectOne('/api/auth/register').request.headers.has('Authorization')).toBe(false);
    expect(ctrl.expectOne('/api/auth/refresh').request.headers.has('Authorization')).toBe(false);
    ctrl.verify();
  });

  it('401 on a non-auth URL triggers one refresh and retries the original request once', () => {
    stub.accessToken.set('stale');
    const refreshSubject = new Subject<void>();
    stub.refresh.mockImplementation(() => refreshSubject.asObservable());

    const http = TestBed.inject(HttpClient);
    const ctrl = TestBed.inject(HttpTestingController);

    let finalResponse: unknown = null;
    http.get<{ items: string[] }>('/api/rooms').subscribe((r) => (finalResponse = r));

    const first = ctrl.expectOne('/api/rooms');
    expect(first.request.headers.get('Authorization')).toBe('Bearer stale');
    first.flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(stub.refresh).toHaveBeenCalledTimes(1);

    // Simulate refresh success: token is updated, subject completes.
    stub.accessToken.set('fresh');
    refreshSubject.next();
    refreshSubject.complete();

    const retry = ctrl.expectOne('/api/rooms');
    expect(retry.request.headers.get('Authorization')).toBe('Bearer fresh');
    retry.flush({ items: ['r1'] });

    expect(finalResponse).toEqual({ items: ['r1'] });
    ctrl.verify();
  });

  it('3 concurrent 401s share exactly one refresh call', () => {
    stub.accessToken.set('stale');
    const refreshSubject = new Subject<void>();
    stub.refresh.mockImplementation(() => refreshSubject.asObservable());

    const http = TestBed.inject(HttpClient);
    const ctrl = TestBed.inject(HttpTestingController);

    const results: number[] = [];
    http.get('/api/rooms').subscribe(() => results.push(1));
    http.get('/api/leaderboard').subscribe(() => results.push(2));
    http.get('/api/users/me').subscribe(() => results.push(3));

    // All three get 401s.
    ctrl.expectOne('/api/rooms').flush(null, { status: 401, statusText: 'Unauthorized' });
    ctrl.expectOne('/api/leaderboard').flush(null, { status: 401, statusText: 'Unauthorized' });
    ctrl.expectOne('/api/users/me').flush(null, { status: 401, statusText: 'Unauthorized' });

    // Exactly one refresh attempt on the wire.
    expect(stub.refresh).toHaveBeenCalledTimes(1);

    // Resolve the refresh.
    stub.accessToken.set('fresh');
    refreshSubject.next();
    refreshSubject.complete();

    // All three originals are retried with the new token.
    ctrl.expectOne('/api/rooms').flush({});
    ctrl.expectOne('/api/leaderboard').flush({});
    ctrl.expectOne('/api/users/me').flush({});

    expect(results.sort()).toEqual([1, 2, 3]);
    ctrl.verify();
  });

  it('refresh failure clears state and navigates to /login?returnUrl=...', () => {
    stub.accessToken.set('stale');
    stub.refresh.mockImplementation(() => {
      // mimic real refresh: on failure it errors out.
      return new Subject<void>().asObservable().pipe();
    });
    const refreshSubject = new Subject<void>();
    stub.refresh.mockReturnValue(refreshSubject.asObservable());

    const http = TestBed.inject(HttpClient);
    const ctrl = TestBed.inject(HttpTestingController);

    let sawError = false;
    http.get('/api/rooms').subscribe({ error: () => (sawError = true) });

    ctrl.expectOne('/api/rooms').flush(null, { status: 401, statusText: 'Unauthorized' });
    refreshSubject.error(new Error('refresh failed'));

    expect(sawError).toBe(true);
    expect(router.navigate).toHaveBeenCalledWith(
      ['/login'],
      expect.objectContaining({ queryParams: expect.objectContaining({ returnUrl: expect.any(String) }) }),
    );
    ctrl.verify();
  });

  it('a retry that 401s does NOT trigger a second refresh', () => {
    stub.accessToken.set('stale');
    const refreshSubject = new Subject<void>();
    stub.refresh.mockReturnValue(refreshSubject.asObservable());

    const http = TestBed.inject(HttpClient);
    const ctrl = TestBed.inject(HttpTestingController);

    let sawError = false;
    http.get('/api/rooms').subscribe({ error: () => (sawError = true) });

    ctrl.expectOne('/api/rooms').flush(null, { status: 401, statusText: 'Unauthorized' });
    stub.accessToken.set('fresh');
    refreshSubject.next();
    refreshSubject.complete();

    // Retry also 401s.
    ctrl.expectOne('/api/rooms').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(stub.refresh).toHaveBeenCalledTimes(1);
    expect(sawError).toBe(true);
    ctrl.verify();
  });

  // Consume EMPTY import so TS doesn't strip the unused marker in minifiers.
  void EMPTY;
});
