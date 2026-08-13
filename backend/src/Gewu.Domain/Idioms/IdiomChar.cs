namespace Gewu.Domain.Idioms;

/// <summary>
/// 成语的一个字及其位置 —— 反查索引的一行。
/// <para>
/// 存在的理由:纵横生成会反复问"哪些成语第 3 个字是「山」"。用 <c>WHERE Word LIKE '__山_'</c>
/// 回答等于每个交叉点扫一遍全表;把字拆成行并在 <c>(Char, Position)</c> 上建索引,
/// 同一个问题变成一次索引 seek。代价是约 12.8 万行窄记录,对 SQLite 微不足道。
/// </para>
/// <para>
/// 只能由 <see cref="Idiom.FromImport"/> 构造,因此不存在字符行与
/// <see cref="Idiom.Word"/> 不一致的状态。
/// </para>
/// </summary>
public sealed class IdiomChar
{
    /// <summary>自增主键。</summary>
    public int Id { get; private set; }

    /// <summary>所属成语。</summary>
    public int IdiomId { get; private set; }

    /// <summary>字在成语中的位置,0 起。</summary>
    public int Position { get; private set; }

    /// <summary>该位置上的字。</summary>
    public char Char { get; private set; }

    // EF 物化用。
    private IdiomChar() { }

    internal IdiomChar(int position, char character)
    {
        Position = position;
        Char = character;
    }
}
