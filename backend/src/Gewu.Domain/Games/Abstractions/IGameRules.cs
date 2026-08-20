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
    /// 收的是**这一局的状态**(走子历史 + 服务端侧设置)而不是一个盘面对象:后者会让聚合根重新知道「有一个盘面」,只是换了个名字,
    /// 而盘面要么冗余存盘(第二份真源)、要么每次重放(那就是现在的做法)。每步 O(n) 重放在
    /// 五子棋 &lt; 100 步、象棋 &lt; 200 步的量级上是亚毫秒的,而且**此前的 <c>Game.ReplayBoard</c>
    /// 已经在这么做** —— 本抽象没有让它变慢,只是把重放搬进了规则。真慢了就在规则内部加缓存,
    /// 那是规则的私事,接口不用动。
    /// </para>
    /// </summary>
    /// <param name="state">规则知道的关于这一局的一切:走子历史 + 服务端侧的对局设置。</param>
    /// <param name="intent">这一步想怎么走。</param>
    /// <param name="seat">走这一步的座位号。</param>
    /// <exception cref="Exceptions.InvalidMoveException">这一步不合法。</exception>
    MoveApplication Apply(MatchState state, MoveIntent intent, int seat);
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
/// 超时**不该判负**的棋种 —— 超时时替那个座位走一步(托管),而不是结束对局。
/// <para>
/// 从 <see cref="IGameRules"/> 分出来,理由与 <see cref="IDealtGameRules"/> 相同:两个座位下
/// "判他负、对手胜"是清楚且唯一的答案,四个现有棋种不需要这个成员,而**骗人的实现是下一个人
/// 删不掉的东西**。
/// </para>
/// <para>
/// 斗地主需要它:三个座位里"对手"不唯一,而"农民赢"更不是一个 <c>WinnerUserId</c> 装得下的结果。
/// </para>
/// </summary>
public interface ITimeoutFallbackRules : IGameRules
{
    /// <summary>
    /// 替 <paramref name="seat"/> 走一步 —— 它超时了。
    /// <para>
    /// MUST 是纯函数、无副作用,并 MUST 返回该座位在该局面下**合法**的一步:返回值会走与真人
    /// 落子完全相同的路径(经过 <see cref="IGameRules.Apply"/> 校验并判定结果),非法就抛。
    /// </para>
    /// <para>
    /// <b>MUST 保证推进对局。</b> 一个可以合法地无限重复的动作(牌类里"永远过牌")会把超时
    /// worker 变成一个永不结束的自动对局。斗地主的形式是"能过就过,**不能过时出最小的一手**",
    /// 而牌只会变少。
    /// </para>
    /// <para>
    /// 这条要求**不是防自旋的护栏**:每一次兜底都要等满一个超时周期(worker 从最后一手的
    /// <c>PlayedAt</c> 重算),所以最坏是每个周期一步 —— 慢、可见、不会自旋。它是对局质量的
    /// 要求,所以这里不发明一个"连续兜底次数上限":那个数字会是凭空的。
    /// </para>
    /// <para>
    /// 它收 <see cref="MatchState"/> 而不是只收历史:兜底动作可能需要**服务端侧的对局设置**。
    /// 斗地主首出时要出"手上最小的一张单牌",而手牌在发牌里,不在历史里。
    /// </para>
    /// <para>
    /// **这个签名第一版写错了,而错法值得记下来。** <c>generalize-turn-flow</c> 加本接口时
    /// <see cref="MatchState"/> 还不存在;紧接着的 <c>pass-setup-to-rules</c> 为了同一个理由
    /// (规则读不到设置)把 <see cref="IGameRules.Apply"/> 改成收 <see cref="MatchState"/>,
    /// **却没有回头看几十行之外这个刚加的接缝**。与 <c>enforce-ai-availability</c> 记下的
    /// "修好规则夹具、没看隔七行的 AI 夹具"是同一个形状:**"我刚修过这个类型的问题"是一个
    /// 应该去搜一遍同类的信号,不是一个可以安心的理由。**
    /// </para>
    /// </summary>
    /// <param name="state">规则知道的关于这一局的一切:走子历史 + 服务端侧的对局设置。</param>
    /// <param name="seat">超时的座位号。</param>
    MoveIntent MoveOnTimeout(MatchState state, int seat);
}

