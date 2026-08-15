import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import type { LeaderboardEntry, PagedResult } from './models/leaderboard.model';

/**
 * Per-game leaderboard reads.
 *
 * `gameKey` is **required** on every method — the same discipline the backend's
 * `GetLeaderboardQuery.GameKey` follows: the service does not guess which game
 * it is being asked about.
 *
 * The backend does default it to `gomoku`, but only at the controller, because
 * that is the backward-compatibility boundary for already-published clients.
 * This client has no such obligation: every call site knows which game it is
 * showing, so letting the service guess would only turn "forgot to pass it"
 * into "silently served gomoku data" — and that mistake is invisible on screen.
 */
export abstract class LeaderboardApiService {
  abstract top(gameKey: string, count: number): Observable<readonly LeaderboardEntry[]>;
  abstract getPage(
    gameKey: string,
    page: number,
    pageSize: number,
  ): Observable<PagedResult<LeaderboardEntry>>;
}

@Injectable({ providedIn: 'root' })
export class DefaultLeaderboardApiService extends LeaderboardApiService {
  private readonly http = inject(HttpClient);

  top(gameKey: string, count: number): Observable<readonly LeaderboardEntry[]> {
    return this.getPage(gameKey, 1, count).pipe(map((page) => page.items));
  }

  getPage(
    gameKey: string,
    page: number,
    pageSize: number,
  ): Observable<PagedResult<LeaderboardEntry>> {
    const params = new HttpParams()
      .set('gameKey', gameKey)
      .set('page', String(page))
      .set('pageSize', String(pageSize));
    return this.http.get<PagedResult<LeaderboardEntry>>('/api/leaderboard', { params });
  }
}
