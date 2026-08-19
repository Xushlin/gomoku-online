using Gewu.Domain.Enums;
using Gewu.Domain.Rooms;

namespace Gewu.Application.Common.DTOs;

/// <summary>用户在 Room 相关 DTO 里的精简表示(避免暴露 email / 战绩等无关字段)。</summary>
public sealed record UserSummaryDto(Guid Id, string Username);

/// <summary>
/// 一个座位:座位号 + 坐在上面的人。
/// <para>
/// <b>它存在的理由是 `Black` / `White` 描述不了三个座位。</b> 那两个字段是 0 号与 1 号的派生读法,
/// 于是三座位房间里 2 号座位上的人**在任何字段里都不出现** —— 实测过。
/// </para>
/// <para>
/// `generalize-match-contract` 刻意**没有**加这个字段,理由是那时没有读者,而
/// 「交付一个没人读的字段」正是 `add-match-setup` 刚踩过的坑。现在读者有了:客户端要画三家的牌,
/// 就得知道谁坐哪。
/// </para>
/// </summary>
/// <param name="Index">座位号,0-based。</param>
/// <param name="Player">坐在这个座位上的人。</param>
public sealed record RoomSeatDto(int Index, UserSummaryDto Player);

/// <summary>对局中一步棋的网络表示。</summary>
/// <param name="Ply">步数(1-based)。</param>
/// <param name="Row">终点 / 落点行;**文本类棋种为 <c>null</c>**。</param>
/// <param name="Col">终点 / 落点列;文本类棋种为 <c>null</c>。</param>
/// <param name="Seat">
/// 走这一步的**座位号**（0-based）。
/// <para>
/// 它此前是 `Stone`（`'Black' | 'White'`），经 `SeatWire.ToStone(seat)` 换算 ——
/// 而那个函数是 `seat == 0 ? Black : White`，于是**2 号座位被说成 1 号**。
/// 实测过：三座位房间里三手 `bid:0` 的 `stone` 是 `Black / White / White`，
/// 两个农民在走子记录里重合。
/// </para>
/// <para>
/// 颜色是**显示层**的事：五子棋 `0 → 黑`，象棋 `0 → 红`。`add-xiangqi` 定下的
/// 规矩就是“`Stone` 一直是先手 / 后手，红黑是显示层的读法”，而 `SeatWire`
/// 把那个读法写进了契约。
/// </para>
/// </param>
/// <param name="PlayedAt">走子时刻(UTC)。</param>
/// <param name="FromRow">
/// 起点行;**落子类棋种(五子棋 / 一字棋)为 <c>null</c>**,走子类(中国象棋)非 <c>null</c>。
/// 追加在末尾且可空 —— 已发布的客户端解析时会忽略它,形状向后兼容。
/// </param>
/// <param name="FromCol">起点列;见 <paramref name="FromRow"/>。</param>
/// <param name="Text">
/// 这一步的文本载荷(成语接龙的一个成语);**位置类棋种为 <c>null</c>**。
/// 与四个坐标恰好互斥 —— 载荷的合法性由 Domain 的构造器保证,本 DTO 只是如实转述。
/// </param>
public sealed record MoveDto(
    int Ply,
    int? Row,
    int? Col,
    int Seat,
    DateTime PlayedAt,
    int? FromRow = null,
    int? FromCol = null,
    string? Text = null);

/// <summary>
/// 对局结束事件的 payload。<paramref name="EndReason"/> 明示"怎么结束的"(Connected5 / Resigned / TurnTimeout),
/// 客户端据此在 UI 区分"连五胜""对方认输""超时判负"。
/// </summary>
public sealed record GameEndedDto(
    GameResult Result,
    Guid? WinnerUserId,
    DateTime EndedAt,
    GameEndReason EndReason);

/// <summary>
/// 房间摘要,用于 <c>GET /api/rooms</c> 列表。不含 Moves / ChatMessages / Spectators 列表,
/// 只含观众数量。
/// </summary>
/// <param name="GameKey">
/// 该房间玩的是哪个棋种。客户端据此决定盘面尺寸等与棋种有关的呈现 ——
/// 详见 <see cref="RoomStateDto"/> 上的说明。
/// </param>
public sealed record RoomSummaryDto(
    Guid Id,
    string Name,
    string GameKey,
    RoomStatus Status,
    UserSummaryDto Host,
    UserSummaryDto? Black,
    UserSummaryDto? White,
    int SpectatorCount,
    DateTime CreatedAt);

