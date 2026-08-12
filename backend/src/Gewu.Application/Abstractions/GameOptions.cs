using System.ComponentModel.DataAnnotations;

namespace Gomoku.Application.Abstractions;

/// <summary>
/// 对局层面的运行参数,绑定 <c>appsettings.json</c> 的 <c>"Game"</c> 段。
/// 目前只含超时相关;随后其它"全局对局规则"(比如 blitz 模式、禁手开关)也会挂在这里。
/// </summary>
public sealed class GameOptions
{
    /// <summary>
    /// 每一回合的最长等待时间(秒)。超过后 <c>TurnTimeoutWorker</c> 会判当前回合玩家负。
    /// 允许范围 [10, 3600]。
    /// </summary>
    [Range(10, 3600)]
    public int TurnTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// <c>TurnTimeoutWorker</c> 的轮询间隔(毫秒)。每周期查一次当前回合已超时的房间。
    /// 允许范围 [1000, 60000]。
    /// </summary>
    [Range(1000, 60_000)]
    public int TimeoutPollIntervalMs { get; set; } = 5000;
}
