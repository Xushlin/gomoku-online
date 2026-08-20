import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import type { Observable } from 'rxjs';
import type { BotDifficulty, BotSide, GameEndedDto, GameReplayDto, PlayHints, RoomState, RoomSummary } from './models/room.model';

/**
 * Room reads and writes.
 *
 * `gameKey` is **required** wherever a room is created or listed, and it has no
 * default. The server used to fill a missing key with `gomoku`, justified as
 * compatibility for already-published clients — of which there are none. This
 * client is the only one, and it had simply never been taught to send the
 * field, so the "compatibility" default was really a hardcoded game living
 * where no reader of this file could see it.
 *
 * An optional parameter here would move that default rather than remove it:
 * "forgot to pass it" and "meant gomoku" would again be indistinguishable at
 * the call site. `myActiveRooms` takes no key on purpose — it answers "which
 * games am I in right now", and across games is the correct answer.
 */
export abstract class RoomsApiService {
  abstract list(gameKey: string): Observable<readonly RoomSummary[]>;
  abstract myActiveRooms(): Observable<readonly RoomSummary[]>;
  abstract getById(roomId: string): Observable<RoomState>;
  abstract create(name: string, gameKey: string): Observable<RoomSummary>;
  abstract createAiRoom(
    name: string,
    difficulty: BotDifficulty,
    humanSide: BotSide,
    gameKey: string,
  ): Observable<RoomState>;
  abstract join(roomId: string): Observable<RoomState>;
  abstract leave(roomId: string): Observable<void>;
  abstract dissolve(roomId: string): Observable<void>;
  abstract spectate(roomId: string): Observable<void>;
  abstract resign(roomId: string): Observable<GameEndedDto>;
  abstract getReplay(roomId: string): Observable<GameReplayDto>;

  /**
   * 我现在能出哪些牌 —— 提示按钮用它。
   *
   * **按需,不进 `RoomState` 广播**:候选可能有几十项,而广播里带的只是一个布尔
   * `seatView.canFollow`。服务端只回答**调用者自己**的那一份 —— 候选由这个座位的手牌决定。
   */
  abstract getHints(roomId: string): Observable<PlayHints>;
}

@Injectable({ providedIn: 'root' })
export class DefaultRoomsApiService extends RoomsApiService {
  getHints(roomId: string): Observable<PlayHints> {
    return this.http.get<PlayHints>(`/api/rooms/${roomId}/hints`);
  }

  private readonly http = inject(HttpClient);

  list(gameKey: string): Observable<readonly RoomSummary[]> {
    const params = new HttpParams().set('gameKey', gameKey);
    return this.http.get<readonly RoomSummary[]>('/api/rooms', { params });
  }

  myActiveRooms(): Observable<readonly RoomSummary[]> {
    return this.http.get<readonly RoomSummary[]>('/api/users/me/active-rooms');
  }

  getById(roomId: string): Observable<RoomState> {
    return this.http.get<RoomState>(`/api/rooms/${encodeURIComponent(roomId)}`);
  }

  create(name: string, gameKey: string): Observable<RoomSummary> {
    return this.http.post<RoomSummary>('/api/rooms', { name, gameKey });
  }

  createAiRoom(
    name: string,
    difficulty: BotDifficulty,
    humanSide: BotSide,
    gameKey: string,
  ): Observable<RoomState> {
    return this.http.post<RoomState>('/api/rooms/ai', {
      name,
      difficulty,
      humanSide,
      gameKey,
    });
  }

  join(roomId: string): Observable<RoomState> {
    return this.http.post<RoomState>(`/api/rooms/${encodeURIComponent(roomId)}/join`, {});
  }

  leave(roomId: string): Observable<void> {
    return this.http.post<void>(`/api/rooms/${encodeURIComponent(roomId)}/leave`, {});
  }

  dissolve(roomId: string): Observable<void> {
    return this.http.delete<void>(`/api/rooms/${encodeURIComponent(roomId)}`);
  }

  spectate(roomId: string): Observable<void> {
    return this.http.post<void>(`/api/rooms/${encodeURIComponent(roomId)}/spectate`, {});
  }

  resign(roomId: string): Observable<GameEndedDto> {
    return this.http.post<GameEndedDto>(`/api/rooms/${encodeURIComponent(roomId)}/resign`, {});
  }

  getReplay(roomId: string): Observable<GameReplayDto> {
    return this.http.get<GameReplayDto>(
      `/api/rooms/${encodeURIComponent(roomId)}/replay`,
    );
  }
}
