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
  /**
   * 全部**在座**的座位。见 {@link RoomSeat}。
   *
   * **它是这份摘要里唯一读得到第三个座位的地方**,而大厅列表读的正是这份摘要 ——
   * 在它之前,三座位房间里 2 号座位上的人在大厅的房间行里根本不出现。
   *
   * 注意它是「坐上了几个」而不是「一共有几个」:一个等待中的三座位房间这里只有一项。
   * 需要后者的地方读棋种描述符的 `seatCount`(`GET /api/games`,非空)。
   *
   * **这一句此前写的是「那个端点今天不发那个数」,而它在 `publish-seat-count`
   * 之后就是假的。** 第一个消费者是大厅的房间行(`room-list-seats`):它按
   * `seatCount` 画满座位,在座的之外画空位 —— 而退化成 `seats.length` 会把每个
   * 等待中的房间画成满座,也就是一个看起来不能加入、其实能加入的房间。
   */
  readonly seats: readonly RoomSeat[];
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
  /**
   * The **seat** that played this move (0-based).
   *
   * It used to be `stone: Stone`, translated on the server by
   * `SeatWire.ToStone(seat)` — which was `seat === 0 ? Black : White`, so
   * **seat 2 was reported as seat 1**. Measured on a real three-seat room: three
   * `bid:0` moves came back `Black / White / White`, i.e. the two farmers were
   * indistinguishable in the move log.
   *
   * Colour is a *display* reading of a seat — see `games/board-seats.ts`.
   */
  readonly seat: number;
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

/**
 * 一个座位:座位号 + 坐在上面的人。
 *
 * `black` / `white` 是 0 号与 1 号的派生读法,所以三座位房间里 2 号座位上的人
 * **在它们里面根本不出现**。要知道"谁坐哪",读这个。
 */
export interface RoomSeat {
  readonly index: number;
  readonly player: UserSummary;
}

export interface GameSnapshot {
  readonly id: string;
  /** The seat to move (0-based). See {@link MoveDto.seat}. */
  readonly currentSeat: number;
  readonly startedAt: string;
  readonly endedAt: string | null;
  readonly result: GameResult | null;
  readonly winnerUserId: string | null;
  readonly endReason: GameEndReason | null;
  readonly turnStartedAt: string;
  readonly turnTimeoutSeconds: number;
  readonly moves: readonly MoveDto[];
  /**
   * 这个看客**能看到**的那一份棋种私有状态,由服务端的规则序列化;棋种没有隐藏信息、
   * 或对局还没开始时不给(`null` / 字段缺失)。
   *
   * **对本层完全不透明** —— 内容由棋种决定,由那个棋种自己的模块解析
   * (斗地主见 `games/doudizhu/seat-view.ts`)。与闯关那条线的 `LayoutJson` 同一个做法。
   *
   * 同一局的不同座位拿到的是**不同的**字符串:斗地主的手牌只有一个座位能看,而服务端
   * 逐张裁剪过,并有一条"没有一个座位看得到别人的任何一张"的断言钉着。
   */
  readonly seatView?: string | null;
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
  readonly seats: readonly RoomSeat[];
  readonly game: GameSnapshot | null;
  readonly chatMessages: readonly ChatMessage[];
  readonly createdAt: string;

  /**
   * 建房时**选定**的开局设置;绝大多数房间没有,那时它不出现。
   *
   * 它对本模型不透明 —— 怎么解由 `gameKey` 决定(象棋残局见
   * `games/xiangqi/setup.ts`)。**等待中的房间也带着它**,所以房主坐在自己刚摆的残局房里
   * 看到的是那一局,而不是一副标准开局。
   *
   * 发牌那种设置**不在这里**,也不在任何一个 DTO 上:斗地主的底牌客户端算不出来,
   * 而那个「算不出来」就是安全性质本身。服务端有一条反射断言钉着这条区别。
   */
  readonly chosenSetup?: string | null;
}

/**
 * 候选出法 —— `GET /api/rooms/:id/hints`,提示按钮用它。
 *
 * 每一项是一串**牌的编码**(与 `play:<cards>` 里那一段同一个格式),按**先弱后强**排。
 * 空列表的含义是「你要不起」,而它与 `seatView.canFollow === false` 是**同一个事实的两个
 * 出口** —— 服务端有一条断言把它们钉在一起。
 */
export interface PlayHints {
  readonly plays: readonly string[];
}
