namespace Gewu.Domain.Games.Klotski;

/// <summary>
/// 一枚棋子:左上角坐标 + 尺寸。
/// <para>
/// **没有「人物」字段。** 曹操、关羽、张飞是显示层的读法;规则只关心形状,以及
/// 哪一枚是要送出去的那一枚(<see cref="IsTarget"/>)。关卡 JSON 里可以带人物名,
/// 领域层原样忽略 —— 少一个能和几何互相矛盾的字段。
/// </para>
/// </summary>
/// <param name="Id">关卡内唯一的标识,提交里的移动按它指名棋子。</param>
/// <param name="Row">左上角行。</param>
/// <param name="Col">左上角列。</param>
/// <param name="Height">占几行。</param>
/// <param name="Width">占几列。</param>
/// <param name="IsTarget">是否是必须送到出口的那一枚(经典局面里的曹操)。</param>
public readonly record struct KlotskiPiece(
    string Id,
    int Row,
    int Col,
    int Height,
    int Width,
    bool IsTarget)
{
    /// <summary>把这枚子整体挪动 <paramref name="dr"/> 行、<paramref name="dc"/> 列。</summary>
    /// <param name="dr">行增量。</param>
    /// <param name="dc">列增量。</param>
    public KlotskiPiece Shifted(int dr, int dc) => this with { Row = Row + dr, Col = Col + dc };
}

/// <summary>
/// 一次移动:某枚子朝上下左右之一滑动**一格**。
/// <para>
/// 一格一步而不是「连滑算一步」,是因为它无歧义 —— 客户端的一次拖拽可能跨两格,
/// 服务端不该猜玩家想算几步。重放、计数、计分因此共用同一个定义。代价是步数比
/// 出版物上的经典数字大,而本游戏**不引用**任何外部数字:<c>minMoves</c> 是算出来的。
/// </para>
/// </summary>
/// <param name="Id">要移动的棋子。</param>
/// <param name="Dr">行增量,与 <paramref name="Dc"/> 恰有一个为 ±1。</param>
/// <param name="Dc">列增量。</param>
public readonly record struct KlotskiMove(string Id, int Dr, int Dc)
{
    /// <summary>是否是一次形状合法的一格移动(不含盘面判定)。</summary>
    public bool IsSingleStep =>
        (Math.Abs(Dr) == 1 && Dc == 0) || (Math.Abs(Dc) == 1 && Dr == 0);
}
