using System;
using System.Collections.Generic;
using System.Linq;
using Gewu.Domain.Exceptions;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Games.Doudizhu;

/// <summary>一局斗地主此刻在哪个阶段。</summary>
public enum DoudizhuPhase
{
    /// <summary>叫分。</summary>
    Bidding = 0,

    /// <summary>出牌。</summary>
    Playing = 1,

    /// <summary>已结束(有人出完了牌,或三家都不叫的流局)。</summary>
    Finished = 2,
}

/// <summary>
/// 一局斗地主的当前局面,从 <see cref="MatchState"/> 重建出来。
/// <para>
/// <b>规则因此无状态</b> —— 同一个规则实例被并发的多个房间共享,这是 <c>IGameRules</c> 的硬要求。
/// 每步 O(n) 重放在一局 ≤ 100 手的量级上无关紧要,与棋盘类棋种每步重放盘面是同一条理由。
/// </para>
/// <para>
/// 重建时**不再校验历史里的每一步** —— 它们当初就是这么被接受的。与 <c>XiangqiRules.Replay</c>
/// 的约定一致。
/// </para>
/// </summary>
public sealed class DoudizhuTable
{
    private readonly List<Card>[] _hands;

    private DoudizhuTable(List<Card>[] hands)
    {
        _hands = hands;
    }

    /// <summary>此刻在哪个阶段。</summary>
    public DoudizhuPhase Phase { get; private set; } = DoudizhuPhase.Bidding;

    /// <summary>地主的座位号;叫分尚未结束、或流局时为 <c>null</c>。</summary>
    public int? Landlord { get; private set; }

    /// <summary>底分 —— 叫分里的最高分;还没定下来时为 <c>0</c>。</summary>
    public int BaseScore { get; private set; }

    /// <summary>已经叫过几次(含不叫)。</summary>
    public int BidsMade { get; private set; }

    /// <summary>
    /// 桌上等着被压的那一手;**为 <c>null</c> 表示自由首出**(开局第一手,或连续两家过牌之后)。
    /// </summary>
    public CardCombo? Current { get; private set; }

    /// <summary>桌上那一手是谁打的。</summary>
    public int? CurrentSeat { get; private set; }

    /// <summary>赢家座位号;还没结束或流局时为 <c>null</c>。</summary>
    public int? Winner { get; private set; }

    /// <summary>某个座位**还剩**哪些牌,按大小升序。</summary>
    /// <param name="seat">座位号。</param>
    public IReadOnlyList<Card> HandOf(int seat) => _hands[seat];

    /// <summary>从对局状态重建局面。</summary>
    /// <param name="state">走子历史 + 服务端侧的发牌。</param>
    /// <exception cref="InvalidMoveException">这一局没有发牌 —— 那是一条损坏的记录。</exception>
    public static DoudizhuTable Reconstruct(MatchState state)
    {
        if (state.Setup is null)
        {
            // 到不了这里:Room.JoinAsPlayer 在开局那一刻就拒绝了没有设置的斗地主。
            // 留着是因为一条损坏的记录该大声坏掉,而不是发一手空牌。
            throw new InvalidMoveException("This doudizhu game has no deal recorded.");
        }

        var deal = DoudizhuDeal.Decode(state.Setup);
        var hands = new List<Card>[DoudizhuDeal.SeatCount];
        for (var seat = 0; seat < DoudizhuDeal.SeatCount; seat++)
        {
            hands[seat] = [.. deal.Hands[seat]];
        }

        var table = new DoudizhuTable(hands);
        var passes = 0;

        foreach (var played in state.History)
        {
            var move = DoudizhuMove.Parse(played.Text
                ?? throw new InvalidMoveException("A doudizhu move must carry text."));

            if (table.Phase == DoudizhuPhase.Bidding)
            {
                table.ApplyHistoricBid(move, played.Seat, deal.Kitty);
                continue;
            }

            switch (move.Kind)
            {
                case DoudizhuMoveKind.Pass:
                    passes++;
                    if (passes == DoudizhuDeal.SeatCount - 1)
                    {
                        // 三家里另外两家都过了,桌面清空 —— 轮到的正是打出那一手的人。
                        table.Current = null;
                        table.CurrentSeat = null;
                        passes = 0;
                    }
                    break;

                case DoudizhuMoveKind.Play:
                    foreach (var card in move.Cards)
                    {
                        hands[played.Seat].Remove(card);
                    }
                    table.Current = CardCombo.Recognise(move.Cards);
                    table.CurrentSeat = played.Seat;
                    passes = 0;
                    if (hands[played.Seat].Count == 0)
                    {
                        table.Phase = DoudizhuPhase.Finished;
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
    private void ApplyHistoricBid(DoudizhuMove move, int seat, IReadOnlyList<Card> kitty)
    {
        if (move.Kind != DoudizhuMoveKind.Bid)
        {
            throw new InvalidMoveException("Only bids can appear during the bidding phase.");
        }

        BidsMade++;
        if (move.Bid > BaseScore)
        {
            BaseScore = move.Bid;
            Landlord = seat;
        }

        var everyoneBid = BidsMade == DoudizhuDeal.SeatCount;
        var unbeatable = move.Bid == DoudizhuScoring.MaxBaseScore;

        if (!unbeatable && !everyoneBid)
        {
            return;
        }

        if (Landlord is null)
        {
            // 三家都不叫 —— 流局。**不重新发牌**:重发需要在同一个 Game 上换第二份 Setup,
            // 而"发牌在开局那一刻定下、之后不变"是重放与"服务端侧设置"这个概念的地基。
            Phase = DoudizhuPhase.Finished;
            return;
        }

        Phase = DoudizhuPhase.Playing;
        _hands[Landlord.Value].AddRange(kitty);
        _hands[Landlord.Value].Sort();
    }

    /// <summary>叫分尚未结束时,下一次叫分至少要多少分才算合法(<c>0</c> 是不叫,永远合法)。</summary>
    public int MinimumRaise => BaseScore + 1;

    /// <summary>这个座位手上是否**全部持有**这些牌。</summary>
    /// <param name="seat">座位号。</param>
    /// <param name="cards">要出的牌。</param>
    public bool Holds(int seat, IReadOnlyList<Card> cards)
    {
        // 一副牌里每张牌只有一张,所以手牌是一个集合 —— 包含关系就是集合包含。
        // "同一张牌出两次"在 DoudizhuMove.Parse 里已经被拒了。
        var hand = _hands[seat];
        return cards.All(hand.Contains);
    }
}
