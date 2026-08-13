namespace Gewu.Domain.Puzzles;

/// <summary>一次完整答案校验的结果。</summary>
/// <param name="IsCorrect">是否通关。</param>
public sealed record PuzzleValidationResult(bool IsCorrect);

/// <summary>一次部分答案校验的结果。</summary>
/// <param name="IsCorrect">这一部分是否正确 —— 为 <c>false</c> 时调用方会给该尝试记一次错。</param>
public sealed record PuzzlePartialResult(bool IsCorrect);

/// <summary>一次提示的结果:只含被揭示的那一个片段。</summary>
/// <param name="RevealedJson">被揭示片段的 JSON。MUST NOT 包含答案的其余部分。</param>
public sealed record PuzzleHintResult(string RevealedJson);

/// <summary>
/// 单人关卡游戏的规则。按 <see cref="GameKey"/> 注册,新增一个关卡类游戏
/// = 一个本接口实现 + 一处 DI 注册,不需要改动 puzzle-core 的任何既有文件。
/// <para>
/// 三个游戏的关卡形状差别极大(成语纵横是字格、华容道是滑块布局、猜成语是一条释义),
/// 所以 <c>layoutJson</c> / <c>solutionJson</c> / 提交内容对本层都是**不透明字符串**
/// —— 平台不理解它们,只保证 <c>solutionJson</c> 不出服务端。
/// </para>
/// </summary>
public interface IPuzzleRules
{
    /// <summary>本规则服务的游戏键,与游戏注册表中的 key 一致。</summary>
    string GameKey { get; }

    /// <summary>校验一份完整答案。</summary>
    /// <param name="solutionJson">服务端答案。</param>
    /// <param name="submissionJson">玩家提交。</param>
    PuzzleValidationResult Validate(string solutionJson, string submissionJson);

    /// <summary>
    /// 校验一份部分答案(一条成语、一个区域)。存在的理由是答案不下发 ——
    /// 客户端没有答案就无法就地给出逐词反馈。
    /// </summary>
    /// <param name="solutionJson">服务端答案。</param>
    /// <param name="partialJson">玩家提交的这一部分。</param>
    PuzzlePartialResult CheckPartial(string solutionJson, string partialJson);

    /// <summary>
    /// 决定下一个要揭示的片段。
    /// </summary>
    /// <param name="solutionJson">服务端答案。</param>
    /// <param name="layoutJson">关卡布局。</param>
    /// <param name="alreadyRevealedCount">此前已揭示的片段数。</param>
    PuzzleHintResult Hint(string solutionJson, string layoutJson, int alreadyRevealedCount);

    /// <summary>
    /// 计算星级(1–3)。三个入参**全部是服务端事实**:提示由服务端发放并计数、
    /// 错误由服务端在部分校验里判定并计数、用时取服务端时钟。实现 MUST NOT
    /// 引入任何客户端自述的数值。
    /// </summary>
    /// <param name="hintsUsed">已用提示数。</param>
    /// <param name="mistakes">服务端记录的错误数。</param>
    /// <param name="duration">服务端测得的用时。</param>
    int Score(int hintsUsed, int mistakes, TimeSpan duration);
}

/// <summary>
/// 按游戏键解析 <see cref="IPuzzleRules"/>。未注册的键返回 <c>null</c>,
/// 由 handler 映射成 404 —— "这个游戏在本平台不存在"。
/// </summary>
public interface IPuzzleRulesRegistry
{
    /// <summary>取指定游戏的规则,未注册则 <c>null</c>。</summary>
    /// <param name="gameKey">游戏键。</param>
    IPuzzleRules? For(string gameKey);
}
