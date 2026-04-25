/**
 * Public user profile DTOs — mirror the backend's `Gomoku.Application.Common.DTOs`
 * shapes from `add-public-profile-and-search` and `add-game-replay`. JSON
 * serialiser is System.Text.Json with default camelCase + JsonStringEnumConverter
 * for enums.
 */
import type { GameEndReason, GameResult, UserSummary } from './room.model';

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
  readonly black: UserSummary;
  readonly white: UserSummary;
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
