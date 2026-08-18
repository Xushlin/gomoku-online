using FluentAssertions;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Games.Tetris;
using Gewu.Domain.ScoreRuns;
using Gewu.Domain.Users;

namespace Gewu.Domain.Tests.ScoreRuns;

/// <summary>
/// <see cref="ScoreRun"/> 的领域规则。与 <c>PuzzleAttempt</c> 的那批用例逐条对应,
/// 因为两者防的是同一批事:不可复用、时刻取服务端、结果只能来自服务端。
/// </summary>
public sealed class ScoreRunTests
{
    private static readonly DateTime Start = new(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc);
    private static readonly UserId Owner = UserId.NewId();

    private static ScoreRun NewRun(int seed = 42)
        => ScoreRun.Start(Guid.NewGuid(), Owner, TetrisRules.GameKey, seed, Start);

    [Fact]
    public void A_new_run_is_unfinished_and_has_no_score()
    {
        var run = NewRun();

        run.IsFinished.Should().BeFalse();
        run.FinishedAt.Should().BeNull();
        run.Duration.Should().BeNull();
        // 0 是一个合法的分数(一行没消),所以"还没结算"必须是 null 而不是 0。
        run.Score.Should().BeNull();
        run.Lines.Should().BeNull();
        run.Level.Should().BeNull();
    }

    [Fact]
    public void Finishing_records_the_replayed_numbers_and_the_server_clock()
    {
        var run = NewRun();
        var end = Start.AddMinutes(7);

        run.Finish(score: 1200, lines: 11, level: 2, finishedAt: end);

        run.IsFinished.Should().BeTrue();
        run.Score.Should().Be(1200);
        run.Lines.Should().Be(11);
        run.Level.Should().Be(2);
        run.FinishedAt.Should().Be(end);
        run.Duration.Should().Be(TimeSpan.FromMinutes(7));
    }

    [Fact]
    public void A_finished_run_cannot_be_submitted_again()
    {
        var run = NewRun();
        run.Finish(500, 5, 1, Start.AddMinutes(3));

        var second = () => run.Finish(999_999, 400, 41, Start.AddMinutes(4));

        second.Should().Throw<ScoreRunAlreadyFinishedException>();
    }

    [Fact]
    public void A_rejected_second_submission_leaves_the_first_result_intact()
    {
        var run = NewRun();
        var firstEnd = Start.AddMinutes(3);
        run.Finish(500, 5, 1, firstEnd);

        try { run.Finish(999_999, 400, 41, Start.AddMinutes(4)); } catch (ScoreRunAlreadyFinishedException) { }

        // 断言"没被改掉"而不只是"抛了" —— 一个先写后校验的实现照样会抛,却已经把分数换了。
        run.Score.Should().Be(500);
        run.Lines.Should().Be(5);
        run.Level.Should().Be(1);
        run.FinishedAt.Should().Be(firstEnd);
    }

    [Fact]
    public void The_seed_is_kept_so_the_sequence_can_be_reproduced_later()
    {
        var run = NewRun(seed: 20260818);

        run.Seed.Should().Be(20260818);
    }

    [Fact]
    public void Finishing_refreshes_the_concurrency_token()
    {
        var run = NewRun();
        var before = run.RowVersion;

        run.Finish(100, 1, 1, Start.AddMinutes(1));

        run.RowVersion.Should().NotEqual(before);
    }

    [Theory]
    [InlineData(-1, 0, 1)]
    [InlineData(0, -1, 1)]
    [InlineData(0, 0, 0)]
    public void Impossible_results_are_refused(int score, int lines, int level)
    {
        var run = NewRun();

        var act = () => run.Finish(score, lines, level, Start.AddMinutes(1));

        act.Should().Throw<ArgumentOutOfRangeException>();
        run.IsFinished.Should().BeFalse();
    }

    [Fact]
    public void A_run_must_name_a_game()
    {
        var act = () => ScoreRun.Start(Guid.NewGuid(), Owner, "  ", 1, Start);

        act.Should().Throw<ArgumentException>();
    }
}
