using FluentAssertions;
using Gewu.Application.Common.DTOs;
using Gewu.Application.Features.ScoreRuns.GetScoreLeaderboard;
using Gewu.Application.Features.ScoreRuns.StartScoreRun;
using Gewu.Application.Features.ScoreRuns.SubmitScoreRun;
using Gewu.Domain.Games.Tetris;
using Gewu.Domain.Users;

namespace Gewu.Application.Tests.Features.ScoreRuns;

/// <summary>计分类三个入口的入参校验。</summary>
public sealed class ScoreRunValidatorTests
{
    private static readonly UserId Who = UserId.NewId();

    // ---- 开局 ----

    [Fact]
    public void An_empty_game_key_is_a_400()
    {
        new StartScoreRunCommandValidator()
            .Validate(new StartScoreRunCommand(Who, "")).IsValid.Should().BeFalse();
    }

    [Fact]
    public void An_unknown_game_key_is_not_a_400()
    {
        // 「这个游戏不存在」是 404,由 handler 给;validator 的产出是 400,两件事。
        new StartScoreRunCommandValidator()
            .Validate(new StartScoreRunCommand(Who, "gomoku")).IsValid.Should().BeTrue();
    }

    // ---- 提交 ----

    private static SubmitScoreRunCommand Submit(params ScorePlacementDto[] ps)
        => new(Who, Guid.NewGuid(), ps);

    [Fact]
    public void A_valid_placement_list_passes()
    {
        new SubmitScoreRunCommandValidator()
            .Validate(Submit(new ScorePlacementDto(0, 0), new ScorePlacementDto(3, 9)))
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void An_empty_placement_list_is_refused()
    {
        new SubmitScoreRunCommandValidator().Validate(Submit()).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(4, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 10)]
    public void An_impossible_rotation_or_column_is_refused(int rotation, int column)
    {
        new SubmitScoreRunCommandValidator()
            .Validate(Submit(new ScorePlacementDto(rotation, column)))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void The_column_bound_follows_the_field_width()
    {
        // 9 合法、10 不合法 —— 而 10 正是 TetrisRules.Columns。这条会在场地变宽时一起变,
        // 而不是留一个硬编码的 9 在两处各说各的。
        TetrisRules.Columns.Should().Be(10);
        new SubmitScoreRunCommandValidator()
            .Validate(Submit(new ScorePlacementDto(0, TetrisRules.Columns - 1)))
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void An_absurdly_long_placement_list_is_refused()
    {
        // 这不是分数上限(分数刻意不设上限),是资源限制:请求体与重放都是 O(n)。
        var tooMany = Enumerable
            .Repeat(new ScorePlacementDto(0, 0), SubmitScoreRunCommandValidator.MaxPlacements + 1)
            .ToArray();

        new SubmitScoreRunCommandValidator().Validate(Submit(tooMany)).IsValid.Should().BeFalse();
    }

    // ---- 榜 ----

    private static GetScoreLeaderboardQuery Board(
        ScoreWindow window = ScoreWindow.Week, int page = 1, int pageSize = 20)
        => new(TetrisRules.GameKey, window, page, pageSize);

    [Fact]
    public void A_window_outside_the_enum_is_refused()
    {
        // 注意这条**不是**为了拦住 ?window=99 —— 实测查询串的枚举绑定自己就会拒(400 来自
        // 模型绑定器)。它护的是「查询」这个对象:GetScoreLeaderboardQuery 可以由任何调用方
        // 构造,而那些路径上没有模型绑定器。真正的防线是 StartOf 现在会抛。
        new GetScoreLeaderboardQueryValidator()
            .Validate(Board(window: (ScoreWindow)99)).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(ScoreWindow.Week)]
    [InlineData(ScoreWindow.Month)]
    [InlineData(ScoreWindow.All)]
    public void The_three_real_windows_pass(ScoreWindow window)
    {
        new GetScoreLeaderboardQueryValidator().Validate(Board(window)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Bad_paging_is_refused(int page, int pageSize)
    {
        new GetScoreLeaderboardQueryValidator()
            .Validate(Board(page: page, pageSize: pageSize)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void An_unregistered_game_key_gets_an_empty_board_not_a_400()
    {
        // 集合端点上「这个游戏没有榜」与「榜是空的」对调用方无从分辨,而 400 会把前者
        // 说成客户端错了 —— 与 ELO 榜同一处理。
        new GetScoreLeaderboardQueryValidator()
            .Validate(new GetScoreLeaderboardQuery("not-a-game", ScoreWindow.Week, 1, 20))
            .IsValid.Should().BeTrue();
    }
}
