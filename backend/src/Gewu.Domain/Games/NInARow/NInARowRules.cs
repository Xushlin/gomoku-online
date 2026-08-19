using Gewu.Domain.Entities;
using Gewu.Domain.Enums;
using Gewu.Domain.Idioms;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Games.NInARow;

/// <summary>
/// 「在 R×C 棋盘上先连 N 子者胜」—— 这一族棋种的通用规则。
/// <para>
/// 五子棋是 (15, 15, 5),一字棋是 (3, 3, 3)。**判胜算法一字不差**,只有三个数不同,
/// 所以不为后者另写一份实现:那等于复制一个算法只为了改两个常量。
/// </para>
/// <para>
/// 无状态,可安全地被并发的多个房间共享。
/// </para>
/// </summary>
public sealed class NInARowRules : INInARowRules
{
    /// <summary>
    /// 构造一个连 N 子棋种。
    /// </summary>
    /// <param name="gameKey">棋种键,非空。</param>
    /// <param name="rows">行数,必须为正。</param>
    /// <param name="cols">列数,必须为正。</param>
    /// <param name="winLength">连子长度,必须为正且不超过 <c>max(rows, cols)</c>。</param>
    /// <param name="supportsHumanVsHuman">
    /// 本棋种是否存在人类对手池。默认 <c>true</c> —— **没有**人类对手才是需要在调用处
    /// 写出理由的那一侧。
    /// </param>
    /// <param name="isRated">
    /// 本棋种是否结算 ELO。默认 <c>true</c> —— 一个棋种默认是算分的,
    /// **不**算分才是需要在调用处写出理由的那一侧。
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="gameKey"/> 为空,或违反不变量
    /// <c>isRated ⇒ supportsHumanVsHuman</c>。
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">尺寸或连子长度不合法。</exception>
    public NInARowRules(
        string gameKey,
        int rows,
        int cols,
        int winLength,
        bool supportsHumanVsHuman = true,
        bool isRated = true)
    {
        if (string.IsNullOrWhiteSpace(gameKey))
        {
            throw new ArgumentException("Game key must be non-empty.", nameof(gameKey));
        }

        // 不变量二:现有 ELO 是两人制的,所以计分的棋种必须正好两个座位。连 N 子永远是两个,
        // 所以这条在本类里恒真 —— 它写在这里是为了让**下一个**棋种照抄这个形状,而不是
        // 在注释里留一句"三人局记得别开分"。判断会过期,不变量不会。
        if (isRated && BoardSeats.SeatCount != 2)
        {
            throw new ArgumentException(
                "A rated game must have exactly two seats; ELO is a two-player rating.",
                nameof(isRated));
        }

        // 不变量:只能跟机器人下的棋种不存在有意义的评分 —— 机器人对局是计分的,
        // 所以那种阶梯排出来的是"谁刷弱档刷得多"而不是棋力。在**构造处**失败,而不是等到
        // 某个 handler 算出一个没人该看的分数。
        if (isRated && !supportsHumanVsHuman)
        {
            throw new ArgumentException(
                $"Game '{gameKey}' cannot be rated: it has no human-vs-human mode, so its only " +
                "opponents are bots and a ladder over it would rank grinding, not skill.",
                nameof(isRated));
        }

        // 尺寸与连子长度的合法性交给 Board 判 —— 那里已经有完整的校验,
        // 在这里复制一遍就等于有了两份真源。构造一块盘顺带把参数验了。
        _ = new Board(rows, cols, winLength);

        GameKey = gameKey;
        Rows = rows;
        Cols = cols;
        WinLength = winLength;
        SupportsHumanVsHuman = supportsHumanVsHuman;
        IsRated = isRated;
    }

    /// <inheritdoc />
    public string GameKey { get; }

    /// <inheritdoc />
    public int Rows { get; }

    /// <inheritdoc />
    public int Cols { get; }

    /// <inheritdoc />
    public int WinLength { get; }

    /// <inheritdoc />
    /// <inheritdoc />
    public int SeatCount => BoardSeats.SeatCount;

    public bool SupportsHumanVsHuman { get; }

    /// <inheritdoc />
    public bool IsRated { get; }

    /// <inheritdoc />
    public Board CreateBoard() => new(Rows, Cols, WinLength);

    /// <inheritdoc />
    public Board ReplayBoard(IReadOnlyList<PlayedMove> history)
    {
        var board = CreateBoard();
        foreach (var played in history)
        {
            board.PlaceStone(new Move(played.RequirePosition(), BoardSeats.ToStone(played.Seat)));
        }
        return board;
    }

    /// <inheritdoc />
    public MoveApplication Apply(
        IReadOnlyList<PlayedMove> history, MoveIntent intent, int seat)
    {
        // 座位 → 棋色的换算在这里,一次。内核已经不知道"黑白"是什么了。
        var side = BoardSeats.ToStone(seat);

        // 形状校验属于规则,不属于聚合根 —— 聚合根不知道哪些棋种走子。连 N 子是**落子类**:
        // 一步棋只有落点。带起点的载荷不是「走错了」,是「客户端发了一个这个棋种不存在的走法」。
        if (intent.From is not null)
        {
            throw new InvalidMoveException(
                $"'{GameKey}' places stones; a move must not carry an origin square.");
        }

        // 文本载荷在这里被挡下:一个成语落到连 N 子规则里,得到的是一句说得清的拒绝,
        // 而不是一个空引用。
        var to = intent.RequirePosition();

        if (!IsInBounds(to))
        {
            throw new InvalidMoveException(
                $"Position ({to.Row}, {to.Col}) is outside the bounds of '{GameKey}'.");
        }

        // 从历史重放。此前这段在 Game.ReplayBoard 里 —— 搬进来是本变更的要点:
        // 盘面语义整个属于规则,聚合根不该知道有一块 Board。
        var board = ReplayBoard(history);

        var result = board.PlaceStone(new Move(to, side));

        // 判胜时赢家就是走这一步的座位。连 N 子里这一点恒成立,但它是**本棋种**的性质:
        // 接缝本身允许"走完就输",所以这里要说出来,而不是让内核去猜。
        return result == GameResult.Decided
            ? MoveApplication.Won(seat)
            : new MoveApplication(result, null);
    }

