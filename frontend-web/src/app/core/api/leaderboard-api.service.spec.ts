import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import {
  DefaultLeaderboardApiService,
  LeaderboardApiService,
} from './leaderboard-api.service';

function setup() {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: LeaderboardApiService, useClass: DefaultLeaderboardApiService },
    ],
  });
  return {
    svc: TestBed.inject(LeaderboardApiService),
    http: TestBed.inject(HttpTestingController),
  };
}

describe('LeaderboardApiService', () => {
  it('getPage(page, pageSize) hits /api/leaderboard with query params', () => {
    const { svc, http } = setup();
    svc.getPage(2, 20).subscribe();
    const req = http.expectOne((r) => r.url === '/api/leaderboard');
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('pageSize')).toBe('20');
    req.flush({ items: [], total: 0, page: 2, pageSize: 20 });
    http.verify();
  });

  it('top(n) wraps getPage(1, n) and returns .items', () => {
    const { svc, http } = setup();
    const entries = [
      { rank: 1, userId: 'u-1', username: 'alice', rating: 1500, gamesPlayed: 10, wins: 7, losses: 2, draws: 1 },
    ];
    let data: unknown;
    svc.top(10).subscribe((v) => (data = v));

    const req = http.expectOne((r) => r.url === '/api/leaderboard');
    expect(req.request.params.get('page')).toBe('1');
    expect(req.request.params.get('pageSize')).toBe('10');
    req.flush({ items: entries, total: 1, page: 1, pageSize: 10 });

    expect(data).toEqual(entries);
    http.verify();
  });
});
