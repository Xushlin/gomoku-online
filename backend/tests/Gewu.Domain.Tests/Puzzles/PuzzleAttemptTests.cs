using Gewu.Domain.Puzzles;

namespace Gewu.Domain.Tests.Puzzles;

/// <summary>
/// <see cref="PuzzleAttempt"/> 是 puzzle-core 的权威单位,这些测试锁住它的核心保证:
/// 一旦结束就不可再改 —— 提交后要不到提示、重复提交刷不了分。
/// </summary>
public class PuzzleAttemptTests
{
    private static readonly DateTime Start = new(2026, 8, 13, 10, 0, 0, DateTimeKind.Utc);
    private static readonly UserId Owner = new(Guid.NewGuid());

    private static PuzzleAttempt NewAttempt()
        => PuzzleAttempt.Start(Guid.NewGuid(), Owner, puzzleLevelId: 7, Start);

    [Fact]
    public void Start_begins_unfinished_with_zeroed_counters()
    {
        var attempt = NewAttempt();

        attempt.FinishedAt.Should().BeNull();
        attempt.HintsUsed.Should().Be(0);
        attempt.Mistakes.Should().Be(0);
        attempt.Stars.Should().BeNull();
        attempt.IsCompleted.Should().BeFalse();
        attempt.Duration.Should().BeNull();
    }

    [Fact]
    public void Counters_increment_independently()
    {
        var attempt = NewAttempt();

        attempt.RecordMistake();
        attempt.RecordMistake();
        attempt.RecordHint();

        attempt.Mistakes.Should().Be(2);
        attempt.HintsUsed.Should().Be(1);
    }

    [Fact]
    public void Complete_records_stars_and_the_server_measured_duration()
    {
        var attempt = NewAttempt();
        var finish = Start.AddSeconds(90);

        attempt.Complete(3, finish);

        attempt.Stars.Should().Be(3);
        attempt.FinishedAt.Should().Be(finish);
        attempt.IsCompleted.Should().BeTrue();
        attempt.Duration.Should().Be(TimeSpan.FromSeconds(90));
    }

    [Fact]
    public void A_finished_attempt_rejects_a_second_submission()
    {
        var attempt = NewAttempt();
        attempt.Complete(2, Start.AddSeconds(30));

        var act = () => attempt.Complete(3, Start.AddSeconds(40));

        act.Should().Throw<AttemptAlreadyFinishedException>();
        attempt.Stars.Should().Be(2);
        attempt.FinishedAt.Should().Be(Start.AddSeconds(30));
    }

    [Fact]
    public void A_finished_attempt_rejects_further_hints()
    {
        var attempt = NewAttempt();
        attempt.Complete(3, Start.AddSeconds(10));

        var act = () => attempt.RecordHint();

        act.Should().Throw<AttemptAlreadyFinishedException>();
        attempt.HintsUsed.Should().Be(0);
    }

    [Fact]
    public void A_finished_attempt_rejects_further_mistakes()
    {
        var attempt = NewAttempt();
        attempt.Complete(3, Start.AddSeconds(10));

        var act = () => attempt.RecordMistake();

        act.Should().Throw<AttemptAlreadyFinishedException>();
        attempt.Mistakes.Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-1)]
    public void Complete_rejects_a_star_rating_outside_1_to_3(int stars)
    {
        var attempt = NewAttempt();

        var act = () => attempt.Complete(stars, Start.AddSeconds(10));

        act.Should().Throw<InvalidStarRatingException>();
        attempt.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public void Every_mutation_refreshes_the_concurrency_token()
    {
        var attempt = NewAttempt();
        var initial = attempt.RowVersion.ToArray();

        attempt.RecordMistake();
        attempt.RowVersion.Should().NotEqual(initial);

        var afterMistake = attempt.RowVersion.ToArray();
        attempt.RecordHint();
        attempt.RowVersion.Should().NotEqual(afterMistake);
    }
}
