using Gewu.Domain.Entities;
using Gewu.Domain.Enums;
using Gewu.Domain.ValueObjects;

namespace Gewu.Domain.Games.Abstractions;

/// <summary>
/// 一个棋盘对抗棋种的盘面属性。
/// <para>
/// 规则由调用方**作为参数传入**聚合(见 <c>Room.PlayMove</c>),而不是由聚合自己去解析
/// 注册表 —— <c>Domain</c> 因此保持零外部依赖,<c>Room</c> 也仍然是其入参的纯函数,
/// 不需要一个注册表才能在测试里构造出来。
/// </para>
/// <para>
/// 实现 MUST 无状态:同一个实例会被并发的多个房间共享。任何随对局变化的字段都会
/// 变成跨房间的串味。
/// </para>
/// <para>
/// 本接口除盘面属性外还承载两个**平台能力**声明(<see cref="SupportsHumanVsHuman"/>、
/// <see cref="IsRated"/>)。严格说它们不是"规则",放在这里只因为本接口就是 Domain 里
/// "按棋种注册的那个东西",为两个布尔另开一个注册表更贵。**门槛:这类能力声明超过三个时,
/// 应抽成独立的 <c>GameCapabilities</c> 类型,让本接口回到只描述盘面。**
/// </para>
/// </summary>
public interface IGameRules
{
    /// <summary>棋种键,与房间的 <c>GameKey</c>、前端游戏注册表中的 key 一致。</summary>
    string GameKey { get; }

    /// <summary>
    /// 本棋种需要几个座位。现有实现全部为 2。
    /// <para>
    /// 这不是"平台能力"声明,而是**棋种形状**,与 <c>Rows</c> / <c>Cols</c> 同类 —— 所以它不计入
    /// 本接口顶部那条「能力声明超过三个就抽成 <c>GameCapabilities</c>」的门槛。
    /// </para>
    /// <para>
    /// 内核靠它轮转:<c>(seat + 1) % SeatCount</c>。在它之前那一行是
    /// <c>stone == Stone.Black ? Stone.White : Stone.Black</c> —— 整个两人假设就是那一行。
    /// </para>
    /// </summary>
    int SeatCount { get; }

    /// <summary>
    /// 本棋种是否存在**人类对手池** —— 平台有没有为它提供人人对战入口。
    /// <para>
    /// 这是一个**结构性事实**,不是判断。它与 <see cref="IsRated"/> 分开,是因为判断会过期
    /// 而事实不会:见 <see cref="IsRated"/> 上的说明。
    /// </para>
    /// <para>
    /// "本棋种有没有 AI"**不在这里声明** —— 那个问题由 <c>IGameAiRegistry.For(gameKey)</c>
    /// 是否解析出工厂回答。再加一个 <c>SupportsAi</c> 字段就是第二份真源,而两份真源迟早
    /// 不一致、且不一致的那天不会有人发现。人机与人人是两个独立的声明,所以一个棋种可以
    /// 只有其中之一(中国象棋大概会先只有人人对战)。
    /// </para>
    /// </summary>
    bool SupportsHumanVsHuman { get; }

    /// <summary>
    /// 本棋种的对局结束时是否结算 ELO。
    /// <para>
    /// **不变量:本属性为 <c>true</c> 时 <see cref="SupportsHumanVsHuman"/> 必须也为
    /// <c>true</c>。** 由 <c>NInARowRules</c> 构造器与一条遍历注册表的测试双重强制。
    /// 一个只能跟机器人下的棋种不存在有意义的评分:机器人对局是计分的(见 add-ai-opponent
    /// D7 的反套利理由),所以那种阶梯排出来的是"谁刷弱档刷得多",不是棋力。
    /// </para>
    /// <para>
    /// **这条注释此前是错的,值得说明改了什么。** 原文写的是「本字段是限期脚手架,唯一作用是
    /// 让第二个棋种不污染共享排行榜,<c>add-per-game-rating</c> MUST 删除它」。那个判断漏了
    /// 一件事:一字棋没有人人对战。池子分开之后"污染"的理由消失,但"没有有意义的对手池"
    /// 这个理由还在 —— 所以本字段不该被那个变更删掉。
    /// </para>
    /// <para>
    /// 真正的教训是形状:一个语义为"要不要算分"的手工布尔是**判断**,而判断会过期且不报错
    /// —— 一字棋将来有了人人对战,得有人**记得**回来翻它。所以它现在受不变量约束:
    /// 翻 <see cref="SupportsHumanVsHuman"/> 会把评分从"禁止"变成"允许",开不开则是一个
    /// 独立的、需要理由的决定,而不是一件依赖记性的事。**注释里的待办事项不是机制。**
    /// </para>
    /// <para>
    /// 拆除条件:本棋种获得人人对战之后,这个开关对它就不再有约束力。
    /// </para>
    /// </summary>
    bool IsRated { get; }

