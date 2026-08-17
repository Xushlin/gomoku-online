import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import {
  DefaultLeaderboardApiService,
  LeaderboardApiService,
} from '../api/leaderboard-api.service';
import {
  DefaultPresenceApiService,
  PresenceApiService,
} from '../api/presence-api.service';
import { DefaultRoomsApiService, RoomsApiService } from '../api/rooms-api.service';
import { DefaultHomeDataService, HomeDataService } from './home-data.service';
import { DefaultLobbyDataService, LobbyDataService } from './lobby-data.service';
import { LOBBY_GAME_KEY } from './lobby-game-key';
import { LOBBY_POLLING_CONFIG } from './lobby-polling.config';

// Large-enough interval so the timers never fire during a test run — we
// exercise polling logic by directly dispatching `visibilitychange` or by
// calling `slice.refresh()` from tests.
const LARGE_INTERVAL = 60_000;

function providers() {
  return [
    provideHttpClient(),
    provideHttpClientTesting(),
    { provide: PresenceApiService, useClass: DefaultPresenceApiService },
    { provide: RoomsApiService, useClass: DefaultRoomsApiService },
    { provide: LeaderboardApiService, useClass: DefaultLeaderboardApiService },
    {
      provide: LOBBY_POLLING_CONFIG,
      useValue: {
        onlineCountMs: LARGE_INTERVAL,
        roomsMs: LARGE_INTERVAL,
        myRoomsMs: LARGE_INTERVAL,
      },
    },
  ];
}

function setupHome() {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [...providers(), { provide: HomeDataService, useClass: DefaultHomeDataService }],
  });
  return {
    http: TestBed.inject(HttpTestingController),
    service: TestBed.inject(HomeDataService),
  };
}

function setupGameLobby(gameKey = 'gomoku') {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      ...providers(),
      { provide: LOBBY_GAME_KEY, useValue: gameKey },
      { provide: LobbyDataService, useClass: DefaultLobbyDataService },
    ],
  });
  return {
    http: TestBed.inject(HttpTestingController),
    service: TestBed.inject(LobbyDataService),
  };
}

function setVisibility(state: DocumentVisibilityState): void {
  Object.defineProperty(document, 'visibilityState', { value: state, configurable: true });
  document.dispatchEvent(new Event('visibilitychange'));
}

function flushLeaderboard(http: HttpTestingController): void {
  http
    .expectOne((r) => r.url === '/api/leaderboard')
    .flush({ items: [], total: 0, page: 1, pageSize: 10 });
}

describe('DefaultHomeDataService', () => {
  beforeEach(() => setVisibility('visible'));
  afterEach(() => TestBed.resetTestingModule());

  it('fires exactly the two account-scoped endpoints on mount', () => {
    const { http } = setupHome();

    http.expectOne('/api/presence/online-count');
    http.expectOne('/api/users/me/active-rooms');
    http.verify();
  });

  it('never asks for anything scoped to a game', () => {
    // The whole reason /home has its own service. A four-slice service given a
    // game key would keep polling /api/rooms every 15s for a card this page no
    // longer renders — a defect visible only in the network panel.
    const { http } = setupHome();

    http.expectOne('/api/presence/online-count').flush({ count: 1 });
    http.expectOne('/api/users/me/active-rooms').flush([]);

    http.expectNone((r) => r.url === '/api/rooms');
    http.expectNone((r) => r.url === '/api/leaderboard');
    http.verify();
  });

  it('populates signals on successful response', () => {
    const { http, service } = setupHome();

    http.expectOne('/api/presence/online-count').flush({ count: 7 });
    http.expectOne('/api/users/me/active-rooms').flush([]);

    expect(service.onlineCount.data()).toBe(7);
    expect(service.myRooms.data()).toEqual([]);
    http.verify();
  });

  it('one slice erroring does not poison the other', () => {
    const { http, service } = setupHome();

    http.expectOne('/api/presence/online-count').flush(null, {
      status: 500,
      statusText: 'Server Error',
    });
    http.expectOne('/api/users/me/active-rooms').flush([]);

    expect(service.onlineCount.error()).not.toBeNull();
    expect(service.myRooms.error()).toBeNull();
    http.verify();
  });
});

describe('DefaultLobbyDataService', () => {
  beforeEach(() => setVisibility('visible'));
  afterEach(() => TestBed.resetTestingModule());

  it('fires exactly the two game-scoped endpoints on mount', () => {
    const { http } = setupGameLobby();

    http.expectOne('/api/rooms?gameKey=gomoku');
    http.expectOne((r) => r.url === '/api/leaderboard');
    http.expectNone('/api/presence/online-count');
    http.expectNone('/api/users/me/active-rooms');
    http.verify();
  });

  it('scopes every request to the injected game key', () => {
    const { http } = setupGameLobby('idiom-chain');

    http.expectOne('/api/rooms?gameKey=idiom-chain').flush([]);
    const board = http.expectOne((r) => r.url === '/api/leaderboard');
    expect(board.request.params.get('gameKey')).toBe('idiom-chain');
    board.flush({ items: [], total: 0, page: 1, pageSize: 10 });
    http.verify();
  });

  it('populates signals on successful response', () => {
    const { http, service } = setupGameLobby();

    http.expectOne('/api/rooms?gameKey=gomoku').flush([]);
    flushLeaderboard(http);

    expect(service.rooms.data()).toEqual([]);
    expect(service.leaderboard.data()).toEqual([]);
    http.verify();
  });

  it('dedups concurrent refresh() calls while one is in-flight', () => {
    const { http, service } = setupGameLobby();

    // Initial mount fires one /api/rooms; it's still pending (not flushed).
    // Subsequent refresh() calls MUST be ignored by the inFlight guard,
    // so match() should find exactly 1 pending request — not 3.
    service.rooms.refresh();
    service.rooms.refresh();

    const pending = http.match('/api/rooms?gameKey=gomoku');
    expect(pending.length).toBe(1);
    pending[0].flush([]);

    flushLeaderboard(http);
    http.verify();
  });

  it('one slice errors do not poison the others', () => {
    const { http, service } = setupGameLobby();

    http.expectOne('/api/rooms?gameKey=gomoku').flush([]);
    http.expectOne((r) => r.url === '/api/leaderboard').flush(null, {
      status: 500,
      statusText: 'Server Error',
    });

    expect(service.leaderboard.error()).not.toBeNull();
    expect(service.rooms.error()).toBeNull();
    http.verify();
  });

  it('visibility=hidden blocks refreshes triggered via the document event listener path', () => {
    const { http, service } = setupGameLobby();

    http.expectOne('/api/rooms?gameKey=gomoku').flush([]);
    flushLeaderboard(http);

    // Go hidden. No refresh should fire automatically.
    setVisibility('hidden');
    http.expectNone('/api/rooms?gameKey=gomoku');

    // Return to visible — because the polled slices were just fetched, the
    // stale check (> half interval) is false, so nothing fires here either.
    setVisibility('visible');
    http.expectNone('/api/rooms?gameKey=gomoku');

    // But an explicit refresh still works.
    service.rooms.refresh();
    http.expectOne('/api/rooms?gameKey=gomoku').flush([]);
    http.verify();

    expect(service.rooms.data()).toEqual([]);
  });
});
