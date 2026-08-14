using Gewu.Application.Common.DTOs;
using MediatR;

namespace Gewu.Application.Features.Rooms.GetRoomList;

/// <summary>
/// 查询某个棋种下所有活跃(Waiting / Playing)房间的摘要。
/// <para>
/// 大厅是分棋种的。五子棋大厅里出现一字棋房间既加入不了(盘面不同),也让"有几局在等人"
/// 这个数字失去意义 —— 那是玩家看大厅唯一想知道的事。
/// </para>
/// <para>
/// <paramref name="GameKey"/> 必填。未登记的键返回**空列表**而不是错误:集合端点上
/// "没有这种房间"与"没有这个棋种"对调用方毫无区别,而让列表接口 404 会逼每个调用方
/// 为一个与空结果无从分辨的情况写一段特例。
/// </para>
/// </summary>
/// <param name="GameKey">棋种键。</param>
public sealed record GetRoomListQuery(string GameKey) : IRequest<IReadOnlyList<RoomSummaryDto>>;
