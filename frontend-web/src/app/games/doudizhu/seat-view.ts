import type { CardTableConfig, CardTableView } from '../cards/card-table-config';
import { decodeHand, type PlayingCard } from '../cards/cards';

/**
 * `GameSnapshotDto.seatView` 里那份**按座位裁剪过**的斗地主局面。
 *
 * 服务端 `DoudizhuSeatView` 的镜像。它是**不透明载荷**:内核不解析它,由这里按棋种解 ——
 * 与闯关那条线的 `LayoutJson` 同一个做法。
 *
 * `myHand` 里只有这个座位自己的牌:服务端逐张裁剪过,并有一条"没有一个座位看得到别人的
 * 任何一张"的断言钉着。客户端因此**不需要**、也不应该自己去藏什么。
 */
export interface DoudizhuSeatView {
  readonly phase: 'Bidding' | 'Playing' | 'Finished';
  readonly landlord: number | null;
  readonly baseScore: number;
  readonly bidsMade: number;
  readonly myHand: readonly PlayingCard[];
  readonly handCounts: readonly number[];
  readonly kitty: readonly PlayingCard[] | null;
  readonly tableSeat: number | null;
  readonly tableCards: readonly PlayingCard[] | null;
  readonly winner: number | null;
}

/**
 * 解 `seatView`。**解不出来时返回 `null`**,而不是抛。
 *
 * 三种"解不出来"都走这条路:字段不在(棋种没有隐藏状态)、对局还没开始(服务端给 `null`)、
 * 以及一个这个构建读不懂的形状。三种的正确反应都是"这一块先不画",而不是让房间页整页挂掉。
 */
export function parseSeatView(raw: string | null | undefined): DoudizhuSeatView | null {
  if (!raw) return null;
  let parsed: Record<string, unknown>;
  try {
    parsed = JSON.parse(raw) as Record<string, unknown>;
  } catch {
    return null;
  }
  if (typeof parsed !== 'object' || parsed === null) return null;

  const phase = parsed['phase'];
  if (phase !== 'Bidding' && phase !== 'Playing' && phase !== 'Finished') return null;

  const counts = Array.isArray(parsed['handCounts'])
    ? (parsed['handCounts'] as unknown[]).filter((n): n is number => typeof n === 'number')
    : [];

  return {
    phase,
    landlord: numberOrNull(parsed['landlord']),
    baseScore: typeof parsed['baseScore'] === 'number' ? parsed['baseScore'] : 0,
    bidsMade: typeof parsed['bidsMade'] === 'number' ? parsed['bidsMade'] : 0,
    myHand: decodeHand(stringOrNull(parsed['myHand'])),
    handCounts: counts,
    kitty: stringOrNull(parsed['kitty']) === null ? null : decodeHand(stringOrNull(parsed['kitty'])),
    tableSeat: numberOrNull(parsed['tableSeat']),
    tableCards:
      stringOrNull(parsed['tableCards']) === null ? null : decodeHand(stringOrNull(parsed['tableCards'])),
    winner: numberOrNull(parsed['winner']),
  };
}

function numberOrNull(value: unknown): number | null {
  return typeof value === 'number' ? value : null;
}

function stringOrNull(value: unknown): string | null {
  return typeof value === 'string' ? value : null;
}

/**
 * 归一到牌桌要画的那一份视图。
 *
 * **`firstBidder` 是 `null`,而那不是「还没算出来」** —— 斗地主里根本没有这个概念:
 * 谁先叫分是约定(0 号先),而挖坑是发牌决定的。用一个 `null` 表示「不适用」在这里是
 * 对的,因为牌桌问的是「要不要画这个标记」,而答案永远是不要。
 */
export function toTableView(raw: string | null | undefined): CardTableView | null {
  const v = parseSeatView(raw);
  if (!v) return null;
  return {
    phase: v.phase,
    roleSeat: v.landlord,
    bid: v.baseScore,
    bidsMade: v.bidsMade,
    myHand: v.myHand,
    handCounts: v.handCounts,
    kitty: v.kitty,
    tableSeat: v.tableSeat,
    tableCards: v.tableCards,
    winner: v.winner,
    firstBidder: null,
    // 斗地主还没有「要不起自动过牌」—— `false` 在这里是「没有这个信号」。
    canFollow: false,
  };
}

/** 斗地主的牌桌配置。 */
export const DOUDIZHU_TABLE: CardTableConfig = {
  kittySize: 3,
  i18nPrefix: 'game.doudizhu',
  roleLabelKey: 'game.doudizhu.landlord',
  showsFirstBidder: false,
  // 斗地主的大小**恰好**就是编码顺序,所以按 `rank` 排是对的 —— 而那是巧合,
  // 不是通则:挖坑是 `3 > 2 > A > … > 4`。见 `cards.ts` 上 `rank` 的说明。
  compareForDisplay: (a, b) => a.rank - b.rank || a.code.localeCompare(b.code),
  parseView: toTableView,
};
