import type { PlayingCard } from '../cards/cards';

/**
 * 一张牌在挖坑里的强弱 —— `3 > 2 > A > K > … > 4`,**3 最大而不是最小**。
 *
 * 与服务端 `WakengRank.Strength` 对齐。它 MUST NOT 用 `PlayingCard.rank` 比大小:
 * 那个数是**编码**顺序(3、4、…、K、A、2),而它恰好等于斗地主的大小顺序 ——
 * 一个只被一个实现验证过的巧合。
 *
 * 挖坑用 52 张,牌堆里没有王;真传进来一张王会拿到 0,比任何一张牌都小。
 */
export function wakengStrength(card: PlayingCard): number {
  // rank: 3=3, 4..14 = 4..A, 15=2, 16/17=王
  if (card.rank === 3) return 13;
  if (card.rank === 15) return 12;
  if (card.rank >= 16) return 0;
  return card.rank - 3;
}

/** 手牌的显示顺序:**最弱在左,最强在右**。 */
export function compareWakengForDisplay(a: PlayingCard, b: PlayingCard): number {
  return wakengStrength(a) - wakengStrength(b) || a.code.localeCompare(b.code);
}
