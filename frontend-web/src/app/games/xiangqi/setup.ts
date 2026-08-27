import { XIANGQI_ENDGAME_KEY } from './game-key';

/** 一块盘面串的长度 —— 10 行 × 9 列,与服务端的 `XiangqiSetup.BoardLength` 同一个数。 */
const BOARD_LENGTH = 90;

/**
 * 从房间的开局设置里取出**起始局面**。
 *
 * 编码是 `<90 字符盘面串>:<先走方座位>`,由服务端的 `XiangqiSetup` 定义。这里只要前一半:
 * 谁先走由 `game.currentSeat` 说,客户端不必自己算 —— **同一件事有两个出口时,读服务端那个**。
 *
 * 长度不对就返回 `null`,而**调用方拿到 `null` 会画标准开局** —— 那正是这里必须严格的理由:
 * 一个 89 字符的串会让 `row * 9 + col` 之后每一行都错开一列,画出一个看着正常的、错的盘面。
 *
 * 只对残局那个键有意义。传别的键返回 `null`,而不是硬解 —— 第二个从局面开局的棋种落地时,
 * 它的编码是它自己的事。
 */
export function startPositionFromSetup(
  gameKey: string | null | undefined,
  setup: string | null | undefined,
): string | null {
  if (gameKey !== XIANGQI_ENDGAME_KEY || !setup) return null;
  const board = setup.split(':')[0];
  return board.length === BOARD_LENGTH ? board : null;
}
