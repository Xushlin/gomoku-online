import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import {
  computed,
  DOCUMENT,
  inject,
  Injectable,
  signal,
  type Signal,
} from '@angular/core';
import { catchError, firstValueFrom, map, Observable, of, tap, throwError, timeout } from 'rxjs';
import {
  parseAuthResponse,
  type AuthResponse,
  type AuthResponseWire,
} from './auth-response.model';
import type { UserDto } from './user.model';

const REFRESH_STORAGE_KEY = 'gomoku:refresh';
const BOOTSTRAP_TIMEOUT_MS = 5_000;

/**
 * Cross-cutting auth API. Signals are the source of truth; methods mutate
 * them synchronously after HTTP success so subscribers observe only fully-
 * populated state.
 */
export abstract class AuthService {
  abstract readonly accessToken: Signal<string | null>;
  abstract readonly user: Signal<UserDto | null>;
  abstract readonly accessTokenExpiresAt: Signal<Date | null>;
  abstract readonly isAuthenticated: Signal<boolean>;
  abstract login(email: string, password: string): Observable<void>;
  abstract register(email: string, username: string, password: string): Observable<void>;
  abstract logout(): Observable<void>;
  abstract changePassword(currentPassword: string, newPassword: string): Observable<void>;
  abstract refresh(): Observable<void>;
  abstract bootstrap(): Promise<void>;
}

@Injectable()
export class DefaultAuthService extends AuthService {
  private readonly http = inject(HttpClient);
  private readonly doc = inject(DOCUMENT);

  private readonly _accessToken = signal<string | null>(null);
  private readonly _user = signal<UserDto | null>(null);
  private readonly _expiresAt = signal<Date | null>(null);

  readonly accessToken: Signal<string | null> = this._accessToken.asReadonly();
  readonly user: Signal<UserDto | null> = this._user.asReadonly();
  readonly accessTokenExpiresAt: Signal<Date | null> = this._expiresAt.asReadonly();
  readonly isAuthenticated: Signal<boolean> = computed(
    () => this._accessToken() !== null && this._user() !== null,
  );

  login(email: string, password: string): Observable<void> {
    return this.http
      .post<AuthResponseWire>('/api/auth/login', { email, password })
      .pipe(
        map(parseAuthResponse),
        tap((res) => this.applyAuthResponse(res)),
        map(() => undefined),
      );
  }

  register(email: string, username: string, password: string): Observable<void> {
    return this.http
      .post<AuthResponseWire>('/api/auth/register', { email, username, password })
      .pipe(
        map(parseAuthResponse),
        tap((res) => this.applyAuthResponse(res)),
        map(() => undefined),
      );
  }

  refresh(): Observable<void> {
    const refreshToken = this.readRefreshToken();
    if (!refreshToken) {
      return throwError(() => new HttpErrorResponse({ status: 401, statusText: 'No refresh token' }));
    }
    return this.http
      .post<AuthResponseWire>('/api/auth/refresh', { refreshToken })
      .pipe(
        map(parseAuthResponse),
        tap((res) => this.applyAuthResponse(res)),
        map(() => undefined),
        catchError((err: unknown) => {
          this.clearAuthState();
          return throwError(() => err);
        }),
      );
  }

  logout(): Observable<void> {
    const refreshToken = this.readRefreshToken();
    const cleanup = () => this.clearAuthState();
    if (!refreshToken) {
      cleanup();
      return of(undefined);
    }
    return this.http.post<void>('/api/auth/logout', { refreshToken }).pipe(
      tap({
        next: () => cleanup(),
        error: () => cleanup(),
      }),
      catchError(() => of(undefined)),
      map(() => undefined),
    );
  }

  changePassword(currentPassword: string, newPassword: string): Observable<void> {
    return this.http
      .post<void>('/api/auth/change-password', { currentPassword, newPassword })
      .pipe(
        tap(() => this.clearAuthState()),
        map(() => undefined),
      );
  }

  async bootstrap(): Promise<void> {
    const refreshToken = this.readRefreshToken();
    if (!refreshToken) return;
    try {
      await firstValueFrom(this.refresh().pipe(timeout({ each: BOOTSTRAP_TIMEOUT_MS })));
    } catch {
      // Refresh failure or timeout — don't block app boot. `refresh()` has
      // already cleared state on HTTP failures; on timeout we deliberately
      // leave the stored refresh token in place so the next attempt can retry.
      return;
    }
  }

  private applyAuthResponse(res: AuthResponse): void {
    // All three signals are set in the same sync block so a `computed` or
    // `effect` watching `isAuthenticated` observes exactly one transition.
    this._accessToken.set(res.accessToken);
    this._user.set(res.user);
    this._expiresAt.set(res.accessTokenExpiresAt);
    this.writeRefreshToken(res.refreshToken);
  }

  private clearAuthState(): void {
    this._accessToken.set(null);
    this._user.set(null);
    this._expiresAt.set(null);
    this.removeRefreshToken();
  }

  private readRefreshToken(): string | null {
    try {
      return this.doc.defaultView?.localStorage.getItem(REFRESH_STORAGE_KEY) ?? null;
    } catch {
      return null;
    }
  }

  private writeRefreshToken(value: string): void {
    try {
      this.doc.defaultView?.localStorage.setItem(REFRESH_STORAGE_KEY, value);
    } catch {
      // Quota / private mode — ignore, refresh becomes session-scoped.
    }
  }

  private removeRefreshToken(): void {
    try {
      this.doc.defaultView?.localStorage.removeItem(REFRESH_STORAGE_KEY);
    } catch {
      // ignore
    }
  }
}
