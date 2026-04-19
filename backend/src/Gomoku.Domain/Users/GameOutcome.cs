namespace Gomoku.Domain.Users;

/// <summary>
/// 一方视角下的对局结果。用于 <see cref="User.RecordGameResult"/> 的入参以及
/// <see cref="EloRating.EloRating"/> 积分变动的计算。底层整数值固定 —— 未来若序列化 / 持久化,
/// 值稳定;MUST NOT 依赖 C# 枚举默认的 0→首值偶然巧合。
/// </summary>
public enum GameOutcome
{
    /// <summary>本方输掉本局。</summary>
    Loss = 0,

    /// <summary>本方赢下本局。</summary>
    Win = 1,

    /// <summary>本局以平局告终。</summary>
    Draw = 2,
}
