import { describe, expect, it } from 'vitest';
import { DOUDIZHU_TABLE, parseSeatView } from './seat-view';

/**
 * 斗地主的 `seatView` 解析 —— **只测这个变更改的那一件事**。
 *
 * `canFollow` 此前是这份 adapter 里一个**写死的 `false`**,注释写着「斗地主还没有这个功能」。
 * 服务端现在真的发它了,所以这里断言它被**读**了,而不是仍然是那个常量。
 *
 * **没有这条断言的话,这个变更在客户端一侧完全没有被验证** —— 而 862 条既有测试全绿。
 */
function view(over: Record<string, unknown> = {}): string {
  return JSON.stringify({
    phase: 'Playing',
    landlord: 0,
    baseScore: 2,
    bidsMade: 3,
    myHand: 'ABC',
    handCounts: [3, 17, 17],
    kitty: 'DEF',
    tableSeat: 1,
    tableCards: 'z',
    winner: null,
    ...over,
  });
}

describe('doudizhu seat view', () => {
  it('reads canFollow from the server instead of hardcoding it', () => {
    expect(parseSeatView(view({ canFollow: true }))?.canFollow).toBe(true);
    // 负向必须钉住 —— 少了它,一个恒 true 的实现在上一条下也是绿的。
    expect(parseSeatView(view({ canFollow: false }))?.canFollow).toBe(false);
  });

  it('a server that does not send the field is treated as no signal', () => {
    // 旧服务端、或者一个这个构建读不懂的形状 —— 缺字段时的正确反应是「没有这个信号」,
    // 而不是「你要不起」。自动过牌因此不会在一个不发这个字段的服务端上乱动。
    expect(parseSeatView(view())?.canFollow).toBe(false);
  });

  it('the table config propagates it, not a constant', () => {
    // **这一条才是那个写死的 `false` 的死因。** 上面两条测的是 `parseSeatView`,
    // 而牌桌读的是 `DOUDIZHU_TABLE.parseView` —— 中间那一层曾经把真值丢掉。
    expect(DOUDIZHU_TABLE.parseView(view({ canFollow: true }))?.canFollow).toBe(true);
    expect(DOUDIZHU_TABLE.parseView(view({ canFollow: false }))?.canFollow).toBe(false);
  });
});