    /// <summary>
    /// 校验一步棋并给出走完之后的对局状态 —— **走子合法性与胜负判定的唯一入口**。
    /// <para>
    /// 实现 MUST 自行完成:形状校验(本棋种要不要 <c>From</c>)、越界、目标格合法性、走法合法性,
    /// 以及走完之后的 <see cref="GameResult"/>。非法走子 MUST 抛
    /// <see cref="Exceptions.InvalidMoveException"/>,且 MUST NOT 产生任何副作用 ——
    /// 规则实例无状态,同一个实例被并发的多个房间共享。
    /// </para>
    /// <para>
    /// **聚合根不再自己判断盘面。** <c>Room.PlayMove</c> 在调本方法之前只做三件事:房间在不在对局中、
    /// 这人是不是玩家、是不是他的回合。越界、重复落子、走法合不合规,全部由这里回答。
    /// 这是象棋能进这个聚合的前提:它一格上是七种棋子之一 × 两方,胜负是将死 / 困毙,
    /// 与最后一步的位置没有直接关系 —— 没有一条塞得进「连 N 子棋盘」。
    /// </para>
    /// <para>
    /// 收的是**走子历史**而不是一个盘面对象:后者会让聚合根重新知道「有一个盘面」,只是换了个名字,
    /// 而盘面要么冗余存盘(第二份真源)、要么每次重放(那就是现在的做法)。每步 O(n) 重放在
    /// 五子棋 &lt; 100 步、象棋 &lt; 200 步的量级上是亚毫秒的,而且**此前的 <c>Game.ReplayBoard</c>
    /// 已经在这么做** —— 本抽象没有让它变慢,只是把重放搬进了规则。真慢了就在规则内部加缓存,
    /// 那是规则的私事,接口不用动。
    /// </para>
    /// </summary>
    /// <param name="history">本局已走的全部步,按 Ply 升序。</param>
    /// <param name="intent">这一步想怎么走。</param>
    /// <param name="side">走这一步的一方。</param>
    /// <exception cref="Exceptions.InvalidMoveException">这一步不合法。</exception>
    MoveApplication Apply(IReadOnlyList<PlayedMove> history, MoveIntent intent, int seat);
}

/// <summary>
/// 有盘面的棋种 —— 一步棋落在某个格子 / 交叉点上。
/// <para>
/// 从 <see cref="IGameRules"/> 分出来,是因为成语接龙**没有盘面**:它的一步是一个成语。
/// 上一次把 <c>WinLength</c> / <c>CreateBoard</c> 分到 <see cref="INInARowRules"/> 时
/// <c>Rows</c> / <c>Cols</c> 留在了基接口,那时是对的 —— 每个棋种都有盘面。现在不是了。
/// </para>
/// <para>
/// **无盘面的棋种 MUST NOT 返回 <c>0, 0</c> 冒充一个盘面。** 那不只是不整洁:
/// <c>GameDescriptorDto</c> 会把这两个数发给客户端,而前端把 <c>rows &lt;= 0</c> 当作"未知"
/// 并代入 15×15,于是一个成语游戏会被描述成一张五子棋盘。用一个合法值表示"不适用",
/// 错误就是这样悄悄流到界面上的。
/// </para>
/// </summary>
public interface IBoardGameRules : IGameRules
{
    /// <summary>行数。</summary>
    int Rows { get; }

    /// <summary>列数。</summary>
    int Cols { get; }
}

/// <summary>
/// 「连 N 子」类棋种的专有成员。
/// <para>
/// 从 <see cref="IGameRules"/> 分出来,是因为中国象棋没有「连几子」,<see cref="CreateBoard"/>
/// 返回的 <see cref="Board"/> 它也不用。留在基接口上,象棋就得实现两个骗人的成员 ——
/// 而骗人的实现是下一个人删不掉的东西(他无从知道有没有调用方)。
/// </para>
/// <para>
/// 这与 <see cref="IGameRules"/> 上那条能力声明的门槛注释是同一条纪律的另一面:
/// **接口只承载对每个实现都成立的东西。**
/// </para>
/// </summary>
public interface INInARowRules : IBoardGameRules
{
    /// <summary>判胜所需的同色连续子数。</summary>
    int WinLength { get; }

