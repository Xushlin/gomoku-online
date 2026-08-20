using System;
using System.Collections.Generic;
using System.Linq;
using Gewu.Domain.Exceptions;
using Gewu.Domain.Games.Cards;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Games.Wakeng;

/// <summary>一局挖坑此刻在哪个阶段。</summary>
public enum WakengPhase
{
    /// <summary>叫分。</summary>
    Bidding = 0,

    /// <summary>出牌。</summary>
    Playing = 1,

    /// <summary>已结束 —— 有人出完了牌。</summary>
    Finished = 2,
}

/// <summary>
/// 一局挖坑的当前局面,从 <see cref="MatchState"/> 重建出来。
/// <para>
/// <b>规则因此无状态</b> —— 同一个规则实例被并发的多个房间共享,这是 <c>IGameRules</c> 的硬要求。
/// 每步 O(n) 重放在一局 ≤ 100 手的量级上无关紧要。
/// </para>
/// <para>
/// 重建时**不再校验历史里的每一步** —— 它们当初就是这么被接受的。与 <c>DoudizhuTable</c> /
/// <c>XiangqiRules.Replay</c> 同一个约定。
/// </para>
/// </summary>
public sealed class WakengTable
{
    private readonly List<Card>[] _hands;

    private WakengTable(List<Card>[] hands, int firstBidderSeat, Card firstBidderCard)
    {
        _hands = hands;
        FirstBidderSeat = firstBidderSeat;
        FirstBidderCard = firstBidderCard;
    }

    /// <summary>此刻在哪个阶段。</summary>
    public WakengPhase Phase { get; private set; } = WakengPhase.Bidding;

    /// <summary>
    /// **首叫者**的座位 —— 拿底牌前持最小 ♣ 的那个人。
    /// <para>
    /// 它既是首叫权也是**首出权**(原文:「获得首叫权和首出权」),而那与斗地主相反 ——
    /// 那边地主先出。所以叫分结束之后出手权回到这个座位,而不是给挖坑者。
    /// </para>
    /// </summary>
    public int FirstBidderSeat { get; }

    /// <summary>
    /// 首叫者亮的那张 ♣。**它是公开的** —— 按规则本来就是明示的(它决定了谁首叫首出),
    /// 而服务端算得出,客户端不该自己猜。
    /// </summary>
    public Card FirstBidderCard { get; }

    /// <summary>挖坑者的座位号;叫分尚未结束时为 <c>null</c>。</summary>
    public int? Digger { get; private set; }

    /// <summary>叫分 —— 目前的最高分;还没人叫过时为 <c>0</c>。</summary>
    public int Bid { get; private set; }

    /// <summary>已经叫过几次(含不挖)。</summary>
    public int BidsMade { get; private set; }

    /// <summary>
    /// 桌上等着被压的那一手;**为 <c>null</c> 表示自由首出**(叫分刚结束,或连续两家过牌之后)。
    /// </summary>
    public WakengCombo? Current { get; private set; }

    /// <summary>桌上那一手是谁打的。</summary>
    public int? CurrentSeat { get; private set; }

    /// <summary>
    /// 桌上那一手**具体是哪几张**;自由首出时为空。
    /// <para>
    /// <see cref="Current"/> 只带压牌需要的三样(牌型、张数、最大那组的强弱),所以它不带牌;
    /// 而屏幕上要画出那几张。
    /// </para>
    /// </summary>
    public IReadOnlyList<Card> CurrentCards { get; private set; } = [];

    /// <summary>赢家座位号;还没结束时为 <c>null</c>。</summary>
    public int? Winner { get; private set; }

    /// <summary>
    /// 底牌 4 张 —— 比斗地主多一张。
    /// <para>
    /// 它**定下挖坑者之后是公开的**:挖坑者当众把它收进手里,而这四张是什么,三家都该知道。
    /// 裁剪"什么时候能看"是 <see cref="WakengRules.ViewFor"/> 的事,不是这里的事 ——
    /// 局面对象说的是"这一局是什么样",视图说的是"谁看得到"。
    /// </para>
    /// </summary>
    public IReadOnlyList<Card> Kitty { get; private set; } = [];

    /// <summary>
    /// 某个座位**还剩**哪些牌,按 <c>Card</c> 的自然序 —— 也就是**编码顺序**(3、4、…、K、A、2),
    /// 而 <b>不是</b>挖坑的强弱顺序。
    /// <para>
    /// 这两者在挖坑里不是一回事:编码顺序恰好是斗地主的大小顺序,而挖坑是 <c>3 &gt; 2 &gt; A</c>。
    /// 要「最弱的一张」得走 <see cref="WakengRank.Strength"/>,MUST NOT 取第 0 张 ——
    /// 那会拿到手上有 3 时最强的那张。<c>WakengRules.MoveOnTimeout</c> 上记着这条踩坑。
    /// </para>
    /// </summary>
    /// <param name="seat">座位号。</param>
    public IReadOnlyList<Card> HandOf(int seat) => _hands[seat];

