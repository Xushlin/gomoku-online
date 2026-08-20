import type { MoveDto } from '../../core/api/models/room.model';
import { decodeHand, type PlayingCard } from './cards';

/** 一个座位在当前一轮里做的事。 */
export type TrickAction =
  | { readonly seat: number; readonly ply: number; readonly kind: 'bid'; readonly points: number }
  | { readonly seat: number; readonly ply: number; readonly kind: 'pass' }
  | { readonly seat: number; readonly ply: number; readonly kind: 'play'; readonly cards: readonly PlayingCard[] };

/**
 * 当前一轮里每个座位做了什么 —— 从**已经公开的 `moves`** 算出来。
 *
 * QQ 那张参照图里最有信息量的是「不要」两个字贴在人旁边。它不需要任何规则知识:
 *
 * > **从最后一手 `play:` 起到末尾的那一段就是当前一轮** —— 因为桌上那手要压的牌就是最后一手
 * > 非 pass 的出牌,它之后只可能是 pass。
 *
 * 于是新一轮开始时上一轮的「不要」会自己消失,因为那一段的起点前移了 —— 不需要谁去清。
 * 叫分阶段还没有任何 `play:`,取全部 `bid:`。
 *
 * **它 MUST NOT 用于任何判断。** 要压的那手牌只认服务端的 `tableCards`;这里做的事是把一份
 * 已经在快照里的公开事实换个位置显示,而不是在客户端重建局面。
 */
export function currentTrick(moves: readonly MoveDto[] | null | undefined): readonly TrickAction[] {
  if (!moves?.length) return [];
  const actions = moves.map(parse).filter((a): a is TrickAction => a !== null);
  const lastPlay = actions.reduce((found, a, i) => (a.kind === 'play' ? i : found), -1);
  if (lastPlay >= 0) return actions.slice(lastPlay);
  return actions.filter((a) => a.kind === 'bid');
}

/**
 * 这条 move 是哪一类动作 —— `null` 表示读不懂或不是斗地主的载荷。
 *
 * 导出它是为了让房间页能问「刚到的这一手是出牌还是过牌」而**不必再抄一遍编码**:
 * `play:` / `pass` / `bid:` 这三个前缀在客户端只有这一个文件认得。
 */
export function moveKind(move: MoveDto): TrickAction['kind'] | null {
  return parse(move)?.kind ?? null;
}

/** 解一条 move 的文本载荷。认不出来的返回 `null` —— 与 `cards.ts` 同一条:看起来不对,但不要白屏。 */
function parse(move: MoveDto): TrickAction | null {
  const text = move.text;
  if (!text) return null;
  const seat = move.seat;
  const ply = move.ply;
  if (text === 'pass') return { seat, ply, kind: 'pass' };
  if (text.startsWith('bid:')) {
    const points = Number(text.slice(4));
    return Number.isInteger(points) ? { seat, ply, kind: 'bid', points } : null;
  }
  if (text.startsWith('play:')) {
    return { seat, ply, kind: 'play', cards: decodeHand(text.slice(5)) };
  }
  return null;
}
