import { InjectionToken } from '@angular/core';

export interface LobbyPollingConfig {
  readonly onlineCountMs: number;
  readonly roomsMs: number;
  readonly myRoomsMs: number;
}

/**
 * Polling cadences for each LobbyDataService slice.
 * Defaults: rooms faster than online/myRooms because the rooms list is the
 * slice most likely to change between clients; leaderboard is intentionally
 * unpolled (rankings shift slowly) and therefore absent from this config.
 *
 * Tests override with `{ provide: LOBBY_POLLING_CONFIG, useValue: { ...all zero or small } }`
 * to exercise polling logic without waiting minutes.
 */
export const LOBBY_POLLING_CONFIG = new InjectionToken<LobbyPollingConfig>(
  'lobby.polling-config',
  {
    providedIn: 'root',
    factory: () => ({
      onlineCountMs: 30_000,
      roomsMs: 15_000,
      myRoomsMs: 30_000,
    }),
  },
);
