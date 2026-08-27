import { describe, expect, it } from 'vitest';
import { XIANGQI_ENDGAME_KEY, XIANGQI_KEY } from './game-key';
import { startPositionFromSetup } from './setup';

/** 标准开局 —— 与服务端那个常量同源。 */
const STANDARD =
  'rnbakabnr..........c.....c.p.p.p.p.p..................P.P.P.P.P.C.....C..........RNBAKABNR';

describe('startPositionFromSetup', () => {
  it('takes the board half and drops the first-seat half', () => {
    expect(startPositionFromSetup(XIANGQI_ENDGAME_KEY, `${STANDARD}:1`)).toBe(STANDARD);
    expect(startPositionFromSetup(XIANGQI_ENDGAME_KEY, `${STANDARD}:0`)).toBe(STANDARD);
  });

  /**
   * **先走方从这里读不出来,那是故意的。**
   *
   * 谁先走由 `game.currentSeat` 说 —— 同一件事有两个出口时,读服务端那个。这条断言
   * 只是把「返回值就是那 90 个字符」钉住:长度多两位就说明冒号那一半漏进来了。
   */
  it('returns exactly the 90 characters of the board', () => {
    expect(startPositionFromSetup(XIANGQI_ENDGAME_KEY, `${STANDARD}:1`)).toHaveLength(90);
  });

  /**
   * 长度不对就 `null`,而调用方拿到 `null` 会画标准开局 —— 所以这里必须严。
   *
   * 一个 89 字符的串会让 `row * 9 + col` 之后每一行都错开一列,画出一个**看着正常的、
   * 错的**盘面;而一个 91 字符的串同样是坏的,所以两边都试。
   */
  it.each([
    ['one short', STANDARD.slice(0, 89)],
    ['one long', `${STANDARD}.`],
    ['empty board half', ''],
  ])('refuses a board of the wrong length (%s)', (_what, board) => {
    expect(startPositionFromSetup(XIANGQI_ENDGAME_KEY, `${board}:0`)).toBeNull();
  });

  it('refuses a setup with no colon at all', () => {
    // 没有冒号时 `split(':')[0]` 是整个串 —— 90 字符的话它**会**通过,而那正是
    // 这条断言要说清楚的:通过的判据是长度,不是格式。所以拿一个长度不对的来试。
    expect(startPositionFromSetup(XIANGQI_ENDGAME_KEY, 'not-a-setup')).toBeNull();
  });

  /**
   * 别的棋种一律 `null`,**包括普通象棋**。
   *
   * 普通象棋的房间没有选定设置,而万一有(那是一个服务端的缺陷),这里也不该硬解它 ——
   * 第二个从局面开局的棋种落地时,它的编码是它自己的事。
   */
  it.each([
    ['plain xiangqi', XIANGQI_KEY],
    ['gomoku', 'gomoku'],
    ['nothing', undefined],
  ])('refuses a setup on another game (%s)', (_what, key) => {
    expect(startPositionFromSetup(key, `${STANDARD}:0`)).toBeNull();
  });

  it.each([
    ['null', null],
    ['undefined', undefined],
    ['empty', ''],
  ])('returns null when there is no setup (%s)', (_what, setup) => {
    expect(startPositionFromSetup(XIANGQI_ENDGAME_KEY, setup)).toBeNull();
  });
});
