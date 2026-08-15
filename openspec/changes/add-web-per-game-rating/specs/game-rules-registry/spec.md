## ADDED Requirements

### Requirement: `IGameRulesRegistry` 能枚举全部已登记棋种

`IGameRulesRegistry` SHALL 提供 `IReadOnlyCollection<IGameRules> All { get; }`,返回全部已登记的规则实例;顺序不作保证。

**提案漏了这一条。** 它写着"handler 直接读 `IGameRulesRegistry`",但那个接口此前只有
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
