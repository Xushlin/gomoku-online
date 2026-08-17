using Gewu.Domain.Enums;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.IdiomChain;
using Gewu.Domain.Idioms;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Tests.Games.IdiomChain;

/// <summary>
/// 成语接龙的三条合法性:词典里有、首字接上末字、本局没说过。
/// </summary>
public class IdiomChainRulesTests
{
    private static readonly IdiomChainRules Rules = new(IdiomLexicons.Small);

    private const Stone First = Stone.Black;
    private const Stone Second = Stone.White;

    private static PlayedMove Said(string word, Stone side) => PlayedMove.Said(word, side);

    private static MoveApplication Apply(IReadOnlyList<PlayedMove> history, string word, Stone side)
        => Rules.Apply(history, MoveIntent.Say(word), side);

    [Fact]
    public void It_is_registered_as_a_boardless_rated_human_game()
    {
        Rules.GameKey.Should().Be("idiom-chain");
        Rules.SupportsHumanVsHuman.Should().BeTrue();
        Rules.IsRated.Should().BeTrue();
        Rules.Should().NotBeAssignableTo<IBoardGameRules>();
    }

    [Fact]
    public void Any_idiom_opens_the_game()
    {
        Apply([], "画蛇添足", First).Result.Should().Be(GameResult.Ongoing);
    }

    [Fact]
    public void A_word_that_links_on_is_accepted()
    {
        Apply([Said("一心一意", First)], "意气风发", Second)
            .Result.Should().Be(GameResult.Ongoing);
    }

    [Fact]
    public void A_word_that_does_not_link_on_is_refused()
    {
        var act = () => Apply([Said("一心一意", First)], "风和日丽", Second);

        act.Should().Throw<InvalidMoveException>()
            .WithMessage("*意*")
            .Which.Code.Should().Be("idiom-does-not-link");
    }

    [Fact]
    public void A_word_outside_the_dictionary_is_refused()
    {
        var act = () => Apply([], "这不是成语", First);

        act.Should().Throw<InvalidMoveException>()
            .WithMessage("*dictionary*")
            .Which.Code.Should().Be("idiom-not-found");
    }

    [Fact]
    public void A_word_already_played_is_refused_even_though_it_links_on()
    {
        // 【合而为一】末字是【一】，所以【一心一意】**真的接得上** —— 而它已经说过了。
        // 历史不必自洽：规则只读最后一项与已用集合，而且只对**提交的**那个词查词典。
        //
        // 本用例之前的历史是【…止于至善】，而【发号施令】接不上【善】——
        // 于是它其实在验第二条规则，名字却说第三条。两条共用一个无区别的
        // `InvalidMoveException` 时，这个谎无从暴露；拆开错误码的第一个收获就是它。
        var history = new List<PlayedMove>
        {
            Said("一心一意", First),
            Said("合而为一", Second),
        };

        var act = () => Apply(history, "一心一意", First);

        act.Should().Throw<InvalidMoveException>()
            .Which.Code.Should().Be("idiom-already-used");
    }

    [Fact]
    public void A_positional_move_is_refused()
    {
        var act = () => Rules.Apply([], MoveIntent.Place(new Position(0, 0)), First);

        // 缺省的 invalid-move：送错了形状不是三条规则之一。
        act.Should().Throw<InvalidMoveException>()
            .WithMessage("*board*")
            .Which.Code.Should().Be("invalid-move");
    }

    [Fact]
    public void An_obscure_but_real_idiom_is_accepted()
    {
        // 校验的是"在不在词典里",不是"常不常见"。拒掉一条冷僻但合法的成语是 bug。
        Apply([], "闲花埜草", First).Result.Should().Be(GameResult.Ongoing);
    }

    [Fact]
    public void A_homophone_does_not_link_on()
    {
        // 「一心一意」末字是「意」(yì),「义无反顾」首字是「义」(yì) —— 同音不同字。
        // 按读音接是常见家规,本平台不采纳:多音字让一条成语有好几个"末音",而客户端
        // 根本拿不到音。字是双方都看得见的东西。
        var act = () => Apply([Said("一心一意", First)], "义无反顾", Second);

        act.Should().Throw<InvalidMoveException>()
            .Which.Code.Should().Be("idiom-does-not-link");
    }

    [Fact]
    public void The_implementation_never_reads_pinyin()
    {
        // 上一条断言的是行为;这一条断言的是它**没有别的路可走** —— 一个读拼音的实现
        // 可以在这套小词典上碰巧全绿。
        var source = File.ReadAllText(
            Path.Combine(SolutionRoot(), "src", "Gewu.Domain", "Games", "IdiomChain",
                "IdiomChainRules.cs"));

        source.Should().NotContain("Pinyin");
    }

    [Fact]
    public void A_legal_move_never_ends_the_game()
    {
        // 接龙没有终局局面。一方答不上来才结束,而那由 Room.TimeOutCurrentTurn 承接。
        var history = new List<PlayedMove>();
        foreach (var (word, side) in new[]
        {
            ("一心一意", First), ("意气风发", Second),
            ("发号施令", First), ("令行禁止", Second), ("止于至善", First),
        })
        {
            Apply(history, word, side).Result.Should().Be(GameResult.Ongoing);
            history.Add(Said(word, side));
        }
    }

    [Fact]
    public void An_empty_side_is_refused()
    {
        var act = () => Apply([], "画蛇添足", Stone.Empty);

        act.Should().Throw<InvalidMoveException>();
    }

    /// <summary>从测试程序集向上找到解决方案根 —— 源码断言要读文件。</summary>
    private static string SolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Gewu.slnx")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Gewu.slnx not found above the test binaries.");
    }
}
