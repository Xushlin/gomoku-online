using System;
using System.Collections.Generic;
using System.Linq;
using Gewu.Application.Abstractions;
using Gewu.Domain.Enums;
using Gewu.Domain.Games.Cards;
using Gewu.Domain.Games.Wakeng;
using MediatR;

namespace Gewu.Application.Features.Rooms.GetPlayHints;

/// <summary>
/// 算出调用者自己那一份候选出法。
/// <para>
/// <b>枚举在 Domain 里(<see cref="WakengFollows"/>),这里只做取数与授权。</b>
/// 「哪几手牌出得起」是规则,而规则是这一局唯一的判据 —— 在这一层再判一遍就是第二个真源。
/// </para>
/// <para>
/// <b>今天只有挖坑。</b> 斗地主的「压得住」要算炸弹、四带二、飞机带翅膀,而且炸弹跨型压,
/// 候选空间大一个量级。别的棋种返回空列表 —— 而那不是「你要不起」,是「这个棋种没有这个功能」。
/// 两者在客户端不会混:只有挖坑的牌桌会去按那个按钮。
/// </para>
/// </summary>
public sealed class GetPlayHintsQueryHandler : IRequestHandler<GetPlayHintsQuery, PlayHintsDto>
{
    private static readonly PlayHintsDto None = new([]);

    private readonly IRoomRepository _rooms;

    /// <inheritdoc />
    public GetPlayHintsQueryHandler(IRoomRepository rooms)
    {
        _rooms = rooms;
    }

    /// <inheritdoc />
    public async Task<PlayHintsDto> Handle(GetPlayHintsQuery request, CancellationToken cancellationToken)
    {
        var room = await _rooms.FindByIdAsync(request.RoomId, cancellationToken);
        if (room is null || room.GameKey != Gewu.Domain.Games.Abstractions.GameKeys.Wakeng)
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
        if (game is null || game.Result is not null || game.Setup is null)
        {
            return None;
        }

        var table = WakengTable.Reconstruct(game.State());
        if (table.Phase != WakengPhase.Playing)
        {
            // 叫分阶段没有「出哪一手牌」这件事。
            return None;
        }

        var plays = WakengFollows.For(table.HandOf(seat), table.Current)
            .Select(Card.Encode)
            .ToList();

        return new PlayHintsDto(plays);
    }
}
