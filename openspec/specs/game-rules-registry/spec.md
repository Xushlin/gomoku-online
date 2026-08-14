# game-rules-registry Specification

## Purpose
TBD - created by archiving change add-game-rules-registry. Update Purpose after archive.
## Requirements
### Requirement: `IGameRules` 描述一个棋种的盘面属性

`Gewu.Domain` SHALL 定义 `IGameRules`:

- `GameKey` —— 棋种键,与房间的 `GameKey`、游戏注册表中的 key 一致。
- `Rows` / `Cols` / `WinLength` —— 盘面尺寸与连子长度。
- `IsRated` —— 本棋种的对局是否结算 ELO。
- `CreateBoard()` —— 造一块该棋种的空棋盘。
- `IsInBounds(Position)` —— 该坐标是否在本棋种界内。

实现 MUST 是无状态的:同一个实例会被并发的多个房间共享,MUST NOT 持有任何对局状态。

`Domain` MUST NOT 因此获得任何外部依赖 —— 规则由调用方传入聚合,注册表住在 `Infrastructure`。

`IsRated` 是**限期存在的脚手架**,不是长期设计。平台当前只有一个评分池,它实际上就是五子棋
排行榜;这个开关的唯一作用是让第二个棋种能在不污染该排行榜的前提下先上线。
`add-per-game-rating` 给每个棋种发一份 `UserGameStats` 之后,本字段 MUST 被删除 ——
到那时"哪个棋种算分"不再是一个布尔,而是"每个棋种各算各的"。实现上 MUST 在
`IsRated` 的文档注释里点名 `add-per-game-rating` 为拆除它的变更。

#### Scenario: 五子棋的盘面属性
- **WHEN** 读取 `gomoku` 规则
- **THEN** `Rows == 15`、`Cols == 15`、`WinLength == 5`

#### Scenario: 规则可安全共享
- **WHEN** 两个房间同时用同一个规则实例落子
- **THEN** 两局互不影响 —— 规则实例上 MUST NOT 出现任何随对局变化的字段

#### Scenario: 五子棋计分
- **WHEN** 读取 `gomoku` 规则
- **THEN** `IsRated == true`

### Requirement: `NInARowRules` 以参数覆盖全部"连 N 子"棋种

`Gewu.Domain` SHALL 提供 `NInARowRules(gameKey, rows, cols, winLength, isRated = true)`,实现 `IGameRules`。

五子棋 SHALL 注册为 `NInARowRules("gomoku", 15, 15, 5)`。一字棋 SHALL 注册为 `NInARowRules("tictactoe", 3, 3, 3, isRated: false)` —— 判胜算法完全相同,只有三个数不同,因此 MUST NOT 为其另写一份实现。

`isRated` 的默认值 SHALL 为 `true` —— 一个棋种默认是算分的,不算分才是需要写出理由的那一侧。

#### Scenario: 同一实现服务不同尺寸
- **WHEN** 分别以 `(15,15,5)` 与 `(3,3,3)` 构造
- **THEN** 两者都是合法的 `IGameRules`,各自的 `IsInBounds` 与判胜按自己的参数工作

#### Scenario: 参数校验
- **WHEN** 以非正的 `rows` / `cols` / `winLength`,或 `winLength` 大于 `max(rows, cols)` 构造
- **THEN** 构造失败 —— 一个永远赢不了的棋种是配置错误,不是可玩的棋

#### Scenario: 一字棋不另写判胜
- **WHEN** 检视一字棋的规则实现
- **THEN** 它 MUST 是 `NInARowRules` 的一个实例,`Gewu.Domain` 中 MUST NOT 存在名为 `TicTacToeRules` 的独立判胜实现

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

