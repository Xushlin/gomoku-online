import type { RoomSeat, UserSummary } from '../core/api/models/room.model';
import type { UserGameSummaryDto } from '../core/api/models/user-profile.model';

/**
 * 战绩里一局对局怎么读 —— **大厅卡片与个人主页共用这一份**。
 *
 * 两处此前各写了一份逐字相同的 `opponentOf` / `resultKey`。抽出来不是为了少打字:
 * 两份副本会分叉,而分叉的症状是**同一局对局在大厅说「负」、在个人主页说「说不出」** ——
 * 一个用户同时看得见两处,所以那种矛盾是看得见的。
 */

/**
 * 这一局里除 `userId` 以外的每一个座位上的人,按座位号升序。
 *
 * **返回数组而不是单个人**,因为三人局有两个对手。此前两处写的都是
 * `black.id === me ? white : black` —— 一个单数的答案,于是三人局里另外两个人
 * 只显示得出一个,而显示的是哪一个取决于本人坐 0 号还是别的座位。
 * **读起来像是那一局只有两个人**,不像少了一个字段。
 *
 * @param seats 这一局的座位表,来自 DTO。
 * @param userId 「我」是谁;`null` 时(未登录)原样返回所有座位。
 */
export function opponentsOf(
  seats: readonly RoomSeat[],
  userId: string | null,
): readonly UserSummary[] {
  return seats.filter((s) => s.player.id !== userId).map((s) => s.player);
}

/** {@link outcomeKeyFor} 的四个取值 —— 第四个是「这一行说不出」。 */
export type OutcomeKey =
  | 'profile.result-draw'
  | 'profile.result-win'
  | 'profile.result-loss'
  | 'profile.result-unrecorded';

/**
 * 「我方视角」的结果,而它有**四**支,不是三支。
 *
 * 第四支存在的理由是**这一行说不出那个答案,而说了会是错的**:`winnerUserId` 只装得下
 * 一个座位,而斗地主两名农民是**一起**赢的。领域层写明了这个取舍,并把出路留给客户端 ——
 * 「客户端从叫分历史里知道谁是地主,自己能说出『农民赢了』」—— 而 `UserGameSummaryDto`
 * 刻意不含 `moves`(列表视图太重,那个决定是对的),所以**那条出路在这一行上不成立**。
 *
 * 按旧的三支渲染,没走出去的那个农民,自己赢的一局显示成「负」。
 *
 * 三支仍然说得出的情况照旧说 —— 平局、赢家是我、以及**两座位**里赢家不是我。
 * 说不出只在真的说不出时出现,而不是把所有人的胜负都变模糊。
 */
export function outcomeKeyFor(g: UserGameSummaryDto, userId: string | null): OutcomeKey {
  if (g.result === 'Draw') return 'profile.result-draw';
  if (g.winnerUserId !== null && g.winnerUserId === userId) return 'profile.result-win';
  // 两个座位时「赢家不是我」等价于「我输了」;三个及以上时不等价 —— 赢家可能是队友。
  if (g.seats.length === 2) return 'profile.result-loss';
  return 'profile.result-unrecorded';
}
