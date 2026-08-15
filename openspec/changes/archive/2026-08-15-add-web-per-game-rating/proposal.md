## Why

`add-per-game-rating` 把评分池按棋种拆开了，但它**刻意让已发布的 Web 客户端零改动** —— 每个
controller 上 `gameKey` 缺省 `gomoku`，所以前端至今看到的还是「一个评分、一个榜」。

代价当时就记为缺口：**资料页只能看一个棋种（五子棋），排行榜没有棋种切换入口。** 现在这只是
「少一个功能」，因为平台上唯一计分的棋种就是五子棋。等中国象棋上线，同一个缺口会变成
**用户看不到自己的象棋分** —— 后端在算、在存、在排，前端不显示。

所以这一步该在中国象棋之前做完，而不是跟它捆在一起。理由和当初把 `add-per-game-rating` 排在
`add-tictactoe` 之后一样：**先让机制有第二个消费者，再让它长第二条腿**。反过来做，象棋那个变更
会同时背着「一个新棋种」和「一套没验证过的多棋种 UI」，坏了都不知道是哪一半坏的。

## 一个必须先解决的问题：前端怎么知道哪个棋种有榜

前端要渲染「棋种切换」就得知道**哪些棋种计分**。今天这个事实只存在于后端
`IGameRules.IsRated`，前端一无所知。两条路：

**(A) 在 `GameManifest` 上加 `rated: boolean`。** 纯前端，零后端改动。

**(B) 加一个只读端点 `GET /api/games`，把 `IGameRulesRegistry` 投影出来。**

**选 (B)，理由不是「更干净」，是 (A) 的漂移看不见。**

manifest 上已经有一份服务端数据的刻意副本 —— `board` 的行列数。它当初能被接受，是因为文档里
写清了两个安全网:失配的症状是**肉眼可见的格数不对**,而且服务端 `IsInBounds` 会挡住越界落子。
`rated` 一个都没有:manifest 说计分而服务端说不计分,症状是**一个永远空着的榜**;而「榜是空的」
恰好也是一个新棋种刚上线时的正常状态。分不出来的失配 = 不会被发现的失配。

`GET /api/games` 是零新状态、零迁移的只读投影(注册表本来就在内存里),而且它顺带把
`GameManifest.board` 那份副本也变成可删的 —— 那正是 manifest 文档里写的「等服务端把尺寸放上线
就删掉这个字段」。本变更**不删**它(一件事一次),但把删除条件从「等 `generalize-match-contract`」
降级成「随时」。

## What Changes

### 1. 后端：`GET /api/games` —— 注册表的只读投影

```
GET /api/games            → GameDescriptorDto[]
GameDescriptorDto(string GameKey, bool IsRated, bool SupportsHumanVsHuman,
                  int Rows, int Cols, int WinLength?)
```

`[Authorize]`，直接从 `IGameRulesRegistry` 读，无 DB 访问、无迁移、无新状态。
它是**投影而不是第二份清单** —— 注册表加一个棋种，这个端点自动多一条。

只覆盖 `IGameRules`(对战棋种)。谜题类走 `IPuzzleRules`,已经有
`GET /api/puzzles/games/{gameKey}/levels` 那条线,不在这里混一起。

### 2. 前端：每个计分棋种一个榜，落在 `/g/:gameKey/leaderboard`

- 新页面,懒加载,复用现有 `LeaderboardEntry` 模型与分页。
- `LeaderboardApiService` 加 `gameKey` 参数(**必填** —— 服务层不猜自己在被问哪个棋种,
  缺省只发生在调用点,与后端 controller 同一条纪律)。
- `/games` 目录页上,`status: 'available'` 且服务端说 `isRated` 的卡片多一个「排行榜」次级入口。

**`/home` 的排行榜卡片不动，仍然钉死五子棋。** 它是**五子棋大厅**的一张卡片,给它加棋种切换等于
开始泛化大厅 —— 那是 roadmap 上单独的一步,且会把 `/home` 在五份 web spec 里的规范地位一起
掀翻。一个变更做一件事。

### 3. 前端：资料页支持切换棋种

- header card 上加一排棋种切换(只列**服务端说计分**的棋种),缺省五子棋。
- 切换时重新拉 `GET /api/users/{id}?gameKey=`,战绩四项与 Rating 跟着换。
- 「这个人没下过这个棋种」是**正常状态**:后端返回 200 + 初始值(1200 / 全 0),不是 404。
  UI 要能把它和「有战绩」区分开,否则一个没下过象棋的人看起来像 1200 分的象棋选手。
  → 显示为「尚无对局」而不是一行 0。

### 4. i18n

`leaderboard.*`(新页面)与 `profile.game-switcher.*` 两组键,`zh-CN` + `en` 对齐。

## What does NOT change

- **`/api/users/me` 与登录 / 注册 / 刷新响应仍然钉死五子棋。** 给 `UserDto` 加棋种维度要改 DTO
  形状,而 `/me` 的消费者是 header 上的一个分数 —— 「header 显示哪个棋种的分」是产品问题
  (主棋种?当前所在棋种?全部?),不该顺手定。记为缺口。
- **搜索仍然钉死五子棋。** 找人卡片是五子棋大厅的组件,随大厅泛化一起走。
- **三个阶梯依然各算各的。** 谜题(星数 + 用时)与将来的分数榜不进这个 UI。
- **`GameManifest.board` 不删。** 见上,条件放宽了但删除是另一件事。

## Impact

- 后端:一个 controller + 一个 DTO + 一个 query handler。无迁移,无 DB 访问,无既有端点改动。
- 前端:一个新路由 + 一个 API 方法加参数 + 资料页 header + 目录页卡片一个链接。
- 破坏性:无。所有既有端点与 DTO 形状不变;`LeaderboardApiService` 的签名变了,但那是内部类型。
