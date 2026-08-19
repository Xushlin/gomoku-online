/**
 * 牌的一字符编码 —— **服务端 `Card.Alphabet` 的一份副本**。
 *
 * 这份副本是必需的,而不是图省事:服务端送来的是编码串(`myHand: "ABDFI…"`),
 * 而屏幕上要画出「♣3」。不解码就没有 UI。
 *
 * 它能被接受,是按这个仓库自己的那条尺子量的 —— **一份副本能不能接受,看的不是它多小,
 * 而是它错了会不会有人发现**:错一个字符,牌面上立刻是一张错的牌,而那是最显眼的一种坏。
 * 而它 MUST NOT 反过来用于**判断**:压不压得住、是不是合法牌型,全在服务端。
 *
 * 编码是持久化格式,所以它**永远不变** —— 服务端那份注释就是这么写的。
 */
const ALPHABET = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz@#';

/** 花色顺序,与服务端 `Card.Suits` 一致。 */
const SUITS = ['clubs', 'diamonds', 'hearts', 'spades'] as const;

/** 花色;王没有花色。 */
export type CardSuit = (typeof SUITS)[number] | 'none';

/** 一张牌 —— 只为显示,不参与任何判断。 */
export interface DoudizhuCard {
  /** 原始编码字符,回传给服务端时按它拼串。 */
  readonly code: string;
  /** 点数:3–15(2)、16(小王)、17(大王)。数值就是大小顺序。 */
  readonly rank: number;
  readonly suit: CardSuit;
  /** 牌面文字:`3`…`10`、`J`/`Q`/`K`/`A`/`2`、`小`/`大`。 */
  readonly label: string;
  /** 红色牌(红桃 / 方块 / 大王)—— 只影响配色。 */
  readonly red: boolean;
}

const RANK_LABELS: Record<number, string> = {
  11: 'J',
  12: 'Q',
  13: 'K',
  14: 'A',
  15: '2',
  16: '小',
  17: '大',
};

/** 一张牌的牌面文字。 */
function labelOf(rank: number): string {
  return RANK_LABELS[rank] ?? String(rank);
}

/**
 * 解一个编码字符。认不出来时返回 `null`。
 *
 * **认不出来不抛异常**:一个未来的服务端多送一张这个构建不认识的牌,应该表现为
 * 那一张画不出来,而不是整页崩掉。与棋盘对越界落子的处理同一条 ——
 * "看起来不对,但不要白屏"。
 */
export function decodeCard(code: string): DoudizhuCard | null {
  const index = ALPHABET.indexOf(code);
  if (index < 0 || code.length !== 1) return null;
  if (index >= 52) {
    const rank = index === 52 ? 16 : 17;
    return { code, rank, suit: 'none', label: labelOf(rank), red: rank === 17 };
  }
  const rank = Math.floor(index / 4) + 3;
  const suit = SUITS[index % 4];
  return { code, rank, suit, label: labelOf(rank), red: suit === 'hearts' || suit === 'diamonds' };
}

/** 解一手牌;认不出来的字符**跳过**。 */
export function decodeHand(encoded: string | null | undefined): readonly DoudizhuCard[] {
  if (!encoded) return [];
  return [...encoded].map(decodeCard).filter((c): c is DoudizhuCard => c !== null);
}

/** 把选中的牌拼回编码串。顺序按点数升序 —— 服务端的编码是排序过的。 */
export function encodeHand(cards: readonly DoudizhuCard[]): string {
  return [...cards].sort((a, b) => a.rank - b.rank || a.code.localeCompare(b.code))
    .map((c) => c.code)
    .join('');
}

/** 花色符号 —— 王没有符号。 */
export function suitSymbol(suit: CardSuit): string {
  switch (suit) {
    case 'clubs': return '♣';
    case 'diamonds': return '♦';
    case 'hearts': return '♥';
    case 'spades': return '♠';
    default: return '';
  }
}
