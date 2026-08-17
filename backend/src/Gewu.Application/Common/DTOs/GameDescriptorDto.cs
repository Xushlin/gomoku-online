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
/// <param name="Rows">行数。</param>
/// <param name="Cols">列数。</param>
public sealed record GameDescriptorDto(
    string GameKey,
    bool IsRated,
    bool SupportsHumanVsHuman,
    int? Rows,
    int? Cols);
