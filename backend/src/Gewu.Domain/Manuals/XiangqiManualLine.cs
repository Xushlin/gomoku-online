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
    /// <summary>盘面串的长度 —— 10 行 x 9 列。</summary>
    public const int BoardStringLength = 90;

    /// <summary>自增主键。</summary>
    public int Id { get; private set; }

    /// <summary>所属古谱的键,例如 <c>meihuapu</c>。</summary>
    public string ManualKey { get; private set; } = string.Empty;

    /// <summary>
    /// 第几局,1 起 —— 由标题推出,不手写。
    /// <para>
    /// **<c>0</c> 表示这部谱没有「第N局」那一层。** 六辑残局就是这样,而为了形状一致给
    /// 它们编一个局号是**编数据**。这道口子是后来开的:原来的守卫写着「局号必须为正」,
    /// 那是《梅花谱》时代的约定 —— 它在 1634 条没有分组层的线路上直接抛。
    /// </para>
    /// </summary>
    public int Chapter { get; private set; }

    /// <summary>同一局内的次序,0 起。决定目录里的排列。</summary>
    public int OrderInChapter { get; private set; }

    /// <summary>原书局名,例如「第1局取中兵压马破上右士」。</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>
    /// 谱主的评断。
    /// <para>
    /// **它是评断,不是终局。** 量过:《梅花谱》31 条线路里只有 11 条真的走到将死,20 条
    /// 走到「优势已成」就停了。所以任何把它当成「这里被将死了」的文案都是错的,而
    /// 在那 20 条上错的样子和对的样子完全一样。
    /// </para>
    /// <para>
    /// 它取代了原来的「获胜座位」—— 见 <see cref="ManualVerdict"/>:和棋没有获胜座位。
    /// </para>
    /// </summary>
    public ManualVerdict Verdict { get; private set; }

    /// <summary>
    /// 起始局面 —— **90 字符的行优先盘面串**,`.` 是空格,红大写黑小写
    /// (<c>R N B A K C P</c> / <c>r n b a k c p</c>),索引 <c>row * 9 + col</c>。
    /// <para>
    /// 存串而不是嵌套数组有两个理由:体积小十倍(1.45 MB → 476 kB),而且**人肉审的时候
    /// 一眼能看出来** —— 一份要提交进仓库的产物,可读性是它的一半价值。
    /// </para>
    /// <para>
    /// 它 MUST NOT 可空:一个可空的起始局面会让「没填」与「从开局起」在代码里长得一样,
    /// 而后者是《梅花谱》那 31 条的全部。
    /// </para>
    /// </summary>
    public string StartPosition { get; private set; } = string.Empty;

    /// <summary>
    /// 先走方的座位号(0 = 红,1 = 黑)。
    /// <para>
    /// **它是数据,不是约定,而这是量出来的。** 1634 局残局里 **7 局是黑先走** ——
    /// 全部落在第一手(不是中途换手),结果是和棋 5 / 红胜 2,是正常的「黑先,红方
    /// 求和求胜」题。一条「第一手必须是红」的校验会拒掉这 7 道合法的题,**而报出来的
    /// 样子和「数据坏了」一模一样**。
    /// </para>
    /// </summary>
    public int FirstSeat { get; private set; }

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
    /// <param name="chapter">第几局,1 起;<c>0</c> 表示这部谱没有分组层。</param>
    /// <param name="orderInChapter">局内次序,不得为负。</param>
    /// <param name="title">原书局名,非空。</param>
    /// <param name="verdict">谱主的评断。</param>
    /// <param name="startPosition">起始局面,90 字符的行优先盘面串。</param>
    /// <param name="firstSeat">先走方座位,不得为负。</param>
    /// <param name="movesJson">着法数组的 JSON,非空。</param>
    /// <exception cref="ArgumentException">键、标题、起始局面或着法为空 / 形状不对。</exception>
    /// <exception cref="ArgumentOutOfRangeException">局号、次序或座位为负。</exception>
    public static XiangqiManualLine Create(
        string manualKey,
        int chapter,
        int orderInChapter,
        string title,
        ManualVerdict verdict,
        string startPosition,
        int firstSeat,
        string movesJson)
    {
        if (string.IsNullOrWhiteSpace(manualKey))
        {
            throw new ArgumentException("Manual key must be non-empty.", nameof(manualKey));
        }
        if (chapter < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(chapter), chapter,
                "Chapter must not be negative (0 means the manual has no chapter layer).");
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
        if (firstSeat < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(firstSeat), firstSeat, "First seat must not be negative.");
        }
        // 长度在这里挡住,而不是在播种器里:一个 89 字符的盘面串会让 `row * 9 + col`
        // 静静读到隔壁那一行,而那种错误画出来的是一个看着正常的、错的盘面。
        if (startPosition is null || startPosition.Length != BoardStringLength)
        {
            throw new ArgumentException(
                $"Start position must be exactly {BoardStringLength} characters (row-major, 10x9).",
                nameof(startPosition));
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
            Verdict = verdict,
            StartPosition = startPosition,
            FirstSeat = firstSeat,
            MovesJson = movesJson,
        };
    }
}