    /// <summary>造一块本棋种的空棋盘。AI 层吃的是这个。</summary>
    Board CreateBoard();

    /// <summary>
    /// 从走子历史重建棋盘。AI 需要看局面,而 <c>Game</c> 已经不再交出 <see cref="Board"/> ——
    /// 它只交出发生过什么,盘面怎么重建属于规则。
    /// </summary>
    /// <param name="history">本局已走的全部步,按 Ply 升序。</param>
    Board ReplayBoard(IReadOnlyList<PlayedMove> history);
}

/// <summary>
/// 开局需要一份**服务端侧对局设置**的棋种 —— 斗地主的发牌是第一个。
/// <para>
/// 从 <see cref="IGameRules"/> 分出来,理由与 <see cref="IBoardGameRules"/> /
/// <see cref="INInARowRules"/> 当初分出来时相同:留在基接口上,四个现有棋种就得各写一个
/// 骗人的实现(<c>=> null</c> 之类),而**骗人的实现是下一个人删不掉的东西** —— 他无从知道
/// 有没有调用方。**接口只承载对每个实现都成立的东西。**
/// </para>
/// <para>
/// 五子棋 / 一字棋 / 象棋 / 成语接龙都不需要秘密:它们的开局是常量,走子历史本来就广播,
/// 没有任何东西要藏。
/// </para>
/// </summary>
public interface IDealtGameRules : IGameRules
{
    /// <summary>
    /// 造一份本局的设置。**同一个种子 MUST 产出同一个字符串** —— 重放靠这一点,测试钉住
    /// 一局牌也靠这一点。
    /// <para>
    /// 实现 MUST NOT 用 <c>System.Random</c>:它的算法在 .NET 版本之间变过,而这条要求
    /// 跨版本成立(同 <c>TetrisPieceSequence</c> 与 <c>DoudizhuDeal</c> 上写下的理由)。
    /// </para>
    /// <para>
    /// 种子由**调用方**给,取自 Application 层的 <c>ISeedProvider</c>。Domain 不自己取随机数,
    /// 所以这个接口收一个 <c>int</c> 而不是一个随机源。
    /// </para>
    /// <para>
    /// 返回的字符串对内核完全不透明:<c>Game</c> 存它、不读它。见 <c>Game.Setup</c>。
    /// </para>
    /// </summary>
    /// <param name="seed">开局种子。</param>
    string CreateSetup(int seed);
}

/// <summary>
/// 按棋种键解析 <see cref="IGameRules"/>。未注册的键返回 <c>null</c>,
/// 由 handler 映射成 404 —— 与 <c>IPuzzleRulesRegistry</c> 同一形状,
/// 平台上"按游戏键解析实现"只该有一种写法。
/// </summary>
public interface IGameRulesRegistry
{
    /// <summary>取指定棋种的规则,未注册则 <c>null</c>。</summary>
    /// <param name="gameKey">棋种键。</param>
    IGameRules? For(string gameKey);

    /// <summary>
    /// 全部已登记的棋种规则。顺序不作保证 —— 需要稳定顺序的调用方自己排。
    /// <para>
    /// 加这个成员是为了 <c>GET /api/games</c>:客户端要知道哪些棋种计分,而在前端再维护一份
    /// 副本会让失配变得**看不见**(症状是"一个永远空着的榜",与"新棋种还没人下过"一模一样)。
    /// 把注册表投影出去,新棋种自动出现在端点上,没有第二份清单需要同步。
    /// </para>
    /// <para>
    /// 它也让"遍历注册表"的不变量测试(<c>IsRated ⇒ SupportsHumanVsHuman</c>)能对着注册表本身
    /// 跑,而不是对着一份手写的棋种清单 —— 后者会在加象棋时静静通过。
    /// </para>
    /// </summary>
    IReadOnlyCollection<IGameRules> All { get; }
}

/// <summary>
/// 平台内置棋种的键。字符串常量而非枚举 —— 新增棋种不该需要改一个共享类型;
/// 这里只是给内置棋种一个不会打错的名字。
/// </summary>
public static class GameKeys
{
    /// <summary>五子棋。</summary>
    public const string Gomoku = "gomoku";

    /// <summary>一字棋。</summary>
    public const string TicTacToe = "tictactoe";

    /// <summary>中国象棋。</summary>
    public const string Xiangqi = "xiangqi";

    /// <summary>成语接龙 —— 平台第一个不在盘面上进行的对战棋种。</summary>
    public const string IdiomChain = "idiom-chain";
}
