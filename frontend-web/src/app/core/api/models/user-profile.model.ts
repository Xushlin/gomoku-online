/**
 * Public user profile DTOs — mirror the backend's `Gewu.Application.Common.DTOs`
 * shapes from `add-public-profile-and-search` and `add-game-replay`. JSON
 * serialiser is System.Text.Json with default camelCase + JsonStringEnumConverter
 * for enums.
 */
import type { GameEndReason, GameResult, RoomSeat } from './room.model';

export interface UserPublicProfileDto {
  readonly id: string;
  readonly username: string;
  readonly rating: number;
  readonly gamesPlayed: number;
  readonly wins: number;
  readonly losses: number;
  readonly draws: number;
  readonly createdAt: string;
}

export interface UserGameSummaryDto {
  readonly roomId: string;
  readonly name: string;
  /**
   * 每一个座位上的人,按 `index` 升序。
   *
   * 此前是 `black` / `white` —— 0 / 1 号座位的派生读法,于是三座位棋种的战绩里
   * 2 号座位上的人不出现。仓储**不按棋种过滤**,所以三座位对局照样进这个列表。
   */
  readonly seats: readonly RoomSeat[];
  readonly startedAt: string;
  readonly endedAt: string;
  readonly result: GameResult;
  readonly winnerUserId: string | null;
  readonly endReason: GameEndReason;
  readonly moveCount: number;
}

export interface PagedResult<T> {
  readonly items: readonly T[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
}
