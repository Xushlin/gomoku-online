namespace Gewu.Domain.Puzzles;

/// <summary>一次完整答案校验的结果。</summary>
/// <param name="IsCorrect">是否通关。</param>
public sealed record PuzzleValidationResult(bool IsCorrect);

/// <summary>
/// 一次部分答案校验的结果。
/// <para>
/// <paramref name="PayloadJson"/> 是给"答对之后要说点什么"用的:成语纵横要在一条成语
/// 填满的瞬间显示它的释义,而释义在数据库里、词典没有 HTTP 面,客户端凭自己拼不出来。
/// 华容道要说"这一步把曹操挪出来了"、猜成语要给出处,都是同一个需求。
/// </para>
/// <para>
/// 它对答案封闭规则**没有**削弱:载荷描述的是玩家刚刚已经解开的那部分,不透露网格
/// 未解部分的任何信息。因此实现 MUST 只在 <paramref name="IsCorrect"/> 为 <c>true</c>
/// 时填充它 —— 答错时附带任何内容都等于借错误路径泄题。
/// </para>
/// </summary>
/// <param name="IsCorrect">这一部分是否正确 —— 为 <c>false</c> 时调用方会给该尝试记一次错。</param>
/// <param name="PayloadJson">答对时的游戏自定义载荷;答错时 MUST 为 <c>null</c>。</param>
public sealed record PuzzlePartialResult(bool IsCorrect, string? PayloadJson = null);

/// <summary>一次提示的结果:只含被揭示的那一个片段。</summary>
/// <param name="RevealedJson">被揭示片段的 JSON。MUST NOT 包含答案的其余部分。</param>
public sealed record PuzzleHintResult(string RevealedJson);

/// <summary>
/// 计分的全部入参。
/// <para>
/// 前三项是既有的三个**服务端信号**:提示由服务端发放并计数、错误由服务端在
/// <c>check</c> 里判定并计数、用时取服务端时钟。它们排在前面,是为了让"入参必须
/// 服务端可观测"这条约束在类型里仍然显式。
/// </para>
/// <para>
/// 后三项是关卡的两半与**已被判定通关的**提交。把提交交给计分不是那条约束的例外,
/// 因为它的性质不同:「我只错了 0 次」是一句无法验证的自述,而「这是我的 81 步」
/// 是一句服务端必须自己走一遍才肯接受的话 —— <c>Validate</c> 已经从
/// <see cref="LayoutJson"/> 出发重放过每一步,任何一步不合法或走完不通关都整份作废。
/// </para>
/// <para>
/// <b>一个客户端给的数字不可信;一个服务端必须重建之后才肯接受的数字,是服务端观测到的事实。</b>
/// 因此实现 MAY 依据提交算出步数一类的量,但 MUST NOT 读取提交里任何**未经重放确认**
/// 的字段(客户端自己写的 <c>moveCount</c> / <c>elapsedMs</c> 之类)—— 那会把约束绕回原样。
/// </para>
/// <para>
/// 关卡的两半也在这里,是因为「多少步算好」是关卡属性(经典局面有已知最少步数),不是常数。
/// </para>
/// </summary>
/// <param name="HintsUsed">已用提示数,服务端计。</param>
/// <param name="Mistakes">错误数,服务端在 <c>check</c> 里判。可能一直是 0 —— 不调用
/// <c>check</c> 的游戏(华容道)从不填充它,所以计分公式 MUST NOT 依赖它非零。</param>
/// <param name="Duration">用时,取自服务端时钟。</param>
/// <param name="LayoutJson">关卡布局(公开的那一半)。</param>
/// <param name="SolutionJson">关卡答案(永不下发的那一半)。</param>
/// <param name="SubmissionJson">玩家的提交,**已经**被 <c>Validate</c> 判定为通关。</param>
public readonly record struct PuzzleScoreInput(
    int HintsUsed,
    int Mistakes,
    TimeSpan Duration,
    string LayoutJson,
    string SolutionJson,
    string SubmissionJson);

