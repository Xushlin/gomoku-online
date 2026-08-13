using Gewu.Domain.Puzzles;

namespace Gewu.Domain.Tests.Puzzles;

/// <summary>
/// 最好成绩"只升不降"是排行榜能稳定的前提,也是所有关卡制游戏的既有行为。
/// 这里覆盖 更好 / 更差 / 同星更快 / 同星更慢 四种组合。
/// </summary>
public class PuzzleLevelProgressTests
{
    private static readonly UserId Owner = new(Guid.NewGuid());

    private static PuzzleLevelProgress FirstRun(int stars = 2, long durationMs = 90_000)
        => PuzzleLevelProgress.First(Owner, puzzleLevelId: 3, stars, durationMs);

    [Fact]
    public void First_records_the_run_as_the_best()
    {
        var progress = FirstRun(2, 90_000);

        progress.BestStars.Should().Be(2);
        progress.BestDurationMs.Should().Be(90_000);
        progress.AttemptCount.Should().Be(1);
    }

    [Fact]
    public void More_stars_replaces_the_best()
    {
        var progress = FirstRun(2, 90_000);

        var newBest = progress.RecordCompletion(3, 120_000);

        newBest.Should().BeTrue();
        progress.BestStars.Should().Be(3);
        // 星级更高就更新用时,即使这次更慢 —— 成绩以星级为先。
        progress.BestDurationMs.Should().Be(120_000);
    }

    [Fact]
    public void Fewer_stars_never_lowers_the_best()
    {
        var progress = FirstRun(3, 60_000);

        var newBest = progress.RecordCompletion(1, 200_000);

        newBest.Should().BeFalse();
        progress.BestStars.Should().Be(3);
        progress.BestDurationMs.Should().Be(60_000);
    }

    [Fact]
    public void Same_stars_and_faster_replaces_the_best()
    {
        var progress = FirstRun(2, 90_000);

        var newBest = progress.RecordCompletion(2, 50_000);

        newBest.Should().BeTrue();
        progress.BestDurationMs.Should().Be(50_000);
    }

    [Fact]
    public void Same_stars_and_slower_keeps_the_best()
    {
        var progress = FirstRun(2, 90_000);

        var newBest = progress.RecordCompletion(2, 95_000);

        newBest.Should().BeFalse();
        progress.BestDurationMs.Should().Be(90_000);
    }

    [Fact]
    public void AttemptCount_increments_on_every_completion_regardless_of_score()
    {
        var progress = FirstRun(3, 60_000);

        progress.RecordCompletion(1, 300_000);
        progress.RecordCompletion(1, 400_000);
        progress.RecordCompletion(3, 10_000);

        // 统计量,不是成绩 —— 变差的重玩也算一次。
        progress.AttemptCount.Should().Be(4);
        progress.BestStars.Should().Be(3);
        progress.BestDurationMs.Should().Be(10_000);
    }
}
