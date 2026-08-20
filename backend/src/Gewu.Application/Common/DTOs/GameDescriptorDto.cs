namespace Gewu.Application.Common.DTOs;

/// <summary>
/// 一个已登记对战棋种的对外描述 —— <c>IGameRulesRegistry</c> 的只读投影。
/// <para>
/// 存在的理由:前端要渲染"棋种切换"、要决定哪张目录卡片配一个排行榜入口,就得知道
/// **哪些棋种计分**,而这个事实此前只存在于服务端。备选是在前端 manifest 上加一个
/// <c>rated</c> 布尔副本 —— 不做:前端已有的那份副本(棋盘行列数)之所以可以接受,是因为
/// 失配的症状是**肉眼可见的格数不对**,且服务端 <c>IsInBounds</c> 会兜底;<c>rated</c> 失配的
/// 症状是**一个永远空着的榜**,而那与"新棋种还没人下过"在屏幕上一模一样。
/// **一份副本能不能接受,看的不是它多小,而是它错了会不会有人发现。**
/// </para>
/// <para>
/// 不含 <c>WinLength</c>。它在 <c>IGameRules</c> 上是因为今天的棋种恰好都是"连 N 子",
/// 而中国象棋没有这个概念 —— 把一个对将来的棋种无意义的字段放进对外契约,只会让客户端
/// 学着去读它。要显示尺寸,<c>Rows</c> / <c>Cols</c> 够了。
/// </para>
/// </summary>
/// <param name="GameKey">棋种键,与房间的 <c>GameKey</c>、前端游戏注册表中的 key 一致。</param>
/// <param name="IsRated">对局结束时是否结算 ELO —— 亦即"这个棋种有没有排行榜"。</param>
/// <param name="SupportsHumanVsHuman">平台是否为它提供人人对战入口。</param>
/// <param name="SupportsAi">
/// 这个棋种有没有机器人 —— 投影自 <c>IGameAiRegistry.For(gameKey) is not null</c>,与
/// <c>POST /api/rooms/ai</c> 的校验读**同一份**注册表,所以客户端看到的与服务端会接受的
/// 不可能不一致。它不是 <c>IGameRules</c> 上的一个手写布尔:那会是同一件事的第二个真源,
/// 而它失配的症状是**一个永远 400 的按钮**。
/// </param>
/// <param name="SeatCount">
/// 这个棋种要几个座位 —— 投影自 <c>IGameRules.SeatCount</c>。
/// <para>
/// **它非空,而那正是它与 <paramref name="Rows"/> / <paramref name="Cols"/> 的区别:**
/// 每个有 <c>IGameRules</c> 的棋种都有座位数,不存在「不适用」;而成语接龙真的没有盘面。
/// </para>
/// <para>
/// **它存在是因为客户端读不到「这个棋种有几个座位」,而它需要。** 房间侧栏此前拿
/// <c>RoomStateDto.Seats.Count</c> 当那个数用,而那是「有几个座位**被坐上了**」——
/// 于是一个**等待中**的三座位房间被当成两座位房间渲染,说出「黑方 / 白方」。
/// 在浏览器里量到的,不是读代码推的。
/// </para>
/// <para>
/// 前端 MUST NOT 存一份副本(在 <c>GameManifest</c> 上加一个 <c>seatCount</c>)——
/// 那正是 <c>remove-manifest-board</c> 删掉的东西,而它的理由在这里逐字成立:
/// **一份没人读的副本错了不会有人发现。**
/// </para>
/// </param>
/// <param name="Rows">行数。</param>
/// <param name="Cols">列数。</param>
public sealed record GameDescriptorDto(
    string GameKey,
    bool IsRated,
    bool SupportsHumanVsHuman,
    bool SupportsAi,
    int SeatCount,
    int? Rows,
    int? Cols);
