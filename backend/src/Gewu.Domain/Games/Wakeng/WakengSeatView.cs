using System.Collections.Generic;
using Gewu.Domain.Games.Abstractions;

namespace Gewu.Domain.Games.Wakeng;

/// <summary>
/// 一个座位**看得到**的挖坑局面。
/// <para>
/// 这是 <see cref="IPerSeatViewRules.ViewFor"/> 的返回内容,序列化成 JSON 之后对内核完全不透明。
/// 它 MUST 只含**这个座位有权知道**的东西。
/// </para>
/// <para>
/// <b>为什么别人的张数是公开的:</b> 那是牌桌上看得见的东西 —— 每个人手上剩几张,三家都数得出。
/// 藏它不会更安全,只会让客户端画不出「上家只剩两张了」这个决定性的信息。
/// </para>
/// <para>
/// <b>为什么首叫者亮的那张 ♣ 是公开的:</b> 按规则它本来就是明示的(它决定了谁首叫首出),
/// 而服务端算得出 ——**客户端不该自己猜**。它是一处判断,记在这里。
/// </para>
/// <para>
/// <b>为什么底牌定完挖坑者才公开:</b> 挖坑者当众把四张底牌收进手里。叫分阶段它 MUST 为
/// <c>null</c> —— 那时它还没被翻开,而它恰恰决定了这一局值不值得挖。
/// </para>
/// <para>
/// <b>为什么没有「基数」:</b> 它今天恒等于 <see cref="WakengScoring.DefaultBase"/> == 1,
/// 而那不是这一局的*状态*,是一个还不存在的房间设置。发一个只有一个取值的字段,等于请客户端
/// 画「×1」;将来它成为设置时,它属于**房间**而不属于按座位的视图 —— 三个座位看到的是同一个数。
/// </para>
/// </summary>
/// <param name="Phase">阶段:<c>Bidding</c> / <c>Playing</c> / <c>Finished</c>。</param>
/// <param name="FirstBidder">首叫者座位号 —— 他也**首出**。</param>
/// <param name="FirstBidderCard">首叫者亮的那张 ♣,编码后的一个字符。**公开**。</param>
/// <param name="Digger">挖坑者座位号;还没定时 <c>null</c>。</param>
/// <param name="Bid">叫分;还没定下来时 <c>0</c>。</param>
/// <param name="BidsMade">已经叫过几次(含不挖)。</param>
/// <param name="MyHand">**这个座位自己的牌**;没有座位的人(围观者)为空串。</param>
/// <param name="HandCounts">三个座位各剩几张,按座位号。</param>
/// <param name="Kitty">底牌 4 张;叫分阶段为 <c>null</c>。</param>
/// <param name="TableSeat">桌上那一手是谁打的;自由首出时 <c>null</c>。</param>
/// <param name="TableCards">桌上那一手的牌;自由首出时 <c>null</c>。</param>
/// <param name="Winner">赢家座位号;还没结束时 <c>null</c>。</param>
/// <param name="CanFollow">
/// **假如此刻轮到你,你出得起吗** —— 亦即 <see cref="WakengFollows.For"/> 非空。
/// <para>
/// <b>它刻意与「是不是轮到你」无关。</b> 这是一个「手牌 × 桌面」的事实,而
/// <c>ViewFor</c> 收的 <c>MatchState</c> 里根本没有当前回合(那在 <c>Game.CurrentTurn</c> 上)。
/// 一个「有时是 false 只因为还没轮到你」的字段,会让客户端在错的时候自动过牌 ——
/// 所以定义写死在这里:**它只回答手牌压不压得住桌面**,轮次由客户端自己合。
/// </para>
/// <para>
/// 桌面为空(自由首出)时它恒为 <c>true</c>:只要手里还有牌,一张单牌永远是合法牌型。
/// 叫分阶段它没有意义 —— 客户端只在出牌阶段、且桌上有牌时读它。
/// </para>
/// <para>
/// **它是一个布尔而不是一个列表**,而那是刻意的:候选出法可能有几十项,每次广播都带着它
/// 是「一个没人渲染但所有人付钱的切片」。列表走按需的 <c>GET /api/rooms/{id}/hints</c>。
/// </para>
/// </param>
public sealed record WakengSeatView(
    string Phase,
    int FirstBidder,
    string FirstBidderCard,
    int? Digger,
    int Bid,
    int BidsMade,
    string MyHand,
    IReadOnlyList<int> HandCounts,
    string? Kitty,
    int? TableSeat,
    string? TableCards,
    int? Winner,
    bool CanFollow);