/// <summary>
/// 单人关卡游戏的规则。按 <see cref="GameKey"/> 注册,新增一个关卡类游戏
/// = 一个本接口实现 + 一处 DI 注册,不需要改动 puzzle-core 的任何既有文件。
/// <para>
/// 三个游戏的关卡形状差别极大(成语纵横是字格、华容道是滑块布局、猜成语是一条释义),
/// 所以 <c>layoutJson</c> / <c>solutionJson</c> / 提交内容对本层都是**不透明字符串**
/// —— 平台不理解它们,只保证 <c>solutionJson</c> 不出服务端。
/// </para>
/// <para>
/// 每个方法都收**关卡的两半**加上玩家这次给的载荷。布局必须一起传,是因为不是每种
/// 答案都自描述:成语纵横的答案是**位置性**的(每一格该填什么都在答案里,判定不参照
/// 任何起点),而华容道的答案是一条**路径**,路径只能对着它的起点验 —— 起点就是布局。
/// <c>Hint</c> 一开始就同时收两半;<c>Validate</c> 与 <c>CheckPartial</c> 当初只收答案,
/// 不是一个决定,是当时唯一那个实现的形状透了出来。
/// </para>
/// </summary>
public interface IPuzzleRules
{
    /// <summary>本规则服务的游戏键,与游戏注册表中的 key 一致。</summary>
    string GameKey { get; }

    /// <summary>校验一份完整答案。</summary>
    /// <param name="solutionJson">服务端答案。</param>
    /// <param name="layoutJson">关卡布局 —— 路径类答案的重放起点。</param>
    /// <param name="submissionJson">玩家提交。</param>
    PuzzleValidationResult Validate(string solutionJson, string layoutJson, string submissionJson);

    /// <summary>
    /// 校验一份部分答案(一条成语、一个区域)。存在的理由是答案不下发 ——
    /// 客户端没有答案就无法就地给出逐词反馈。
    /// <para>
    /// 因此它对每个游戏都是**可选**的:客户端自己判得了的游戏可以完全不调它。
    /// 华容道的滑动合法性由公开的盘面与公开的规则决定,为每一步发一个请求不会让
    /// 服务端多知道任何东西 —— 它最后无论如何都要重放整条路径。实现仍然 MUST 提供
    /// 本方法,但平台 MUST NOT 假定它被调用过。
    /// </para>
    /// </summary>
    /// <param name="solutionJson">服务端答案。</param>
    /// <param name="layoutJson">关卡布局。</param>
    /// <param name="partialJson">玩家提交的这一部分。</param>
    PuzzlePartialResult CheckPartial(string solutionJson, string layoutJson, string partialJson);

    /// <summary>
    /// 决定要揭示的片段。
    /// <para>
    /// <paramref name="stateJson"/> 是客户端上报的盘面状态,对平台**不透明** —— 与
    /// <c>CheckPartial</c> / <c>Validate</c> 的载荷同一性质,由各游戏自行解析。
    /// 成语纵横传的是"哪些格已有字 + 光标在哪"。
    /// </para>
    /// <para>
    /// 它决定的是**揭哪一格**,MUST NOT 影响计分:提示次数由服务端在每次调用时递增,
    /// 是唯一算数的那个数字。采信这份上报不构成漏洞 —— 客户端报告的是自己可见的盘面,
    /// 不是答案;答案始终只在服务端,响应也始终只有一格。客户端确实能借此指定揭哪一格,
    /// 那是特性:原型本来就让玩家点着某格要提示,而且每次照样扣一颗星。
    /// </para>
    /// <para>
    /// 缺省或无法解析时,实现 MUST 退化到一个合理的默认揭示,MUST NOT 抛错 ——
    /// 一个没更新的客户端应该拿到提示,而不是 400。
    /// </para>
    /// </summary>
    /// <param name="solutionJson">服务端答案。</param>
    /// <param name="layoutJson">关卡布局。</param>
    /// <param name="stateJson">客户端上报的盘面状态;可为 <c>null</c>。</param>
    PuzzleHintResult Hint(string solutionJson, string layoutJson, string? stateJson);

    /// <summary>
    /// 计算星级(1–3)。入参**全部是服务端事实** —— 见 <see cref="PuzzleScoreInput"/>
    /// 对「已被重放确认的提交也算事实」的说明。实现 MUST NOT 引入任何客户端自述的数值。
    /// <para>
    /// 星级**公式**按游戏而异(华容道计步数、成语纵横计错误与提示),但入参必须服务端
    /// 可观测这条属于平台,MUST NOT 由单个游戏放宽。
    /// </para>
    /// </summary>
    /// <param name="input">计分入参。</param>
    int Score(PuzzleScoreInput input);
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