    /// <summary>叫分尚未结束时,下一次叫分至少要多少分才算合法(<c>0</c> 是不挖,永远合法)。</summary>
    public int MinimumRaise => Bid + 1;

    /// <summary>这个座位手上是否**全部持有**这些牌。</summary>
    /// <param name="seat">座位号。</param>
    /// <param name="cards">要出的牌。</param>
    public bool Holds(int seat, IReadOnlyList<Card> cards)
    {
        // 一副牌里每张牌只有一张,所以手牌是一个集合 —— 包含关系就是集合包含。
        // 同一张牌出两次在 Card.DecodeMany 里已经被拒了。
        var hand = _hands[seat];
        return cards.All(hand.Contains);
    }

    /// <summary>从对局状态重建局面。</summary>
    /// <param name="state">走子历史 + 服务端侧的发牌。</param>
    /// <exception cref="InvalidMoveException">这一局没有发牌 —— 那是一条损坏的记录。</exception>
    public static WakengTable Reconstruct(MatchState state)
    {
        if (state.Setup is null)
        {
            // 到不了这里:Room.JoinAsPlayer 在开局那一刻就拒绝了没有设置的挖坑。
            // 留着是因为一条损坏的记录该大声坏掉,而不是发一手空牌。
            throw new InvalidMoveException("This wakeng game has no deal recorded.");
        }

        var deal = WakengDeal.Decode(state.Setup);
        var hands = new List<Card>[WakengDeal.SeatCount];
        for (var seat = 0; seat < WakengDeal.SeatCount; seat++)
        {
            hands[seat] = [.. deal.Hands[seat]];
        }

        var (firstSeat, firstCard) = deal.FirstBidder();
        var table = new WakengTable(hands, firstSeat, firstCard) { Kitty = deal.Kitty };
        var passes = 0;

        foreach (var played in state.History)
        {
            var move = WakengMove.Parse(played.Text
                ?? throw new InvalidMoveException("A wakeng move must carry text."));

            if (table.Phase == WakengPhase.Bidding)
            {
                table.ApplyHistoricBid(move, played.Seat, deal.Kitty);
                continue;
            }

            switch (move.Kind)
            {
                case WakengMoveKind.Pass:
                    passes++;
                    if (passes == WakengDeal.SeatCount - 1)
                    {
                        // 另外两家都过了,桌面清空 —— 轮到的正是打出那一手的人。
                        table.Current = null;
                        table.CurrentSeat = null;
                        table.CurrentCards = [];
                        passes = 0;
                    }
                    break;

                case WakengMoveKind.Play:
                    foreach (var card in move.Cards)
                    {
                        hands[played.Seat].Remove(card);
                    }
                    table.Current = WakengCombo.TryRecognise(move.Cards, out var combo)
                        ? combo
                        : throw new InvalidMoveException(
                            "A recorded play is not a legal combination.");
                    table.CurrentSeat = played.Seat;
                    table.CurrentCards = move.Cards;
                    passes = 0;
                    if (hands[played.Seat].Count == 0)
                    {
                        table.Phase = WakengPhase.Finished;
                        table.Winner = played.Seat;
                    }
                    break;

                default:
                    throw new InvalidMoveException(
                        "A bid cannot appear after the bidding has ended.");
            }
        }

        return table;
    }

    /// <summary>重放一次历史里的叫分。</summary>
    private void ApplyHistoricBid(WakengMove move, int seat, IReadOnlyList<Card> kitty)
    {
        if (move.Kind != WakengMoveKind.Bid)
        {
            throw new InvalidMoveException("Only bids can appear during the bidding phase.");
        }

        BidsMade++;
        if (move.Bid > Bid)
        {
            Bid = move.Bid;
            Digger = seat;
        }

        var everyoneBid = BidsMade == WakengDeal.SeatCount;
        var unbeatable = move.Bid == WakengScoring.MaxBid;

        if (!unbeatable && !everyoneBid)
        {
            return;
        }

        if (Digger is null)
        {
            // **三家都说不挖 —— 首叫者兜底,1 倍。** 原文没写这种情况,这是用户定的一处判断,
            // 而它不是重新发牌:重发需要在同一个 Game 上换第二份 Setup,而「发牌在开局那一刻
            // 定下、之后不变」是重放与「服务端侧设置」这个概念的地基。
            //
            // 于是**挖坑没有流局** —— 斗地主三家不叫是和局,挖坑不是。
            Digger = FirstBidderSeat;
            Bid = WakengScoring.ForcedBid;
        }

        Phase = WakengPhase.Playing;
        _hands[Digger.Value].AddRange(kitty);
        _hands[Digger.Value].Sort();
    }
}
