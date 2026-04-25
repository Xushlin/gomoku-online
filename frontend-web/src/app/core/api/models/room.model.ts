/**
 * Room DTO shapes — mirror the backend's
 * `Gomoku.Application.Common.DTOs.RoomDtos`.
 * JSON serialiser: System.Text.Json with default camelCase naming,
 * enums as strings via JsonStringEnumConverter.
 */

export type RoomStatus = 'Waiting' | 'Playing' | 'Finished';
export type Stone = 'Empty' | 'Black' | 'White';
export type GameResult = 'Ongoing' | 'BlackWin' | 'WhiteWin' | 'Draw';
export type GameEndReason = 'Connected5' | 'Resigned' | 'TurnTimeout';
export type ChatChannel = 'Room' | 'Spectator';
export type BotDifficulty = 'Easy' | 'Medium' | 'Hard';

export interface UserSummary {
  readonly id: string;
  readonly username: string;
}

export interface RoomSummary {
  readonly id: string;
  readonly name: string;
  readonly status: RoomStatus;
  readonly host: UserSummary;
  readonly black: UserSummary | null;
  readonly white: UserSummary | null;
  readonly spectatorCount: number;
  readonly createdAt: string;
}

export interface MoveDto {
  readonly ply: number;
  readonly row: number;
  readonly col: number;
  readonly stone: Stone;
  readonly playedAt: string;
}

export interface GameSnapshot {
  readonly id: string;
  readonly currentTurn: Stone;
  readonly startedAt: string;
  readonly endedAt: string | null;
  readonly result: GameResult | null;
  readonly winnerUserId: string | null;
  readonly endReason: GameEndReason | null;
  readonly turnStartedAt: string;
  readonly turnTimeoutSeconds: number;
  readonly moves: readonly MoveDto[];
}

export interface ChatMessage {
  readonly id: string;
  readonly senderUserId: string;
  readonly senderUsername: string;
  readonly content: string;
  readonly channel: ChatChannel;
  readonly sentAt: string;
}

export interface GameEndedDto {
  readonly result: GameResult;
  readonly winnerUserId: string | null;
  readonly endedAt: string;
  readonly endReason: GameEndReason;
}

/**
 * Returned by `GET /api/rooms/{id}/replay`. Always represents a Finished
 * game — `result` / `winnerUserId-on-non-draw` / `endReason` / `endedAt` are
 * all guaranteed non-null by the backend's domain invariants. `Moves` is in
 * ply order (ascending). No `chatMessages`, no `spectators`, no `status` —
 * those are live-room concerns.
 */
export interface GameReplayDto {
  readonly roomId: string;
  readonly name: string;
  readonly host: UserSummary;
  readonly black: UserSummary;
  readonly white: UserSummary;
  readonly startedAt: string;
  readonly endedAt: string;
  readonly result: GameResult;
  readonly winnerUserId: string | null;
  readonly endReason: GameEndReason;
  readonly moves: readonly MoveDto[];
}

export interface UrgeDto {
  readonly roomId: string;
  readonly urgerUserId: string;
  readonly urgedUserId: string;
  readonly sentAt: string;
}

/**
 * Full room state returned by GET /api/rooms/{id} and POST /api/rooms/{id}/join.
 * The `game` and `chatMessages` shapes are locked here by `add-web-game-board`.
 */
export interface RoomState {
  readonly id: string;
  readonly name: string;
  readonly status: RoomStatus;
  readonly host: UserSummary;
  readonly black: UserSummary | null;
  readonly white: UserSummary | null;
  readonly spectators: readonly UserSummary[];
  readonly game: GameSnapshot | null;
  readonly chatMessages: readonly ChatMessage[];
  readonly createdAt: string;
}
