using Gewu.Application.Abstractions;
using Gewu.Domain.Games.Abstractions;
using Gewu.Application.Common.DTOs;
using Gewu.Application.Common.Exceptions;
using Gewu.Application.Common.Mapping;
using Gewu.Domain.Games.Xiangqi;
using Gewu.Domain.Rooms;
using MediatR;

namespace Gewu.Application.Features.Rooms.CreateRoom;

/// <summary>创建房间 handler。</summary>
public sealed class CreateRoomCommandHandler : IRequestHandler<CreateRoomCommand, RoomSummaryDto>
{
    private readonly IRoomRepository _rooms;
    private readonly IUserRepository _users;
    private readonly IXiangqiManualRepository _manuals;
    private readonly IGameRulesRegistry _rules;
    private readonly IDateTimeProvider _clock;
    private readonly IUnitOfWork _uow;

    /// <inheritdoc />
    public CreateRoomCommandHandler(
        IRoomRepository rooms,
        IUserRepository users,
        IXiangqiManualRepository manuals,
        IGameRulesRegistry rules,
        IDateTimeProvider clock,
        IUnitOfWork uow)
    {
        _rooms = rooms;
        _users = users;
        _manuals = manuals;
        _rules = rules;
        _clock = clock;
        _uow = uow;
    }

    /// <inheritdoc />
    public async Task<RoomSummaryDto> Handle(CreateRoomCommand request, CancellationToken cancellationToken)
    {
        var host = await _users.FindByIdAsync(request.HostUserId, cancellationToken)
            ?? throw new UserNotFoundException($"User '{request.HostUserId.Value}' was not found.");

        var room = request.ManualLineId is int lineId
            ? await CreateFromManualLineAsync(request, lineId, cancellationToken)
            : Room.Create(
                RoomId.NewId(), request.Name, request.HostUserId, _clock.UtcNow, request.GameKey);

        await _rooms.AddAsync(room, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        var usernames = new Dictionary<Guid, string> { [host.Id.Value] = host.Username.Value };
        return room.ToSummary(usernames);
    }

    /// <summary>
    /// 「摆这一则古谱残局对弈」:起始局面与先走方**从库里那条线路上取**,而不是从请求里读。
    /// <para>
    /// 这个 handler 因此认识象棋的设置编码,而它是**目前唯一**的选定式棋种。
    /// **拆除条件:第二个 <c>IPositionalStartRules</c> 棋种落地** —— 到那天这里会出现一个
    /// 二选一,而一个两条腿的分支就该换成一个按棋种解析的东西。在那之前造那个东西,
    /// 是给一个只有一条腿的开关配一个拨杆。
    /// </para>
    /// </summary>
    private async Task<Room> CreateFromManualLineAsync(
        CreateRoomCommand request, int lineId, CancellationToken ct)
    {
        // 顺序要紧:**先拒绝,再造房间**。反过来的话,一条不存在的线路会落地成一局
        // 开局摆错的棋,而它和一局正常的棋在界面上完全一样。
        var line = await _manuals.GetLineAsync(lineId, ct)
            ?? throw new UnknownManualLineException(
                $"Xiangqi manual line '{lineId}' does not exist.");

        // 类型判断,不是键判断 —— 与 validator 那两条同一个理由。validator 已经拦过一次,
        // 这里再拦是因为 `Room.CreateFromPosition` 要的就是这个类型:拿不到它,房间根本
        // 造不出来,于是"键与设置各说各话"在这条路上不成立。
        var rules = _rules.For(request.GameKey) as IPositionalStartRules
            ?? throw new UnknownManualLineException(
                $"'{request.GameKey}' does not start from a chosen position; "
                + "a manual line id is not meaningful for it.");

        var setup = new XiangqiSetup(line.StartPosition, line.FirstSeat).Encode();
        return Room.CreateFromPosition(
            RoomId.NewId(), request.Name, request.HostUserId, _clock.UtcNow, rules, setup);
    }
}
