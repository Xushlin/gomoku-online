import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it } from 'vitest';
import { DefaultGamesApiService, GamesApiService } from './games-api.service';

function setup() {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: GamesApiService, useClass: DefaultGamesApiService },
    ],
  });
  return {
    svc: TestBed.inject(GamesApiService),
    http: TestBed.inject(HttpTestingController),
  };
}

describe('GamesApiService', () => {
  it('list() GETs /api/games with no parameters', () => {
    const { svc, http } = setup();
    svc.list().subscribe();
    const req = http.expectOne('/api/games');
    expect(req.request.method).toBe('GET');
    expect(req.request.params.keys()).toEqual([]);
    req.flush([]);
    http.verify();
  });

  it('passes the descriptors straight through', () => {
    const { svc, http } = setup();
    const payload = [
      { gameKey: 'gomoku', isRated: true, supportsHumanVsHuman: true, rows: 15, cols: 15 },
      { gameKey: 'tictactoe', isRated: false, supportsHumanVsHuman: false, rows: 3, cols: 3 },
    ];
    let data: unknown;
    svc.list().subscribe((v) => (data = v));
    http.expectOne('/api/games').flush(payload);
    expect(data).toEqual(payload);
    http.verify();
  });
});
