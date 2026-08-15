import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import type { Observable } from 'rxjs';
import type {
  PagedResult,
  UserGameSummaryDto,
  UserPublicProfileDto,
} from './models/user-profile.model';

/**
 * Cross-cutting read-side API for "any user that isn't the logged-in caller":
 *   - public profile (`UserPublicProfileDto`),
 *   - paginated finished-games list (`UserGameSummaryDto`),
 *   - prefix search across non-bot accounts.
 *
 * `AuthService` still owns the logged-in user via `/api/users/me`. This
 * service is a sibling, so the boundary stays tidy. Components MUST
 * `inject(UsersApiService)` (the abstract DI token), never the default impl
 * directly, so tests can swap a stub.
 */
export abstract class UsersApiService {
  /**
   * Public profile, optionally scoped to one game's record.
   *
   * `gameKey` is optional here while `LeaderboardApiService`'s is required, and
   * that is not an inconsistency: omitting it is a *meaningful value* — "use
   * the server's gomoku default" — which is exactly how the profile page's
   * first paint arrives. On the leaderboard page there is no such reading;
   * omitting it there could only ever be a forgotten argument.
   */
  abstract getProfile(userId: string, gameKey?: string): Observable<UserPublicProfileDto>;
  abstract getGames(
    userId: string,
    page: number,
    pageSize: number,
  ): Observable<PagedResult<UserGameSummaryDto>>;
  abstract search(
    query: string,
    page: number,
    pageSize: number,
  ): Observable<PagedResult<UserPublicProfileDto>>;
}

@Injectable({ providedIn: 'root' })
export class DefaultUsersApiService extends UsersApiService {
  private readonly http = inject(HttpClient);

  getProfile(userId: string, gameKey?: string): Observable<UserPublicProfileDto> {
    const options = gameKey ? { params: new HttpParams().set('gameKey', gameKey) } : {};
    return this.http.get<UserPublicProfileDto>(
      `/api/users/${encodeURIComponent(userId)}`,
      options,
    );
  }

  getGames(
    userId: string,
    page: number,
    pageSize: number,
  ): Observable<PagedResult<UserGameSummaryDto>> {
    const params = new HttpParams()
      .set('page', String(page))
      .set('pageSize', String(pageSize));
    return this.http.get<PagedResult<UserGameSummaryDto>>(
      `/api/users/${encodeURIComponent(userId)}/games`,
      { params },
    );
  }

  search(
    query: string,
    page: number,
    pageSize: number,
  ): Observable<PagedResult<UserPublicProfileDto>> {
    const params = new HttpParams()
      .set('search', query)
      .set('page', String(page))
      .set('pageSize', String(pageSize));
    return this.http.get<PagedResult<UserPublicProfileDto>>('/api/users', {
      params,
    });
  }
}
