using Gewu.Application.Common.DTOs;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;
using MediatR;

namespace Gewu.Application.Features.Rooms.GetRoomState;

/// <summary>
/// 按 RoomId 查询完整房间状态(含所有 Moves / Spectators,以及**该观看者可见的** ChatMessages)。
/// <para>
/// <paramref name="ViewerId"/> 是必需的,因为围观频道仅围观者可见,而这条查询是那条规则在
/// REST 侧的执行点。它此前不带观看者,于是玩家一次 <c>GET /api/rooms/{id}</c> 就拿到了
/// 对手围观区的全部内容。
/// </para>
/// </summary>
/// <param name="RoomId">房间。</param>
/// <param name="ViewerId">发起查询的人 —— 决定围观频道给不给看。</param>
public sealed record GetRoomStateQuery(RoomId RoomId, UserId ViewerId) : IRequest<RoomStateDto>;
