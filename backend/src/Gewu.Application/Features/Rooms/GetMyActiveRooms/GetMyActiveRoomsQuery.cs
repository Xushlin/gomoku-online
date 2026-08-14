using Gewu.Application.Common.DTOs;
using Gewu.Domain.Users;
using MediatR;

namespace Gewu.Application.Features.Rooms.GetMyActiveRooms;

/// <summary>
/// 当前登录用户的活动房间列表(Waiting + Playing,作为玩家参与;不含围观)。
/// 供前端登录后"继续对局"区域使用。不分页(典型 0-5 条)。
/// <para>
/// **刻意不按棋种过滤** —— 与 <c>GetRoomListQuery</c> 相反,而这不是遗漏。大厅回答的是
/// "这个棋种现在有哪些房间",所以必须分棋种;本查询回答的是"我此刻在哪些局里",
/// 跨棋种正是该问题的正确答案 —— 也是玩家唯一希望它们混在一起的地方:一个人可能同时
/// 有一局五子棋在等对手、一局一字棋轮到自己走。
/// </para>
/// <para>
/// 别为了"和大厅保持一致"给它加棋种参数。<c>GetMyActiveRoomsQueryHandlerTests</c> 里有
/// 一条测试专门把这个差异钉住。
/// </para>
/// </summary>
public sealed record GetMyActiveRoomsQuery(UserId UserId)
    : IRequest<IReadOnlyList<RoomSummaryDto>>;
