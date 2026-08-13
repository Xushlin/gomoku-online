namespace Gewu.Domain.Tests.Idioms;

/// <summary>
/// <see cref="IdiomTiering"/> 的表驱动测试。
/// <para>
/// 重点覆盖 <c>"无"</c> 哨兵:上游用它表示"没有例句 / 没有出处"(30,895 条里 example
/// 为 <c>"无"</c> 的有 19,208 条)。若把它当成正文,分层信号就恒真、整套精选失去意义 ——
/// 这是真实数据推翻初版设计的那一处,所以必须有测试锁住。
/// </para>
/// </summary>
public class IdiomTieringTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("无", false)]          // 上游哨兵
    [InlineData("  无  ", false)]      // 带空白的哨兵
    [InlineData("语出《论语》", true)]
    [InlineData("无关紧要的正文", true)] // 以「无」开头但确实是正文
    public void HasContent_treats_the_upstream_sentinel_as_absent(string? value, bool expected)
    {
        IdiomTiering.HasContent(value).Should().Be(expected);
    }

    [Theory]
    // charCount, hasExample, hasDerivation, minCharFrequency, expected
    [InlineData(4, true, true, 80, IdiomTier.Common)]        // 恰好达线
    [InlineData(4, true, true, 2369, IdiomTier.Common)]      // 语料最高字频
    [InlineData(4, true, true, 79, IdiomTier.Usable)]        // 差 1 掉到可用层
    [InlineData(4, false, true, 500, IdiomTier.Usable)]      // 缺例句
    [InlineData(4, true, false, 500, IdiomTier.Usable)]      // 缺出处
    [InlineData(4, false, false, 500, IdiomTier.Obscure)]    // 两者皆缺
    [InlineData(4, true, true, 20, IdiomTier.Usable)]        // 可用层下线
    [InlineData(4, true, true, 19, IdiomTier.Obscure)]       // 差 1 掉到生僻层
    [InlineData(3, true, true, 500, IdiomTier.Obscure)]      // 三字
    [InlineData(5, true, true, 500, IdiomTier.Obscure)]      // 五字
    [InlineData(7, true, true, 2000, IdiomTier.Obscure)]     // 七字,字频再高也不出题
    public void Classify_maps_signals_to_tiers(
        int charCount, bool hasExample, bool hasDerivation, int minCharFrequency, IdiomTier expected)
    {
        IdiomTiering.Classify(charCount, hasExample, hasDerivation, minCharFrequency)
            .Should().Be(expected);
    }

    [Fact]
    public void Classify_is_pure()
    {
        var first = IdiomTiering.Classify(4, true, true, 120);
        for (var i = 0; i < 50; i++)
        {
            IdiomTiering.Classify(4, true, true, 120).Should().Be(first);
        }
    }

    [Fact]
    public void Thresholds_are_ordered_so_tier1_is_stricter_than_tier2()
    {
        IdiomTiering.CommonMinCharFrequency
            .Should().BeGreaterThan(IdiomTiering.UsableMinCharFrequency);
    }
}
