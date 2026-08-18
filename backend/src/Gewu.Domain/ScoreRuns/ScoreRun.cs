using Gewu.Domain.Exceptions;
using Gewu.Domain.Users;

namespace Gewu.Domain.ScoreRuns;

/// <summary>
/// 一次计分类的**单局**(run)—— 本类别的权威单位。
/// <para>
/// 规则与 <see cref="Gewu.Domain.Puzzles.PuzzleAttempt"/> **逐条对齐**,因为要防的是同一批事:
/// 已结束的 run 再提交被拒(客户端因此无法重复提交刷分)、起止时刻取服务端时钟、
/// 以及"只能由所有者操作"—— 后者由仓储把 <see cref="UserId"/> 放进查询条件来实现,
/// 于是"别人的 run"与"不存在的 run"对调用方是同一个结果(404)。
/// </para>
/// <para>
/// 命名是通用的(<c>ScoreRun</c> 而不是 <c>TetrisRun</c>),因为它是**数据**而不是接缝:
/// 数据形状错了改一列,接缝形状错了改每个实现。这也是本变更刻意不造
/// <c>IScoreAttackRules</c> 注册表的同一条理由 —— 计分类只有一款游戏在规划里,
/// 而在只有一个实现时猜通用形状,是这个仓库已经付过账的赌注(<c>generalize-puzzle-rules</c>)。
/// </para>
/// </summary>
public sealed class ScoreRun
{
    /// <summary>主键。</summary>
    public Guid Id { get; private set; }

    /// <summary>发起者。</summary>
    public UserId UserId { get; private set; }

    /// <summary>游戏键。</summary>
    public string GameKey { get; private set; } = string.Empty;

    /// <summary>
    /// 方块序列的种子 —— 由**服务端**在开局时生成并下发。
    /// <para>
    /// 客户端不能选择它,否则它可以挑一个对自己有利的序列,而重放会照样通过(那些放置确实合法)。
    /// 它落库,所以重放在任何时候都能重现同一串方块。
    /// </para>
    /// </summary>
    public int Seed { get; private set; }

    /// <summary>开始时刻(UTC,服务端时钟)。</summary>
    public DateTime StartedAt { get; private set; }

    /// <summary>结束时刻(UTC);进行中为 <c>null</c>。</summary>
    public DateTime? FinishedAt { get; private set; }

    /// <summary>
    /// 服务端重放算出的得分;进行中为 <c>null</c>。
    /// <para>
    /// 用 <c>null</c> 而不是 <c>0</c> 表示"还没结算"是刻意的:**0 是一个合法的分数**
    /// (一行没消的一局),而用一个合法值表示"不适用"是这个仓库在
    /// <c>generalize-match-payload</c> 里明令禁止的形状。
    /// </para>
    /// </summary>
    public int? Score { get; private set; }

    /// <summary>服务端重放算出的累计消行数;进行中为 <c>null</c>。</summary>
    public int? Lines { get; private set; }

    /// <summary>服务端重放算出的结束等级;进行中为 <c>null</c>。</summary>
    public int? Level { get; private set; }

    /// <summary>是否已结算。</summary>
    public bool IsFinished => FinishedAt is not null;

    /// <summary>服务端测得的用时;未结束时为 <c>null</c>。</summary>
    public TimeSpan? Duration => FinishedAt is null ? null : FinishedAt.Value - StartedAt;

    /// <summary>
    /// 乐观并发令牌。SQLite 无原生 rowversion,由 Domain 在状态变更后自行刷新
    /// —— 与 <c>PuzzleAttempt</c> / <c>Game</c> / <c>User</c> 同一纪律。
    /// </summary>
    public byte[] RowVersion { get; private set; } = Guid.NewGuid().ToByteArray();

    // EF 物化用。
    private ScoreRun() { }

    /// <summary>开一局。</summary>
    /// <param name="id">主键。</param>
    /// <param name="userId">发起者。</param>
    /// <param name="gameKey">游戏键。</param>
    /// <param name="seed">服务端生成的种子。</param>
    /// <param name="startedAt">开始时刻(服务端时钟)。</param>
    /// <exception cref="ArgumentException"><paramref name="gameKey"/> 为空。</exception>
    public static ScoreRun Start(Guid id, UserId userId, string gameKey, int seed, DateTime startedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameKey);

        return new ScoreRun
        {
            Id = id,
            UserId = userId,
            GameKey = gameKey,
            Seed = seed,
            StartedAt = startedAt,
            FinishedAt = null,
            Score = null,
            Lines = null,
            Level = null,
            RowVersion = Guid.NewGuid().ToByteArray(),
        };
    }

    /// <summary>
    /// 结算一局。三个数字**只能**来自服务端对放置序列的重放 —— 客户端上报的分数进不到这里,
    /// 因为命令里没有承载它的字段。
    /// </summary>
    /// <param name="score">得分。</param>
    /// <param name="lines">累计消行数。</param>
    /// <param name="level">结束时的等级。</param>
    /// <param name="finishedAt">结束时刻(服务端时钟)。</param>
    /// <exception cref="ScoreRunAlreadyFinishedException">已结算 —— 重复提交被拒。</exception>
    /// <exception cref="ArgumentOutOfRangeException">分数或消行为负,或等级小于 1。</exception>
    public void Finish(int score, int lines, int level, DateTime finishedAt)
    {
        if (FinishedAt is not null)
        {
            throw new ScoreRunAlreadyFinishedException(
                $"Run {Id} was already scored at {FinishedAt:o}.");
        }

        // 纯编程错误的护栏,不是领域规则:唯一的生产者是 TetrisRules.Replay,它算不出负分。
        // 所以这里用 ArgumentOutOfRangeException 而不是一个新的领域异常 —— 后者会让
        // "客户端做错了什么"和"我们的重放坏了"在错误码上长得一样。
        ArgumentOutOfRangeException.ThrowIfNegative(score);
        ArgumentOutOfRangeException.ThrowIfNegative(lines);
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);

        Score = score;
        Lines = lines;
        Level = level;
        FinishedAt = finishedAt;
        RowVersion = Guid.NewGuid().ToByteArray();
    }
}
