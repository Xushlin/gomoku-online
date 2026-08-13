namespace Gewu.Domain.Idioms;

/// <summary>
/// 成语难度分层。数值有序,<c>1</c> 最适合出题。
/// </summary>
public enum IdiomTier
{
    /// <summary>适合出题:四字、有例句、有出处、用字常见。</summary>
    Common = 1,

    /// <summary>可用:四字、例句或出处至少有一项、用字不算冷僻。</summary>
    Usable = 2,

    /// <summary>生僻:其余全部。可用于校验玩家输入,不应用于生成题目。</summary>
    Obscure = 3,
}

/// <summary>
/// 由上游可得信号推断成语难度的**纯函数**。相同入参必产相同出参 —— 不读时钟、不用随机、不访问 IO。
/// <para>
/// 三个信号:字数、<c>example</c> / <c>derivation</c> 是否真实存在、以及
/// <paramref name="minCharFrequency"/>(成语中最生僻那个字在全语料里的文档频率)。
/// </para>
/// <para>
/// 重要:上游用字符串 <c>"无"</c> 表示"没有例句 / 没有出处",而不是空串
/// —— 30,895 条里 <c>example</c> 为 <c>"无"</c> 的有 19,208 条。因此判定必须走
/// <see cref="HasContent"/>,只判空会让信号恒真。
/// </para>
/// <para>
/// 上游不含任何词频数据,所以字频代理是从语料自身统计出来的。由此得到的
/// <see cref="IdiomTier"/> 是**难度假设,不是事实** —— 抽样显示
/// <see cref="IdiomTier.Common"/> 里仍有约两成偏生僻。收敛靠人工
/// <c>TierOverride</c> 加试玩,不靠把这个函数写得更复杂。
/// </para>
/// </summary>
public static class IdiomTiering
{
    /// <summary>上游表示"该字段无内容"的哨兵值。</summary>
    public const string MissingMarker = "无";

    /// <summary>出题层要求的最低字频。</summary>
    public const int CommonMinCharFrequency = 80;

    /// <summary>可用层要求的最低字频。</summary>
    public const int UsableMinCharFrequency = 20;

    /// <summary>出题层与可用层都要求的成语字数。</summary>
    public const int PreferredCharCount = 4;

    /// <summary>
    /// 判断上游某个文本字段是否真的有内容 —— 空、空白、以及哨兵值
    /// <see cref="MissingMarker"/> 都算没有。
    /// </summary>
    /// <param name="value">上游字段原值。</param>
    public static bool HasContent(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Trim() != MissingMarker;
    }

    /// <summary>
    /// 计算难度层级。
    /// </summary>
    /// <param name="charCount">成语字数。</param>
    /// <param name="hasExample">是否有真实例句(已通过 <see cref="HasContent"/> 判定)。</param>
    /// <param name="hasDerivation">是否有真实出处(同上)。</param>
    /// <param name="minCharFrequency">成语中最生僻那个字的语料文档频率。</param>
    public static IdiomTier Classify(
        int charCount,
        bool hasExample,
        bool hasDerivation,
        int minCharFrequency)
    {
        if (charCount != PreferredCharCount)
        {
            return IdiomTier.Obscure;
        }

        if (hasExample && hasDerivation && minCharFrequency >= CommonMinCharFrequency)
        {
            return IdiomTier.Common;
        }

        if ((hasExample || hasDerivation) && minCharFrequency >= UsableMinCharFrequency)
        {
            return IdiomTier.Usable;
        }

        return IdiomTier.Obscure;
    }
}
