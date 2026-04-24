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
import {
  DefaultLobbyDataService,
  LobbyDataService,
} from './lobby-data.service';
import { LOBBY_POLLING_CONFIG } from './lobby-polling.config';

// Large-enough interval so the timers never fire during a test run — we
// exercise polling logic by directly dispatching `visibilitychange` or by
// calling `slice.refresh()` from tests.
const LARGE_INTERVAL = 60_000;

function setup() {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
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

describe('DefaultLobbyDataService', () => {
  beforeEach(() => {
    setVisibility('visible');
  });

  afterEach(() => {
    TestBed.resetTestingModule();
  });

  it('initial mount fires all four endpoints in parallel', () => {
    const { http } = setup();

    http.expectOne('/api/presence/online-count');
    http.expectOne('/api/rooms');
    http.expectOne('/api/users/me/active-rooms');
    http.expectOne((r) => r.url === '/api/leaderboard');
    http.verify();
  });

  it('populates signals on successful response', () => {
    const { http, service } = setup();

    http.expectOne('/api/presence/online-count').flush({ count: 7 });
    http.expectOne('/api/rooms').flush([]);
    http.expectOne('/api/users/me/active-rooms').flush([]);
    http.expectOne((r) => r.url === '/api/leaderboard').flush({
      items: [],
      total: 0,
      page: 1,
      pageSize: 10,
    });

    expect(service.onlineCount.data()).toBe(7);
    expect(service.rooms.data()).toEqual([]);
    expect(service.myRooms.data()).toEqual([]);
    expect(service.leaderboard.data()).toEqual([]);
    http.verify();
  });

  it('dedups concurrent refresh() calls while one is in-flight', () => {
    const { http, service } = setup();

    // Initial mount fires one /api/rooms; it's still pending (not flushed).
    // Subsequent refresh() calls MUST be ignored by the inFlight guard,
    // so match() should find exactly 1 pending request — not 3.
    service.rooms.refresh();
    service.rooms.refresh();

    const pending = http.match('/api/rooms');
    expect(pending.length).toBe(1);
    pending[0].flush([]);

    // Drain the other three slices' initial calls to satisfy verify().
    http.expectOne('/api/presence/online-count').flush({ count: 0 });
    http.expectOne('/api/users/me/active-rooms').flush([]);
    http.expectOne((r) => r.url === '/api/leaderboard').flush({
      items: [],
      total: 0,
      page: 1,
      pageSize: 10,
    });
    http.verify();
  });

  it('one slice errors do not poison the others', () => {
    const { http, service } = setup();

    http.expectOne('/api/presence/online-count').flush({ count: 3 });
    http.expectOne('/api/rooms').flush([]);
    http.expectOne('/api/users/me/active-rooms').flush([]);
    http.expectOne((r) => r.url === '/api/leaderboard').flush(null, {
      status: 500,
      statusText: 'Server Error',
    });

    expect(service.leaderboard.error()).not.toBeNull();
    expect(service.onlineCount.error()).toBeNull();
    expect(service.rooms.error()).toBeNull();
    expect(service.myRooms.error()).toBeNull();
    http.verify();
  });

  it('visibility=hidden blocks refreshes triggered via the document event listener path', () => {
    const { http, service } = setup();

    // Drain initial calls.
    http.expectOne('/api/presence/online-count').flush({ count: 1 });
    http.expectOne('/api/rooms').flush([]);
    http.expectOne('/api/users/me/active-rooms').flush([]);
    http.expectOne((r) => r.url === '/api/leaderboard').flush({
      items: [],
      total: 0,
      page: 1,
      pageSize: 10,
    });

    // Go hidden. No refresh should fire automatically.
    setVisibility('hidden');
    http.expectNone('/api/rooms');

    // Return to visible — because the polled slices were just fetched, the
    // stale check (> half interval) is false, so nothing fires here either.
    setVisibility('visible');
    http.expectNone('/api/rooms');

    // But an explicit refresh still works.
    service.rooms.refresh();
    http.expectOne('/api/rooms').flush([]);
    http.verify();

    // And readings stay consistent.
    expect(service.rooms.data()).toEqual([]);
  });
});
