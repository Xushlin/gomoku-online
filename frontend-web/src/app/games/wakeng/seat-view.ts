import type { CardTableConfig, CardTableView } from '../cards/card-table-config';
import { decodeCard, decodeHand, type PlayingCard } from '../cards/cards';
import { compareWakengForDisplay } from './strength';

/**
 * `GameSnapshotDto.seatView` 里那份**按座位裁剪过**的挖坑局面 —— 服务端
 * `WakengSeatView` 的镜像。
 *
 * **它与斗地主那一份是两种形状,所以是两个解析函数。** `digger` / `bid` /
 * `firstBidder` / `firstBidderCard` 对 `landlord` / `baseScore` —— 而「它们可以分歧」
 * 正是「这不是一个事实」的检验。绘制只有一份(见 `CardTableView`)。
 *
 * `myHand` 里只有这个座位自己的牌:服务端逐张裁剪过,并有一条「没有一个座位看得到别人的
 * 任何一张」的断言钉着。客户端因此**不需要**、也不应该自己去藏什么。
 *
 * **没有「基数」。** 服务端刻意不发它:它今天恒等于 1,而那不是这一局的状态,
 * 是一个还不存在的房间设置。
 */
export interface WakengSeatView {
  readonly phase: 'Bidding' | 'Playing' | 'Finished';
  readonly firstBidder: number;
  readonly firstBidderCard: PlayingCard | null;
  readonly digger: number | null;
  readonly bid: number;
  readonly bidsMade: number;
  readonly myHand: readonly PlayingCard[];
  readonly handCounts: readonly number[];
  readonly kitty: readonly PlayingCard[] | null;
  readonly tableSeat: number | null;
  readonly tableCards: readonly PlayingCard[] | null;
  readonly winner: number | null;
  /** 「假如此刻轮到我,我出得起吗」—— 服务端算的,见 `WakengSeatView`。 */
  readonly canFollow: boolean;
}

/** 解 `seatView`。**解不出来时返回 `null`**,而不是抛 —— 与斗地主那份同一条理由。 */
export function parseSeatView(raw: string | null | undefined): WakengSeatView | null {
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
  const firstCard = stringOrNull(parsed['firstBidderCard']);

  return {
    phase,
    firstBidder: typeof parsed['firstBidder'] === 'number' ? parsed['firstBidder'] : 0,
    firstBidderCard: firstCard === null ? null : decodeCard(firstCard),
    digger: numberOrNull(parsed['digger']),
    bid: typeof parsed['bid'] === 'number' ? parsed['bid'] : 0,
    bidsMade: typeof parsed['bidsMade'] === 'number' ? parsed['bidsMade'] : 0,
    myHand: decodeHand(stringOrNull(parsed['myHand'])),
    handCounts: counts,
    kitty: stringOrNull(parsed['kitty']) === null ? null : decodeHand(stringOrNull(parsed['kitty'])),
    tableSeat: numberOrNull(parsed['tableSeat']),
    tableCards:
      stringOrNull(parsed['tableCards']) === null ? null : decodeHand(stringOrNull(parsed['tableCards'])),
    winner: numberOrNull(parsed['winner']),
    canFollow: parsed['canFollow'] === true,
  };
}

/** 归一到牌桌要画的那一份视图。 */
export function toTableView(raw: string | null | undefined): CardTableView | null {
  const v = parseSeatView(raw);
  if (!v) return null;
  return {
    phase: v.phase,
    roleSeat: v.digger,
    bid: v.bid,
    bidsMade: v.bidsMade,
    myHand: v.myHand,
    handCounts: v.handCounts,
    kitty: v.kitty,
    tableSeat: v.tableSeat,
    tableCards: v.tableCards,
    winner: v.winner,
    // 首叫者与他亮的那张 ♣ —— **公开的**,而服务端算得出,客户端不该自己猜。
    firstBidder:
      v.firstBidderCard === null ? null : { seat: v.firstBidder, card: v.firstBidderCard },
    canFollow: v.canFollow,
  };
}

/** 挖坑的牌桌配置 —— 与斗地主那份的**全部**差别就在这里。 */
export const WAKENG_TABLE: CardTableConfig = {
  // 底牌 4 张,比斗地主多一张。只用于显示(叫分阶段桌心画几张牌背)。
  kittySize: 4,
  i18nPrefix: 'cards.wakeng',
  showsFirstBidder: true,
  // **不是配置项凑数,是一处真缺陷的预防** —— 见 `CardTableConfig.compareForDisplay`。
  compareForDisplay: compareWakengForDisplay,
  parseView: toTableView,
};

function numberOrNull(value: unknown): number | null {
  return typeof value === 'number' ? value : null;
}

function stringOrNull(value: unknown): string | null {
  return typeof value === 'string' ? value : null;
}
