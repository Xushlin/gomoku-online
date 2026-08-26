namespace Gewu.Domain.Manuals;

/// <summary>
/// 一部古谱的身份:键、书名、有没有「第N局」那一层。
/// <para>
/// **`add-xiangqi-manual` 里我明确反对过这张表**,理由是「今天只有一部谱,第二张表会是一张
/// 单行表加一次没有信息的 join」。那个理由**现在不成立了**:七部谱各有自己的书名(《橘中秘》
/// 《适情雅趣》…,都是原书名、公有领域)与各自的分组形态,而把书名抄在 1634 行线路上是
/// 一份重复。
/// </para>
/// <para>
/// 记下这次改主意的**依据**而不只是结论:数据从 1 部变成 7 部,属性从 0 个变成 2 个。
/// </para>
/// </summary>
public sealed class XiangqiManual
{
    /// <summary>古谱键,主键。与数据文件名和 DI 清单里的那个字符串是同一个。</summary>
    public string Key { get; private set; } = string.Empty;

    /// <summary>书名 —— 原书名,取自明清刊本,公有领域。</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// 这部谱有没有「第N局」那一层。
    /// <para>
    /// 《梅花谱》有(第1局 … 第8局,每局若干变化);六辑残局没有 —— 而为了形状一致给它们
    /// 编一个局号是**编数据**,所以这是一个字段而不是一个约定。
    /// </para>
    /// </summary>
    public bool Grouped { get; private set; }

    // EF 物化用。
    private XiangqiManual() { }

    /// <summary>创建一部谱。</summary>
    /// <param name="key">古谱键,非空。</param>
    /// <param name="name">书名,非空。</param>
    /// <param name="grouped">有没有分组层。</param>
    /// <exception cref="ArgumentException">键或书名为空。</exception>
    public static XiangqiManual Create(string key, string name, bool grouped)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Manual key must be non-empty.", nameof(key));
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Manual name must be non-empty.", nameof(name));
        }
        return new XiangqiManual { Key = key.Trim(), Name = name.Trim(), Grouped = grouped };
    }
}
