# game-rules-registry Specification Delta

## ADDED Requirements

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

## MODIFIED Requirements

### Requirement: `GET /api/games` 把棋种注册表投影给客户端

Api 层 SHALL 暴露 `GET /api/games`(`[Authorize]`),返回 `IGameRulesRegistry` 中每个已登记棋种的一条描述:

```
public sealed record GameDescriptorDto(
    string GameKey,
    bool IsRated,
    bool SupportsHumanVsHuman,
    int? Rows,
    int? Cols);
```

`Rows` / `Cols` MUST 可空,且**当且仅当**该规则实现 `IBoardGameRules` 时非空。`null` 的含义是"这个棋种没有盘面",与"客户端还没拿到描述符"是两件不同的事,客户端 MUST 分别处理(见 `web-game-board`)。

它 MUST 是**投影**而不是第二份清单:注册表加一个棋种,本端点自动多一条;实现 MUST NOT 内联任何
"哪些棋种存在"的硬编码列表 —— 与建房校验不许内联棋种白名单是同一条理由。

Handler MUST NOT 访问数据库。注册表本来就在内存里,这是一次纯投影。

端点只覆盖 `IGameRules`(对战棋种)。谜题类走 `IPuzzleRules`,已经有自己的一条 REST 线
(`GET /api/puzzles/games/{gameKey}/levels`),MUST NOT 混进本端点 —— 两者塞进一个 DTO 会造出
一半字段永远为空的形状,而那种 DTO 的下一步永远是加一个 `type` 字段然后到处 switch。

**存在的理由**:前端要渲染"棋种切换"就得知道哪些棋种计分,而这个事实此前只存在于服务端。
备选是在前端 `GameManifest` 上加一个 `rated` 布尔副本 —— 不做:`rated` 失配的症状是
**一个永远空着的榜**,而那与"新棋种还没人下过"在屏幕上一模一样。分不出来的失配 =
不会被发现的失配。

#### Scenario: 列出全部已登记棋种
- **WHEN** 登录用户 `GET /api/games`
- **THEN** HTTP 200;返回条数等于 `IGameRulesRegistry` 中已登记棋种的数量

#### Scenario: 有盘面的棋种带尺寸
- **WHEN** 描述 `gomoku`
- **THEN** `rows == 15`、`cols == 15`

#### Scenario: 无盘面的棋种尺寸为 null
- **WHEN** 描述一个不实现 `IBoardGameRules` 的棋种
- **THEN** `rows == null`、`cols == null`,MUST NOT 是 `0`
