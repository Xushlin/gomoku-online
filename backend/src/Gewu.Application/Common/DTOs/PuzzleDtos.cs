namespace Gewu.Application.Common.DTOs;

/// <summary>
/// 关卡列表中的一项。
/// <para>
/// ⚠️ 本 DTO 与 <see cref="PuzzleLevelDto"/> 都**没有**任何能承载答案的字段,这是
/// puzzle-core 的答案封闭保证:泄漏必须表现为"有人新增了一个属性"。改动这两个 record
/// 前请先看 <c>PuzzleLevelDtoTests</c> —— 那里有断言拦着。
/// </para>
/// </summary>
/// <param name="LevelIndex">关卡序号。</param>
/// <param name="Difficulty">难度分档。</param>
/// <param name="Unlocked">对当前调用者是否已解锁。</param>
/// <param name="BestStars">当前调用者的最好星级;未通关为 <c>null</c>。</param>
/// <param name="BestDurationMs">取得最好星级时的用时;未通关为 <c>null</c>。</param>
public sealed record PuzzleLevelSummaryDto(
    int LevelIndex,
    int Difficulty,
    bool Unlocked,
    int? BestStars,
    long? BestDurationMs);

/// <summary>单个关卡的可下发内容。**不含答案**。</summary>
/// <param name="LevelIndex">关卡序号。</param>
/// <param name="Difficulty">难度分档。</param>
/// <param name="LayoutJson">关卡布局 —— 唯一会下发的关卡内容。</param>
public sealed record PuzzleLevelDto(
    int LevelIndex,
    int Difficulty,
    string LayoutJson);

/// <summary>发起尝试的结果。</summary>
/// <param name="AttemptId">尝试 id,后续 check / hint / submit 都用它。</param>
/// <param name="LevelIndex">关卡序号。</param>
/// <param name="LayoutJson">关卡布局。</param>
/// <param name="StartedAt">服务端记录的开始时间。</param>
public sealed record PuzzleAttemptStartedDto(
    Guid AttemptId,
    int LevelIndex,
    string LayoutJson,
    DateTime StartedAt);

/// <summary>部分校验的结果。</summary>
/// <param name="IsCorrect">这一部分是否正确。</param>
/// <param name="Mistakes">服务端记录的累计错误数 —— 权威值,客户端应以此为准。</param>
public sealed record PuzzleCheckResultDto(bool IsCorrect, int Mistakes);

/// <summary>一次提示的结果。</summary>
/// <param name="RevealedJson">被揭示的**单个**片段。</param>
/// <param name="HintsUsed">服务端记录的累计提示数。</param>
public sealed record PuzzleHintDto(string RevealedJson, int HintsUsed);

/// <summary>提交的结果。</summary>
/// <param name="IsCorrect">是否通关。</param>
/// <param name="Stars">通关星级;未通关为 <c>null</c>。</param>
/// <param name="DurationMs">服务端测得的用时;未通关为 <c>null</c>。</param>
/// <param name="Mistakes">服务端记录的错误数。</param>
/// <param name="HintsUsed">服务端记录的提示数。</param>
/// <param name="NewBest">本次是否刷新了该关的最好成绩。</param>
public sealed record PuzzleSubmitResultDto(
    bool IsCorrect,
    int? Stars,
    long? DurationMs,
    int Mistakes,
    int HintsUsed,
    bool NewBest);

/// <summary>某游戏的整体进度。两个字段都是查询得出的派生量,不落库。</summary>
/// <param name="GameKey">游戏键。</param>
/// <param name="UnlockedLevelIndex">已解锁到的关卡序号(= 已完成最大序号 + 1)。</param>
/// <param name="TotalStars">各关最好星级之和。</param>
/// <param name="LevelsCompleted">已通关的关卡数。</param>
public sealed record PuzzleProgressDto(
    string GameKey,
    int UnlockedLevelIndex,
    int TotalStars,
    int LevelsCompleted);
