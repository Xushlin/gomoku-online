using Gewu.Domain.Rooms;
using Gewu.Domain.Users;
using MediatR;

namespace Gewu.Application.Features.Rooms.GetRoomRole;

/// <summary>某个用户在某个房间里的身份。</summary>
public enum RoomRole
{
    /// <summary>既不是玩家也不是围观者 —— 或者房间不存在。</summary>
    None = 0,

    /// <summary>黑方或白方。</summary>
    Player = 1,

    /// <summary>围观者。</summary>
    Spectator = 2,
}

/// <summary>
/// 查一个用户在房间里的身份。
/// <para>
/// 存在的理由是一条安全判定:SignalR 的子群分配必须取自**聚合**,而不是客户端自报。
/// Hub 只能路由、不能访问 DbContext,所以它派这个 query;而聚合是"谁是玩家"的唯一真源。
/// </para>
/// <para>
/// 房间不存在时返回 <see cref="RoomRole.None"/> 而不是抛异常:调用方是分群逻辑,
/// 它对"房间没了"的正确反应是不加任何子群,而不是把一个连接建立过程变成错误路径。
/// </para>
/// </summary>
/// <param name="UserId">要判断的用户。</param>
/// <param name="RoomId">房间。</param>
public sealed record GetRoomRoleQuery(UserId UserId, RoomId RoomId) : IRequest<RoomRole>;
