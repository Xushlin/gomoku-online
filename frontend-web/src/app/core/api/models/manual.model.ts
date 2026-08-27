/**
 * 谱主的评断 —— **四态**。
 *
 * 它不是「获胜座位」,而这是量出来的:六辑残局里**和棋 391 局**,而一个座位号表达不了
 * 和棋;**谱未标注 338 局**(烂柯神机整辑 258 局全无结果字段),把它们默认成红优就是
 * 替谱主编话。
 *
 * 而它是**评断,不是终局** —— 《梅花谱》31 条里只有 11 条真的走到将死。界面 MUST NOT
 * 把它说成「将死」。
 */
export type ManualVerdict = 'Unrecorded' | 'RedBetter' | 'BlackBetter' | 'Draw';

/** 四态的清单 —— 走查从它推导,而不是手写一份副本。 */
export const MANUAL_VERDICTS: readonly ManualVerdict[] = [
  'Unrecorded',
  'RedBetter',
  'BlackBetter',
  'Draw',
];

/** 一条古谱线路在目录里的样子。 */
export interface ManualLineSummary {
  readonly id: number;
  readonly title: string;
  readonly moveCount: number;
  readonly verdict: ManualVerdict;
  /**
   * 起始局面上的子数 —— 界面据此区分残局与满盘。
   *
   * **它不是「是不是标准开局」的判据**:实测满盘 163 局、标准开局 157 局,有 6 局是
   * 32 子却不是标准摆法。
   */
  readonly pieceCount: number;
}

/** 古谱清单里的一部。 */
export interface ManualSummary {
  readonly manualKey: string;
  readonly name: string;
  readonly lineCount: number;
  /** 这部谱有没有「第N局」那一层。六辑残局没有,而给它们编一个局号是编数据。 */
  readonly grouped: boolean;
}

/** 目录里的一局及其变化。 */
export interface ManualChapter {
  readonly chapter: number;
  readonly lines: readonly ManualLineSummary[];
}

/** 一部古谱的目录。`gameKey` 决定用哪个只读棋盘,与回放页同一条理由。 */
export interface ManualCatalogue {
  readonly manualKey: string;
  /** 书名 —— 标题用它,而不是一份客户端的「键 → 名字」映射(那份会在加一辑那天落后)。 */
  readonly name: string;
  /**
   * 这部谱有没有「第N局」那一层。
   *
   * 六辑残局没有,而给它们编一个局号是编数据 —— 所以目录页据此决定画不画分组标题,
   * 而不是看 `chapter === 0`(那把一个约定藏在了一个数字里)。
   */
  readonly grouped: boolean;
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
  readonly verdict: ManualVerdict;
  /**
   * 起始局面,90 字符的行优先盘面串。**首帧就是这个局面。**
   *
   * 让棋盘从标准开局重放会把一条 10 子的残局画成 32 子加几步棋 —— 一个看起来完全正常
   * 的错盘面,而没有任何断言会红。
   */
  readonly startPosition: string;
  /** 先走方座位(0 = 红)。1634 局残局里 7 局是黑先走,所以它是数据不是约定。 */
  readonly firstSeat: number;
  readonly moves: readonly ManualMove[];
}