/// <summary>
/// 有**隐藏信息**的棋种:同一局的状态,不同座位看到的不一样。
/// <para>
/// 只有需要藏东西的棋种实现它。五子棋 / 一字棋 / 中国象棋 / 成语接龙**一行不动** ——
/// 它们的全部状态就是走子历史,而走子历史本来就广播给所有人。
/// </para>
/// <para>
/// **分出一个接口而不是给 <see cref="IGameRules"/> 加成员**,理由与
/// <see cref="IDealtGameRules"/> / <see cref="IBoardGameRules"/> 相同:留在基接口上,四个棋种
/// 就得各写一个骗人的实现,而**骗人的实现是下一个人删不掉的东西**。
/// </para>
/// </summary>
public interface IPerSeatViewRules : IGameRules
{
    /// <summary>
    /// 座位 <paramref name="seat"/> **能看到**的那一份状态,序列化成对内核不透明的字符串。
    /// <para>
    /// <paramref name="seat"/> 为 <c>null</c> 表示"不占座位的人"(围观者,或进了房间还没入座的)。
    /// 实现 MUST 只给这类人**公开信息**。
    /// </para>
    /// <para>
    /// <b>内核 MUST NOT 解析返回值。</b> 它原样进 <c>GameSnapshotDto.SeatView</c>,由客户端按棋种解。
    /// 这与闯关那条线的 <c>LayoutJson</c> / <c>SolutionJson</c> 是同一个做法:内核不该知道什么是牌,
    /// 而每个棋种要送的东西天生不一样。
    /// </para>
    /// <para>
    /// 它 MUST 是纯函数:同一个 <paramref name="state"/> 与同一个 <paramref name="seat"/> 给出
    /// 同一个字符串。这样"某个座位看得到什么"是可断言的,而不是取决于调用时机。
    /// </para>
    /// <para>
    /// <b>它 MUST NOT 泄漏别人的隐藏状态</b> —— 这条不是靠自觉:一条测试把三个座位的视图
    /// 与另两家的手牌逐张比对,任何一张出现在不该出现的视图里就红。
    /// </para>
    /// </summary>
    /// <param name="state">规则知道的关于这一局的一切。</param>
    /// <param name="seat">看这份快照的座位号;<c>null</c> 表示没有座位的人。</param>
    string ViewFor(MatchState state, int? seat);
}

/// <summary>
/// **谁先走由规则决定**的棋种 —— 挖坑是第一个。
/// <para>
/// 内核的默认是 0 号座位,而那对到目前为止的每一个棋种都成立:五子棋 / 一字棋 / 象棋 / 成语接龙
/// 的先手是**约定**(谁坐 0 号谁先),斗地主的叫分从 0 号起也只是约定。挖坑不是:
/// **持最小 ♣ 的人首叫且首出**,而那是发牌决定的,是规则的一部分。而且它必须**每局轮换** ——
/// 把发牌旋转成"最小 ♣ 总在 0 号"在统计上等价、在体验上不等价:那样同一个人每一局都先叫。
/// </para>
/// <para>
/// 从 <see cref="IGameRules"/> 分出来,理由与 <see cref="IDealtGameRules"/> /
/// <see cref="IPerSeatViewRules"/> 相同:留在基接口上,五个现有棋种就得各写一个骗人的实现,
/// 而**骗人的实现是下一个人删不掉的东西**。
/// </para>
/// </summary>
public interface IFirstSeatRules : IGameRules
{
    /// <summary>
    /// 本局谁先走。开局那一刻调用,此时 <paramref name="state"/> 的走子历史是空的,
    /// 唯一有内容的是设置(发牌)。
    /// <para>
    /// 返回值 MUST 落在 <c>[0, SeatCount)</c> 内 —— 否则这一局**谁都动不了**,而那是一种
    /// 几十秒后才由超时兜底暴露出来的坏。内核会校验并抛
    /// <c>InvalidFirstSeatException</c>,而不是把它存下来。
    /// </para>
    /// <para>
    /// 它 MUST 是纯函数:同一份设置给出同一个座位。重放靠这一点。
    /// </para>
    /// </summary>
    /// <param name="state">规则知道的关于这一局的一切;开局时只有设置。</param>
    int FirstSeat(MatchState state);
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

    /// <summary>斗地主 —— 平台第一个三座位、有隐藏信息、按分结算的棋种。</summary>
    public const string Doudizhu = "doudizhu";
}
