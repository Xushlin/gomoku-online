# game-rules-registry Specification

## Purpose
TBD - created by archiving change add-game-rules-registry. Update Purpose after archive.
## Requirements
### Requirement: `IGameRules` 描述一个棋种的盘面属性

`Gewu.Domain` SHALL 定义 `IGameRules`:

- `GameKey` —— 棋种键,与房间的 `GameKey`、游戏注册表中的 key 一致。
- `SeatCount` —— 本棋种需要几个座位。**本次新增**,现有实现全部为 2。
- `SupportsHumanVsHuman` —— 本棋种是否存在人类对手池(平台是否提供人人对战入口)。
- `IsRated` —— 本棋种的对局是否结算 ELO。
- `Apply(history, intent, seat)` —— 判定并应用一手,出手方以**座位号**给出。

**本条此前还把 `Rows` / `Cols` / `WinLength` / `CreateBoard()` / `IsInBounds(Position)` 列在 `IGameRules` 名下,那是过期的。** `generalize-match-domain` 把 `WinLength` / `CreateBoard` 下沉到 `INInARowRules`,`generalize-match-payload` 又把 `Rows` / `Cols` 下沉到 `IBoardGameRules`(理由是成语接龙没有棋盘,而把 `0,0` 当"不适用"是本内核明令禁止的),但这条 requirement 一直没跟上。顺手改正,不是本变更的新决定 —— 与 `enable-xiangqi-human-play` 顺手删掉 `board: { rows, cols }` 是同一类修正。

层级因此是:`IGameRules`(所有棋种)→ `IBoardGameRules`(有棋盘的,带 `Rows` / `Cols`)→ `INInARowRules`(连 N 子的,带 `WinLength` / `CreateBoard`)。

实现 MUST 是无状态的:同一个实例会被并发的多个房间共享,MUST NOT 持有任何对局状态。

`Domain` MUST NOT 因此获得任何外部依赖 —— 规则由调用方传入聚合,注册表住在 `Infrastructure`。

**不变量一:`IsRated` 为 `true` 时 `SupportsHumanVsHuman` MUST 也为 `true`。** 一个只能跟机器人下的棋种不存在有意义的评分:机器人对局是计分的(见 `ai-opponent` 的反套利约束),所以它的阶梯排出来的是"谁刷弱档刷得多",而不是棋力。

**不变量二:`IsRated` 为 `true` 时 `SeatCount` MUST 等于 2。** 现有 ELO 是两人制的,三人及以上的评分需要单独设计。这条与不变量一同源同形:把"三人局暂不计分"钉成**结构性约束**,而不是留在注释里的 TODO —— 后者这个仓库付过账(`add-game-capabilities` 就是把一个手工维护的布尔约束成结构性事实的那次)。三人评分真设计出来那天,动作是**改这条不变量**,而不是希望有人记得回来翻某个布尔。

两条不变量 MUST 在构造器中校验,违反时抛 `ArgumentException` —— 在构造处失败,而不是等到某个 handler 算出一个没人该看的分数。两条都 MUST 由遍历注册表的测试强制,不能只写在文档里。

`SupportsHumanVsHuman` 是**结构性事实**,`IsRated` 是**判断**,`SeatCount` 是**棋种形状**(与 `Rows` / `Cols` 同类,不是平台能力)。这个区分决定了下面那条阈值怎么数:本接口承载的**平台能力**声明仍是两个(`SupportsHumanVsHuman`、`IsRated`),`SeatCount` 不计入。超过三个时 SHOULD 抽成独立的 `GameCapabilities` 类型,使 `IGameRules` 回到只描述棋种本身。

平台 MUST NOT 增加 `SupportsAi` 之类的声明 —— 该问题由 `IGameAiRegistry.For(gameKey)` 是否解析出工厂回答,再加一个字段就是第二份真源。

#### Scenario: 五子棋的棋种属性
- **WHEN** 读取 `gomoku` 规则
- **THEN** `SeatCount == 2`;作为 `INInARowRules` 时 `Rows == 15`、`Cols == 15`、`WinLength == 5`

#### Scenario: 现有棋种一律两个座位
- **WHEN** 遍历注册表中每一个 `IGameRules`
- **THEN** 每一个 `SeatCount == 2` —— 本变更行为零变化,这条是它的可执行形式

#### Scenario: 规则可安全共享
- **WHEN** 两个房间同时用同一个规则实例落子
- **THEN** 两局互不影响 —— 规则实例上 MUST NOT 出现任何随对局变化的字段

#### Scenario: 五子棋计分
- **WHEN** 读取 `gomoku` 规则
- **THEN** `IsRated == true` 且 `SupportsHumanVsHuman == true` 且 `SeatCount == 2`

#### Scenario: 一字棋无人类对手池,因此不计分
- **WHEN** 读取 `tictactoe` 规则
- **THEN** `SupportsHumanVsHuman == false`,因此 `IsRated == false`

#### Scenario: 两条不变量都被测试强制
- **WHEN** 遍历注册表中每一个 `IGameRules`
- **THEN** 每一个满足 `IsRated == false || SupportsHumanVsHuman == true`,且满足 `IsRated == false || SeatCount == 2`;构造一个违反任一式的规则实例 MUST 失败

#### Scenario: 拒绝一个三座位却要计分的规则
- **WHEN** 构造一个 `SeatCount == 3` 且 `IsRated == true` 的规则实例
- **THEN** 抛 `ArgumentException`,消息点明是哪条不变量

