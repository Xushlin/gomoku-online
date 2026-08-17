namespace Gewu.Domain.Exceptions;

/// <summary>
/// 领域级非法着法异常。表达违反某个棋种不变量的行为:越界、落到已有子的格、
/// 以 <c>Stone.Empty</c> 构造、某种棋子走不出那一步,等等。
/// 仅用于保护 Domain 不变量;调用方(Application / AI / SignalR Hub)应先校验合法性,
/// 不要把异常当作常规流程控制手段。
/// <para>
/// 绝大多数情形共用 <c>invalid-move</c> 这个码。**自将/照面是唯一的例外**,见
/// <see cref="SelfCheck"/>:它是象棋里最常见的一种拒绝,而「这步不合法」不告诉玩家
/// 他漏看了什么。类型不拆成两个,是因为聚合根与既有测试都以本类型表达「规则拒绝了」;
/// 拆开会为了一句文案改动一条已经稳定的契约。
/// </para>
/// </summary>
public sealed class InvalidMoveException : DomainException
{
    /// <summary>以给定消息构造异常。</summary>
    public InvalidMoveException(string message) : base("invalid-move", message)
    {
    }

    /// <summary>以给定消息与内部异常构造异常。</summary>
    public InvalidMoveException(string message, Exception innerException)
        : base("invalid-move", message, innerException)
    {
    }

    private InvalidMoveException(string message, string code) : base(code, message)
    {
    }

    /// <summary>
    /// 走完之后本方将帅会被吃 —— 送将、自将,或与对方将帅照面。
    /// <para>
    /// 它带自己的码,因为它需要自己的文案。象棋的棋盘刻意不懂规则,所以被拒绝是玩家
    /// 了解走法的常规途径,而这一种拒绝的原因不在他刚点的那两格上。
    /// </para>
    /// </summary>
    /// <param name="message">人类散文,给日志看。</param>
    public static InvalidMoveException SelfCheck(string message) => new(message, "self-check");

    // ── 成语接龙的三条规则 ────────────────────────────────────────────────────
    //
    // 三条各带自己的码,而不是共用 invalid-move。理由不是整洁:接龙的界面**故意不在客户端
    // 判合法性**(见 web-idiom-chain),所以服务端的拒绝是玩家了解规则的**唯一**途径,而
    // 「不是成语」「接不上」「说过了」是三种完全不同的纠正 —— 「这一步不合法」一种都说不出。
    //
    // 象棋能共用是因为玩家看着盘面能自己想明白;接龙没有盘面可看。

    /// <summary>这个词不在词典里。</summary>
    /// <param name="message">人类散文,给日志看。</param>
    public static InvalidMoveException IdiomNotFound(string message)
        => new(message, "idiom-not-found");

    /// <summary>这个词的首字接不上上一个词的末字。</summary>
    /// <param name="message">人类散文,给日志看。</param>
    public static InvalidMoveException IdiomDoesNotLink(string message)
        => new(message, "idiom-does-not-link");

    /// <summary>这个词本局已经说过了。</summary>
    /// <param name="message">人类散文,给日志看。</param>
    public static InvalidMoveException IdiomAlreadyUsed(string message)
        => new(message, "idiom-already-used");
}
