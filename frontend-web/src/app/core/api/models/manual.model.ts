/**
 * 一条古谱线路在目录里的样子。
 *
 * `winnerSeat` 是**谱主的评断**,不是招法真的走到了将死 —— 量过:《梅花谱》31 条线路里
 * 只有 11 条以杀棋收,其余走到「优势已成」就停。所以界面 MUST NOT 把它说成「将死」。
 */
export interface ManualLineSummary {
  readonly id: number;
  readonly title: string;
  readonly moveCount: number;
  readonly winnerSeat: number;
}

/** 目录里的一局及其变化。 */
export interface ManualChapter {
  readonly chapter: number;
  readonly lines: readonly ManualLineSummary[];
}

/** 一部古谱的目录。`gameKey` 决定用哪个只读棋盘,与回放页同一条理由。 */
export interface ManualCatalogue {
  readonly manualKey: string;
  readonly gameKey: string;
  readonly chapters: readonly ManualChapter[];
}

/**
 * 古谱里的一手。**比 `MoveDto` 窄**,而这是故意的:象棋棋盘只读起点与终点四个数,
 * 而给三百年前的谱编一个 `playedAt` 会得到一份看起来和真的一模一样的假数据。
 */
export interface ManualMove {
  readonly ply: number;
  readonly fromRow: number;
  readonly fromCol: number;
  readonly row: number;
  readonly col: number;
  readonly seat: number;
}

/** 一条古谱线路的完整内容。 */
export interface ManualLine {
  readonly id: number;
  readonly manualKey: string;
  readonly gameKey: string;
  readonly chapter: number;
  readonly title: string;
  readonly winnerSeat: number;
  readonly moves: readonly ManualMove[];
}
