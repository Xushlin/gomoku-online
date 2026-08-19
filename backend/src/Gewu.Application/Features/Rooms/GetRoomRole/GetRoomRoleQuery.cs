using System;
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
public sealed record GetRoomRoleQuery(UserId UserId, RoomId RoomId) : IRequest<RoomMembership>;

/// <summary>
/// 一个用户在房间里的身份,以及**他坐第几号座位**。
/// <para>
/// 座位号是 <c>add-doudizhu-visibility</c> 加的:分群从"玩家 / 围观者"变成"每个座位一个群",
/// 因为斗地主不同座位收到的快照**内容不同**。只知道"他是玩家"不够,得知道是哪一个。
/// </para>
/// <para>
/// <see cref="Seat"/> 非 <c>null</c> **当且仅当** <see cref="Role"/> 是
/// <see cref="RoomRole.Player"/> —— 构造函数强制这一点,因为"是玩家但不知道坐哪"是一个
/// 分群逻辑无法处理的状态,而它悄悄出现的后果是那个连接一份快照都收不到。
/// </para>
/// </summary>
/// <param name="Role">身份。</param>
/// <param name="Seat">座位号;非玩家时 <c>null</c>。</param>
public readonly record struct RoomMembership(RoomRole Role, int? Seat)
{
    /// <summary>身份。</summary>
    public RoomRole Role { get; } = (Role == RoomRole.Player) == (Seat is not null)
        ? Role
        : throw new InvalidOperationException(
            $"Role {Role} and seat {Seat?.ToString() ?? "<none>"} disagree: a player has a seat and a non-player has none.");

    /// <summary>座位号;非玩家时 <c>null</c>。</summary>
    public int? Seat { get; } = Seat;

    /// <summary>坐在某个座位上。</summary>
    /// <param name="seat">座位号。</param>
    public static RoomMembership AtSeat(int seat) => new(RoomRole.Player, seat);

    /// <summary>围观者。</summary>
    public static RoomMembership Spectator => new(RoomRole.Spectator, null);

    /// <summary>既不是玩家也不是围观者。</summary>
    public static RoomMembership None => new(RoomRole.None, null);
}