#### Scenario: 是否支持人机由 AI 注册表回答
- **WHEN** 检视 `IGameRules` 的成员
- **THEN** MUST NOT 存在 `SupportsAi` 或同义字段;"该棋种有没有 AI" MUST 由 `IGameAiRegistry.For` 解析

### Requirement: `NInARowRules` 以参数覆盖全部"连 N 子"棋种

`Gewu.Domain` SHALL 提供 `NInARowRules(gameKey, rows, cols, winLength, supportsHumanVsHuman = true, isRated = true)`,实现 `IGameRules`。

五子棋 SHALL 注册为 `NInARowRules("gomoku", 15, 15, 5)`。一字棋 SHALL 注册为 `NInARowRules("tictactoe", 3, 3, 3, supportsHumanVsHuman: false, isRated: false)` —— 判胜算法完全相同,只有三个数不同,因此 MUST NOT 为其另写一份实现。

两个布尔的默认值 SHALL 均为 `true` —— 一个棋种默认有人类对手池且算分,**不**是那一侧才需要在调用处写出理由。

构造器 MUST 校验不变量 `IsRated ⇒ SupportsHumanVsHuman`,违反时抛 `ArgumentException` —— 在构造处失败,而不是等到某个 handler 算出一个没人该看的分数。

#### Scenario: 同一实现服务不同尺寸
- **WHEN** 分别以 `(15,15,5)` 与 `(3,3,3)` 构造
- **THEN** 两者都是合法的 `IGameRules`,各自的 `IsInBounds` 与判胜按自己的参数工作

#### Scenario: 参数校验
- **WHEN** 以非正的 `rows` / `cols` / `winLength`,或 `winLength` 大于 `max(rows, cols)` 构造
- **THEN** 构造失败 —— 一个永远赢不了的棋种是配置错误,不是可玩的棋

#### Scenario: 一字棋不另写判胜
- **WHEN** 检视一字棋的规则实现
- **THEN** 它 MUST 是 `NInARowRules` 的一个实例,`Gewu.Domain` 中 MUST NOT 存在名为 `TicTacToeRules` 的独立判胜实现

#### Scenario: 拒绝一个没有人类对手却要计分的棋种
- **WHEN** 以 `supportsHumanVsHuman: false, isRated: true` 构造
- **THEN** 抛 `ArgumentException`

#### Scenario: 默认是有人类对手且计分
- **WHEN** 以 `NInARowRules("some-new-game", 3, 3, 3)` 构造
- **THEN** `SupportsHumanVsHuman == true` 且 `IsRated == true`

### Requirement: 规则注册表按棋种键解析,未知键返回 `null`

`Gewu.Infrastructure` SHALL 提供 `IGameRulesRegistry`,由 DI 注入的 `IGameRules` 集合构成,按 `GameKey` 解析;未注册的键 SHALL 返回 `null`,handler MUST 将其映射为 404。

形状 MUST 与 `IPuzzleRulesRegistry` 一致 —— 平台上"按游戏键解析实现"只该有一种写法。

#### Scenario: 解析已注册棋种
- **WHEN** 以 `"gomoku"` 解析
- **THEN** 返回 `Rows == 15` 的规则

#### Scenario: 未知棋种
- **WHEN** 以任意未注册键解析
- **THEN** 返回 `null`,调用方据此返回 404

### Requirement: 扩展点 —— 加一个棋类游戏是一个类加一行注册

新增一个棋盘对抗游戏 MUST 只需要:

1. 一个 `IGameRules` 实现(连 N 子类棋种直接用 `NInARowRules`,连类都不用写);
2. 一处 `services.AddSingleton<IGameRules, ...>()` 注册。

MUST NOT 需要:修改 `Room`、`Game`、`Board`、任何 handler、任何 DTO、任何 Hub 方法,或任何既有棋种的文件。

本要求由 `add-tictactoe` 走一遍流程自验证。

#### Scenario: 扩展仪式
- **WHEN** 假想新增一个 `connect-four` 棋种
- **THEN** 从 diff 角度:纯新增一行注册(必要时加一个规则类),`grep` MUST NOT 显示任何既有聚合、handler、DTO 或 Hub 文件被修改

#### Scenario: 落子路径不含棋种分支
- **WHEN** 检索落子相关的 handler 与聚合代码
- **THEN** MUST NOT 出现任何形如 `if (gameKey == "gomoku")` 的分支 —— 棋种差异只经 `IGameRules` 表达

### Requirement: 一字棋登记进规则注册表

`Gewu.Infrastructure` 的 DI 组合根 SHALL 把 `NInARowRules("tictactoe", 3, 3, 3, isRated: false)` 注册为一个 `IGameRules`,使 `IGameRulesRegistry.For("tictactoe")` 能解析出规则。

本次注册 MUST NOT 修改 `NInARowRules`、`Board`、`Position`、`Room` 或五子棋的任何既有文件 ——
这正是 `add-game-rules-registry` 承诺的"一个类加一处注册",由一字棋第一次真正验证。
若实现过程中发现必须修改上述文件,该修改 MUST 被记录在本变更的 tasks 里作为注册表的欠债,
而不是悄悄改掉。

#### Scenario: 键可解析
- **WHEN** 以 `"tictactoe"` 调用 `IGameRulesRegistry.For`
- **THEN** 返回非 `null` 的规则,其 `Rows == 3`、`Cols == 3`、`WinLength == 3`、`IsRated == false`

