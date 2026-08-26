using Gewu.Domain.Idioms;
using Gewu.Domain.Manuals;
using Gewu.Domain.Puzzles;
using Gewu.Domain.Rooms;
using Gewu.Domain.ScoreRuns;
using Gewu.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Move = Gewu.Domain.Rooms.Move;

namespace Gewu.Infrastructure.Persistence;

/// <summary>
/// 应用主 <see cref="DbContext"/>。Code-first 建模,配置通过
/// <see cref="IEntityTypeConfiguration{TEntity}"/> 分拆到同目录 <c>Configurations/</c> 文件夹。
/// </summary>
public sealed class AppDbContext : DbContext
{
    /// <summary>用户聚合根。</summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>刷新令牌子实体。</summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>
    /// 每个玩家在每个棋种上的战绩与 ELO。主键 <c>(UserId, GameKey)</c>。
    /// 评分数据的唯一真源 —— 见 <see cref="UserGameStats"/> 上关于为什么 <c>User</c> 不留镜像的说明。
    /// </summary>
    public DbSet<UserGameStats> UserGameStats => Set<UserGameStats>();

    /// <summary>房间聚合根。</summary>
    public DbSet<Room> Rooms => Set<Room>();

    /// <summary>对局子实体。</summary>
    public DbSet<Game> Games => Set<Game>();

    /// <summary>落子记录子实体。</summary>
    public DbSet<Move> Moves => Set<Move>();

    /// <summary>房间聊天消息。</summary>
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    /// <summary>房间围观者联结记录。</summary>
    public DbSet<RoomSpectator> RoomSpectators => Set<RoomSpectator>();

    /// <summary>房间座位 —— 谁坐在第几号。</summary>
    public DbSet<RoomSeat> RoomSeats => Set<RoomSeat>();

    /// <summary>成语词典(参考数据,游戏侧只读)。</summary>
    public DbSet<Idiom> Idioms => Set<Idiom>();

    /// <summary>成语的字级反查索引。</summary>
    public DbSet<IdiomChar> IdiomChars => Set<IdiomChar>();

    /// <summary>单人关卡。<c>SolutionJson</c> 永不下发客户端。</summary>
    public DbSet<PuzzleLevel> PuzzleLevels => Set<PuzzleLevel>();

    /// <summary>闯关尝试 —— puzzle-core 的权威单位。</summary>
    public DbSet<PuzzleAttempt> PuzzleAttempts => Set<PuzzleAttempt>();

    /// <summary>每人每关的最好成绩。</summary>
    public DbSet<PuzzleLevelProgress> PuzzleLevelProgress => Set<PuzzleLevelProgress>();

    /// <summary>计分类单局 —— score-attack 的权威单位。</summary>
    /// <summary>古谱线路 —— 只读资料,不是对局。见 <see cref="Gewu.Domain.Manuals.XiangqiManualLine"/>。</summary>
    /// <summary>古谱的身份(键、书名、有没有分组层)。</summary>
    public DbSet<XiangqiManual> XiangqiManuals => Set<XiangqiManual>();

    public DbSet<XiangqiManualLine> XiangqiManualLines => Set<XiangqiManualLine>();

    public DbSet<ScoreRun> ScoreRuns => Set<ScoreRun>();

    /// <inheritdoc />
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
