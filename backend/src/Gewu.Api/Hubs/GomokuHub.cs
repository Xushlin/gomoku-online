using System.IdentityModel.Tokens.Jwt;
using Gewu.Application.Abstractions;
using Gewu.Application.Features.Rooms.GetRoomRole;
using Gewu.Application.Features.Rooms.MakeMove;
using Gewu.Application.Features.Rooms.SendChatMessage;
using Gewu.Application.Features.Rooms.UrgeOpponent;
using Gewu.Domain.Rooms;
using Gewu.Domain.Users;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Serilog.Context;

namespace Gewu.Api.Hubs;

/// <summary>
/// 单一 SignalR Hub:所有实时操作都经它路由到 MediatR handler。Hub 本身 MUST NOT
/// 读写数据库或直接发送业务事件 —— 事件由 handler 完成 <c>SaveChangesAsync</c> 后通过
/// <c>IRoomNotifier</c> 广播。
/// </summary>
[Authorize]
public sealed class GomokuHub : Hub
{
    private readonly ISender _mediator;
    private readonly IConnectionTracker _tracker;
    private readonly ILogger<GomokuHub> _logger;

    /// <inheritdoc />
    public GomokuHub(ISender mediator, IConnectionTracker tracker, ILogger<GomokuHub> logger)
    {
        _mediator = mediator;
        _tracker = tracker;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        await _tracker.TrackAsync(Context.ConnectionId, userId);
        using (LogContext.PushProperty("ConnectionId", Context.ConnectionId))
        using (LogContext.PushProperty("UserId", userId.Value))
        {
            _logger.LogInformation("SignalR connection opened");
        }
        await base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _tracker.UntrackAsync(Context.ConnectionId);
        using (LogContext.PushProperty("ConnectionId", Context.ConnectionId))
        using (LogContext.PushProperty("UserId", Context.UserIdentifier ?? "anonymous"))
        {
            if (exception is not null)
            {
                _logger.LogWarning(exception, "SignalR connection closed with exception");
            }
            else
            {
                _logger.LogInformation("SignalR connection closed");
            }
        }
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>把当前连接加入指定房间的 SignalR group(为了接收该房间的推送);不改 <see cref="Room"/> 聚合。</summary>
    public async Task JoinRoom(Guid roomId)
    {
        var id = new RoomId(roomId);
        await Groups.AddToGroupAsync(Context.ConnectionId, RoomGroupName(id));

        // 身份取自**聚合**,不取自客户端自报。spec 一直是这么写的
        // (「若调用方已是该房间的玩家或围观者…则额外加入子群」),而实现此前是客户端
        // 自己调 JoinSpectatorGroup —— 于是玩家把自己塞进围观子群就能实时收到吐槽。
        var role = await _mediator.Send(new GetRoomRoleQuery(GetUserId(), id), Context.ConnectionAborted);
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            role == RoomRole.Spectator ? SpectatorsGroupName(id) : NonSpectatorsGroupName(id));

        await _tracker.AssociateRoomAsync(Context.ConnectionId, id);
    }