#### Scenario: 未登记的键仍返回 null
- **WHEN** 以 `"xiangqi"` 调用 `IGameRulesRegistry.For`
- **THEN** 返回 `null`

#### Scenario: 判胜在 3×3 上按 3 子生效
- **WHEN** 一方在 3×3 盘上连成任意一行 / 一列 / 一条对角线
- **THEN** `NInARowRules("tictactoe", 3, 3, 3)` 的判胜判定该方获胜

### Requirement: `IGameRulesRegistry` 能枚举全部已登记棋种

`IGameRulesRegistry` SHALL 提供 `IReadOnlyCollection<IGameRules> All { get; }`,返回全部已登记的规则实例;顺序不作保证。

**`add-web-per-game-rating` 的提案漏了这一条。** 它写着"handler 直接读 `IGameRulesRegistry`",但那个接口此前只有
`For(gameKey)` —— 能按键取,不能列举。没有 `All`,`GET /api/games` 只能拿一份手写的棋种清单去
逐个 `For()`,而那正是本变更要消灭的第二份清单,只是换了个位置。

它同时让"遍历注册表"的不变量测试(`IsRated ⇒ SupportsHumanVsHuman`)能对着注册表本身跑,
而不是对着一份手写清单 —— 后者会在加中国象棋时静静通过。

`IPuzzleRulesRegistry` 与 `IGameAiRegistry` 目前同样只有 `For()`。**不顺手给它们加** ——
今天没有消费者,而"三个注册表长得一样"不构成给两个接口加未使用成员的理由。

#### Scenario: 列出全部
- **WHEN** 注册表登记了五子棋与一字棋
- **THEN** `All` 含且仅含这两个实例

#### Scenario: 与 `For` 一致
- **WHEN** 对 `All` 中每个实例的 `GameKey` 调 `For`
- **THEN** 每次都返回同一个实例

### Requirement: `GET /api/games` 把棋种注册表投影给客户端

Api 层 SHALL 暴露 `GET /api/games`(`[Authorize]`),返回 `IGameRulesRegistry` 中每个已登记棋种的一条描述:

```
public sealed record GameDescriptorDto(
    string GameKey,
    bool IsRated,
    bool SupportsHumanVsHuman,
    bool SupportsAi,
    int SeatCount,
    int? Rows,
    int? Cols);
```

`SeatCount` MUST **非空**,投影自 `IGameRules.SeatCount`。每个有 `IGameRules` 的棋种都有座位数,
不存在「不适用」—— 这正是它与 `Rows` / `Cols` 的区别。

**它存在是因为客户端读不到「这个棋种有几个座位」,而它需要。** 房间侧栏此前用
`state.seats.length` 当那个数用,而 `seats` 只含**在座的**座位 —— 于是一个**等待中**的
三座位房间被当成两座位房间渲染,说出「黑方 / 白方」。在屏幕上量到的。

前端 MUST NOT 存一份副本(`GameManifest` 上加一个 `seatCount`)——
那正是 `remove-manifest-board` 删掉的东西,而它的理由在这里逐字成立。

`Rows` / `Cols` MUST 可空,且**当且仅当**该规则实现 `IBoardGameRules` 时非空。`null` 的含义是"这个棋种没有盘面",与"客户端还没拿到描述符"是两件不同的事,客户端 MUST 分别处理(见 `web-game-board`)。

`SupportsAi` MUST 投影自 `IGameAiRegistry.For(gameKey) is not null` —— 与 `POST /api/rooms/ai` 的校验读同一份注册表,所以客户端看到的与服务端会接受的**不可能不一致**。它 MUST NOT 来自 `IGameRules` 上的一个手写布尔:那会是同一件事的第二个真源,而它失配的症状是**一个永远 400 的按钮**。

投影的**遍历断言 MUST 两侧都有样本**:`SeatCount` 的取值集合里 MUST 同时出现 `2` 与大于 `2`
的值 —— 一条只走到 2 的遍历,在一个恒返回 2 的实现下是绿的。

它 MUST 是**投影**而不是第二份清单:注册表加一个棋种,本端点自动多一条;实现 MUST NOT 内联任何
"哪些棋种存在"的硬编码列表 —— 与建房校验不许内联棋种白名单是同一条理由。

Handler MUST NOT 访问数据库。两份注册表本来就在内存里,这是一次纯投影。

端点只覆盖 `IGameRules`(对战棋种)。谜题类走 `IPuzzleRules`,已经有自己的一条 REST 线
(`GET /api/puzzles/games/{gameKey}/levels`),MUST NOT 混进本端点 —— 两者塞进一个 DTO 会造出
一半字段永远为空的形状,而那种 DTO 的下一步永远是加一个 `type` 字段然后到处 switch。

**存在的理由**:前端要渲染"棋种切换"就得知道哪些棋种计分,而这个事实此前只存在于服务端。
备选是在前端 `GameManifest` 上加一个 `rated` 布尔副本 —— 不做:`rated` 失配的症状是
**一个永远空着的榜**,而那与"新棋种还没人下过"在屏幕上一模一样。分不出来的失配 =
不会被发现的失配。`SupportsAi` 是同一条理由的第二次应用。

#### Scenario: 列出全部已登记棋种
- **WHEN** 登录用户 `GET /api/games`
- **THEN** HTTP 200;返回条数等于 `IGameRulesRegistry` 中已登记棋种的数量

#### Scenario: 有盘面的棋种带尺寸
- **WHEN** 描述 `gomoku`
- **THEN** `rows == 15`、`cols == 15`

