namespace Gewu.Domain.Enums;

/// <summary>
/// 一盘棋的终局状态。
/// <para>
/// **这里没有"谁赢了"。** 判出胜负时取值是 <see cref="Decided"/>,赢家由旁边那个字段说明:
/// 规则层是 <c>MoveApplication.WinnerSeat</c>,聚合根与 DTO 是 <c>WinnerUserId</c>。
/// </para>
/// <para>
/// 此前的取值是 <c>Ongoing / BlackWin / WhiteWin / Draw</c>,而那两个带颜色的值是**同一个事实的
/// 第二份**,两处都是:
/// </para>
/// <list type="bullet">
/// <item><description><c>Board.PlaceStone(move)</c> 被告知了 <c>move.Stone</c>,然后用它算出返回值里的
/// 颜色 —— 落子类棋种里落子的一方不可能因为落子而输,所以那个颜色恒等于入参,没有一点新信息。</description></item>
/// <item><description><c>Game</c> 同时有 <c>Result ∈ {BlackWin, WhiteWin}</c> 与 <c>WinnerUserId</c>。
/// 两个字段说同一句"谁赢了" —— 而 <c>add-per-game-rating</c> 已经为这种形状付过账:镜像是第二份
/// 真源,漂移的那天没有东西会报。</description></item>
/// </list>
/// <para>
/// 顺带补上的是三座位:斗地主的 2 号座位赢了,在旧枚举里**没有值可以表示**。先问"这个值是从哪来的"
/// 而不是先问"怎么加第三个值",答案就从"加一个 <c>Seat2Win</c>"变成了"删两个"。
/// </para>
/// <para>
/// 底层值:<c>Draw</c> 保持 <c>3</c>,所以历史数据只需要把 <c>2</c> 重映射成 <c>1</c>。
/// </para>
/// </summary>
public enum GameResult
{
    /// <summary>对局进行中,尚未决出胜负或平局。</summary>
    Ongoing = 0,

    /// <summary>已判出胜负。赢的是谁**不在本枚举里** —— 见类型说明。</summary>
    Decided = 1,

    /// <summary>和局。</summary>
    Draw = 3,
}
