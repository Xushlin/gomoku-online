import { DOCUMENT, inject, Injectable, OnDestroy } from '@angular/core';
import { forkJoin, map, type Observable } from 'rxjs';
import { GameCatalogService } from '../../games/game-catalog.service';
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
  private readonly catalog = inject(GameCatalogService);
  private readonly engine = new SliceEngine(inject(DOCUMENT));

  readonly rooms: LobbySlice<readonly RoomSummary[]>;
  readonly leaderboard: LobbySlice<readonly LeaderboardEntry[]>;

  constructor() {
    super();
    this.rooms = this.engine.add('rooms', () => this.listRooms(), this.config.roomsMs);
    // No interval: a ladder does not move often enough to poll, and the page
    // refetches it explicitly after anything that could change it.
    this.leaderboard = this.engine.add(
      'leaderboard',
      () => this.leaderboardApi.top(this.gameKey, LEADERBOARD_SIZE),
      null,
    );
    this.engine.start();
  }

  /**
   * 这个大厅要列的房间 —— 自己的键,加上 manifest 声明的伴生键。
   *
   * **伴生键为空时只发一次请求**,那不是优化:一个每 15 秒多打一次、结果永远是空数组的
   * 端点,只会在网络面板里露面,而没有任何断言会红。九个游戏里有八个走这一支。
   *
   * 合并后按房间自己的 `gameKey` 分辨形态 —— 房间摘要里那个字段本来就在,所以卡片不需要
   * 知道它是从哪一次请求回来的。
   */
  private listRooms(): Observable<readonly RoomSummary[]> {
    const companions = this.catalog.byKey(this.gameKey)?.companionRoomKeys ?? [];
    if (companions.length === 0) {
      return this.roomsApi.list(this.gameKey);
    }
    return forkJoin(
      [this.gameKey, ...companions].map((key) => this.roomsApi.list(key)),
    ).pipe(map((lists) => lists.flat()));
  }

  ngOnDestroy(): void {
    this.engine.teardown();
  }
}
