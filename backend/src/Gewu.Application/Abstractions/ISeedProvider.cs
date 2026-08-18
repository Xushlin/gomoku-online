namespace Gewu.Application.Abstractions;

/// <summary>
/// 开局种子的来源。生产实现包一层 <see cref="Random.Shared"/>;测试注入固定值。
/// <para>
/// 它与 <see cref="IAiRandomProvider"/> 分开,不是因为形状不同,而是因为**用途不同**:
/// AI 那个是"思考时的抖动",这个是"一局的身份"。把它们合成一个 <c>IRandomProvider</c>
/// 会让一条针对 AI 随机性的测试替身顺带决定所有玩家拿到的方块序列。
/// </para>
/// <para>
/// 种子**不需要**跨版本稳定的生成算法 —— 它一开局就落库,重放读的是库里那个值。
/// 必须跨版本、跨语言稳定的是 <c>seed → 方块序列</c> 那一步,而那一步没有用
/// <c>System.Random</c>,理由写在 <c>TetrisPieceSequence</c> 上。
/// </para>
/// </summary>
public interface ISeedProvider
{
    /// <summary>取一个新种子。</summary>
    int NextSeed();
}
