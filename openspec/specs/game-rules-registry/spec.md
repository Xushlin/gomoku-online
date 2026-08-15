# game-rules-registry Specification

## Purpose
TBD - created by archiving change add-game-rules-registry. Update Purpose after archive.
## Requirements
### Requirement: `IGameRules` 描述一个棋种的盘面属性

`Gewu.Domain` SHALL 定义 `IGameRules`:

- `GameKey` —— 棋种键,与房间的 `GameKey`、游戏注册表中的 key 一致。
- `Rows` / `Cols` / `WinLength` —— 盘面尺寸与连子长度。
- `SupportsHumanVsHuman` —— 本棋种是否存在人类对手池(平台是否提供人人对战入口)。
- `IsRated` —— 本棋种的对局是否结算 ELO。
- `CreateBoard()` —— 造一块该棋种的空棋盘。
- `IsInBounds(Position)` —— 该坐标是否在本棋种界内。

实现 MUST 是无状态的:同一个实例会被并发的多个房间共享,MUST NOT 持有任何对局状态。

`Domain` MUST NOT 因此获得任何外部依赖 —— 规则由调用方传入聚合,注册表住在 `Infrastructure`。

**不变量:`IsRated` 为 `true` 时 `SupportsHumanVsHuman` MUST 也为 `true`。** 一个只能跟机器人下的
棋种不存在有意义的评分:机器人对局是计分的(见 `ai-opponent` 的反套利约束),所以它的阶梯排出来的
是"谁刷弱档刷得多",而不是棋力。本不变量 MUST 由一条遍历注册表的测试强制,不能只写在文档里。

`SupportsHumanVsHuman` 是**结构性事实**,`IsRated` 是**判断**。这样分是因为判断会过期而不报错:
`IsRated` 原本的语义是"要不要给这个棋种算分",于是"一字棋将来有了人人对战要记得回来翻它"成了
一件依赖记性的事。改成受不变量约束之后,一字棋的 `IsRated` 只能是 `false` —— 不是谁的判断,
是被结构逼出来的;而它获得人人对战那天,翻 `SupportsHumanVsHuman` 会把评分从**禁止**变成**允许**,
开不开则是一个独立的、需要理由的决定。

平台 MUST NOT 增加 `SupportsAi` 之类的声明 —— 该问题由 `IGameAiRegistry.For(gameKey)` 是否解析出
工厂回答,再加一个字段就是第二份真源。

本接口承载的"平台能力"声明超过三个时,SHOULD 抽成独立的 `GameCapabilities` 类型,使 `IGameRules`
回到只描述盘面。

#### Scenario: 五子棋的盘面属性
- **WHEN** 读取 `gomoku` 规则
- **THEN** `Rows == 15`、`Cols == 15`、`WinLength == 5`

#### Scenario: 规则可安全共享
- **WHEN** 两个房间同时用同一个规则实例落子
- **THEN** 两局互不影响 —— 规则实例上 MUST NOT 出现任何随对局变化的字段

#### Scenario: 五子棋计分
- **WHEN** 读取 `gomoku` 规则
- **THEN** `IsRated == true` 且 `SupportsHumanVsHuman == true`

#### Scenario: 一字棋无人类对手池,因此不计分
- **WHEN** 读取 `tictactoe` 规则
- **THEN** `SupportsHumanVsHuman == false`,因此 `IsRated == false`

#### Scenario: 不变量被测试强制
- **WHEN** 遍历注册表中每一个 `IGameRules`
- **THEN** 每一个满足 `IsRated == false || SupportsHumanVsHuman == true`;构造一个违反此式的规则实例 MUST 失败

#### Scenario: 是否支持人机由 AI 注册表回答
- **WHEN** 检视 `IGameRules` 的成员
- **THEN** MUST NOT 存在 `SupportsAi` 或同义字段;"该棋种有没有 AI"MUST 由 `IGameAiRegistry.For` 解析

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
    int Rows,
    int Cols);
```

它 MUST 是**投影**而不是第二份清单:注册表加一个棋种,本端点自动多一条;实现 MUST NOT 内联任何
"哪些棋种存在"的硬编码列表 —— 与建房校验不许内联棋种白名单是同一条理由。

Handler MUST NOT 访问数据库。注册表本来就在内存里,这是一次纯投影。

端点只覆盖 `IGameRules`(对战棋种)。谜题类走 `IPuzzleRules`,已经有自己的一条 REST 线
(`GET /api/puzzles/games/{gameKey}/levels`),MUST NOT 混进本端点 —— 两者塞进一个 DTO 会造出
一半字段永远为空的形状(谜题没有 `Rows`/`IsRated`,对战没有关卡数),而那种 DTO 的下一步永远是
加一个 `type` 字段然后到处 switch。三个类别刻意不共享一个聚合,端点跟着分。

**存在的理由**:前端要渲染"棋种切换"就得知道哪些棋种计分,而这个事实此前只存在于服务端。
备选是在前端 `GameManifest` 上加一个 `rated` 布尔副本 —— 不做:`GameManifest.board` 那份副本能被
接受,是因为失配的症状是**肉眼可见的格数不对**且服务端 `IsInBounds` 兜底;`rated` 失配的症状是
**一个永远空着的榜**,而那与"新棋种还没人下过"在屏幕上一模一样。分不出来的失配 =
不会被发现的失配。

#### Scenario: 列出全部已登记棋种
- **WHEN** 登录用户 `GET /api/games`
- **THEN** HTTP 200;返回条数等于 `IGameRulesRegistry` 中已登记棋种的数量

#### Scenario: 能力如实反映规则实例
- **WHEN** 响应中取出 `tictactoe` 那条
- **THEN** `isRated == false`、`supportsHumanVsHuman == false`、`rows == 3`、`cols == 3`

#### Scenario: 五子棋
- **WHEN** 响应中取出 `gomoku` 那条
- **THEN** `isRated == true`、`supportsHumanVsHuman == true`、`rows == 15`、`cols == 15`

#### Scenario: 不查库
- **WHEN** handler 执行
- **THEN** MUST NOT 发生任何数据库访问

#### Scenario: 未登录被拒
- **WHEN** 无 Bearer token
- **THEN** HTTP 401

