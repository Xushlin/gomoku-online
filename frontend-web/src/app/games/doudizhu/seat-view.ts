import { decodeHand, type DoudizhuCard } from './cards';

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
  readonly myHand: readonly DoudizhuCard[];
  readonly handCounts: readonly number[];
  readonly kitty: readonly DoudizhuCard[] | null;
  readonly tableSeat: number | null;
  readonly tableCards: readonly DoudizhuCard[] | null;
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