#### Scenario: 无盘面的棋种尺寸为 null
- **WHEN** 描述一个不实现 `IBoardGameRules` 的棋种
- **THEN** `rows == null`、`cols == null`,MUST NOT 是 `0`

#### Scenario: 有 AI 的棋种
- **WHEN** 描述 `gomoku`
- **THEN** `supportsAi == true`

#### Scenario: 没有 AI 的棋种
- **WHEN** 描述 `idiom-chain`
- **THEN** `supportsAi == false`

#### Scenario: 描述符与建房校验读同一份注册表
- **WHEN** 对每个已登记棋种比对 `supportsAi` 与 `POST /api/rooms/ai` 是否接受它
- **THEN** 两者 MUST 逐一相符

### Requirement: `IGameRules.Apply` 是走子合法性与胜负判定的唯一入口

`IGameRules` SHALL 提供:

```
public readonly record struct MatchState(string? Setup, IReadOnlyList<PlayedMove> History);

MoveApplication Apply(MatchState state, MoveIntent intent, int seat);
```

规则 MUST 自行完成:形状校验(该棋种要不要 `From`)、越界、目标格合法性、走法合法性、
以及走完之后的 `GameResult`。非法走子 MUST 抛 `InvalidMoveException`,且 MUST NOT 产生副作用
—— 规则实例是无状态的,同一个实例被并发的多个房间共享。

判出胜负时,规则 MUST 同时给出**赢家的座位号**(`MoveApplication.WinnerSeat`)。它 MUST NOT 被当成
"走这一步的人"的同义词:落子类棋种里赢家恒等于走子方,但那是**那些棋种**的性质,不是接口的性质。

`state.History` 是本局已走的全部步,按 `Ply` 升序。`state.Setup` 是本局的服务端侧对局设置
(见 `room-and-gameplay` 的 `Game.Setup`),不需要设置的棋种恒为 `null`。规则从这两者重建自己
需要的表示。

#### 状态是一个记录,而不是两个平铺的参数

`Apply(history, setup, intent, seat)` 有四个参数,其中两个是**这局到目前为止的状态**、两个是
**这一步**。四个平铺的参数要求读代码的人记住顺序;`Apply(state, intent, seat)` 按它们实际的用法
分组。

