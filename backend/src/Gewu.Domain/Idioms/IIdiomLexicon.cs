namespace Gewu.Domain.Idioms;

/// <summary>
/// 「这个词是不是一条成语」—— 落子路径上的成语词典。
/// <para>
/// 与 <c>IIdiomRepository</c> **并存**,两者都不删。那一个是异步的、返回完整的
/// <see cref="Idiom"/> 行,供纵横的生成器与将来的猜成语使用;这一个只回答一个布尔。
/// </para>
/// <para>
/// 分开的理由是**调用路径**,不是洁癖:<c>IGameRules.Apply</c> 是同步的、在 Domain 里、
/// 由聚合方法内部调用。为一个棋种把它改成异步,会让五子棋和象棋为一个它们没有的需求
/// 买单,还会把一次数据库往返塞进 <c>Room.PlayMove</c>。
/// </para>
/// <para>
/// <c>add-idiom-dictionary</c> 当初就是为成语接龙建的 <c>FindByWordAsync</c>,它的注释
/// 写着「成语接龙用它判断"这是不是一条真成语"」。**那个端口选对了消费者,选错了调用路径。**
/// 一个为尚不存在的消费者建的端口是一次预测,而这次预测只对了一半。
/// </para>
/// <para>
/// 实现 MUST 是不可变且线程安全的 —— 规则实例被并发的多个房间共享。
/// </para>
/// </summary>
public interface IIdiomLexicon
{
    /// <summary>
    /// 这个词是不是词典里的成语。
    /// <para>
    /// MUST NOT 按层级过滤 —— 玩家答一条冷僻但合法的成语,拒掉是 bug。校验的是
    /// "在不在词典里",不是"常不常见"。
    /// </para>
    /// </summary>
    /// <param name="word">成语原文。</param>
    bool Contains(string word);
}
