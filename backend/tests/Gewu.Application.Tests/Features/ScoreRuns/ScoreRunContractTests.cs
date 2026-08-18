using System.Reflection;
using FluentAssertions;
using Gewu.Application.Features.ScoreRuns;
using Gewu.Application.Features.ScoreRuns.StartScoreRun;
using Gewu.Application.Features.ScoreRuns.SubmitScoreRun;
using Gewu.Domain.Games.Tetris;

namespace Gewu.Application.Tests.Features.ScoreRuns;

/// <summary>
/// 计分类的两条**结构性**断言:客户端报的数字无处可放,以及这里没有注册表。
/// <para>
/// 两条都不是行为断言,而这正是它们的价值:一条"handler 忽略了 score 字段"的行为测试,
/// 只能证明今天的 handler 忽略了它;而一个**根本不存在的字段**没有明天。
/// </para>
/// </summary>
public sealed class ScoreRunContractTests
{
    private static readonly string[] SelfReportedNames =
        ["score", "lines", "level", "duration", "elapsed", "seed", "finishedat", "startedat"];

    [Fact]
    public void The_submit_command_has_nowhere_to_put_a_client_reported_number()
    {
        var members = typeof(SubmitScoreRunCommand)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name.ToLowerInvariant())
            .ToList();

        members.Should().NotBeEmpty();
        members.Should().NotIntersectWith(SelfReportedNames,
            "分数 / 消行 / 等级 / 用时全是服务端事实 —— 不是靠 handler 记得忽略,而是无处可放");
    }

    [Fact]
    public void The_start_command_has_nowhere_to_put_a_client_chosen_seed()
    {
        typeof(StartScoreRunCommand)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name.ToLowerInvariant())
            .Should().NotContain("seed",
                "客户端能选种子就能挑一串对自己有利的方块,而重放会照样通过");
    }

    [Fact]
    public void There_is_no_score_attack_registry()
    {
        // 计分类只有一款游戏在规划里。先造注册表就是在没有第二个实现的情况下猜通用形状 ——
        // add-puzzle-core 押过这一注(IPuzzleRules + 一个形状像成语纵横的假实现),
        // 华容道一来两个方法都得改。第二款计分游戏出现那天,内核从两个真实现之间长出来。
        var types = new[]
            {
                typeof(TetrisRules).Assembly,          // Gewu.Domain
                typeof(ScoreAttackGames).Assembly,     // Gewu.Application
            }
            .SelectMany(a => a.GetTypes())
            .Select(t => t.Name)
            .ToList();

        types.Should().NotContain(n =>
            n.Contains("ScoreAttackRules", StringComparison.OrdinalIgnoreCase)
            || n.Contains("ScoreAttackRegistry", StringComparison.OrdinalIgnoreCase)
            || n.Contains("ScoreRunRules", StringComparison.OrdinalIgnoreCase)
            || n.Contains("ScoreGameRegistry", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Starting_a_run_and_replaying_one_read_the_same_fact()
    {
        // enforce-ai-availability 的教训:两处各写一份判断,端点就会接受一个后台
        // 永远处理不了的状态。这里把两者钉在一起 —— 能开的键必须能重放,反之亦然。
        var keys = new[] { TetrisRules.GameKey, "gomoku", "klotski", "", "tetris-2" };

        foreach (var key in keys)
        {
            var accepted = ScoreAttackGames.IsScoreAttackGame(key);
            var replayable = TryReplay(key);

            replayable.Should().Be(accepted, $"'{key}' 上两个判断必须一致");
        }
    }

    private static bool TryReplay(string key)
    {
        try
        {
            ScoreAttackGames.Replay(key, 1, [new TetrisPlacement(0, 0)]);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}
