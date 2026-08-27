using System.Collections.Generic;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.NInARow;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;
using Gewu.Domain.ValueObjects;
using Gewu.Infrastructure.Persistence;
using Gewu.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Gewu.Infrastructure.Tests.Rooms;

/// <summary>
/// 对局设置要真的落库、真的读回来。
/// <para>
/// 一个只在内存里对的字段没有意义 —— 斗地主的发牌必须活过一次进程重启,而 EF 的映射漏配置
/// 的表现是"值悄悄变成 null":第一次出牌时规则读不到手牌,而那时离开局已经过去几十秒。
/// </para>
/// <para>
/// 也顺带钉住**它没有被 <c>.IsRequired()</c> 意外收紧**:<c>generalize-match-payload</c> 付过
/// 那个账 —— 显式配置盖过 CLR 可空性,于是类型改了、迁移干净生成了,而数据库在第一次写
/// <c>NULL</c> 时才拒绝。这里正反两种都存一遍。
/// </para>
/// </summary>
public sealed class GameSetupPersistenceTests : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

    private const string DealtKey = "dealt-probe";

    /// <summary>需要设置的探针。<c>GameKey</c> 不必在注册表里 —— 仓库不解析规则。</summary>
    private sealed class DealtRules : IDealtGameRules
    {
        public string GameKey => DealtKey;
        public int SeatCount => 2;
        public bool SupportsHumanVsHuman => true;
        public bool IsRated => false;

        public string CreateSetup(int seed) => $"deal-{seed}";

        public MoveApplication Apply(
            MatchState state, MoveIntent intent, int seat)
            => MoveApplication.Ongoing();
    }

    private SqliteConnection _connection = null!;
    private AppDbContext _db = null!;
    private RoomRepository _repo = null!;
    private RoomId _dealtId;
    private RoomId _plainId;
    private RoomId _chosenId;

    /// <summary>斗地主一副牌是 57 字符;这里用一个更长的串顺带证明没有长度上限。</summary>
    private const string Setup = "AB/CD/EF/GH-and-then-some-more-so-nobody-can-claim-a-short-column-would-do";

    private const string PositionalKey = "positional-probe";

    /// <summary>象棋残局的设置是 92 字符 —— 90 的盘面串 + 冒号 + 座位号。</summary>
    private const string ChosenSetup =
        "rnbakabnr..........c.....c.p.p.p.p.p..................P.P.P.P.P.C.....C..........RNBAKABNR:1";

    /// <summary>设置由调用方选定的探针。</summary>
    private sealed class PositionalRules : IGameRules, IPositionalStartRules
    {
        public string GameKey => PositionalKey;
        public int SeatCount => 2;
        public bool SupportsHumanVsHuman => true;
        public bool IsRated => false;

        public void ValidateSetup(string setup) { }

        public MoveApplication Apply(
            MatchState state, MoveIntent intent, int seat)
            => MoveApplication.Ongoing();
    }

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();

        var host = UserId.NewId();
        var guest = UserId.NewId();

        var dealt = Room.Create(RoomId.NewId(), "dealt", host, Now, DealtKey);
        dealt.JoinAsPlayer(guest, Now.AddSeconds(1), new DealtRules(), setup: Setup);
        _dealtId = dealt.Id;

        var plain = Room.Create(RoomId.NewId(), "plain", host, Now, GameKeys.Gomoku);
        plain.JoinAsPlayer(guest, Now.AddSeconds(1), BuiltInGameRules.Gomoku, setup: null);
        _plainId = plain.Id;

        // 选定式:设置在**建房**那一刻就定了,而对局要等第二个人。所以这一间**留在 Waiting**
        // —— 它正是「房间上的那一列」唯一能被验到的状态。
        var chosen = Room.CreateFromPosition(
            RoomId.NewId(), "chosen", host, Now, new PositionalRules(), ChosenSetup);
        _chosenId = chosen.Id;

        _db.Rooms.AddRange(dealt, plain, chosen);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        _repo = new RoomRepository(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task The_setup_comes_back_verbatim()
    {
        var room = await _repo.FindByIdAsync(_dealtId, default);

        room!.Game!.Setup.Should().Be(Setup);
    }

    [Fact]
    public async Task A_game_without_a_setup_reads_back_as_null()
    {
        var room = await _repo.FindByIdAsync(_plainId, default);

        // 若这一列被误配成 IsRequired,存的那一刻就会失败 —— 所以这条测试的价值一半在
        // InitializeAsync 里,它写进了一个 NULL。
        room!.Game!.Setup.Should().BeNull();
    }

    [Fact]
    public async Task The_column_is_nullable_in_the_actual_schema()
    {
        // 直接问库,而不是问模型 —— 模型是生成迁移的那份输入,拿它自证等于什么都没验。
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT \"notnull\" FROM pragma_table_info('Games') WHERE name = 'Setup';";
        var notNull = await cmd.ExecuteScalarAsync();

        notNull.Should().NotBeNull("Games 表必须真的有 Setup 这一列");
        System.Convert.ToInt32(notNull).Should().Be(0, "它必须可空 —— 四个现有棋种没有设置");
    }

    /// <summary>
    /// 建房时选定的设置要活过一次进程重启。
    /// <para>
    /// 漏掉这一列的表现**不是**报错:房间照常建出来、照常出现在大厅,第二个人进来那一刻
    /// 才抛「说要设置却没给」—— 而那时创建者早已拿到 201 并等在房里。
    /// </para>
    /// </summary>
    [Fact]
    public async Task The_chosen_setup_survives_a_round_trip()
    {
        var room = await _repo.FindByIdAsync(_chosenId, default);

        room!.ChosenSetup.Should().Be(ChosenSetup);
        room.Status.Should().Be(RoomStatus.Waiting, "它还没坐满 —— 这一列存的就是「还没开局」那段时间");
        room.Game.Should().BeNull();
    }

    /// <summary>而不是选定式的房间读回来是 <c>null</c> —— 否则上一条在「处处非空」上恒真。</summary>
    [Fact]
    public async Task A_room_that_chose_nothing_reads_back_as_null()
    {
        var room = await _repo.FindByIdAsync(_plainId, default);

        room!.ChosenSetup.Should().BeNull();
    }
}