    /// <summary>从指定房间的 SignalR group 中移除当前连接。</summary>
    public async Task LeaveRoom(Guid roomId)
    {
        var id = new RoomId(roomId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, RoomGroupName(id));
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, SpectatorsGroupName(id));
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, NonSpectatorsGroupName(id));
        await _tracker.DissociateRoomAsync(Context.ConnectionId, id);
    }

    /// <summary>
    /// 把当前连接加入某房间的围观者子 group。
    /// <para>
    /// <b>身份由服务端查聚合确认,不采信调用方自报。</b> 此前这个方法无条件加群,于是任何持有
    /// JWT 的人 —— 包括这局的**玩家** —— 调一次就能实时收到围观频道的全部消息。实测过:
    /// <c>JoinSpectatorGroup -> OK</c>,紧接着玩家就收到了本不该给他的那条评论。
    /// </para>
    /// <para>
    /// 保留这个方法(而不是完全交给 <see cref="JoinRoom"/>)是为了重连与"先看看再决定围观"
    /// 这两条路径:围观者可能在 <c>JoinRoom</c> 之后才 <c>POST /spectate</c>。它现在是幂等的、
    /// 且对非围观者是**静默无操作** —— 抛异常会把"我还不是围观者"变成一个需要客户端处理的错误,
    /// 而那不是错误。
    /// </para>
    /// </summary>
    /// <param name="roomId">房间。</param>
    public async Task JoinSpectatorGroup(Guid roomId)
    {
        var id = new RoomId(roomId);
        var role = await _mediator.Send(new GetRoomRoleQuery(GetUserId(), id), Context.ConnectionAborted);
        if (role == RoomRole.Spectator)
        {
            // 先出后进:两个子群 MUST 互斥,否则这个连接会收到两份快照,
            // 而后到的那一份覆盖前一份 —— 于是"看得到围观区"变成一件靠到达顺序的事。
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, NonSpectatorsGroupName(id));
            await Groups.AddToGroupAsync(Context.ConnectionId, SpectatorsGroupName(id));
        }
    }

    /// <summary>
    /// 落子 —— **落子类**棋种(五子棋 / 一字棋)的走子入口。签名一个字没改。
    /// </summary>
    public async Task MakeMove(Guid roomId, int row, int col)
    {
        var command = new MakeMoveCommand(GetUserId(), new RoomId(roomId), row, col);
        await _mediator.Send(command, Context.ConnectionAborted);
    }

    /// <summary>
    /// 走子 —— **走子类**棋种(中国象棋)的走子入口:把 <paramref name="fromRow"/> /
    /// <paramref name="fromCol"/> 上的棋子走到 <paramref name="row"/> / <paramref name="col"/>。
    /// <para>
    /// **为什么是第二个方法,而不是给 <see cref="MakeMove"/> 加两个可选参数:**
    /// SignalR **不套用 C# 的可选参数默认值**。三参调用打到五参方法上,服务端直接回
    /// <c>InvalidDataException: Invocation provides 3 argument(s) but target expects 5</c> ——
    /// 也就是说那种写法会让每一个已发布的客户端当场下不了棋。
    /// 这一条是 AiSmoke 跑出来的:那个工具不知道本次重构存在,所以它撞上的正是真实客户端会撞上的东西。
    /// </para>
    /// <para>
    /// 这与 design D2「不给规则开两个方法」不矛盾:那里的问题是**调用方得判断棋种**,
    /// 而调用方是通用的聚合根。这里的调用方是象棋自己的棋盘组件 —— 它按定义只服务一个棋种,
    /// 不存在判断。Domain 那一侧仍然只有 <c>Apply</c> 一个入口。
    /// </para>
    /// </summary>
    public async Task MovePiece(Guid roomId, int fromRow, int fromCol, int row, int col)
    {
        var command = new MakeMoveCommand(
            GetUserId(), new RoomId(roomId), row, col, fromRow, fromCol);
        await _mediator.Send(command, Context.ConnectionAborted);
    }

    /// <summary>
    /// 说出一个词 —— **文本类**棋种(成语接龙)的走子入口。
    /// <para>
    /// 与 <see cref="MovePiece"/> 同理,这是**第三个方法**而不是给 <see cref="MakeMove"/>
    /// 加一个可选参数:SignalR 不套用 C# 的可选参数默认值,少参调用打到多参方法上会被
    /// 直接拒掉。那一条是实测出来的,见 <see cref="MovePiece"/> 的说明。
    /// </para>
    /// <para>
    /// 这个词是不是一条成语、接不接得上,全由规则判 —— Hub 只把参数搬过去。
    /// </para>
    /// </summary>
    /// <param name="roomId">房间。</param>
    /// <param name="word">这一步说出的词。</param>
    public async Task SayWord(Guid roomId, string word)
    {
        var command = new MakeMoveCommand(GetUserId(), new RoomId(roomId), Text: word);
        await _mediator.Send(command, Context.ConnectionAborted);
    }

    /// <summary>发送聊天。</summary>
    public async Task SendChat(Guid roomId, string content, ChatChannel channel)
    {
        var command = new SendChatMessageCommand(GetUserId(), new RoomId(roomId), content, channel);
        await _mediator.Send(command, Context.ConnectionAborted);
    }

    /// <summary>催促对手。</summary>
    public async Task Urge(Guid roomId)
    {
        var command = new UrgeOpponentCommand(GetUserId(), new RoomId(roomId));
        await _mediator.Send(command, Context.ConnectionAborted);
    }

    internal static string RoomGroupName(RoomId id) => $"room:{id.Value}";
    internal static string SpectatorsGroupName(RoomId id) => $"room:{id.Value}:spectators";

    /// <summary>
    /// **非围观者**的子群 —— 玩家,以及进了房但还没围观的连接。
    /// <para>
    /// 加它是因为 <c>RoomState</c> 广播要发两份:这一份不含围观频道。此前只有
    /// <c>room:{id}</c>(全体)与 <c>room:{id}:spectators</c>(仅围观者),没有这一侧,
    /// 于是那一份只能推给全体 —— 围观者的吐槽就这样进了玩家的客户端。
    /// </para>
    /// <para>
    /// 它叫"非围观者"而不是"玩家",是因为它必须**穷尽**另一侧:两个子群加起来要覆盖房间里
    /// 每一个连接,否则有人收不到任何快照。我第一版按"玩家"分,结果一个还没点围观的旁观连接
    /// 两个组都不在,实时更新就断了。**分组要么互斥且穷尽,要么就有人掉在缝里。**
    /// </para>
    /// </summary>
    /// <param name="id">房间。</param>
    internal static string NonSpectatorsGroupName(RoomId id) => $"room:{id.Value}:non-spectators";

    private UserId GetUserId()
    {
        var sub = Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? Context.User?.FindFirst("sub")?.Value
            ?? throw new HubException("Missing sub claim.");
        return new UserId(Guid.Parse(sub));
    }
}
