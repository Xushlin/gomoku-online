using System.Collections.Generic;

namespace Gewu.Domain.Games.Doudizhu;

/// <summary>
/// 一个座位**看得到**的斗地主局面。
/// <para>
/// 这是 <see cref="IPerSeatViewRules.ViewFor"/> 的返回内容,序列化成 JSON 之后对内核完全不透明。
/// 它 MUST 只含**这个座位有权知道**的东西:自己的牌、别人的**张数**、桌面上的一手、地主是谁、
/// 底分,以及定完地主之后的底牌。
/// </para>
/// <para>
/// <b>为什么"别人的张数"是公开的:</b> 那是牌桌上看得见的东西 —— 每个人手上剩几张,三家都数得出。
/// 藏它不会更安全,只会让客户端画不出"对家只剩两张了"这个决定性的信息。
/// </para>
/// <para>
/// <b>为什么底牌定完地主就公开:</b> 地主当众把三张底牌收进手里,是这个游戏的规则。农民靠它推断
/// 地主手上有什么;藏起来只会让屏幕上少一件本来看得见的事。叫分阶段它 MUST 为 <c>null</c> ——
/// 那时它还没被翻开,而它决定了谁值得抢地主。
/// </para>
/// </summary>
/// <param name="Phase">阶段:<c>Bidding</c> / <c>Playing</c> / <c>Finished</c>。</param>
/// <param name="Landlord">地主座位号;还没定或流局时 <c>null</c>。</param>
/// <param name="BaseScore">底分;还没定下来时 <c>0</c>。</param>
/// <param name="BidsMade">已经叫过几次(含不叫)。</param>
/// <param name="MyHand">
/// **这个座位自己的牌**,编码后的字符串;没有座位的人(围观者)为空串。
/// </param>
/// <param name="HandCounts">三个座位各剩几张,按座位号。</param>
/// <param name="Kitty">底牌;叫分阶段为 <c>null</c>。</param>
/// <param name="TableSeat">桌上那一手是谁打的;自由首出时 <c>null</c>。</param>
/// <param name="TableCards">桌上那一手的牌;自由首出时 <c>null</c>。</param>
/// <param name="Winner">赢家座位号;还没结束或流局时 <c>null</c>。</param>
public sealed record DoudizhuSeatView(
    string Phase,
    int? Landlord,
    int BaseScore,
    int BidsMade,
    string MyHand,
    IReadOnlyList<int> HandCounts,
    string? Kitty,
    int? TableSeat,
    string? TableCards,
    int? Winner);
