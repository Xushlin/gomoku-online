using Gewu.Domain.Games.NInARow;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;
using Gewu.Infrastructure.Persistence;
using Gewu.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Gewu.Infrastructure.Tests.Rooms;

/// <summary>
/// 每一条取房间的路径都必须把座位一起取回来。
/// <para>
/// **这个文件是变异测试逼出来的。** 我把 <c>RoomRepository</c> 里五处 <c>.Include("_seats")</c>
/// 全删掉之后,整个 Infrastructure 测试套件**还是绿的** —— 而那种状态下,任何一次加载房间再读
/// <c>BlackPlayerId</c> 都会抛 <c>Single()</c>:座位集合是空的。也就是说,那时没有任何测试
/// 加载过一个房间再读它的座位。
/// </para>
/// <para>
/// 座位是聚合的一部分,不是可选的附加数据。漏一处 <c>Include</c> 的表现不是"少个字段",
/// 而是那条路径整个炸掉 —— 响得很,但要等到它被走到那天才响。
/// </para>
/// </summary>
public sealed class RoomRepositorySeatsTests : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

    private SqliteConnection _connection = null!;
    private AppDbContext _db = null!;
    private RoomRepository _repo = null!;
    private UserId _host;
    private UserId _guest;
    private RoomId _playingId;
    private RoomId _waitingId;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();

        _host = UserId.NewId();
        _guest = UserId.NewId();

        var playing = Room.Create(RoomId.NewId(), "playing", _host, Now, GameKeys.Gomoku);
        playing.JoinAsPlayer(_guest, Now.AddSeconds(1), BuiltInGameRules.Gomoku, setup: null);
        _playingId = playing.Id;

        var waiting = Room.Create(RoomId.NewId(), "waiting", _host, Now, GameKeys.Gomoku);
        _waitingId = waiting.Id;

        _db.Rooms.AddRange(playing, waiting);
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
    public async Task FindById_brings_the_seats_back()
    {
        var room = await _repo.FindByIdAsync(_playingId, CancellationToken.None);

        room.Should().NotBeNull();
        // 没有 Include 的话下面这一行抛 InvalidOperationException,而不是返回错的值。
        room!.BlackPlayerId.Should().Be(_host);
        room.WhitePlayerId.Should().Be(_guest);
        room.Seats.Select(s => s.Index).Should().Equal(0, 1);
    }

    [Fact]
    public async Task A_waiting_room_comes_back_with_just_seat_zero()
    {
        var room = await _repo.FindByIdAsync(_waitingId, CancellationToken.None);

        room!.Seats.Should().HaveCount(1);
        room.BlackPlayerId.Should().Be(_host);
        room.WhitePlayerId.Should().BeNull();
    }

    [Fact]
    public async Task Rooms_a_user_plays_in_are_found_by_seat()
    {
        // 这条过滤此前写的是 `BlackPlayerId == userId || WhitePlayerId == userId`,
        // 现在走 RoomSeats 表。它 MUST NOT 退化成客户端求值 —— 用真 SQLite 才看得出来。
        var mine = await _repo.GetActiveRoomsByUserAsync(_guest, CancellationToken.None);

        mine.Select(r => r.Id).Should().Contain(_playingId);
    }

    [Fact]
    public async Task Every_room_returned_by_the_lobby_can_read_its_own_seats()
    {
        var lobby = await _repo.GetActiveRoomsAsync(GameKeys.Gomoku, CancellationToken.None);

        lobby.Should().NotBeEmpty();
        foreach (var room in lobby)
        {
            // 大厅那条路径也要带座位 —— 它渲染的正是"谁在等人"。
            room.Seats.Should().NotBeEmpty($"room '{room.Name}' came back without its seats");
        }
    }
}
