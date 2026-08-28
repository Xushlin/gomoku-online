import type { GameManifest } from './game-manifest';

/**
 * 一个席位**怎么称呼**。
 *
 * 两支,而不是一个可空的字符串:调用方要用的**键族**不同。侧栏要一个名词(「红方」),
 * 回合指示要一句话(「红方走棋」/「轮到 3 号座位」),而这两句话在「有名字」和「只有
 * 编号」两种情况下用的是不同的键。把判断留在这里、把措辞留给调用方,是因为**措辞是
 * 那个组件的事,而「这个棋种的席位有没有名字」是这个游戏的事。**
 */
export type SeatNaming =
  | { readonly kind: 'named'; readonly key: string }
  | { readonly kind: 'numbered'; readonly seat: number };

/**
 * 这个棋种的第 `seat` 号席位怎么称呼。
 *
 * **一个棋种要么给所有席位起名,要么一个都不起 —— 而这条规则是量出来才加的。**
 * 第一版按「这一格有没有名字」逐个判断,于是一个声明了两个名字、却有三个座位的棋种
 * 渲染出「黑方 / 白方 / 第 3 位」—— 半边有名字半边没有,**读起来像是第三个人不算玩家**。
 * 它在测试里当场露了面,而在浏览器里它只会看起来"有点怪"。
 *
 * 所以判据是**条数对得上**:`seatLabelKeys.length === seatCount`。对不上就整间房说编号,
 * 而那是一句诚实的话 ——「我不知道这些席位叫什么」。
 *
 * **缺省是「编号」,而不是「黑方 / 白方」** —— 见 `GameManifest.seatLabelKeys` 的说明:
 * 旧的缺省就是这个变更要修的那个失效。清单查不到、没声明、条数对不上,答案都是同一个。
 *
 * @param manifest 这个房间棋种的清单 —— 用 `byRoomKey` 取,伴生键才解析得出来。
 * @param seat 座位号,0 起。
 * @param seatCount 这个棋种一共有几个座位。
 */
export function seatNaming(
  manifest: GameManifest | undefined,
  seat: number,
  seatCount: number,
): SeatNaming {
  const keys = manifest?.seatLabelKeys;
  if (keys === undefined || keys.length !== seatCount) return { kind: 'numbered', seat };
  const key = keys[seat];
  return key === undefined ? { kind: 'numbered', seat } : { kind: 'named', key };
}
