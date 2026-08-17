using Gewu.Application.Features.Rooms.MakeMove;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;

namespace Gewu.Application.Tests.Features.Rooms;

/// <summary>
/// `MakeMoveCommand` 上的三种载荷,以及校验器对它们各自的态度。
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
}
