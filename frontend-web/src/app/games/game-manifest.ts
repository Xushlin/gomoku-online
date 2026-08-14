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

  /** Card glyph. A plain string so it needs no asset pipeline and inherits theme colours. */
  readonly icon: string;

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
   * Board dimensions. Only meaningful for `category: 'match'`, and required once
   * such a game is `'available'`.
   *
   * This is a **deliberate copy of server-authoritative data**. The real source is
   * the backend's `IGameRules`; this exists only because the room DTO carries the
   * game key but not the size — threading the rules registry through nine
   * `ToState`/`ToSummary` call sites was not worth two integers (see
   * add-web-tictactoe-ai design D1).
   *
   * The cost of drift is bounded twice over: the symptom is a visibly wrong number
   * of cells rather than a silent error, and the server's `rules.IsInBounds` rejects
   * out-of-range moves, so an oversized client board cannot corrupt a game. Delete
   * this field when `generalize-match-contract` puts dimensions on the wire.
   */
  readonly board?: { readonly rows: number; readonly cols: number };
}