**这不是为将来的扩展付钱** —— 本 spec 已经拒绝过那条理由(不加 JSON 载荷列,因为"一个成语是
一个标量")。这里的理由是可读性:`state` 是一个有名字的东西,而 `(history, setup)` 是两个碰巧
相邻的参数。

#### `Setup` 到得了规则,到不了客户端

`state.Setup` 让规则读得到发牌,而那条「任何 DTO 都不得有名字含 `Setup` 的成员」的反射断言
**不变**。这是同一条平台规则的两半:规则在服务端,所以它可以知道;客户端不能。

**聚合根 MUST NOT 再自行判断盘面。** `Room.PlayMove` 在调用本方法之前只做三件事:房间在不在
对局中、这人是不是玩家、是不是他的回合。越界、重复落子、走法是否合规,全部 MUST 由本方法回答。

传状态而不是传一个盘面对象:后者会让聚合根重新知道「有一个盘面」,只是换了个名字,而盘面要么
冗余存盘(第二份真源)、要么每次重放(那就是现在的做法)。每步 O(n) 重放在这个量级上是亚毫秒的。

第三个参数是座位号而不是 `Stone`。

#### Scenario: 合法落子返回 Ongoing
- **WHEN** 对空盘调 `Apply(new MatchState(null, []), MoveIntent.Place((7,7)), 0)`(五子棋)
- **THEN** 返回 `Result == Ongoing`、`WinnerSeat == null`

#### Scenario: 越界由规则拒绝
- **WHEN** 对一字棋调 `Apply(new MatchState(null, []), MoveIntent.Place((3,0)), 0)`
- **THEN** 抛 `InvalidMoveException`

#### Scenario: 重复落子由规则拒绝
- **WHEN** 历史里 (0,0) 已有子,再对 (0,0) 落子
- **THEN** 抛 `InvalidMoveException`

#### Scenario: 判胜时说出赢家座位
- **WHEN** 落子类棋种的一步造成连 N 子,由座位 `s` 走出
- **THEN** 返回 `Result == Decided` 且 `WinnerSeat == s`

#### Scenario: 不需要设置的棋种收到的 Setup 恒为 null
- **WHEN** 一个不实现 `IDealtGameRules` 的棋种被调用
- **THEN** `state.Setup == null`

#### Scenario: 需要设置的棋种收到开局那份设置
- **WHEN** 一个实现 `IDealtGameRules` 的棋种被调用
- **THEN** `state.Setup` 恰好是 `Game.Setup`,一字不改

  这一条是本变更存在的理由:`add-match-setup` 把设置存下来了,而**规则拿不到它** ——
  一个存下来再也没人读的值。

#### Scenario: 无状态
- **WHEN** 同一个规则实例被两个不同的 `MatchState` 先后调用
- **THEN** 两次结果只取决于各自的 `state`,MUST NOT 互相影响

### Requirement: `MoveIntent.From` 可空,形状由规则校验

`Gewu.Domain` SHALL 定义:

```
public readonly record struct MoveIntent(Position? From, Position? To, string? Text);
public readonly record struct PlayedMove(Position? From, Position? To, string? Text, int Seat);
public readonly record struct MoveApplication(GameResult Result, int? WinnerSeat);
```

`From` 为 `null` 表示**落子类**棋种的一步(五子棋 / 一字棋:只有落点);非 `null` 表示
**走子类**棋种的一步(中国象棋:从哪儿到哪儿)。两种载荷(位置 / 文本)的互斥不变量由
`room-and-gameplay` 的「一步棋要么是位置,要么是文本」那条 requirement 定义,本条 MUST NOT 复述它
—— 本 spec 此前写的是 `MoveIntent(Position? From, Position To)`,即 `generalize-match-payload`
之前的签名,而那次改动新增了一条正确的 requirement 却把这条错的留在原地。**同一个事实被两条
requirement 描述、其中一条是旧的,是这个仓库反复付账的那个形状。**

规则 MUST 校验形状:落子类棋种收到非 `null` 的 `From` MUST 抛 `InvalidMoveException`,
走子类棋种收到 `null` 的 `From` 同样 MUST 抛。**这条校验属于规则,不属于聚合根** ——
聚合根不知道哪些棋种走子。

`MoveApplication.WinnerSeat` MUST 非 `null` 当且仅当 `Result == Decided`,**由构造器强制**。
`Ongoing` / `Draw` 带一个赢家、或 `Decided` 不带赢家,都 MUST 在构造时抛异常,而 MUST NOT 只写在
文档里 —— 与上面那条互斥载荷同一种机制,同一个理由。

#### Scenario: 落子类拒绝带起点的走子
- **WHEN** 对五子棋调 `Apply([], MoveIntent(from: (0,0), to: (1,1)), 0)`
- **THEN** 抛 `InvalidMoveException` —— 五子棋没有「从哪儿走」

#### Scenario: 历史保留起点
- **WHEN** 一步走子类的棋被记录
- **THEN** `PlayedMove.From` 非 `null`,重放时能还原

#### Scenario: 结果与赢家必须一致
- **WHEN** 构造 `MoveApplication(GameResult.Ongoing, 0)` 或 `MoveApplication(GameResult.Decided, null)`
- **THEN** 构造 MUST 失败并抛异常

### Requirement: `INInARowRules` 承载连 N 子专有成员

`Gewu.Domain` SHALL 定义 `INInARowRules : IGameRules`,承载 `int WinLength { get; }` 与 `Board CreateBoard()`。

`IGameRules` 本体 MUST NOT 再有这两个成员 —— 中国象棋没有「连几子」,`CreateBoard()` 返回的
`Board` 它也不用。留在基接口上,象棋就得实现两个骗人的成员,而骗人的实现是下一个人删不掉的
(他无从知道有没有调用方)。

这与 `IGameRules` 上那条既有门槛注释是同一条纪律的另一面:**接口只承载对每个实现都成立的东西。**

n-in-a-row 的 AI 工厂 MUST 接 `INInARowRules`;走子类棋种自带表示,不经过 `Board`。

#### Scenario: 五子棋实现窄接口
- **WHEN** 检视 `NInARowRules`
- **THEN** 它实现 `INInARowRules`,`WinLength` / `CreateBoard()` 仍然可用

#### Scenario: 基接口上没有连子概念
- **WHEN** 反射检视 `IGameRules` 的成员
- **THEN** MUST NOT 含 `WinLength` 或 `CreateBoard`

### Requirement: `SupportsHumanVsHuman` 由服务端强制,不只是被声明

平台 SHALL 在服务端拒绝为 `SupportsHumanVsHuman == false` 的棋种创建人人对战房间;该字段 MUST NOT 只是一个提供给客户端参考的声明。

理由是这个字段的措辞本身:它的定义是「平台是否提供人人对战入口」。若 `POST /api/rooms` 接受该棋种,平台就**确实**提供了一个入口,字段的值与事实相反 —— 而 `IsRated ⇒ SupportsHumanVsHuman` 这条不变量正是靠它作为**结构性事实**才成立的。判断会过期,结构性事实不会;但一个没人强制的"结构性事实"只是另一个判断。

客户端据此隐藏"创建房间"入口是**展示决定**,MUST NOT 被当作强制手段 —— 任何人都可以直接调 API。

不变量与本条的分工:`IsRated ⇒ SupportsHumanVsHuman` 由构造器强制(见上),回答"这个棋种能不能计分";本条由建房校验强制,回答"平台给不给它开人人对战入口"。

#### Scenario: 声明与行为一致
- **WHEN** 对注册表中每一个 `IGameRules`,尝试以其键创建人人对战房间
- **THEN** 成功当且仅当 `SupportsHumanVsHuman == true`

#### Scenario: 人机不受约束
- **WHEN** 以 `SupportsHumanVsHuman == false` 的棋种创建人机房间
- **THEN** 成功 —— 本条只约束人类对手池,与 AI 无关

#### Scenario: 一字棋不计分的前提为真
- **WHEN** 检视 `tictactoe` 为何 `IsRated == false`
- **THEN** 其依据「唯一的对手是机器人」MUST 是服务端强制的事实,而不只是当前 Web 界面恰好没有入口

### Requirement: 盘面尺寸属于 `IBoardGameRules`,不属于每一个棋种

`Gewu.Domain` SHALL 定义 `IBoardGameRules : IGameRules`,由它承载 `Rows` 与 `Cols`;`IGameRules` 本身 MUST NOT 再声明这两个成员。

`INInARowRules` SHALL 继承 `IBoardGameRules`,`XiangqiRules` SHALL 实现它。**没有盘面的棋种(成语接龙)两个都不实现。**

这与 `INInARowRules` 当初从 `IGameRules` 分出去是同一条纪律,也是同一句话:**接口只承载对每个实现都成立的东西**,而「骗人的实现是下一个人删不掉的东西」。上一次留下 `Rows`/`Cols` 不是疏忽 —— 那时每个棋种都有盘面,这句话是真的。它现在不真了。

**MUST NOT 让无盘面的棋种返回 `0, 0`。** 那不只是不整洁:`GameDescriptorDto` 会把这两个数发给客户端,而前端 `boardSizeFor` 把 `rows <= 0` 当作"未知"并代入 **15×15**,于是一个成语游戏会被描述成一张五子棋盘。用一个合法值表示"不适用",错误就是这样悄悄流到界面上的。

#### Scenario: 连 N 子棋种有盘面
- **WHEN** 读取 `gomoku` 规则
- **THEN** 它是 `IBoardGameRules`,`Rows == 15`、`Cols == 15`

#### Scenario: 象棋有盘面但不是连 N 子
- **WHEN** 读取 `xiangqi` 规则
- **THEN** 它是 `IBoardGameRules` 但**不是** `INInARowRules`,`Rows == 10`、`Cols == 9`

#### Scenario: 无盘面的棋种不实现该接口
- **WHEN** 一个棋种没有盘面
- **THEN** 它 MUST NOT 实现 `IBoardGameRules`,也 MUST NOT 以任何数值伪装出一个盘面

#### Scenario: 基接口不再声明尺寸
- **WHEN** 审阅 `IGameRules` 的成员
- **THEN** 其中 MUST NOT 出现 `Rows` 或 `Cols`

### Requirement: 内置棋种清单是它所需依赖的函数

`BuiltInGameRules.All(IIdiomLexicon idioms)` SHALL 返回**全部**内置棋种的规则实例,而 DI 与
"遍历注册表"的不变量测试 MUST 都从它取。

它是**函数**而不是静态列表,因为有的棋种需要依赖(成语接龙要一本词典)。诱人的替代方案是
"这个棋种在 DI 里单独注册",而那正是本仓库修过两次的缺陷:**一份手写的清单,被一条遍历测试
当成注册表**。

清单 SHALL 包含 `doudizhu`。`DoudizhuRules` 不需要外部依赖(发牌与牌型都是纯函数),所以它进
清单不需要新参数 —— 但它 MUST 进这**同一份**清单,MUST NOT 只在 DI 里注册。

#### Scenario: 清单与生产 DI 一致
- **WHEN** 比较 `BuiltInGameRules.All` 的键集合与 DI 注册的键集合
- **THEN** 两者相等

#### Scenario: 斗地主在清单里
- **WHEN** 遍历 `BuiltInGameRules.All(lexicon)`
- **THEN** 其中有一个 `GameKey == "doudizhu"`,且它 `SeatCount == 3`

#### Scenario: 遍历注册表的不变量自动覆盖新棋种
- **WHEN** `IsRated ⇒ SupportsHumanVsHuman`、`IsRated ⇒ SeatCount == 2`、以及建房能力那几条遍历测试运行
- **THEN** 它们**不需要改一个断言**就覆盖到斗地主

### Requirement: `IDealtGameRules` 承载"这个棋种开局要一份服务端侧设置"

`Gewu.Domain` SHALL 定义:

```
public interface IDealtGameRules : IGameRules
{
    string CreateSetup(int seed);
}
```

只有需要秘密初始状态的棋种实现它。五子棋、一字棋、中国象棋、成语接龙**一行不动** —— 它们的开局是常量,走子历史本来就广播,没有任何东西要藏。

**分出一个接口而不是给 `IGameRules` 加成员**,理由与 `IBoardGameRules` / `INInARowRules` 当初分出来时相同:留在基接口上,四个棋种就得各写一个骗人的实现,而**骗人的实现是下一个人删不掉的东西**。

`CreateSetup` MUST 是纯函数:同一个 `seed` MUST 产出同一个字符串。这是重放的前提,也是测试能钉住一局牌的前提。实现 MUST NOT 用 `System.Random` —— 它的算法在 .NET 版本之间变过,而这条要求跨版本成立。

`seed` 由**调用方**给,取自 Application 层的 `ISeedProvider`。Domain MUST NOT 自己取随机数。

返回的字符串对内核完全不透明,但**规则读得到它**(`MatchState.Setup`)—— 见 `IGameRules.Apply`。

#### Scenario: 恰好两个内置棋种实现它
- **WHEN** 遍历 `BuiltInGameRules.All(lexicon)`
- **THEN** 恰好两个实现 `IDealtGameRules`,它们的 `GameKey` 恰好是 `{"doudizhu", "wakeng"}`

  这一条走过两级:先是"没有一个实现它"(`add-match-setup` 钉的是"没有偷偷改动现有棋种"),
  再是"恰好一个"(斗地主)。它按自己的预告红了第二次,而**那个时刻要问的问题被真的问了**:
  这两个棋种的设置是同一种东西吗?是 —— 两者都是"一副洗好的牌",都由一个种子确定,都
  MUST NOT 出服务端。**这个 seam 因此第一次被一个不同的游戏验证过**,而不只是被第二个
  实现填满:挖坑的牌是 52 张无王、16/16/16 + 4,与斗地主的 54 张、17/17/17 + 3 没有一处
  共用的常量。

  「恰好」的牙没有拔掉:第三个的那天它还会红。

#### Scenario: 同一个种子给出同一份设置
- **WHEN** 对同一个实现两次调 `CreateSetup(20260819)`
- **THEN** 两个字符串相等

#### Scenario: 设置由 Application 造好再交给聚合
- **WHEN** 一个需要设置的棋种开局
- **THEN** `ISeedProvider.NextSeed()` 被调用一次,其结果传给 `CreateSetup`,而 `CreateSetup` 的结果传给 `Room.JoinAsPlayer` —— **`Room` 与 `Game` 都不曾见过那个种子**

#### Scenario: 不需要设置的棋种不触发随机源
- **WHEN** 一个不实现 `IDealtGameRules` 的棋种开局
- **THEN** `ISeedProvider.NextSeed()` MUST NOT 被调用

### Requirement: `ITimeoutFallbackRules` 让超时变成"替他走一步"而不是"判他负"

`Gewu.Domain` SHALL 定义:

```
public interface ITimeoutFallbackRules : IGameRules
{
    MoveIntent MoveOnTimeout(MatchState state, int seat);
}
```

只有"超时不该判负"的棋种实现它。五子棋、一字棋、中国象棋、成语接龙**一行不动** —— 两个座位下"判他负、对手胜"是清楚且唯一的答案。

分出一个接口而不是给 `IGameRules` 加成员,理由与 `IBoardGameRules` / `IDealtGameRules` 相同:**骗人的实现是下一个人删不掉的东西**。

它收 `MatchState` 而 MUST NOT 只收历史。兜底动作可能需要**服务端侧的对局设置**:斗地主首出时要出"手上最小的一张单牌",而手牌在发牌里,不在历史里。

`MoveOnTimeout` MUST 是纯函数,MUST NOT 有副作用,并 MUST 返回一个该座位在该局面下**合法**的一步。它的返回值 MUST 与真人走的一步走同一条路 —— 即由 `IGameRules.Apply` 校验并判定结果。

**实现 MUST 保证推进对局。** 一个可以合法地无限重复的兜底动作(牌类游戏里"永远过牌")会把超时 worker 变成一个永不结束的自动对局。斗地主的形式是"能过就过,**不能过时出最小的一手**",而牌只会变少。

这条要求**不是防自旋的护栏**:每一次兜底都要等满一个超时周期,所以最坏情况是每个周期一步 —— 慢、可见、不会自旋。它是**对局质量**的要求,所以本 spec MUST NOT 规定一个"连续兜底次数上限"。

> **本要求的正文是手工合并的,而那是一条比它本身更值得记的账。**
> 本变更的这一段是在 `pass-state-to-fallback` 之前写的,所以它带的是**旧签名**
> (`MoveOnTimeout(IReadOnlyList<PlayedMove>, int)`);而那个变更**先合并**,把签名改成了
> `MatchState`。两个变更改同一条要求,而 MODIFIED 是整体替换 —— 于是"按合并顺序归档"
> 会让**后合并的那个**用旧正文盖掉新正文。归档前逐条比对发现了它,合并结果是:签名与
> `MatchState` 那两段取新的,"恰好一个实现"与斗地主的兜底形式取本变更的。
> **按合并顺序归档是必要的,不是充分的。**

#### Scenario: 恰好两个内置棋种实现它
- **WHEN** 遍历 `BuiltInGameRules.All(lexicon)`
- **THEN** 恰好两个实现 `ITimeoutFallbackRules`,它们的 `GameKey` 恰好是 `{"doudizhu", "wakeng"}`

  它按预告红了第二次,而该问的问题是"这两个棋种的超时真是同一种东西吗"。是,而**理由比
  '都是牌类'窄**:两者的座位数都是 3,所以"判他负、对手胜"里的"对手"都不唯一;而两者的兜底
  动作都能推进,因为**牌只会变少**。这两条与花色、大小、牌型全都无关 —— 一个三座位的非牌类
  棋种会落进同一条。

  一处差别写下来,因为它是这两个实现唯一不同的地方:斗地主三家都不叫是**流局**,兜底三次就
  终局;挖坑三家都不挖是**第一家兜底 1 倍**,叫分阶段结束后对局继续,所以它的"推进"要靠出牌
  阶段每次让一张牌离开某只手。**同一条要求,两条不同的终止论证。**

#### Scenario: 兜底看得到对局设置
- **WHEN** 一个实现设置的棋种超时,规则的 `MoveOnTimeout` 被调用
- **THEN** `state.Setup` 恰好是 `Game.Setup`,一字不改

#### Scenario: 兜底动作要经过合法性校验
- **WHEN** 一个实现返回了该局面下非法的一步
- **THEN** `Apply` MUST 抛 `InvalidMoveException`,而对局状态 MUST NOT 改变 —— "系统替他走的"不是绕过校验的理由

### Requirement: `MoveApplication.NextSeat` 让规则指定下一手是谁

`MoveApplication` SHALL 为:

```
public readonly record struct MoveApplication(GameResult Result, int? WinnerSeat, int? NextSeat);
```

`NextSeat` 为 `null` 表示**按环轮转**(`(seat + 1) % SeatCount`);非 `null` 表示下一手轮到该座位。

斗地主需要它:叫分结束之后先出牌的是**地主**,而地主可能是任何一个座位,与最后叫分的是谁无关。

#### `null` 表示轮转,而这与「参数不给默认值」不矛盾

本平台的既有纪律是"默认值会让'忘了传'和'故意不传'长得一样"(见 `Room.JoinAsPlayer` 的 `setup`)。这里给 `null` 一个默认语义,判据是**忘了会不会有人发现**:

- 忘了传 `setup` → 一局没有牌的棋,要到第一次出牌才炸,离开局已过去几十秒。
- 忘了给 `NextSeat` → **下一手轮到错的人**,在那个棋种的第一条测试里就会红。

而且 `null` 在这里有真实含义,不是"没填":四个现有棋种的每一手、以及斗地主出牌阶段的每一手,答案确实都是"按环轮转"。让五个实现每次都算一遍内核已经知道的事,是重复而不是明确。

**判出胜负或和局时 `NextSeat` MUST 为 `null`** —— 对局结束了,没有下一手。由构造器强制,与 `WinnerSeat` 那条同一种机制。

#### Scenario: 不指定就按环轮转
- **WHEN** 规则返回 `MoveApplication.Ongoing()`,由座位 `s` 走出,`SeatCount == n`
- **THEN** `Game.CurrentTurn == (s + 1) % n`

#### Scenario: 指定了就听规则的
- **WHEN** 一个三座位规则在座位 `0` 走完之后返回 `NextSeat == 2`
- **THEN** `Game.CurrentTurn == 2`

#### Scenario: 结束的对局不能有下一手
- **WHEN** 构造 `MoveApplication(GameResult.Decided, 0, nextSeat: 1)` 或 `MoveApplication(GameResult.Draw, null, nextSeat: 0)`
- **THEN** 构造 MUST 失败并抛

#### Scenario: 负数不是座位
- **WHEN** 构造一个 `NextSeat` 为负数的 `MoveApplication`
- **THEN** 构造 MUST 失败并抛

### Requirement: `IPerSeatViewRules` 承载"同一局,不同座位看到的不一样"

`Gewu.Domain` SHALL 定义:

```
public interface IPerSeatViewRules : IGameRules
{
    string ViewFor(MatchState state, int? seat);
}
```

只有有隐藏信息的棋种实现它。五子棋、一字棋、中国象棋、成语接龙**一行不动** —— 它们的全部状态就是走子历史,而走子历史本来就广播给所有人。

**分出一个接口而不是给 `IGameRules` 加成员**,理由与 `IDealtGameRules` / `IBoardGameRules` 相同:留在基接口上,四个棋种就得各写一个骗人的实现,而**骗人的实现是下一个人删不掉的东西**。

`seat` 为 `null` 表示"不占座位的人"(围观者,或进了房间还没入座的)。实现 MUST 只给这类人**公开信息**。

`ViewFor` MUST 是纯函数:同一个 `state` 与同一个 `seat` 给出同一个字符串。这样"某个座位看得到什么"是可断言的,而不是取决于调用时机。

**返回值对内核完全不透明。** 它原样进 `GameSnapshotDto.SeatView`,由客户端按棋种解析。内核 MUST NOT 解析它 —— 与闯关那条线的 `LayoutJson` / `SolutionJson` 同一个做法:内核不该知道什么是牌,而每个棋种要藏的东西天生不一样。

**实现 MUST NOT 泄漏别人的隐藏状态**,而这条 MUST 有一条**逐项比对**的断言,MUST NOT 只断言"我看得到我自己的":后者在一个把三家手牌都塞进去的实现上同样是绿的。

#### Scenario: 恰好两个内置棋种实现它
- **WHEN** 遍历 `BuiltInGameRules.All(lexicon)`
- **THEN** 恰好两个实现 `IPerSeatViewRules`,它们的 `GameKey` 恰好是 `{"doudizhu", "wakeng"}`

  **这条 Scenario 在被改成"两个"之前从来没有被实现过。** `add-doudizhu-visibility` 写下了
  "恰好一个实现 `IPerSeatViewRules`,且它的 `GameKey == \"doudizhu\"`",而
  `backend/tests/` 下**一次都没有出现过 `IPerSeatViewRules` 这个词** —— 用一条阳性对照
  (同样的搜法必须搜得到 `IDealtGameRules`)量过,不是读代码推出来的。它的两个邻居
  (`IDealtGameRules` / `ITimeoutFallbackRules`)各有一条真断言,所以这一条读起来像也有。

  这是本仓库同一个缺陷的第四次:`web-board-skins` 抄了 11 个变量名的 requirement、
  `web-shell` 数 sound pack 的 Scenario、`web-idiom-chain` 的 375 px 断言,都是"写下来了、
  没有实现"。**一条没有实现的 Scenario 与一条错的 Scenario 在归档时长得一模一样**,而
  `openspec validate --strict` 两者都放行 —— 它验的是形状,从不验真假。

  它现在有断言了,并且是变异验过的。

#### Scenario: 没有隐藏信息的棋种不带私有切片
- **WHEN** 为一个不实现本接口的棋种投影房间快照
- **THEN** `GameSnapshotDto.SeatView` MUST 是 `null` —— 不是空串、不是空对象。空串会让客户端以为"有私有状态,只是空的"

#### Scenario: 尚未开局时没有私有切片
- **WHEN** 一个实现本接口的棋种,其房间还在 `Waiting`
- **THEN** 投影 MUST NOT 抛异常,`SeatView` MUST 是 `null` —— 大厅里每个等待中的房间都会走到这条路,而一个抛异常的投影会让房间列表整页挂掉

#### Scenario: 同一个座位问两次得到同一个答案
- **WHEN** 对同一个 `state` 与同一个 `seat` 调两次
- **THEN** 两个字符串相等;而两个**不同**座位的字符串 MUST 不相等(否则裁剪根本没发生)

