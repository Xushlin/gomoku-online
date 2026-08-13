using Gewu.Domain.Puzzles;
using Gewu.Domain.Users;

namespace Gewu.Application.Abstractions;

/// <summary>
/// puzzle-core 限界上下文的持久化口。
/// <para>
/// 这里用**一个**仓储覆盖三个小聚合(关卡 / 尝试 / 每关最好成绩),而不是按项目惯例
/// 一聚合一仓储。理由:三者构成一个内聚的上下文,且提交流程要在同一事务里同时改
/// 尝试与最好成绩 —— 拆成三个接口只会让那条事务边界被切开、更难看懂。
/// </para>
/// <para>
/// 进度相关的两个读操作是**聚合查询**(<c>MAX</c> / <c>SUM</c>),刻意不落库为计数器
/// —— 见 <c>PuzzleLevelProgress</c> 的说明。
/// </para>
/// </summary>
public interface IPuzzleRepository
{
    /// <summary>取某游戏的全部关卡,按 <c>LevelIndex</c> 升序。</summary>
    /// <param name="gameKey">游戏键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<IReadOnlyList<PuzzleLevel>> ListLevelsAsync(
        string gameKey, CancellationToken cancellationToken = default);

    /// <summary>按游戏与序号取一个关卡;不存在则 <c>null</c>。</summary>
    /// <param name="gameKey">游戏键。</param>
    /// <param name="levelIndex">关卡序号。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<PuzzleLevel?> FindLevelAsync(
        string gameKey, int levelIndex, CancellationToken cancellationToken = default);

    /// <summary>按主键取关卡;不存在则 <c>null</c>。</summary>
    /// <param name="puzzleLevelId">关卡主键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<PuzzleLevel?> FindLevelByIdAsync(
        int puzzleLevelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 按 <c>(尝试 id, 所有者)</c> 取尝试 —— 所有权是查询条件的一部分,
    /// 所以"别人的尝试"和"不存在的尝试"对调用方是同一个结果。
    /// </summary>
    /// <param name="attemptId">尝试 id。</param>
    /// <param name="userId">调用者。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<PuzzleAttempt?> FindAttemptAsync(
        Guid attemptId, UserId userId, CancellationToken cancellationToken = default);

    /// <summary>新增一次尝试。</summary>
    /// <param name="attempt">尝试。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task AddAttemptAsync(PuzzleAttempt attempt, CancellationToken cancellationToken = default);

    /// <summary>取某用户在某关的最好成绩;没有则 <c>null</c>。</summary>
    /// <param name="userId">用户。</param>
    /// <param name="puzzleLevelId">关卡。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<PuzzleLevelProgress?> FindLevelProgressAsync(
        UserId userId, int puzzleLevelId, CancellationToken cancellationToken = default);

    /// <summary>新增一条最好成绩。</summary>
    /// <param name="progress">最好成绩。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task AddLevelProgressAsync(
        PuzzleLevelProgress progress, CancellationToken cancellationToken = default);

    /// <summary>取某用户在某游戏各关的最好成绩,键为 <c>LevelIndex</c>。</summary>
    /// <param name="userId">用户。</param>
    /// <param name="gameKey">游戏键。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<IReadOnlyDictionary<int, PuzzleLevelProgress>> ListLevelProgressAsync(
        UserId userId, string gameKey, CancellationToken cancellationToken = default);
}
