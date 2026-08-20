import { describe, expect, it } from 'vitest';

import { pipPath, SUITS_WITH_ART } from './card-art';
import { decodeCard } from './cards';

/**
 * 每一张有花色的牌都画得出形状,而两张王一张都不画。
 *
 * **走遍全部 54 个编码,而不是抽查四个花色。** 抽查证明的是「这个映射对某一张牌是对的」,
 * 而屏幕上要画的是一副牌:一个漏掉的点数(比如 10 的四张)在抽查下完全看不见。
 *
 * 上一版这条测试要去磁盘上找四个 PNG,而那需要一个**惰性** `import.meta.glob`(这份构建没有
 * `.png` 的 loader,eager 会让整个构建失败)。花色改成自绘 path 之后,那整段绕路没有了 ——
 * **能被删掉的机制才是最好的机制。**
 */

/** 服务端字母表的全部 54 个编码。 */
const ALL_CODES = [...'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz@#'];

describe('card art', () => {
  it('has a shape for each of the four suits', () => {
    expect([...SUITS_WITH_ART].sort()).toEqual(['clubs', 'diamonds', 'hearts', 'spades']);
  });

  it('draws all 52 suited cards and neither joker', () => {
    expect(ALL_CODES).toHaveLength(54);
    const jokers: string[] = [];
    for (const code of ALL_CODES) {
      const card = decodeCard(code);
      expect(card, `alphabet character ${code} does not decode`).not.toBeNull();
      const d = pipPath(card!.suit);
      if (card!.suit === 'none') {
        jokers.push(code);
        expect(d, `${code} is a joker and must have no pip`).toBeNull();
        continue;
      }
      // 闭合路径,而且不是空串 —— 一个空 `d` 在屏幕上是一张没有花色的牌,不是一个错误。
      expect(d, `${code} (${card!.label}) has no shape`).toBeTruthy();
      expect(d!.startsWith('M'), `${code}: path does not start with a moveto`).toBe(true);
      expect(d!.trimEnd().endsWith('Z'), `${code}: path is not closed`).toBe(true);
    }
    // 两张王,不是零张也不是三张 —— 否则上面那个 continue 可能吞掉了整副牌。
    expect(jokers).toEqual(['@', '#']);
  });

  it('gives the four suits four different shapes', () => {
    // 复制粘贴一条 path 忘了改,表现是屏幕上两个花色长得一样 —— 而每一张牌单看都「有花色」。
    const shapes = SUITS_WITH_ART.map((s) => pipPath(s));
    expect(new Set(shapes).size).toBe(4);
  });

  it('is null for a suit it does not know', () => {
    // `'none'` 是王;而一个未来的服务端多送一个这个构建不认识的花色,该表现为那一格不画。
    expect(pipPath('none')).toBeNull();
    expect(pipPath('sticks' as never)).toBeNull();
  });
});
