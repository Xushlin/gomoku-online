using Gewu.Application.Common.DTOs;
using Gewu.Domain.Users;
using MediatR;

namespace Gewu.Application.Features.Rooms.CreateRoom;

/// <summary>
/// 创建房间,调用方成为 Host 和黑方。返回房间摘要。
/// <para>
/// <paramref name="GameKey"/> 是**必填**的 —— Application 层不猜自己在被问哪个棋种。
/// <c>RoomsController</c> 也不猜:<c>require-room-game-key</c> 删掉了那里的
/// <c>?? GameKeys.Gomoku</c>,因为它是为「已发布的客户端」写的兼容层,而那是**零个**。
/// 于是"这一局是什么棋"这件事,从请求到聚合只有一个地方写着。
/// </para>
/// </summary>
/// <param name="HostUserId">创建者。</param>
/// <param name="Name">房间名,trim 后 3–50 字符。</param>
/// <param name="GameKey">棋种键,MUST 已登记在规则注册表中(由 validator 保证)。</param>
public sealed record CreateRoomCommand(
    UserId HostUserId,
    string Name,
    string GameKey) : IRequest<RoomSummaryDto>;
