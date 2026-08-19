using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Gewu.Domain.Enums;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Games.Abstractions;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Games.Doudizhu;

/// <summary>
/// 斗地主的规则 —— 平台第一个三座位、有隐藏信息、按分结算的棋种。
/// <para>
/// 牌、牌型、压牌、发牌、计分都在 <c>add-doudizhu-cards</c> 里,并且是纯函数。本类做的是把它们
/// 接到内核上:阶段、叫分、出牌、过牌、超时兜底。
/// </para>
/// <para>
/// <b>无状态。</b> 同一个实例被并发的多个房间共享,所以每次 <c>Apply</c> 都从
/// <see cref="MatchState"/> 重建局面(见 <see cref="DoudizhuTable"/>)。
/// </para>
/// </summary>
public sealed class DoudizhuRules : IGameRules, IDealtGameRules, ITimeoutFallbackRules, IPerSeatViewRules
{
    /// <inheritdoc />
    public string GameKey => GameKeys.Doudizhu;

    /// <inheritdoc />
    public int SeatCount => DoudizhuDeal.SeatCount;

    /// <inheritdoc />
    public bool SupportsHumanVsHuman => true;

    /// <summary>
    /// **不计分,而理由是结构性的。**
    /// <para>
    /// ELO 是**两人**模型,而斗地主按分结算(<see cref="DoudizhuSettlement"/> 给出三个座位各得
    /// 多少)。一个按分的阶梯是**另一条榜** —— 与俄罗斯方块的分数榜和 ELO 榜分开是同一件事。
    /// </para>
    /// <para>
    /// 这也让 <c>IsRated ⇒ SeatCount == 2</c> 那条不变量保持成立,不需要为斗地主开例外。
    /// **一个需要开例外的不变量已经不是不变量了。**
    /// </para>
    /// </summary>
    public bool IsRated => false;

    /// <summary>
    /// 发一副牌,并把它编码成本局的服务端侧设置。
    /// <para>
    /// 这个字符串**就是三家的底牌**,所以它 MUST NOT 出现在任何 DTO 上 —— 与成语纵横
    /// 「答案不出服务端」同一条平台规则。
    /// </para>
    /// </summary>
    /// <param name="seed">开局种子,由 Application 层的 <c>ISeedProvider</c> 给。</param>
    public string CreateSetup(int seed) => DoudizhuDeal.FromSeed(seed).Encode();

    /// <inheritdoc />
    public MoveApplication Apply(MatchState state, MoveIntent intent, int seat)
    {
        // 形状校验:斗地主没有盘面,一步棋是文本。带坐标的载荷在这里被挡下。
        var move = DoudizhuMove.Parse(intent.RequireText());
        var table = DoudizhuTable.Reconstruct(state);

        return table.Phase switch
        {
            DoudizhuPhase.Bidding => ApplyBid(table, move, seat),
            DoudizhuPhase.Playing => ApplyPlay(table, move, seat),
            _ => throw new InvalidMoveException("This game has already ended."),
        };
    }

    /// <summary>叫分阶段的一步。</summary>
    private MoveApplication ApplyBid(DoudizhuTable table, DoudizhuMove move, int seat)
    {
        if (move.Kind != DoudizhuMoveKind.Bid)
        {
            throw new InvalidMoveException(
                "The landlord has not been decided yet; this turn is a bid.");
        }

        if (move.Bid != DoudizhuMove.NoBid && move.Bid < table.MinimumRaise)
        {
            throw new InvalidMoveException(
                $"A bid must beat {table.BaseScore}; bid at least {table.MinimumRaise}, or pass with 0.");
        }

        var bidsAfter = table.BidsMade + 1;
        var takesIt = move.Bid > table.BaseScore;
        var landlord = takesIt ? seat : table.Landlord;

        // 叫 3 分立即结束叫分 —— 没人压得过,再问一遍是浪费一次交互。
        if (move.Bid == DoudizhuScoring.MaxBaseScore)
        {
            return MoveApplication.OngoingWithTurn(seat);
        }

        if (bidsAfter < DoudizhuDeal.SeatCount)
        {
            return MoveApplication.Ongoing();
        }

        // 三家各叫过一次。没人叫 → 流局(和局);否则最高者当地主并先出牌。
        return landlord is int who
            ? MoveApplication.OngoingWithTurn(who)
            : MoveApplication.Drawn();
    }

