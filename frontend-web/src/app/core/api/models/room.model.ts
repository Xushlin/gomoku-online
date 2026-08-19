/**
 * Room DTO shapes — mirror the backend's
 * `Gewu.Application.Common.DTOs.RoomDtos`.
 * JSON serialiser: System.Text.Json with default camelCase naming,
 * enums as strings via JsonStringEnumConverter.
 */

export type RoomStatus = 'Waiting' | 'Playing' | 'Finished';
export type Stone = 'Empty' | 'Black' | 'White';
/**
 * How a game ended. **There is no winner in here** — that is `winnerUserId`.
 *
 * The server merged `BlackWin` / `WhiteWin` into `Decided` because those two values and
 * `winnerUserId` were the same fact stored twice, and because a colour-named result only
 * has room for two seats.
 */
export type GameResult = 'Ongoing' | 'Decided' | 'Draw';
export type GameEndReason = 'Decided' | 'Resigned' | 'TurnTimeout';
export type ChatChannel = 'Room' | 'Spectator';
export type BotDifficulty = 'Easy' | 'Medium' | 'Hard';
/**
 * Which side the human plays in an AI room. Black = human plays first
 * (legacy default); White = bot plays first. Subset of `Stone` excluding
 * `'Empty'` — backend rejects Empty in the AI command validator.
 */
export type BotSide = 'Black' | 'White';

export interface UserSummary {
  readonly id: string;
  readonly username: string;
}

export interface RoomSummary {
  readonly id: string;
  readonly name: string;
  /** Which game this room plays. See RoomState.gameKey for why it is on the wire. */
  readonly gameKey: string;
  readonly status: RoomStatus;
  readonly host: UserSummary;
  readonly black: UserSummary | null;
  readonly white: UserSummary | null;
  readonly spectatorCount: number;
  readonly createdAt: string;
}

export interface MoveDto {
  readonly ply: number;
  /**
   * Destination row — `null` for games whose move is **not** a square.
   *
   * 成语接龙's move is an idiom, carried in {@link MoveDto.text}. Exactly one of
   * the two payloads is present; the server enforces that at construction, so a
   * reader that has checked `row != null` may treat `col` as present too.
   */
  readonly row: number | null;
  /** Destination column; `null` whenever {@link MoveDto.row} is. */
  readonly col: number | null;
  /** The move's text payload; `null` for games played on a board. */
  readonly text?: string | null;
  readonly stone: Stone;
  readonly playedAt: string;
  /**
   * Origin row, present only for games where a move slides a piece from one
   * square to another. Placement games (gomoku, tic-tac-toe) omit it — the
   * server sends `null`, and the board components never read it.
   *
   * Optional rather than `number | null` so the two published-client shapes
   * (field absent, field null) both type-check without a cast at every use.
   */
  readonly fromRow?: number | null;
  /** Origin column. See {@link MoveDto.fromRow}. */
  readonly fromCol?: number | null;
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
  /** Which game was played — the replay board needs it for the same reason the live board does. */
  readonly gameKey: string;
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
  /**
   * Which game this room plays — the only thing that tells the client how big the
   * board is.
   *
   * A player reaches a room by four routes: from the create page, by refreshing,
   * from a bookmarked link, or from the "my games" card. Only the first one leaves
   * the client knowing the game; the other three give it nothing but a room id. So
   * "carry the size in the route" is a shortcut that works on one route out of four
   * and renders 3x3 as 15x15 on the other three.
   *
   * Dimensions themselves are not on the wire — the client resolves them from its
   * own game registry using this key. See add-web-tictactoe-ai design D1.
   */
  readonly gameKey: string;
  readonly status: RoomStatus;
  readonly host: UserSummary;
  readonly black: UserSummary | null;
  readonly white: UserSummary | null;
  readonly spectators: readonly UserSummary[];
  readonly game: GameSnapshot | null;
  readonly chatMessages: readonly ChatMessage[];
  readonly createdAt: string;
}
