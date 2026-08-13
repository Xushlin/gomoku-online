namespace Gewu.Domain.Idioms;

/// <summary>
/// 一条成语。**参考数据**,不是聚合根 —— 它没有生命周期、没有不变量需要跨事务保护,
/// 只由种子载入器从提交进仓库的精选产物里灌进来,之后对游戏只读。
/// <para>
/// 主键用 <see cref="int"/> 自增而非仓库里其它实体惯用的强类型 <c>Guid</c>:参考数据不存在
/// 分布式生成 Id 的需求,而 <see cref="IdiomChar"/> 有约 12.8 万行外键指向本表,
/// 窄外键在这个量级上是实打实的收益。
/// </para>
/// </summary>
public sealed class Idiom
{
    private readonly List<IdiomChar> _characters = new();

    /// <summary>自增主键。</summary>
    public int Id { get; private set; }

    /// <summary>成语本身,全库唯一。</summary>
    public string Word { get; private set; } = string.Empty;

    /// <summary>拼音,空格分隔。</summary>
    public string Pinyin { get; private set; } = string.Empty;

    /// <summary>释义。仅 <see cref="IdiomTier.Common"/> / <see cref="IdiomTier.Usable"/> 有值,
    /// <see cref="IdiomTier.Obscure"/> 为空串 —— 生僻条目只用于校验玩家输入,不会被展示。</summary>
    public string Explanation { get; private set; } = string.Empty;

    /// <summary>出处。缺失或属生僻层时为空串(上游哨兵 <c>"无"</c> 在导入时已被归一化掉)。</summary>
    public string Derivation { get; private set; } = string.Empty;

    /// <summary>例句。缺失或属生僻层时为空串。</summary>
    public string Example { get; private set; } = string.Empty;

    /// <summary>字数。</summary>
    public int CharCount { get; private set; }

    /// <summary>
    /// 成语中最生僻那个字在全语料里的文档频率。落库是为了让"这条为什么是这一层"
    /// 可以直接查出来,不必重跑导入器。
    /// </summary>
    public int MinCharFrequency { get; private set; }

    /// <summary>由 <see cref="IdiomTiering.Classify"/> 算出的层级。导入器可重写。</summary>
    public IdiomTier Tier { get; private set; }

    /// <summary>
    /// 人工校订的层级。导入器 MUST NOT 写这一列 —— 它是人工判断的积累处,
    /// 重新导入不会把它冲掉。消费方一律读 <see cref="EffectiveTier"/>。
    /// </summary>
    public IdiomTier? TierOverride { get; private set; }

    /// <summary>生效层级:人工值优先,没有则用计算值。</summary>
    public IdiomTier EffectiveTier => TierOverride ?? Tier;

    /// <summary>逐字展开,供纵横生成与接龙首字检索使用(只读视图)。</summary>
    public IReadOnlyCollection<IdiomChar> Characters => _characters;

    // EF 物化用。
    private Idiom() { }

    /// <summary>
    /// 从精选产物创建一条成语,并同步展开 <see cref="Characters"/> —— 走这个入口
    /// 就不可能出现"字符行与 <see cref="Word"/> 不一致"的状态。
    /// </summary>
    /// <param name="word">成语,非空。</param>
    /// <param name="pinyin">拼音。</param>
    /// <param name="explanation">释义,可空。</param>
    /// <param name="derivation">出处,可空。</param>
    /// <param name="example">例句,可空。</param>
    /// <param name="minCharFrequency">最生僻字的语料文档频率。</param>
    /// <exception cref="ArgumentException"><paramref name="word"/> 为空或空白。</exception>
    public static Idiom FromImport(
        string word,
        string pinyin,
        string? explanation,
        string? derivation,
        string? example,
        int minCharFrequency)
    {
        if (string.IsNullOrWhiteSpace(word))
        {
            throw new ArgumentException("Idiom word must be non-empty.", nameof(word));
        }

        var trimmed = word.Trim();
        var idiom = new Idiom
        {
            Word = trimmed,
            Pinyin = pinyin?.Trim() ?? string.Empty,
            Explanation = Normalize(explanation),
            Derivation = Normalize(derivation),
            Example = Normalize(example),
            CharCount = trimmed.Length,
            MinCharFrequency = minCharFrequency,
            TierOverride = null,
        };

        idiom.Tier = IdiomTiering.Classify(
            idiom.CharCount,
            IdiomTiering.HasContent(idiom.Example),
            IdiomTiering.HasContent(idiom.Derivation),
            minCharFrequency);

        for (var position = 0; position < trimmed.Length; position++)
        {
            idiom._characters.Add(new IdiomChar(position, trimmed[position]));
        }

        return idiom;
    }

    /// <summary>
    /// 人工校订层级。传 <c>null</c> 表示撤销校订、回落到计算值。
    /// 这是 <see cref="TierOverride"/> 的**唯一**写入口,导入器不得调用。
    /// </summary>
    /// <param name="tier">人工判定的层级,或 <c>null</c> 撤销。</param>
    public void OverrideTier(IdiomTier? tier) => TierOverride = tier;

    /// <summary>把上游哨兵值与空白归一化成空串。</summary>
    private static string Normalize(string? value)
        => IdiomTiering.HasContent(value) ? value!.Trim() : string.Empty;
}
