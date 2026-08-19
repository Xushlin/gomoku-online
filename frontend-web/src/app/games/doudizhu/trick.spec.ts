import { describe, expect, it } from 'vitest';

import type { MoveDto } from '../../core/api/models/room.model';
import { currentTrick } from './trick';

function move(ply: number, seat: number, text: string | null): MoveDto {
  return { ply, row: null, col: null, text, seat, playedAt: 'x' };
}

describe('currentTrick', () => {
  it('is empty with no moves', () => {
    expect(currentTrick([])).toEqual([]);
    expect(currentTrick(null)).toEqual([]);
    expect(currentTrick(undefined)).toEqual([]);
  });

  it('shows every bid during bidding', () => {
    const trick = currentTrick([move(1, 0, 'bid:2'), move(2, 1, 'bid:0')]);
    expect(trick).toEqual([
      { seat: 0, ply: 1, kind: 'bid', points: 2 },
      { seat: 1, ply: 2, kind: 'bid', points: 0 },
    ]);
  });

  it('starts the trick at the last play and keeps the passes after it', () => {
    const trick = currentTrick([
      move(1, 0, 'bid:3'),
      move(2, 0, 'play:AB'),
      move(3, 1, 'pass'),
      move(4, 2, 'play:CD'),
      move(5, 0, 'pass'),
    ]);
    expect(trick.map((a) => [a.seat, a.kind])).toEqual([
      [2, 'play'],
      [0, 'pass'],
    ]);
  });

  it('drops the previous round of passes when a new hand is played', () => {
    // **这是「这一轮」这个概念的全部内容**:桌上那手要压的牌就是最后一手非 pass 的出牌,
    // 它之后只可能是 pass。所以新一轮开始时,上一轮的「不要」自己消失 —— 没有谁去清它。
    const before = currentTrick([move(1, 0, 'play:A'), move(2, 1, 'pass'), move(3, 2, 'pass')]);
    expect(before).toHaveLength(3);

    const after = currentTrick([
      move(1, 0, 'play:A'),
      move(2, 1, 'pass'),
      move(3, 2, 'pass'),
      move(4, 0, 'play:BC'),
    ]);
    expect(after.map((a) => [a.seat, a.kind])).toEqual([[0, 'play']]);
  });

  it('decodes the cards of the hand on the table', () => {
    const [action] = currentTrick([move(1, 1, 'play:AB')]);
    expect(action.kind).toBe('play');
    expect(action.kind === 'play' && action.cards.map((c) => c.code)).toEqual(['A', 'B']);
  });

  it('ignores moves it cannot read instead of throwing', () => {
    // 与 `cards.ts` 同一条:看起来不对,但不要白屏。棋盘棋种的 move 没有 text,也走这条路。
    const trick = currentTrick([
      move(1, 0, null),
      move(2, 0, 'nonsense'),
      move(3, 0, 'bid:not-a-number'),
      move(4, 1, 'bid:1'),
    ]);
    expect(trick).toEqual([{ seat: 1, ply: 4, kind: 'bid', points: 1 }]);
  });

  it('shows no bubbles for passes that precede any play', () => {
    // 叫分阶段只可能有 bid;一个不该出现的 pass 不该被当成「这一轮」的内容。
    expect(currentTrick([move(1, 0, 'pass')])).toEqual([]);
  });
});
