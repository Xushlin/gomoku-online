# web-xiangqi 的规格变化

## MODIFIED Requirements

### Requirement: 象棋 manifest 从「即将上线」翻到「可玩」

`src/app/games/xiangqi/manifest.ts` SHALL 为 `status: 'available'`，`launchRoute: '/g/xiangqi/lobby'`。

`launchRoute` 指向**大厅**而不是人机页,与五子棋一致。`gameEntryRoute` 读的就是这个字段,所以离开
象棋房间会回到象棋大厅 —— 那是"再来一局"该去的地方。`/g/xiangqi` 人机页保留,它仍然是人机入口;
大厅上的「人机对战」卡片是第二个,而两个入口通往同一件事是 `leaderboard-page` 已记下的既有瑕疵,
本要求不扩大也不修它。

目录页 SHALL 为它渲染排行榜入口 —— 象棋自 `enable-xiangqi-human-play` 起计分。这**不需要**任何
新代码:目录与大厅都按 `GET /api/games` 的 `isRated` 渲染,服务端翻一个布尔,客户端跟着变。
那正是 `add-web-per-game-rating` 拒绝在前端放一份 `rated` 副本换来的东西。

**本要求此前还写着 `board: { rows: 10, cols: 9 }`,那个字段已被 `remove-manifest-board` 删除** ——
顺手改正一处遗留漂移,不是本变更的新决定。

#### Scenario: 目录页可点进
- **WHEN** 打开 `/games`
- **THEN** 象棋卡片可交互，指向 `/g/xiangqi/lobby`

#### Scenario: 象棋现在有排行榜入口
- **WHEN** 检视目录页的象棋卡片
- **THEN** MUST 存在指向 `/g/xiangqi/leaderboard` 的链接 —— **本场景此前断言的正是它的反面**

#### Scenario: 不计分的棋种没有排行榜入口
- **WHEN** 检视目录页上一个不计分的对战棋种卡片
- **THEN** 它没有排行榜链接 —— 那条既有断言是"前端不许自己存一份 `rated` 副本"的可执行形式

**本场景此前的标题是「只有一字棋仍然没有排行榜入口」,理由写的是「它是唯一不计分的对战棋种」。
`add-doudizhu` 让那句话不成立了**:斗地主同样不计分,而**两者的理由不同** —— 一字棋没有人人对战
(唯一的对手是机器人,而机器人对局计分),斗地主是 ELO 按两人建模、而它按分结算。

**可执行形式一个字都没改。** 那条断言用的是桩、读的是 `isRated`,从来不数棋种个数 —— 也就是说
它一直是绿的,错的只有描述它的那句话。这正是 `openspec validate --strict` 验不出来的那一类:
它验形状,不验真假。
