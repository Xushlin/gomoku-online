using Gewu.Domain.Idioms;

namespace Gewu.Application.Abstractions;

/// <summary>
/// 成语词典的读取口。
/// <para>
/// 只暴露三个成语游戏确实需要的四个读操作,**不** 暴露 <c>IQueryable</c> 或通用查询对象
/// —— 一是不让 LINQ 表达式树漏出 Infrastructure,二是每加一个需求都必须显式想清楚它的
/// 访问路径,而不是被一个"顺手能扫全表"的通用方法悄悄吸收掉。
/// </para>
/// <para>
/// 所有带 <c>maxTier</c> 的方法都按**生效层级**过滤,即
/// <c>COALESCE(TierOverride, Tier) &lt;= maxTier</c> —— 人工校订必须生效。
/// </para>
/// </summary>
public interface IIdiomRepository
{
    /// <summary>
    /// 按成语原文精确查找 —— 成语接龙用它判断"这是不是一条真成语"。
    /// 不做层级过滤:玩家答一条冷僻但合法的成语,拒掉是 bug。
    /// </summary>
    /// <param name="word">成语原文。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>找到的成语,不存在则 <c>null</c>。</returns>
    Task<Idiom?> FindByWordAsync(string word, CancellationToken cancellationToken = default);

    /// <summary>
    /// 查出在指定位置上是指定字的成语 —— 纵横生成填交叉点用。
    /// </summary>
    /// <param name="character">要匹配的字。</param>
    /// <param name="position">字在成语中的位置,0 起。</param>
    /// <param name="maxTier">生效层级上限。</param>
    /// <param name="limit">最多返回条数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<IReadOnlyList<Idiom>> FindContainingCharAsync(
        char character,
        int position,
        IdiomTier maxTier,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 查出以指定字开头的成语 —— 成语接龙找候选用。
    /// </summary>
    /// <param name="character">首字。</param>
    /// <param name="maxTier">生效层级上限。</param>
    /// <param name="limit">最多返回条数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<IReadOnlyList<Idiom>> FindStartingWithCharAsync(
        char character,
        IdiomTier maxTier,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 随机取若干条成语 —— 猜成语选题用。随机源由实现决定,调用方不得依赖顺序稳定。
    /// </summary>
    /// <param name="maxTier">生效层级上限。</param>
    /// <param name="count">要取的条数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<IReadOnlyList<Idiom>> GetRandomAsync(
        IdiomTier maxTier,
        int count,
        CancellationToken cancellationToken = default);
}
