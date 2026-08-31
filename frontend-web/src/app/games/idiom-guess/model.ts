/**
 * 猜成语的关卡形状 —— 与后端 `IdiomGuessLevel.cs` 里的记录一一对应。
 *
 * 这些类型描述的是**题面**。答案不在其中,也不会出现在任何响应里:关卡的另一半
 * (`solutionJson`)永不出服务端,而答对之后的出处由 `check` 的载荷带回来。
 */

/** 一道题。被挖的位置在 `chars` 里是 `null`。 */
export interface IdiomGuessPuzzle {
  readonly index: number;
  readonly explanation: string;
  /**
   * 四个位置上的字;被挖的位置是 `null`。
   *
   * 空位是 `null` 而不是空串 —— 一个合法值不得用来表示「不适用」。
   */
  readonly chars: readonly (string | null)[];
}

/** 一关的布局。 */
export interface IdiomGuessLayout {
  readonly puzzles: readonly IdiomGuessPuzzle[];
}

/** 答对一题时服务端回传的载荷。 */
export interface IdiomGuessSolved {
  readonly index: number;
  readonly word: string;
  /** 出处;**可能没有** —— 没有时不画那张纸条。 */
  readonly derivation: string | null;
}

/** 一次提示揭示的内容。 */
export interface IdiomGuessRevealed {
  readonly puzzleIndex: number;
  readonly position: number;
  readonly char: string;
}

/** 提示状态里那个键的写法:题号:位置。与后端 `IdiomGuessRules.Key` 同一份约定。 */
export function blankKey(puzzleIndex: number, position: number): string {
  return `${puzzleIndex}:${position}`;
}
