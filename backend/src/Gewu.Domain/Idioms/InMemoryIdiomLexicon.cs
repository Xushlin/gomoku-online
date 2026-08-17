using System.Collections.Frozen;

namespace Gewu.Domain.Idioms;

/// <summary>
/// 把一批成语原文装进内存的 <see cref="IIdiomLexicon"/>。
/// <para>
/// 放在 Domain 而不是 Infrastructure,因为它**没有任何外部依赖** —— 它就是一个不可变的
/// 字符串集合。Infrastructure 负责的是"词从哪来"(读库),不是"怎么查"。这样测试也能用
/// 同一份实现构造一本小词典,而不必各写一个假的。
/// </para>
/// <para>
/// <see cref="FrozenSet{T}"/>:构造一次、之后只读、查询 O(1),正好是规则实例
/// 被并发的多个房间共享时需要的形状。
/// </para>
/// </summary>
public sealed class InMemoryIdiomLexicon : IIdiomLexicon
{
    private readonly FrozenSet<string> _words;

    /// <summary>用一批成语原文构造词典。</summary>
    /// <param name="words">成语原文;重复项会被合并。</param>
    public InMemoryIdiomLexicon(IEnumerable<string> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        _words = words.ToFrozenSet(StringComparer.Ordinal);
    }

    /// <summary>收录的成语条数。</summary>
    public int Count => _words.Count;

    /// <inheritdoc />
    public bool Contains(string word)
        => !string.IsNullOrWhiteSpace(word) && _words.Contains(word);
}
