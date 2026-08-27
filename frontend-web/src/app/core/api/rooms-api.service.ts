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
  /**
   * 建房。`manualLineId` 只对「从选定局面开局」的棋种有意义,而且**两个方向都由服务端
   * 校验**:给了它而棋种不是残局 → 400,是残局而没给 → 400。
   *
   * **参数是一个 id,不是一个盘面** —— 起始局面与先走方由服务端从那条线路上取。
   * 让客户端递盘面等于让客户端定义棋局,而那个口子不需要开。
   */
  abstract create(
    name: string,
    gameKey: string,
    manualLineId?: number,
  ): Observable<RoomSummary>;
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

  create(name: string, gameKey: string, manualLineId?: number): Observable<RoomSummary> {
    // 没有线路 id 时**不发这个字段**,而不是发一个 `null`:服务端两个方向都校验,
    // 而一个显式的 null 与「没给」在 JSON 里长得一样但读起来不一样。
    return this.http.post<RoomSummary>(
      '/api/rooms',
      manualLineId === undefined ? { name, gameKey } : { name, gameKey, manualLineId },
    );
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
