using Gewu.Domain.Enums;
using Gewu.Domain.Games.NInARow;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;
using Gewu.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Gewu.Infrastructure.Tests.Rooms;

/// <summary>
/// 大厅按棋种过滤,打真 SQLite。
/// <para>
/// 必须在这一层测:过滤是一条 EF 谓词,而 Application 层的仓库 mock 无论传什么键都会
/// 返回同一批房间 —— 那里能证明的只有"handler 把键传下去了"。**谓词本身有没有生效**
/// 只有真 SQL 说得清。
/// </para>
/// <para>
/// 用 in-memory SQLite 而不是 EF 的 InMemory provider,与 <c>IdiomRepositoryTests</c> 同理:
/// InMemory provider 会用 LINQ-to-Objects 假装成功,一个写错的 <c>Where</c> 照样过。
/// </para>
/// </summary>
public sealed class RoomRepositoryGameKeyTests : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc);

    private SqliteConnection _connection = null!;
    private AppDbContext _db = null!;
    private RoomRepository _repo = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options);
        await _db.Database.EnsureCreatedAsync();

        var alice = UserId.NewId();
        var bob = UserId.NewId();

        // 两个五子棋房间(一个在等人、一个在下),三个一字棋房间,
        // 外加一个已结束的五子棋房间 —— 后者永远不该出现在大厅里。
        _db.Rooms.AddRange(
            Waiting("gomoku waiting", alice, GameKeys.Gomoku),
            Playing("gomoku playing", alice, bob, GameKeys.Gomoku),
            Waiting("ttt one", alice, GameKeys.TicTacToe),
            Waiting("ttt two", bob, GameKeys.TicTacToe),
            Playing("ttt three", alice, bob, GameKeys.TicTacToe),
            Finished("gomoku finished", alice, bob, GameKeys.Gomoku));
        await _db.SaveChangesAsync();

        _repo = new RoomRepository(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static Room Waiting(string name, UserId host, string gameKey)
        => Room.Create(RoomId.NewId(), name, host, Now, gameKey);

    private static Room Playing(string name, UserId host, UserId guest, string gameKey)
    {
        var room = Waiting(name, host, gameKey);
        room.JoinAsPlayer(guest, Now, BuiltInGameRules.Gomoku, setup: null);
        return room;
    }

    private static Room Finished(string name, UserId host, UserId guest, string gameKey)
    {
        var room = Playing(name, host, guest, gameKey);
        room.Resign(host, Now);
        return room;
    }

    [Fact]
    public async Task Returns_only_the_requested_game()
    {
        var gomoku = await _repo.GetActiveRoomsAsync(GameKeys.Gomoku, default);
        var ttt = await _repo.GetActiveRoomsAsync(GameKeys.TicTacToe, default);

        gomoku.Should().HaveCount(2);
        gomoku.Should().OnlyContain(r => r.GameKey == GameKeys.Gomoku);

        ttt.Should().HaveCount(3);
        ttt.Should().OnlyContain(r => r.GameKey == GameKeys.TicTacToe);
    }

    [Fact]
    public async Task Finished_rooms_stay_out_of_every_game_s_lobby()
    {
        // 加了棋种谓词不该把状态谓词挤掉 —— 两个条件是 AND,不是替换。
        var gomoku = await _repo.GetActiveRoomsAsync(GameKeys.Gomoku, default);

        gomoku.Should().NotContain(r => r.Status == RoomStatus.Finished);
        gomoku.Select(r => r.Name).Should().NotContain("gomoku finished");
    }

    [Fact]
    public async Task An_unregistered_game_key_yields_an_empty_list()
    {
        // 未登记的棋种在这一层不是错误 —— 库里就是没有这种房间,如实回答"没有"。
        var rooms = await _repo.GetActiveRoomsAsync("xiangqi", default);

        rooms.Should().BeEmpty();
    }

    [Fact]
    public async Task The_key_comparison_is_case_sensitive()
    {
        // 与注册表的大小写敏感一致:宁可查不到,也不要 "Gomoku" 和 "gomoku" 都能用。
        var rooms = await _repo.GetActiveRoomsAsync("Gomoku", default);

        rooms.Should().BeEmpty();
    }

    [Fact]
    public async Task Playing_rooms_still_arrive_with_their_game_and_moves()
    {
        // 谓词加在 Include 之后,写错位置会把子实体丢掉,而大厅要显示手数。
        var ttt = await _repo.GetActiveRoomsAsync(GameKeys.TicTacToe, default);

        var playing = ttt.Single(r => r.Status == RoomStatus.Playing);
        playing.Game.Should().NotBeNull();
        playing.Game!.Moves.Should().NotBeNull();
    }
}
