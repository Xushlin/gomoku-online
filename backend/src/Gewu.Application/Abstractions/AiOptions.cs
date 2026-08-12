using System.ComponentModel.DataAnnotations;

namespace Gomoku.Application.Abstractions;

/// <summary>
/// <c>AiMoveWorker</c> 的运行参数,绑定 <c>appsettings.json</c> 的 <c>"Ai"</c> 段。
/// </summary>
public sealed class AiOptions
{
    /// <summary>
    /// 轮询间隔(毫秒)。每个周期开始时 worker <c>Task.Delay</c> 该时长,然后查一次
    /// <c>IUserRepository.GetRoomsNeedingBotMoveAsync</c>。允许范围 [100, 60000]。
    /// </summary>
    [Range(100, 60_000)]
    public int PollIntervalMs { get; set; } = 1500;

    /// <summary>
    /// AI 最短"思考时间"(毫秒)。worker 命中一个待走房间时,若对手最后一步距今小于该值,
    /// 跳过本轮 —— 给人以"AI 在思考"的观感,避免瞬发。允许范围 [0, 10000]。
    /// </summary>
    [Range(0, 10_000)]
    public int MinThinkTimeMs { get; set; } = 800;
}
