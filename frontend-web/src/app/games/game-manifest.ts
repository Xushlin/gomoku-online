import type { EmblemShape } from './game-emblem';

/**
 * Which kind of game this is. The three categories deliberately do NOT share
 * one backend aggregate — see the platform roadmap in CLAUDE.md.
 *
 *   - `match`  — turn-based adversarial, two seats, realtime over SignalR.
 *   - `puzzle` — single-player levels, plain REST, no opponent and no hub.
 *   - `score`  — single-player score-attack, one run submitted at the end.
 */
export type GameCategory = 'match' | 'puzzle' | 'score';

/** Whether the game is playable today or only announced. */
export type GameStatus = 'available' | 'planned';

/**
 * The single declaration of a game on the platform.
 *
 * Adding a game MUST be: one new `games/<key>/` folder, one entry in
 * `games/index.ts`, and one `games.<key>.*` block in each locale file. No
 * edits to the catalogue page or to any other game.
 */
export interface GameManifest {
  /** Globally unique kebab-case identifier, e.g. `gomoku`, `idiom-crossword`. */
  readonly key: string;

  /** Which of the three platform categories this game belongs to. */
  readonly category: GameCategory;

  /** Playable today, or announced only. */
  readonly status: GameStatus;

  /** Transloco key for the display name. MUST be `games.<key>.title`. */
  readonly titleKey: string;

  /** Transloco key for the one-line description. MUST be `games.<key>.description`. */
  readonly descriptionKey: string;

  /**
   * The game's emblem, as a table of primitives on a 24×24 grid.
   *
   * It replaced a single character (`icon: '⬤'`). Nine games rendered as
   * nine characters on nine identical plates is what "the UI is too rough for a
   * game" looked like when measured: the whole hall had four visual values.
   *
   * **Required, not optional**, and for the same reason every other registry
   * entry here is: a game with no emblem would render an invisible tile, and an
   * invisible tile is not something a walking test notices unless the field is
   * mandatory. Planned games get one too.
   *
   * The renderer (`GameEmblem`) owns the grid, the stroke and the caps — a
   * manifest cannot specify them, which is what keeps ten emblems reading as one
   * set. Colour is always `currentColor`, so the tile picks the identity hue.
   */
  readonly emblem: readonly EmblemShape[];

  /**
   * Locales the game's **content** exists in — not its UI. The idiom games are
   * 成语 data with Chinese explanations, so they are `['zh-CN']` however well
   * the surrounding chrome is translated. The catalogue badges a mismatch
   * between this list and the active locale.
   */
  readonly contentLocales: readonly string[];

  /**
   * Entry point route. Invariant: MUST be a non-empty route when `status` is
   * `'available'`, and is never read when `status` is `'planned'`.
   */
  readonly launchRoute?: string;

  /**
   * 这个棋种的**古谱**路由,可选。
   *
   * 它是一个 manifest 字段而不是大厅里的一句 `gameKey === 'xiangqi'`:大厅现有的两张卡片
   * 都由服务端能力开关(`isRated` / `supportsAi`),而古谱不是服务端能力 —— 它是这个棋种
   * 有没有配套资料。写成字段,《橘中秘》或者五子棋的定式谱落地时就是加一行,不动大厅。
   *
   * 不填 = 这个棋种没有古谱,大厅不画那个入口。
   */
  readonly manualRoute?: string;

  /** 古谱入口的显示文案键。填了 `manualRoute` 就 MUST 一起填。 */
  readonly manualLabelKey?: string;

  /**
   * 除自己的键之外,这个游戏的大厅还要列出哪些棋种的房间。
   *
   * 象棋残局在服务端是一个**独立的棋种键**(内核的设置不变量逼出来的,见
   * `games/xiangqi/game-key.ts`),但它不是一个独立的游戏 —— 它没有自己的大厅、
   * 没有自己的排行榜,而且**开一间残局房必须先选一则残局**,所以它也不该有
   * 「创建房间」那个按钮。
   *
   * 写成 manifest 字段而不是大厅里的一句 `gameKey === 'xiangqi'`,与 `manualRoute`
   * 同一个理由:那样《橘中秘》的定式对局或者五子棋的死活题落地时是加一行,不动大厅。
   *
   * 不填 = 这个大厅只列自己那个键。**不填是绝大多数**,所以它是可选的。
   */
  readonly companionRoomKeys?: readonly string[];

}
