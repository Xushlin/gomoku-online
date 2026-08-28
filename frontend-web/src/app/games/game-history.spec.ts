import { describe, expect, it } from 'vitest';
import { opponentsOf, outcomeKeyFor } from './game-history';
import type { UserGameSummaryDto } from '../core/api/models/user-profile.model';
import type { RoomSeat } from '../core/api/models/room.model';

const ME = 'u-me';

function seats(...ids: readonly string[]): readonly RoomSeat[] {
  return ids.map((id, index) => ({ index, player: { id, username: id.toUpperCase() } }));
}

function game(over: Partial<UserGameSummaryDto> = {}): UserGameSummaryDto {
  return {
    roomId: 'r-1',
    name: 'game',
    seats: seats(ME, 'u-2'),
    startedAt: '2026-04-24T00:00:00Z',
    endedAt: '2026-04-24T00:05:00Z',
    result: 'Decided',
    winnerUserId: ME,
    endReason: 'Decided',
    moveCount: 9,
    ...over,
  };
}

describe('opponentsOf', () => {
  it('两人局恰好一个对手', () => {
    expect(opponentsOf(seats(ME, 'u-2'), ME).map((p) => p.id)).toEqual(['u-2']);
  });

  it('三人局恰好两个对手,而不是一个', () => {
    // 修之前这里是 `black.id === me ? white : black` —— 一个单数的答案。
    // **恰好两个**:一个「至少一个」的断言在缺陷下依然是绿的。
    expect(opponentsOf(seats(ME, 'u-2', 'u-3'), ME).map((p) => p.id)).toEqual(['u-2', 'u-3']);
  });

  it('我坐 2 号座位时,另外两个人一个都不少', () => {
    // 此前显示得出哪一个对手取决于我坐几号 —— 这一条钉住那件事不再发生。
    expect(opponentsOf(seats('u-1', 'u-2', ME), ME).map((p) => p.id)).toEqual(['u-1', 'u-2']);
  });

  it('按座位号升序,不按别的', () => {
    expect(opponentsOf(seats('u-1', 'u-2', 'u-3'), ME).map((p) => p.id)).toEqual([
      'u-1',
      'u-2',
      'u-3',
    ]);
  });
});

describe('outcomeKeyFor', () => {
  it('平局说平', () => {
    expect(outcomeKeyFor(game({ result: 'Draw', winnerUserId: null }), ME)).toBe(
      'profile.result-draw',
    );
  });

  it('两人局赢家是我 → 胜', () => {
    expect(outcomeKeyFor(game({ winnerUserId: ME }), ME)).toBe('profile.result-win');
  });

  it('两人局赢家不是我 → 负', () => {
    // **反面控制**:第四支 MUST NOT 把两座位的负也吞掉,否则「说不出」会变成
    // 一个把所有人胜负都变模糊的开关,而那不是它存在的理由。
    expect(outcomeKeyFor(game({ winnerUserId: 'u-2' }), ME)).toBe('profile.result-loss');
  });

  it('三人局赢家是我 → 胜(说得出就要说)', () => {
    expect(
      outcomeKeyFor(game({ seats: seats(ME, 'u-2', 'u-3'), winnerUserId: ME }), ME),
    ).toBe('profile.result-win');
  });

  it('三人局赢家不是我 → 说不出,而 MUST NOT 是「负」', () => {
    // 这是整个变更最容易被「只加 seats」盖过去的一条:那一行会列出两个对手、
    // 看起来完全正常,然后继续把赢了的农民说成输了。
    const g = game({ seats: seats(ME, 'u-2', 'u-3'), winnerUserId: 'u-2' });

    expect(outcomeKeyFor(g, ME)).toBe('profile.result-unrecorded');
    expect(outcomeKeyFor(g, ME)).not.toBe('profile.result-loss');
  });

  it('三人局的平局仍然说平 —— 说不出只挡胜负那一支', () => {
    // 三家都不叫 = 流局,而流局是**说得出**的:它对每个座位都一样。
    expect(
      outcomeKeyFor(
        game({ seats: seats(ME, 'u-2', 'u-3'), result: 'Draw', winnerUserId: null }),
        ME,
      ),
    ).toBe('profile.result-draw');
  });
});
