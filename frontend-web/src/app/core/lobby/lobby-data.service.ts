import { DOCUMENT, inject, Injectable, OnDestroy } from '@angular/core';
import { LeaderboardApiService } from '../api/leaderboard-api.service';
import type { LeaderboardEntry } from '../api/models/leaderboard.model';
import type { RoomSummary } from '../api/models/room.model';
import { RoomsApiService } from '../api/rooms-api.service';
import { LOBBY_GAME_KEY } from './lobby-game-key';
import { LOBBY_POLLING_CONFIG } from './lobby-polling.config';
import { SliceEngine, type LobbySlice } from './slice-engine';

const LEADERBOARD_SIZE = 10;

/**
 * `/g/:gameKey/lobby`'s data — the slices whose endpoints **require** a game
 * key, which comes from {@link LOBBY_GAME_KEY} and therefore from the route.
 *
 * Kept separate from `HomeDataService` rather than parameterising one
 * four-slice service, because that service on `/home` would go on polling
 * `/api/rooms?gameKey=gomoku` every 15 seconds for a card `/home` no longer
 * renders. **A slice nobody renders but everybody pays for is a defect that
 * only ever shows up in the network panel.**
 */
export abstract class LobbyDataService {
  abstract readonly rooms: LobbySlice<readonly RoomSummary[]>;
  abstract readonly leaderboard: LobbySlice<readonly LeaderboardEntry[]>;
}

@Injectable()
export class DefaultLobbyDataService extends LobbyDataService implements OnDestroy {
  private readonly roomsApi = inject(RoomsApiService);
  private readonly leaderboardApi = inject(LeaderboardApiService);
  private readonly config = inject(LOBBY_POLLING_CONFIG);
  private readonly gameKey = inject(LOBBY_GAME_KEY);
  private readonly engine = new SliceEngine(inject(DOCUMENT));

  readonly rooms: LobbySlice<readonly RoomSummary[]>;
  readonly leaderboard: LobbySlice<readonly LeaderboardEntry[]>;

  constructor() {
    super();
    this.rooms = this.engine.add(
      'rooms',
      () => this.roomsApi.list(this.gameKey),
      this.config.roomsMs,
    );
    // No interval: a ladder does not move often enough to poll, and the page
    // refetches it explicitly after anything that could change it.
    this.leaderboard = this.engine.add(
      'leaderboard',
      () => this.leaderboardApi.top(this.gameKey, LEADERBOARD_SIZE),
      null,
    );
    this.engine.start();
  }

  ngOnDestroy(): void {
    this.engine.teardown();
  }
}
