import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import type { LeaderboardEntry, PagedResult } from './models/leaderboard.model';

export abstract class LeaderboardApiService {
  abstract top(count: number): Observable<readonly LeaderboardEntry[]>;
  abstract getPage(page: number, pageSize: number): Observable<PagedResult<LeaderboardEntry>>;
}

@Injectable({ providedIn: 'root' })
export class DefaultLeaderboardApiService extends LeaderboardApiService {
  private readonly http = inject(HttpClient);

  top(count: number): Observable<readonly LeaderboardEntry[]> {
    return this.getPage(1, count).pipe(map((page) => page.items));
  }

  getPage(page: number, pageSize: number): Observable<PagedResult<LeaderboardEntry>> {
    const params = new HttpParams()
      .set('page', String(page))
      .set('pageSize', String(pageSize));
    return this.http.get<PagedResult<LeaderboardEntry>>('/api/leaderboard', { params });
  }
}
