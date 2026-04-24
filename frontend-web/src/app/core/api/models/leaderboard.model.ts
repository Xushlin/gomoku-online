/**
 * Leaderboard DTO shapes — mirror the backend's
 * `Gomoku.Application.Common.DTOs.LeaderboardEntryDto` + `PagedResult<T>`.
 */

export interface LeaderboardEntry {
  readonly rank: number;
  readonly userId: string;
  readonly username: string;
  readonly rating: number;
  readonly gamesPlayed: number;
  readonly wins: number;
  readonly losses: number;
  readonly draws: number;
}

export interface PagedResult<T> {
  readonly items: readonly T[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
}
