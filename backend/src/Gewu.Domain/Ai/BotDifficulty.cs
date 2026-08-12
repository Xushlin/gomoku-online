namespace Gomoku.Domain.Ai;

/// <summary>
/// AI 机器人难度。底层整数值 MUST 保持稳定,以便未来序列化 / 配置文件 / migration seed 的
/// 兼容性。后续扩展 <c>Hard=2</c> 时仅**追加**,不得重排现有值。
/// </summary>
public enum BotDifficulty
{
    /// <summary>随机合法落点,无策略。</summary>
    Easy = 0,

    /// <summary>启发式:自赢 → 堵五 → 中心 + 邻接打分,无博弈树。</summary>
    Medium = 1,

    /// <summary>
    /// Minimax + α-β 两层前瞻搜索,结合活三 / 冲四 / 活四等模式识别评估函数。
    /// 比 <see cref="Medium"/> 明显强;设计上不追求顶级水平(不做 VCF/VCT / 迭代深化 / transposition)。
    /// </summary>
    Hard = 2,
}
