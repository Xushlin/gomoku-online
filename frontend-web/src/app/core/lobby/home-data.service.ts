import { DOCUMENT, inject, Injectable, OnDestroy } from '@angular/core';
import type { RoomSummary } from '../api/models/room.model';
import { PresenceApiService } from '../api/presence-api.service';
import { RoomsApiService } from '../api/rooms-api.service';
import { LOBBY_POLLING_CONFIG } from './lobby-polling.config';
import { SliceEngine, type LobbySlice } from './slice-engine';

/**
 * `/home`'s data — the slices whose endpoints take **no** game key.
 *
 * That is the whole membership rule, and it is why this service exists apart
 * from `LobbyDataService`: `GET /api/presence/online-count` and
 * `GET /api/users/me/active-rooms` answer questions about the account, not
 * about a game. "Which games am I in right now" is *correctly* answered across
 * games, which is why that endpoint never took a key.
 */
export abstract class HomeDataService {
  abstract readonly onlineCount: LobbySlice<number>;
  abstract readonly myRooms: LobbySlice<readonly RoomSummary[]>;
}

@Injectable()
export class DefaultHomeDataService extends HomeDataService implements OnDestroy {
  private readonly presenceApi = inject(PresenceApiService);
  private readonly roomsApi = inject(RoomsApiService);
  private readonly config = inject(LOBBY_POLLING_CONFIG);
  private readonly engine = new SliceEngine(inject(DOCUMENT));

  readonly onlineCount: LobbySlice<number>;
  readonly myRooms: LobbySlice<readonly RoomSummary[]>;

  constructor() {
    super();
    this.onlineCount = this.engine.add(
      'onlineCount',
      () => this.presenceApi.getOnlineCount(),
      this.config.onlineCountMs,
    );
    this.myRooms = this.engine.add(
      'myRooms',
      () => this.roomsApi.myActiveRooms(),
      this.config.myRoomsMs,
    );
    this.engine.start();
  }

  ngOnDestroy(): void {
    this.engine.teardown();
  }
}
