using Gewu.Application.Common.DTOs;
using Gewu.Domain.Users;
using MediatR;

namespace Gewu.Application.Features.Rooms.CreateRoom;

/// <summary>
/// 创建房间,调用方成为 Host 和黑方。返回房间摘要。
/// <para>
/// <paramref name="GameKey"/> 是**必填**的 —— Application 层不猜自己在被问哪个棋种。
/// HTTP 层对缺省的兼容处理(缺省填 <c>gomoku</c>)只存在于 controller,理由见
/// <c>add-tictactoe</c> design D3:那个妥协应该待在一个能被看见的地方。
/// </para>
/// </summary>
/// <param name="HostUserId">创建者。</param>
/// <param name="Name">房间名,trim 后 3–50 字符。</param>
/// <param name="GameKey">棋种键,MUST 已登记在规则注册表中(由 validator 保证)。</param>
public sealed record CreateRoomCommand(
    UserId HostUserId,
    string Name,
    string GameKey) : IRequest<RoomSummaryDto>;
