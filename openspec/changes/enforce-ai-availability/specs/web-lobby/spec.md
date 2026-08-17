# web-lobby Specification Delta

## MODIFIED Requirements

### Requirement: "Play vs AI" 卡片提供创建 AI 对局入口

`/g/:gameKey/lobby` SHALL 在卡片网格中渲染一张 `ai-game` 卡片,代码位于 `src/app/pages/lobby/cards/ai-game/ai-game.{ts,html}` + spec。

卡片渲染标题(`lobby.ai-game.title`)、一行说明(`lobby.ai-game.description`)、一个主按钮(`lobby.ai-game.button`)。

卡片 SHALL 只在该棋种的描述符 `supportsAi === true` 时渲染,与 Leaderboard 卡按 `isRated` 渲染同形。`supportsAi` 来自 `GET /api/games`,投影自 `IGameAiRegistry`(见 `game-rules-registry`)。

**本条此前写的是"无条件渲染,并且这不是疏漏",附带一段推迟的理由,而那段理由自己写下了触发条件:**

> 于是它留到第一个"有人人对战、但没有 AI"的棋种出现那天再做 —— 那时它才第一次能被真实用例检验。

触发条件已经到了:成语接龙就是那个棋种。**推迟本身是对的** —— 为一个不存在的情况建一个测不了的分支确实是这个仓库反复付过的账。错的是它对代价的估计。

那段理由从头到尾在谈**卡片**:今天只有五子棋有大厅,五子棋有 AI,所以没有消费者。它没有谈 `POST /api/rooms/ai`,而那个端点从来不看有没有大厅。实测:该端点为 `idiom-chain` 返回 201,房间进入 `Playing` 且轮到一个不存在的机器人,60 秒后超时判真人胜 —— 成语接龙计分,于是零手棋换约 +46 ELO,可无限重复。

**所以这不是一个"渲染了没用的按钮"的缺陷,而是一个计分漏洞;卡片只是通往它的第二条路。** 这与 `enforce-human-vs-human` 是同一种错法:一条结论对着 Web UI 成立、对着 API 不成立,而写下它的人只检查了前者。本条因此明确:**隐藏卡片是展示决定,不是防线**;防线在 `ai-opponent` 的校验器里,并且 MUST 独立于本条成立。

点击按钮 SHALL 打开 `CreateAiRoomDialog`(CDK Dialog),并把当前棋种键传给它。Dialog 关闭后:

- 若 `closed` emit 一个 `RoomState` → `router.navigateByUrl('/rooms/' + state.id)`,**MUST NOT** 再发任何 REST 请求。
- 若 emit `undefined`(取消)→ 不导航。

样式契约与其它大厅卡一致:`bg-surface text-text border-border rounded-card shadow-elevated`,无硬编码色值。

#### Scenario: 卡片在棋种大厅渲染
- **WHEN** 登录用户打开 `/g/gomoku/lobby`
- **THEN** 卡片网格中能找到 `ai-game` 卡片

#### Scenario: 没有 AI 的棋种不渲染这张卡
- **WHEN** 登录用户打开 `/g/idiom-chain/lobby`
- **THEN** MUST NOT 渲染 `ai-game` 卡片

#### Scenario: 描述符未到时不下结论
- **WHEN** `capabilities.loaded()` 为 false
- **THEN** 页面显示骨架,MUST NOT 已经决定这张卡片渲不渲染

#### Scenario: 卡片带上路由的棋种
- **WHEN** 在 `/g/gomoku/lobby` 提交该卡片的 dialog
- **THEN** `createAiRoom` 收到的第四个参数是 `'gomoku'`,MUST NOT 是任何字面量常量

#### Scenario: 创建成功后跳转
- **WHEN** 用户点按钮 → dialog 提交合法表单 → 后端回 201 + RoomStateDto
- **THEN** `router.navigateByUrl('/rooms/<roomId>')` 被调一次;dialog 关闭

#### Scenario: 取消不跳转
- **WHEN** dialog 关闭 with `undefined`
- **THEN** `router.navigateByUrl` MUST NOT 被调
