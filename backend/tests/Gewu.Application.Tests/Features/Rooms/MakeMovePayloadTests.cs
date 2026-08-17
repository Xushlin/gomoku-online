using Gewu.Application.Features.Rooms.MakeMove;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;

namespace Gewu.Application.Tests.Features.Rooms;

/// <summary>
/// `MakeMoveCommand` 上的三种载荷:校验器对它们各自的态度,以及 handler 为它们各自
/// 挑哪个 <c>MoveIntent</c> 工厂。
/// <para>
/// 这是"位置或文本"在本仓库的第三处编码(值对象、持久化实体、命令)。第三处存在是
/// **有取舍的选择**:让命令直接带 `MoveIntent` 会把编码降到一处,但那会把负坐标的拒绝
/// 从校验器(400 + 点名字段)挪到命令还不存在的时候抛出,而那条错误路径被两份 spec 钉着。
/// 所以这里的用例除了正向,还专门钉住"负坐标仍然是校验失败"。
/// </para>
/// </summary>
public class MakeMovePayloadTests
{
    private static readonly MakeMoveCommandValidator Validator = new();

    private static MakeMoveCommand Placement(int row, int col)
        => new(UserId.NewId(), RoomId.NewId(), row, col);

    [Fact]
    public void A_placement_passes()
    {
        Validator.Validate(Placement(7, 7)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_slide_passes()
    {
        var command = new MakeMoveCommand(UserId.NewId(), RoomId.NewId(), 5, 0, 6, 0);

        Validator.Validate(command).IsValid.Should().BeTrue();
    }

    [Fact]
    public void A_spoken_move_passes_with_no_coordinates_at_all()
    {
        var command = new MakeMoveCommand(UserId.NewId(), RoomId.NewId(), Text: "一心一意");

        Validator.Validate(command).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void A_negative_coordinate_still_fails_validation(int row, int col)
    {
        // 这条错误路径与本变更之前完全一致 —— 400 + 点名字段,不是别的什么。
        var result = Validator.Validate(Placement(row, col));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_word_fails_validation(string word)
    {
        var command = new MakeMoveCommand(UserId.NewId(), RoomId.NewId(), Text: word);

        Validator.Validate(command).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_command_with_no_payload_at_all_passes_the_validator_and_is_stopped_downstream()
    {
        // 校验器**不**判"恰好一种载荷" —— 那条不变量只有一个家,就是 MoveIntent 的构造器。
        // 在这里再实现一遍,就是第四处编码。这条用例钉住的是这个分工:校验器放行,
        // handler 组装时当场抛。
        var command = new MakeMoveCommand(UserId.NewId(), RoomId.NewId());

        Validator.Validate(command).IsValid.Should().BeTrue();
    }

    // ── handler:三种载荷各选对工厂 ────────────────────────────────────────────
    //
    // 上面全是校验器。校验器放不放行与 handler 挑哪个工厂是两件事,而 spec 的三条
    // Scenario 钉的是后者。这一组补上 —— 之前只有一次真实连接跑通证明过它,
    // 那是端到端证据,不是这一层的。

    private readonly Mock<IRoomRepository> _rooms = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IRoomNotifier> _notifier = new();

    /// <summary>建一间进行中的房,把 handler 需要的依赖都摆好。</summary>
    private (MakeMoveCommandHandler Handler, Room Room, User Host) Playing(string gameKey)
    {
        var host = RoomsFixtures.NewUser("Alice");
        var bob = RoomsFixtures.NewUser("Bob", "bob@example.com");
        var room = RoomsFixtures.PlayingRoom(host, bob, gameKey: gameKey);
        RoomsFixtures.SetupClock(_clock, RoomsFixtures.Now.AddMinutes(1));
        RoomsFixtures.SetupUserLookup(_users, host, bob);
        RoomsFixtures.SetupGameStats(_users);
        _rooms.Setup(r => r.FindByIdAsync(room.Id, It.IsAny<CancellationToken>())).ReturnsAsync(room);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new MakeMoveCommandHandler(
            _rooms.Object, GomokuRules.Registry, _users.Object, _clock.Object,
            _uow.Object, _notifier.Object, RoomsFixtures.TestGameOptions());
        return (handler, room, host);
    }

    [Fact]
    public async Task Text_picks_Say_and_the_stored_move_has_no_coordinates()
    {
        var (handler, room, host) = Playing(GameKeys.IdiomChain);

        var move = await handler.Handle(
            new MakeMoveCommand(host.Id, room.Id, Text: "一心一意"), default);

        move.Text.Should().Be("一心一意");
        move.Row.Should().BeNull();
        move.Col.Should().BeNull();
        move.FromRow.Should().BeNull();
        move.FromCol.Should().BeNull();
    }

    [Fact]
    public async Task Row_and_Col_alone_pick_Place()
    {
        var (handler, room, host) = Playing(GameKeys.Gomoku);

        var move = await handler.Handle(new MakeMoveCommand(host.Id, room.Id, 7, 7), default);

        move.Row.Should().Be(7);
        move.Col.Should().Be(7);
        move.FromRow.Should().BeNull("a placement has no origin");
        move.Text.Should().BeNull();
    }

    [Fact]
    public async Task A_from_square_picks_Slide()
    {
        // 象棋红兵 (6,0) → (5,0)。选对了工厂才可能合法 —— Place 到 (5,0) 在象棋里
        // 不是一步棋,规则会拒,所以"没抛"本身就是工厂选对了的证据。
        var (handler, room, host) = Playing(GameKeys.Xiangqi);

        var move = await handler.Handle(
            new MakeMoveCommand(host.Id, room.Id, 5, 0, 6, 0), default);

        move.FromRow.Should().Be(6);
        move.FromCol.Should().Be(0);
        move.Row.Should().Be(5);
        move.Col.Should().Be(0);
        move.Text.Should().BeNull();
    }

    [Fact]
    public async Task No_payload_at_all_throws_where_the_invariant_lives()
    {
        // 校验器放行的那条命令,到 handler 组装 MoveIntent 时当场抛 —— 上面
        // A_command_with_no_payload… 断的是前半句,这条断的是后半句。
        var (handler, room, host) = Playing(GameKeys.Gomoku);

        var act = () => handler.Handle(new MakeMoveCommand(host.Id, room.Id), default);

        await act.Should().ThrowAsync<InvalidMoveException>();
    }

    [Fact]
    public async Task A_half_given_coordinate_is_not_quietly_completed()
    {
        // Row 有、Col 没有。补一个 0 会变成一步合法的棋 —— 落在 (7,0),玩家没点过的地方。
        var (handler, room, host) = Playing(GameKeys.Gomoku);

        var act = () => handler.Handle(new MakeMoveCommand(host.Id, room.Id, Row: 7), default);

        await act.Should().ThrowAsync<InvalidMoveException>();
        room.Game!.Moves.Should().BeEmpty();
    }
}
