## ADDED Requirements

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
