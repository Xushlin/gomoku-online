/**
 * 牌桌的**座位几何** —— 纯函数,不量 DOM。
 *
 * 不量 DOM 有两个后果,都是想要的:在 jsdom 里可测(所以可变异),以及**不在乎排版** ——
 * 桌子在 375 px 下换了摆法,方位仍然对。
 */

/** 一个座位在屏幕上的方位。`self` 是我(下方)。 */
export type SeatDirection = 'self' | 'right' | 'across' | 'left';

/**
 * 从我开始的环绕顺序。
 *
 * **下家在右手边** —— 出牌逆时针,而俯视时从下方逆时针数的下一个位置就是右边。
 * 这与 `CardTable.others()` 一直用的"从我的下一个座位开始"是同一个顺序,只是给了它屏幕方向。
 *
 * `mySeat` 为 `null`(围观者)时**从 0 号座位的椅子上看** —— 围观者总得坐在某个方向上,
 * 而"座位表里的第一个"是唯一一个不需要额外约定就能说清的选择。
 */
export function seatRing(mySeat: number | null, total: number): readonly number[] {
  if (total <= 0) return [];
  const base = mySeat ?? 0;
  return Array.from({ length: total }, (_, i) => (base + i) % total);
}

/**
 * 这个座位在我看来的方位。
 *
 * 三家时是 `self` / `right` / `left`。**`across` 不是为将来留的抽象**:挖坑是四个人,
 * 而没有 `across` 的话第四个人会和另一个人叠在同一个位置上 —— 那是画错,不是少画。
 */
export function relativeSeat(seat: number, mySeat: number | null, total: number): SeatDirection {
  const ring = seatRing(mySeat, total);
  const index = ring.indexOf(seat);
  if (index < 0) return 'self';
  if (index === 0) return 'self';
  if (index === 1) return 'right';
  if (index === ring.length - 1) return 'left';
  return 'across';
}

/**
 * 头像上那个字。
 *
 * 按**码点**取而不是 `name[0]`:一个 emoji 或一个增补平面汉字用 `[0]` 会切出半个代理对,
 * 屏幕上是一个替换字符。
 */
export function seatInitial(username: string): string {
  return [...(username ?? '')][0]?.toUpperCase() ?? '?';
}
