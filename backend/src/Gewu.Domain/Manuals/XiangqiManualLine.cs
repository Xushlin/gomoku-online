namespace Gewu.Domain.Manuals;

/// <summary>
/// 古谱里的一条着法线路(《梅花谱》的一个「变化」)。
/// <para>
/// 它**不是** <c>Room</c>。把古谱塞成 Finished 房间零改动就能复用 <c>/replay/{id}</c>,
/// 而代价是往用户战绩、ELO、排行榜与 <c>GET /api/users/{id}/games</c> 里注入几十局
/// 没人下过的棋 —— 而**一个写错的标记看起来和一局没人下过的棋一模一样**。
/// </para>
/// <para>
/// 它也没有聚合根:服务时没有规则要执行,着法是给定的。校验发生在**导入**那一次
/// (播种器逐手过 <c>XiangqiRules</c>),因为这里没有玩家的声称要判。
/// </para>
/// <para>
/// 目录展示是两级(局 → 变化),但「局」是一个**列**而不是一张表:梅花谱的局没有自己的
/// 标题,共用的只有「第N局」三个字。谱的身份同理由 <see cref="ManualKey"/> 承载,
/// 所以《橘中秘》落地时不需要加表。
/// </para>
/// </summary>
public sealed class XiangqiManualLine
{
    /// <summary>自增主键。</summary>
    public int Id { get; private set; }

    /// <summary>所属古谱的键,例如 <c>meihuapu</c>。</summary>
    public string ManualKey { get; private set; } = string.Empty;

    /// <summary>第几局,1 起 —— 由标题推出,不手写。</summary>
    public int Chapter { get; private set; }

    /// <summary>同一局内的次序,0 起。决定目录里的排列。</summary>
    public int OrderInChapter { get; private set; }

    /// <summary>原书局名,例如「第1局取中兵压马破上右士」。</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>
    /// 谱主判谁占优 —— <c>BoardSeats</c> 的座位号(0 = 红先手,1 = 黑)。
    /// <para>
    /// **它是评断,不是终局。** 量过:31 条线路里只有 11 条真的走到将死,20 条走到
    /// 「优势已成」就停了。所以任何把它当成「这里被将死了」的文案都是错的,而
    /// 在那 20 条上错的样子和对的样子完全一样。
    /// </para>
    /// </summary>
    public int WinnerSeat { get; private set; }

    /// <summary>
    /// 着法,<c>[[fromRow,fromCol,toRow,toCol], …]</c>,**本项目的坐标约定**
    /// (row 0 在上,row 9 是红方底线)。座位由下标奇偶决定:红先。
    /// <para>
    /// 来源数据是 <c>(列,行)</c>,转置只发生在播种那一次 —— 外部的坐标约定
    /// MUST NOT 渗进库里。
    /// </para>
    /// </summary>
    public string MovesJson { get; private set; } = string.Empty;

    // EF 物化用。
    private XiangqiManualLine() { }

    /// <summary>创建一条线路。</summary>
    /// <param name="manualKey">古谱键,非空。</param>
    /// <param name="chapter">第几局,须为正。</param>
    /// <param name="orderInChapter">局内次序,不得为负。</param>
    /// <param name="title">原书局名,非空。</param>
    /// <param name="winnerSeat">谱主判占优的座位,不得为负。</param>
    /// <param name="movesJson">着法数组的 JSON,非空。</param>
    /// <exception cref="ArgumentException">键、标题或着法为空。</exception>
    /// <exception cref="ArgumentOutOfRangeException">局号非正,或次序 / 座位为负。</exception>
    public static XiangqiManualLine Create(
        string manualKey,
        int chapter,
        int orderInChapter,
        string title,
        int winnerSeat,
        string movesJson)
    {
        if (string.IsNullOrWhiteSpace(manualKey))
        {
            throw new ArgumentException("Manual key must be non-empty.", nameof(manualKey));
        }
        if (chapter <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(chapter), chapter, "Chapter must be positive.");
        }
        if (orderInChapter < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(orderInChapter), orderInChapter, "Order must not be negative.");
        }
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title must be non-empty.", nameof(title));
        }
        if (winnerSeat < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(winnerSeat), winnerSeat, "Winner seat must not be negative.");
        }
        if (string.IsNullOrWhiteSpace(movesJson))
        {
            throw new ArgumentException("Moves must be non-empty.", nameof(movesJson));
        }

        return new XiangqiManualLine
        {
            ManualKey = manualKey.Trim(),
            Chapter = chapter,
            OrderInChapter = orderInChapter,
            Title = title.Trim(),
            WinnerSeat = winnerSeat,
            MovesJson = movesJson,
        };
    }
}