    /// <summary>该坐标是否在本棋种界内。<c>Position</c> 只保证非负,上界在这里判。</summary>
    /// <param name="position">坐标。</param>
    private bool IsInBounds(Position position)
        => position.Row < Rows && position.Col < Cols;
}

/// <summary>平台内置棋种的规则常量。</summary>
public static class BuiltInGameRules
{
    /// <summary>五子棋:15×15 连五。与本变更前写死的常量完全一致。</summary>
    public static readonly INInARowRules Gomoku =
        new NInARowRules(GameKeys.Gomoku, 15, 15, 5);

    /// <summary>
    /// 一字棋:3×3 连三。**没有人人对战,因此不计分。**
    /// <para>
    /// 不计分不是一个独立的选择,而是不变量的后果:平台没有为一字棋提供人人对战入口
    /// (它只有 <c>/g/tictactoe</c> 这一个人机页面),于是它唯一的对手是机器人,而机器人
    /// 对局是计分的 —— 一字棋阶梯的榜首会是刷 Easy 档最多的人。构造器会拒绝
    /// <c>supportsHumanVsHuman: false, isRated: true</c> 的组合,所以这件事不靠谁记得。
    /// </para>
    /// <para>
    /// 它将来获得人人对战时,翻 <c>supportsHumanVsHuman</c> 会把评分从"禁止"变成"允许";
    /// 开不开是那时的一个独立决定。顺带一提,即便开了,一字棋是**已解游戏**(双方稍具水平
    /// 即必和,<c>TicTacToeHardAi</c> 不可战胜),阶梯的分辨力也很有限 —— 但那时它至少
    /// 量的是人,而不是刷机器人的次数。
    /// </para>
    /// <para>
    /// <b>它现在是注册表里唯一 <c>SupportsHumanVsHuman == false</c> 的棋种。</b>
    /// <c>enable-xiangqi-human-play</c> 给象棋开了人人对战,而一字棋**故意**没跟着翻:上面那句
    /// "已解游戏"就是理由 —— 真人对战没有可下的东西。
    /// </para>
    /// <para>
    /// 这件事有一个不显眼的副作用值得记下:好几条遍历注册表的测试断言"放行与拒绝两种结果
    /// MUST 都出现过",而在象棋翻过去之后,**一字棋是拒绝那一侧唯一的样本**。哪天它也开了
    /// 人人对战,那些断言会立刻变红 —— 那是想要的:它们会告诉你"这条遍历现在只走一边了",
    /// 而不是全绿地什么都不验。
    /// </para>
    /// <para>
    /// 这里没有第二份判胜实现,整个棋种就是这三个数 —— 这正是 <c>NInARowRules</c>
    /// 存在的理由,也是 <c>add-game-rules-registry</c> 那句"一个类加一处注册"
    /// 第一次被真正验证。
    /// </para>
    /// </summary>
    public static readonly INInARowRules TicTacToe = new NInARowRules(
        GameKeys.TicTacToe, 3, 3, 3, supportsHumanVsHuman: false, isRated: false);

    /// <summary>
    /// 中国象棋。规则整个在 <see cref="Xiangqi.XiangqiRules"/> 里 —— 它**不是**连 N 子棋种,
    /// 所以不实现 <see cref="INInARowRules"/>。
    /// <para>
    /// 今天没有任何进入象棋对局的入口(没有 AI、没有人人对战),因此
    /// <c>SupportsHumanVsHuman == false</c> 且不计分。见该类的说明。
    /// </para>
    /// </summary>
    public static readonly IGameRules Xiangqi = new Xiangqi.XiangqiRules();

    /// <summary>
    /// **全部内置棋种,唯一的一份清单。** DI 注册与「遍历注册表」的不变量测试都从这里取。
    /// <para>
    /// 加它是因为此前有两份:<c>DependencyInjection</c> 里逐个 <c>AddSingleton</c>,
    /// 外加测试里手写的 <c>AllBuiltInRules() =&gt; { Gomoku, TicTacToe }</c>。
    /// 后者的注释写着「遍历注册表…将来加中国象棋它自动被覆盖」——**那句话是假的**,
    /// 数据源是手写的,象棋会静静绕过 <c>IsRated ⇒ SupportsHumanVsHuman</c> 那条测试。
    /// 那正是它自己预言的失效方式,只是它预言错了自己的机制。
    /// </para>
    /// <para>
    /// 它此前是一个静态列表。成语接龙需要词典,而词典不能在类型初始化时加载,于是它变成了
    /// 一个**函数**。诱惑是把成语接龙单独注册到 DI、把这份清单留在原地 —— 那正是上面那段
    /// 描述的缺陷再来一次:清单之外的棋种会同时从 <c>IsRated ⇒ SupportsHumanVsHuman</c>
    /// 与建房能力校验两条遍历里静静溜过去。代价是每个调用方都得说明它拿什么来描述这个平台,
    /// 而那正是诚实的形状。
    /// </para>
    /// </summary>
    /// <param name="idioms">成语词典 —— 成语接龙的规则需要它。</param>
    public static IReadOnlyList<IGameRules> All(IIdiomLexicon idioms) =>
        [Gomoku, TicTacToe, Xiangqi, new IdiomChain.IdiomChainRules(idioms)];
}
