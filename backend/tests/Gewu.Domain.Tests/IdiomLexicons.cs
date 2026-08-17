using Gewu.Domain.Idioms;

namespace Gewu.Domain.Tests;

/// <summary>
/// 测试用的小词典。用的是**生产实现** <c>InMemoryIdiomLexicon</c>,只是词少 ——
/// 假一个 <c>IIdiomLexicon</c> 出来,测的就变成"规则会调词典",而不是"规则怎么判接龙"。
/// </summary>
internal static class IdiomLexicons
{
    /// <summary>够走一条链的一本小词典。</summary>
    internal static readonly IIdiomLexicon Small = new InMemoryIdiomLexicon(
    [
        "一心一意", "意气风发", "发号施令", "令行禁止", "止于至善",
        "风和日丽", "画蛇添足", "足智多谋", "闲花埜草", "义无反顾",
    ]);
}
