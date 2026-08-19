using System.IdentityModel.Tokens.Jwt;
using Gewu.Application.Abstractions;
using Gewu.Application.Features.Rooms.GetRoomRole;
using Gewu.Application.Features.Rooms.MakeMove;
using Gewu.Application.Features.Rooms.SendChatMessage;
using Gewu.Application.Features.Rooms.UrgeOpponent;
using Gewu.Domain.Games.Abstractions;
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
public sealed class MatchHub : Hub
{
    private readonly ISender _mediator;
    private readonly IConnectionTracker _tracker;
    private readonly ILogger<MatchHub> _logger;
    private readonly IGameRulesRegistry _rules;

    /// <inheritdoc />
    public MatchHub(
        ISender mediator,
        IConnectionTracker tracker,
        ILogger<MatchHub> logger,
        IGameRulesRegistry rules)
    {
        _mediator = mediator;
        _tracker = tracker;
        _logger = logger;
        _rules = rules;
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
        var membership = await _mediator.Send(new GetRoomRoleQuery(GetUserId(), id), Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId, ViewGroupName(id, membership));

        await _tracker.AssociateRoomAsync(Context.ConnectionId, id);
    }

    /// <summary>从指定房间的 SignalR group 中移除当前连接。</summary>
    public async Task LeaveRoom(Guid roomId)
    {
        var id = new RoomId(roomId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, RoomGroupName(id));
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, SpectatorsGroupName(id));
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, ObserversGroupName(id));
        // 座位群按座位号命名,所以离开时要把**每一个**都退掉。
        //
        // 不在这里重查一次身份:查到的可能已经变了(他可能刚从座位上离开),而"按现在的身份退群"
        // 会把一个陈旧的座位群留在这个连接上 —— 那个座位后来若坐了别人,他就会收到别人的手牌。
        // 退一个不存在的群是无操作,所以宁可多退。
        //
        // 上界**从注册表取**,不是一个手写常量:手写的那个在"座位更多的棋种"落地时要有人记得涨,
        // 而忘记涨的症状是那个座位的人离开房间之后**还在收快照**。
        for (var seat = 0; seat < SeatBound(); seat++)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, SeatGroupName(id, seat));
        }
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
        var membership = await _mediator.Send(new GetRoomRoleQuery(GetUserId(), id), Context.ConnectionAborted);
        if (membership.Role == RoomRole.Spectator)
        {
            // 先出后进:视图子群 MUST 互斥,否则这个连接会收到两份快照,
            // 而后到的那一份覆盖前一份 —— 于是"看得到围观区"变成一件靠到达顺序的事。
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, ObserversGroupName(id));
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
    /// 平台上座位数最多的棋种有几个座位 —— 只用于"退掉全部座位群"的上界。
    /// <para>
    /// 从注册表算,不写死:一个手写常量在座位更多的棋种落地那天要有人记得涨,而**忘记涨没有
    /// 任何报错** —— 症状是那个座位的人离开房间之后还在收快照。这与
    /// <c>enforce-ai-availability</c> 让校验去读 <c>IGameAiRegistry</c> 而不是加一个
    /// 手写布尔是同一条:**一个复述结构性事实的手写值是判断,而判断会悄悄过期。**
    /// </para>
    /// </summary>
    private int SeatBound() => _rules.All.Max(r => r.SeatCount);

    /// <summary>
    /// 某个座位的视图子群。
    /// <para>
    /// 一个座位一个群,而不是 <c>Clients.User(...)</c>:后者会打到那个用户的**全部连接**,
    /// 包括他开在另一个房间的标签页 —— 一个催促弹错标签无所谓,一份房间快照盖掉另一个房间的
    /// 状态不行。
    /// </para>
    /// </summary>
    /// <param name="id">房间。</param>
    /// <param name="seat">座位号。</param>
    internal static string SeatGroupName(RoomId id, int seat) => $"room:{id.Value}:seat:{seat}";

    /// <summary>
    /// **观察者**的子群 —— 进了房间、没坐座位、也没围观的连接。
    /// <para>
    /// 它此前叫 <c>non-spectators</c>,里面既有坐着的人也有没坐的人。座位群出现之后那样不行了:
    /// 坐着的人会收到两份快照(一份带手牌、一份不带),而**看到哪一份由到达顺序决定** ——
    /// 正是 <c>fix-spectator-chat-leak</c> 立下"互斥且穷尽"这条规矩要挡的事。
    /// </para>
    /// <para>
    /// 三类连接各进恰好一个视图群:座位群、围观群、观察者群。改名不是整理 ——
    /// 它把"在房间里、没坐座位、也没围观"这件事说出来,而那正是这个群现在的全部成员。
    /// </para>
    /// </summary>
    /// <param name="id">房间。</param>
    internal static string ObserversGroupName(RoomId id) => $"room:{id.Value}:observers";

    /// <summary>这份身份对应的视图子群 —— 三类各一个,互斥且穷尽。</summary>
    /// <param name="id">房间。</param>
    /// <param name="membership">身份 + 座位号。</param>
    internal static string ViewGroupName(RoomId id, RoomMembership membership) => membership.Role switch
    {
        RoomRole.Player => SeatGroupName(id, membership.Seat!.Value),
        RoomRole.Spectator => SpectatorsGroupName(id),
        _ => ObserversGroupName(id),
    };

    private UserId GetUserId()
    {
        var sub = Context.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? Context.User?.FindFirst("sub")?.Value
            ?? throw new HubException("Missing sub claim.");
        return new UserId(Guid.Parse(sub));
    }
}
