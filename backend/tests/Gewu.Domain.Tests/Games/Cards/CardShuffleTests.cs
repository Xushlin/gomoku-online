using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Gewu.Domain.Games.Cards;

namespace Gewu.Domain.Tests.Games.Cards;

/// <summary>
/// 按种子洗牌 —— 从 <c>DoudizhuDeal</c> 里提出来的那一段,现在有了自己的断言。
/// <para>
/// 提出来的理由是挖坑要洗同一副牌,而那会是这段 Fisher–Yates 加 xorshift32 的**第三份**副本。
/// 而「搬家没有改变任何一副牌」由斗地主那边的 <c>The_encoded_deal_is_pinned</c> 钉着 ——
/// 这里钉的是这段代码**自己**的性质。
/// </para>
/// </summary>
public class CardShuffleTests
{
    private static List<int> Shuffled(int seed, int count = 54)
    {
        var items = Enumerable.Range(0, count).ToList();
        CardShuffle.Shuffle(items, seed);
        return items;
    }

    [Fact]
    public void The_same_seed_always_gives_the_same_order()
    {
        // 重放靠这一条:同一个种子在任何运行时上都必须洗出同一副牌。
        Shuffled(20260820).Should().Equal(Shuffled(20260820));
    }

    [Fact]
    public void Different_seeds_give_different_orders()
    {
        Shuffled(1).Should().NotEqual(Shuffled(2));
    }

    [Fact]
    public void Shuffling_keeps_every_item_exactly_once()
    {
        // 洗牌不是发牌:一张不多、一张不少,而顺序变了。
        var shuffled = Shuffled(7);

        shuffled.Should().BeEquivalentTo(Enumerable.Range(0, 54));
        shuffled.Should().NotEqual(Enumerable.Range(0, 54), "洗过之后不该还是原顺序");
    }

    [Fact]
    public void Seed_zero_is_substituted_so_the_entropy_is_not_lost()
    {
        // xorshift32 在 0 上永远停在 0。**后果不是「没洗」** —— 每一步的 j 都是 0,
        // 于是每一步都跟 0 号位交换,得到一个**与种子无关的固定置换**:牌动了、张数也对,
        // 那种一眼可见的症状不会出现。真正的后果是熵全丢。
        //
        // 所以钉的是那条精确的性质:0 号种子必须与替代常数给出**同一个**顺序。
        Shuffled(0).Should().Equal(Shuffled(unchecked((int)0x9E3779B9)));
    }

    [Fact]
    public void A_single_item_list_is_left_alone()
    {
        // 循环从 count - 1 开始且要求 i > 0 —— 一张牌时它一次都不转。
        var one = new List<int> { 42 };

        CardShuffle.Shuffle(one, 5);

        one.Should().Equal(42);
    }

    [Fact]
    public void An_empty_list_does_not_throw()
    {
        var empty = new List<int>();

        var act = () => CardShuffle.Shuffle(empty, 5);

        act.Should().NotThrow();
        empty.Should().BeEmpty();
    }
}