    /// <summary>出牌阶段的一步。</summary>
    private MoveApplication ApplyPlay(DoudizhuTable table, DoudizhuMove move, int seat)
    {
        if (move.Kind == DoudizhuMoveKind.Bid)
        {
            throw new InvalidMoveException("The bidding has ended; this turn is a play or a pass.");
        }

        if (move.Kind == DoudizhuMoveKind.Pass)
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

        var combo = CardCombo.Recognise(move.Cards)
            ?? throw new InvalidMoveException("Those cards are not a legal combination.");

        if (table.Current is { } onTable && !combo.Beats(onTable))
        {
            throw new InvalidMoveException(
                $"A {combo.Kind} of {combo.Key} does not beat the {onTable.Kind} on the table.");
        }

        // 打完最后一张就赢了 —— 赢家是**这个座位**,不是"农民方"。`WinnerUserId` 只能装一个人,
        // 而两名农民一起赢装不进去;客户端从叫分历史里知道谁是地主,自己能说出"农民赢了"。
        var remaining = table.HandOf(seat).Count - move.Cards.Count;
        return remaining == 0
            ? MoveApplication.Won(seat)
            : MoveApplication.Ongoing();
    }

    /// <summary>
    /// 超时替这个座位走一步 —— 托管。
    /// <para>
    /// 叫分阶段不叫;出牌阶段能过就过,首出则出**手上最小的一张单牌**。两条都严格推进:
    /// 叫分最多三手就结束,而出牌时每次至少让一张牌离开某只手。单牌永远是合法牌型,所以
    /// "出最小的一张"在首出时总是可行的。
    /// </para>
    /// </summary>
    /// <param name="state">走子历史 + 服务端侧的发牌 —— 手牌就在后者里。</param>
    /// <param name="seat">超时的座位号。</param>
    public MoveIntent MoveOnTimeout(MatchState state, int seat)
    {
        var table = DoudizhuTable.Reconstruct(state);

        if (table.Phase == DoudizhuPhase.Bidding)
        {
            return MoveIntent.Say(DoudizhuMove.Bidding(DoudizhuMove.NoBid).Encode());
        }

        if (table.Current is not null)
        {
            return MoveIntent.Say(DoudizhuMove.Passing().Encode());
        }

        // 首出不能过牌。手牌按大小升序,所以第一张就是最小的。
        var smallest = table.HandOf(seat)[0];
        return MoveIntent.Say(DoudizhuMove.Playing([smallest]).Encode());
    }

    /// <summary>
    /// 序列化选项 —— camelCase,与平台上其它 JSON 载荷一致。
    /// <para>
    /// **不放宽转义**,而这是一个基于内容的判断:牌的字母表是 <c>A-Za-z@#</c>,全是 ASCII,
    /// 默认转义器不会碰它们。<c>compact-puzzle-artefacts</c> 那里必须放宽,是因为它存的是汉字
    /// (成语 / 曹操),默认转义会把每个字变成六个字符 —— **比它省下的空白还大**。
    /// 同一个选项在那里必需、在这里多余,理由都在内容上。
    /// </para>
    /// </summary>
    private static readonly JsonSerializerOptions ViewJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <inheritdoc />
    public string ViewFor(MatchState state, int? seat)
    {
        var table = DoudizhuTable.Reconstruct(state);

        // 自己的牌只在"真占着一个座位"时给。围观者与还没入座的人拿到空串 ——
        // 不是"某一家的牌",更不是三家的牌。
        var myHand = seat is int s && s >= 0 && s < SeatCount
            ? Card.Encode(table.HandOf(s))
            : string.Empty;

        // 底牌:定下地主之后才公开。叫分阶段它 MUST 为 null —— 那时它还没被翻开,
        // 而它恰恰决定了谁值得抢地主,所以早给一步就是给了不该有的信息。
        var kitty = table.Landlord is null ? null : Card.Encode(table.Kitty);

        var view = new DoudizhuSeatView(
            Phase: table.Phase.ToString(),
            Landlord: table.Landlord,
            BaseScore: table.BaseScore,
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
