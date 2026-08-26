namespace Gewu.Domain.Manuals;

/// <summary>
/// 谱主对一条线路的评断。
/// <para>
/// **它取代了原来的「获胜座位」,而这不是加一个状态,是列的类型错了。** 六辑残局里
/// <c>和棋</c> 有 391 局,而一个座位号**表达不了和棋**;《梅花谱》那 31 条只有红黑两种,
/// 所以那份类型在 31 局的样本上是够的 —— 这正是它错得看不出来的原因。
/// </para>
/// <para>
/// <see cref="Unrecorded"/> MUST 是一个显式取值,而 MUST NOT 用 <see cref="RedBetter"/>
/// 当默认:实测 338 / 1634 的记录没有结果字段(**烂柯神机整辑 258 局全部没有**),
/// 把它们说成「谱主判红胜」是我们编的话。
/// </para>
/// <para>
/// **这是评断,不是终局。** 《梅花谱》31 条里只有 11 条真的走到将死;残局同理 ——
/// 界面 MUST NOT 把它说成「将死」。
/// </para>
/// </summary>
public enum ManualVerdict
{
    /// <summary>谱未标注 —— 来源没有给结果。</summary>
    Unrecorded = 0,

    /// <summary>谱主判红方占优 / 红胜。</summary>
    RedBetter = 1,

    /// <summary>谱主判黑方占优 / 黑胜。</summary>
    BlackBetter = 2,

    /// <summary>谱主判和 —— **没有获胜座位可言**,而这是旧类型装不下的那一格。</summary>
    Draw = 3,
}
