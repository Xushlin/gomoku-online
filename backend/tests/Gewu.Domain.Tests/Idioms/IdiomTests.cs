namespace Gewu.Domain.Tests.Idioms;

/// <summary><see cref="Idiom"/> 的构造不变量与人工校订优先级。</summary>
public class IdiomTests
{
    private static Idiom Common(string word = "一举一动")
        => Idiom.FromImport(word, "yī jǔ yī dòng", "指人的每一个动作。", "语出《朱子语类》", "他的～都被盯着。", 500);

    [Fact]
    public void FromImport_expands_characters_in_order()
    {
        var idiom = Common();

        idiom.CharCount.Should().Be(4);
        idiom.Characters.Should().HaveCount(4);
        idiom.Characters.Select(c => c.Position).Should().Equal(0, 1, 2, 3);
        idiom.Characters.Select(c => c.Char).Should().Equal('一', '举', '一', '动');
    }

    [Fact]
    public void FromImport_normalises_the_upstream_sentinel_to_empty()
    {
        var idiom = Idiom.FromImport("张三李四", "zhāng sān lǐ sì", "泛指某人。", "无", "无", 300);

        idiom.Derivation.Should().BeEmpty();
        idiom.Example.Should().BeEmpty();
        // 例句与出处都缺 → 生僻层,即便字频很高。
        idiom.Tier.Should().Be(IdiomTier.Obscure);
    }

    [Fact]
    public void FromImport_assigns_the_tier_from_the_shared_tiering_function()
    {
        Common().Tier.Should().Be(IdiomTier.Common);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void FromImport_rejects_an_empty_word(string? word)
    {
        var act = () => Idiom.FromImport(word!, "p", "e", "d", "x", 100);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void EffectiveTier_falls_back_to_the_computed_tier()
    {
        var idiom = Common();

        idiom.TierOverride.Should().BeNull();
        idiom.EffectiveTier.Should().Be(idiom.Tier);
    }

    [Fact]
    public void EffectiveTier_prefers_the_manual_override()
    {
        var idiom = Idiom.FromImport("闲花埜草", "xián huā yě cǎo", "", "无", "无", 3);
        idiom.Tier.Should().Be(IdiomTier.Obscure);

        idiom.OverrideTier(IdiomTier.Common);

        idiom.EffectiveTier.Should().Be(IdiomTier.Common);
        // 计算值本身不被覆盖 —— 它记录的是"启发式怎么看",人工值记录的是"人怎么看"。
        idiom.Tier.Should().Be(IdiomTier.Obscure);
    }

    [Fact]
    public void OverrideTier_with_null_reverts_to_the_computed_tier()
    {
        var idiom = Common();
        idiom.OverrideTier(IdiomTier.Obscure);
        idiom.EffectiveTier.Should().Be(IdiomTier.Obscure);

        idiom.OverrideTier(null);

        idiom.EffectiveTier.Should().Be(IdiomTier.Common);
    }
}
