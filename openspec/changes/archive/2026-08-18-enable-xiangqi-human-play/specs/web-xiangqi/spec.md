# web-xiangqi Specification Delta

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

#### Scenario: 只有一字棋仍然没有排行榜入口
- **WHEN** 检视目录页的全部对战棋种卡片
- **THEN** 恰好一字棋没有排行榜链接 —— 它是唯一不计分的对战棋种,而那条既有断言是"前端不许自己存一份 `rated` 副本"的可执行形式
