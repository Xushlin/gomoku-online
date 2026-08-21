using System.Collections.Generic;
using Gewu.Application.Abstractions;
using Gewu.Domain.Games.Abstractions;
using MediatR;

namespace Gewu.Application.Features.Rooms.GetPlayHints;

/// <summary>
/// 取出调用者自己那一份候选出法。
/// <para>
/// <b>它按注册表解析 <see cref="IPlayHintRules"/>,而不认识任何具体棋种键。</b>
/// 第一版写死了 <c>GameKeys.Wakeng</c>,而那在只有一个牌类棋种时看不出问题 ——
/// 加第二个的那天它就会长成一个 <c>switch (gameKey)</c>,而
/// <c>game-rules-registry</c> 明写着「实现 MUST NOT 内联任何『哪些棋种存在』的硬编码列表」。
/// </para>
/// <para>
/// 枚举整个在 Domain 里(两个棋种各一份),这里只做取数与授权:「哪几手牌出得起」是规则,
/// 而规则是这一局唯一的判据 —— 在这一层再判一遍就是第二个真源。
/// </para>
/// </summary>
public sealed class GetPlayHintsQueryHandler : IRequestHandler<GetPlayHintsQuery, PlayHintsDto>
{
    private static readonly PlayHintsDto None = new([]);

    private readonly IRoomRepository _rooms;
    private readonly IGameRulesRegistry _rules;

    /// <inheritdoc />
    public GetPlayHintsQueryHandler(IRoomRepository rooms, IGameRulesRegistry rules)
    {
        _rooms = rooms;
        _rules = rules;
    }

    /// <inheritdoc />
    public async Task<PlayHintsDto> Handle(GetPlayHintsQuery request, CancellationToken cancellationToken)
    {
        var room = await _rooms.FindByIdAsync(request.RoomId, cancellationToken);
        if (room is null)
        {
            return None;
        }

        // 解析不出这个接口的棋种返回空 —— 而那不是「你要不起」,是**这个棋种没有这个功能**。
        // 两者在客户端不会混:只有牌类棋种的牌桌会去按那个按钮。
        if (_rules.For(room.GameKey) is not IPlayHintRules hints)
        {
            return None;
        }

        // **只回答调用者自己的那一份。** 一个能查别人候选的端点,等于把别人的手牌算出来给你;
        // 围观者与非玩家因此拿到空,而不是某一家的候选。
        if (room.SeatOf(request.UserId) is not int seat)
        {
            return None;
        }

        var game = room.Game;
        if (game is null || game.Result is not null)
        {
            return None;
        }

        return new PlayHintsDto(hints.LegalPlays(game.State(), seat));
    }
}