/// <summary>
/// 对局运行时的完整快照(含全部 Moves,最多 225 条)。
/// <para>
/// <paramref name="TurnStartedAt"/> = 最后一步 <c>PlayedAt</c>,无 Moves 时 = <paramref name="StartedAt"/>;
/// 客户端根据 <c>TurnStartedAt + TurnTimeoutSeconds</c> 本地 tick 倒计时 UI。
/// </para>
/// <para>
/// <paramref name="EndReason"/> 与 <paramref name="Result"/> 同时为 <c>null</c> 或同时非 <c>null</c>。
/// </para>
/// </summary>
/// <param name="SeatView">
/// 这个看客**能看到**的那一份棋种私有状态,已由规则序列化;棋种没有隐藏信息时为 <c>null</c>。
/// <para>
/// 对本层完全不透明 —— 内容由棋种规则决定(<c>IPerSeatViewRules</c>),由客户端按棋种解析。
/// 与闯关那条线的 <c>LayoutJson</c> 同一个做法:内核不该知道什么是牌。
/// </para>
/// <para>
/// <b>同一局的不同座位拿到的是不同的字符串</b>,所以广播 MUST 按座位分别投影 ——
/// 见 <c>SignalRRoomNotifier</c>。
/// </para>
/// </param>
public sealed record GameSnapshotDto(
    Guid Id,
    int CurrentSeat,
    DateTime StartedAt,
    DateTime? EndedAt,
    GameResult? Result,
    Guid? WinnerUserId,
    GameEndReason? EndReason,
    DateTime TurnStartedAt,
    int TurnTimeoutSeconds,
    IReadOnlyList<MoveDto> Moves,
    string? SeatView = null);

/// <summary>聊天消息的网络表示。</summary>
public sealed record ChatMessageDto(
    Guid Id,
    Guid SenderUserId,
    string SenderUsername,
    string Content,
    ChatChannel Channel,
    DateTime SentAt);

/// <summary>催促事件的 payload(仅推给被催方)。</summary>
public sealed record UrgeDto(Guid FromUserId, string FromUsername, DateTime SentAt);

/// <summary>
/// 房间的完整状态,用于 <c>GET /api/rooms/{id}</c> 和 <c>RoomStateChanged</c> 事件。
/// <para>
/// <paramref name="GameKey"/> 不是装饰性字段 —— 客户端**画不出棋盘就得靠它**。玩家进入
/// 一个房间有四条路:从建房页跳转、刷新页面、点收藏链接、从"我的对局"进入。只有第一条路上
/// 客户端知道棋种(是它自己刚选的);另外三条它手上只有一个房间 id。所以"棋种从路由参数
/// 带过来"这条捷径只在四条路里的一条上成立,另外三条会把 3×3 画成 15×15。
/// </para>
/// <para>
/// 盘面尺寸(行列数)本 DTO **不**下发:那需要把 <c>IGameRulesRegistry</c> 穿过九处
/// <c>ToState</c> / <c>ToSummary</c> 调用点。客户端从自己的游戏注册表按本键解析尺寸 ——
/// 见 <c>add-web-tictactoe-ai</c> design D1。<c>generalize-match-contract</c> 反正要重写
/// 本 DTO(座位、JSON 载荷),届时改为服务端下发。
/// </para>
/// </summary>
public sealed record RoomStateDto(
    Guid Id,
    string Name,
    string GameKey,
    RoomStatus Status,
    UserSummaryDto Host,
    UserSummaryDto? Black,
    UserSummaryDto? White,
    IReadOnlyList<RoomSeatDto> Seats,
    IReadOnlyList<UserSummaryDto> Spectators,
    GameSnapshotDto? Game,
    IReadOnlyList<ChatMessageDto> ChatMessages,
    DateTime CreatedAt);
