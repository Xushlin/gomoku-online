using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.Games.Cards;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Games.Wakeng;

/// <summary>
/// 挖坑的规则 —— 平台第一个**先手由发牌决定**的棋种。
/// <para>
/// 牌、大小、牌型、压牌、发牌、首叫权、计分都在 <c>add-wakeng-cards</c> 里,并且是纯函数。
/// 本类做的是把它们接到内核上:阶段、叫分、出牌、过牌、按座位可见、超时兜底。
/// </para>
/// <para>
/// <b>它实现五个接口</b>,而其中 <see cref="IFirstSeatRules"/> 是它第一个真实现:五个现有棋种的
/// 先手都是**约定**(谁坐 0 号谁先),挖坑不是 —— **持最小 ♣ 的人首叫且首出**,而那是发牌
/// 决定的。
/// </para>
/// <para>
/// <b>无状态。</b> 同一个实例被并发的多个房间共享,所以每次 <c>Apply</c> 都从
/// <see cref="MatchState"/> 重建局面(见 <see cref="WakengTable"/>)。
/// </para>
/// </summary>
public sealed class WakengRules
    : IGameRules, IDealtGameRules, IFirstSeatRules, ITimeoutFallbackRules, IPerSeatViewRules
{
    /// <inheritdoc />
    public string GameKey => GameKeys.Wakeng;

    /// <inheritdoc />
    public int SeatCount => WakengDeal.SeatCount;

    /// <summary>
    /// 平台为它开人人对战入口。
    /// <para>
    /// 这是一个**结构性事实**而不是判断:<c>enforce-human-vs-human</c> 把这个字段定义成
    /// 「建房端点收不收」,所以 <c>POST /api/rooms</c> 一旦接受这个棋种,声明就得跟上。
    /// </para>
    /// </summary>
    public bool SupportsHumanVsHuman => true;

    /// <summary>
    /// **不计分,而理由是结构性的** —— 与斗地主逐字相同。
    /// <para>
    /// ELO 是**两人**模型,而挖坑按分结算(<see cref="WakengScoring.Settle"/> 给出三个座位各得
    /// 多少)。一个按分的阶梯是**另一条榜** —— 与俄罗斯方块的分数榜和 ELO 榜分开是同一件事。
    /// </para>
    /// <para>
    /// 这也让 <c>IsRated ⇒ SeatCount == 2</c> 那条不变量保持成立,不需要为挖坑开例外。
    /// **一个需要开例外的不变量已经不是不变量了。**
    /// </para>
    /// <para>
    /// 挖坑**没有 AI**,而那不需要任何新代码:不在 <c>BuiltInGameAis.All</c> 里,
    /// <c>enforce-ai-availability</c> 就会让 <c>POST /api/rooms/ai</c> 返回 400。
    /// </para>
    /// </summary>
    public bool IsRated => false;

    /// <summary>
    /// 发一副牌(52 张无王,16/16/16 + 4),并把它编码成本局的服务端侧设置。
    /// <para>
    /// 这个字符串**就是三家的底牌**,所以它 MUST NOT 出现在任何 DTO 上。
    /// </para>
    /// </summary>
    /// <param name="seed">开局种子,由 Application 层的 <c>ISeedProvider</c> 给。</param>
    public string CreateSetup(int seed) => WakengDeal.FromSeed(seed).Encode();

    /// <summary>
    /// 本局谁先走 —— **持最小 ♣ 的座位**。
    /// <para>
    /// 它每局都不同,而那正是重点:把发牌旋转成「最小 ♣ 总在 0 号」在统计上等价、在体验上
    /// 不等价 —— 那样同一个人每一局都先叫。
    /// </para>
    /// </summary>
    /// <param name="state">开局那一刻的对局状态;此时只有发牌。</param>
    public int FirstSeat(MatchState state) => WakengTable.Reconstruct(state).FirstBidderSeat;

    /// <inheritdoc />
    public MoveApplication Apply(MatchState state, MoveIntent intent, int seat)
    {
        // 形状校验:挖坑没有盘面,一步棋是文本。带坐标的载荷在这里被挡下。
        var move = WakengMove.Parse(intent.RequireText());
        var table = WakengTable.Reconstruct(state);

        return table.Phase switch
        {
            WakengPhase.Bidding => ApplyBid(table, move, seat),
            WakengPhase.Playing => ApplyPlay(table, move, seat),
            _ => throw new InvalidMoveException("This game has already ended."),
        };
    }

    /// <summary>叫分阶段的一步。</summary>
    private static MoveApplication ApplyBid(WakengTable table, WakengMove move, int seat)
    {
        if (move.Kind != WakengMoveKind.Bid)
        {
            throw new InvalidMoveException(
                "The digger has not been decided yet; this turn is a bid.");
        }

        if (move.Bid != WakengMove.NoBid && move.Bid < table.MinimumRaise)
        {
            throw new InvalidMoveException(
                $"A bid must beat {table.Bid}; bid at least {table.MinimumRaise}, or pass with 0.");
        }

        var bidsAfter = table.BidsMade + 1;
        var unbeatable = move.Bid == WakengScoring.MaxBid;

        if (!unbeatable && bidsAfter < WakengDeal.SeatCount)
        {
            return MoveApplication.Ongoing();
        }

        // **叫分结束 —— 出手权回到首叫者,不给挖坑者。**
        //
        // 原文:「持有 ♣4(拿底牌前最小的 ♣ 牌)的玩家获得首叫权和首出权」。这与斗地主相反,
        // 那边地主先出。
        //
        // 两条结束路径都**显式**指名那个座位。三家各叫一次时自然轮转恰好也落在首叫者身上
        // (3 个座位、3 次叫分),而那是一个**巧合**;有人叫 3 时自然轮转会给错人。
        // 依赖那个巧合的实现会在「有人叫 3」那条路径上把出手权交给下一家。
        //
        // 三家都不挖时首叫者兜底 1 倍(在 WakengTable.ApplyHistoricBid 里做),所以
        // **这里永远不会返回 Drawn()** —— 挖坑没有流局。
        return MoveApplication.OngoingWithTurn(table.FirstBidderSeat);
    }

    /// <summary>出牌阶段的一步。</summary>
    private static MoveApplication ApplyPlay(WakengTable table, WakengMove move, int seat)
    {
        if (move.Kind == WakengMoveKind.Bid)
        {
            throw new InvalidMoveException("The bidding has ended; this turn is a play or a pass.");
        }

        if (move.Kind == WakengMoveKind.Pass)
        {
            if (table.Current is null)
            {
                throw new InvalidMoveException(
                    "The table is empty; you lead and must play, not pass.");
            }
            return MoveApplication.Ongoing();
        }

        if (!table.Holds(seat, move.Cards))
        {
            throw new InvalidMoveException("You do not hold every card in that play.");
        }

        if (!WakengCombo.TryRecognise(move.Cards, out var combo))
        {
            throw new InvalidMoveException("Those cards are not a legal combination.");
        }

        if (table.Current is { } onTable && !combo.Beats(onTable))
        {
            // 挖坑**没有炸弹**,所以跟牌必须同型同张数 —— 一个四头压不住桌上的顺子。
            throw new InvalidMoveException(
                $"A {combo.Kind} of {combo.CardCount} cards does not beat the {onTable.Kind} on the table.");
        }

        // 打完最后一张就赢了 —— 赢家是**这个座位**,不是「联手方」。`WinnerUserId` 只装得下
        // 一个人,而两名联手方一起赢装不进去;客户端从叫分历史里知道谁是挖坑者,自己能说出
        // 「联手赢了」。
        var remaining = table.HandOf(seat).Count - move.Cards.Count;
        return remaining == 0
            ? MoveApplication.Won(seat)
            : MoveApplication.Ongoing();
    }

    /// <summary>
    /// 超时替这个座位走一步 —— 托管。
    /// <para>
    /// 叫分阶段不挖;出牌阶段能过就过,首出则出**手上最弱的一张单牌**(按挖坑的强弱,
    /// 不是按 <c>Card</c> 的自然序 —— 见方法体里的说明)。单牌永远是合法牌型,所以
    /// 「出最弱的一张」在首出时总是可行的。
    /// </para>
    /// <para>
    /// <b>它推进对局,而挖坑的终止论证与斗地主不同。</b> 斗地主三家都被托管的结果是流局,
    /// 三步就终局;挖坑三家都不挖会**进入出牌阶段并继续**,所以推进靠的是出牌阶段每一次兜底
    /// 都让一张牌离开某只手 —— 牌只会变少。
    /// </para>
    /// </summary>
    /// <param name="state">走子历史 + 服务端侧的发牌 —— 手牌就在后者里。</param>
    /// <param name="seat">超时的座位号。</param>
    public MoveIntent MoveOnTimeout(MatchState state, int seat)
    {
        var table = WakengTable.Reconstruct(state);

        if (table.Phase == WakengPhase.Bidding)
        {
            return MoveIntent.Say(WakengMove.Bidding(WakengMove.NoBid).Encode());
        }

        if (table.Current is not null)
        {
            return MoveIntent.Say(WakengMove.Passing().Encode());
        }

        // 首出不能过牌,所以出手上**最弱**的一张。
        //
        // **这里 MUST NOT 写 `HandOf(seat)[0]`,而这是一条真缺陷,不是洁癖。** 手牌按
        // `Card` 的自然序排,而那是**编码**顺序(3、4、…、K、A、2)—— 它恰好就是斗地主的
        // 大小顺序,所以斗地主那边「第一张就是最小的」是对的。挖坑是 `3 > 2 > A > … > 4`,
        // 于是 `[0]` 在手上有 3 的时候是**最强**的一张:托管会替他把最好的牌打掉。
        //
        // 这与 `hoist-card-model` 修 `CardRank` 注释(「数值就是大小顺序」只对斗地主成立)
        // 是同一个巧合在**上面一层**又咬了一次。所以这里显式按挖坑的强弱取最小。
        var weakest = table.HandOf(seat).MinBy(WakengRank.Strength);
        return MoveIntent.Say(WakengMove.Playing([weakest]).Encode());
    }

    /// <summary>
    /// 序列化选项 —— camelCase,与平台上其它 JSON 载荷一致。
    /// <para>
    /// **不放宽转义**:牌的字母表是 <c>A-Za-z@#</c>,全是 ASCII,默认转义器不会碰它们。
    /// (<c>compact-puzzle-artefacts</c> 那里必须放宽,是因为它存的是汉字。)
    /// </para>
    /// </summary>
    private static readonly JsonSerializerOptions ViewJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <inheritdoc />
    public string ViewFor(MatchState state, int? seat)
    {
        var table = WakengTable.Reconstruct(state);

        // 自己的牌只在「真占着一个座位」时给。围观者与还没入座的人拿到空串 ——
        // 不是「某一家的牌」,更不是三家的牌。越界的座位号同理:一个坏的座位号 MUST NOT
        // 变成别人的牌。
        var myHand = seat is int s && s >= 0 && s < SeatCount
            ? Card.Encode(table.HandOf(s))
            : string.Empty;

        // 底牌:定下挖坑者之后才公开。叫分阶段它 MUST 为 null —— 那时它还没被翻开,
        // 而它恰恰决定了这一局值不值得挖。
        var kitty = table.Digger is null ? null : Card.Encode(table.Kitty);

        var view = new WakengSeatView(
            Phase: table.Phase.ToString(),
            FirstBidder: table.FirstBidderSeat,
            FirstBidderCard: table.FirstBidderCard.Encode().ToString(),
            Digger: table.Digger,
            Bid: table.Bid,
            BidsMade: table.BidsMade,
            MyHand: myHand,
            HandCounts: Enumerable.Range(0, SeatCount).Select(i => table.HandOf(i).Count).ToList(),
            Kitty: kitty,
            TableSeat: table.CurrentSeat,
            TableCards: table.Current is null ? null : Card.Encode(table.CurrentCards),
            Winner: table.Winner);

        return JsonSerializer.Serialize(view, ViewJson);
    }
}
