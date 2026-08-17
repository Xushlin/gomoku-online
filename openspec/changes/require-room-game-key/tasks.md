# Tasks — require-room-game-key

## 1. 后端:去掉缺省

- [x] 1.1 `CreateRoomRequest.GameKey` / `CreateAiRoomRequest.GameKey` 改为必填 `string`。
- [x] 1.2 `RoomsController` 三处 `?? GameKeys.Gomoku` 删除;`List` 的参数改为 `[FromQuery][Required] string gameKey`。
- [x] 1.3 `HumanSide ?? Stone.Black` 保留 —— 缺省一个边是在已指名的棋种**之内**补全请求;缺省一个棋种是换掉他在玩的游戏。
- [x] 1.4 修掉 `CreateRoomRequest` 上重复的 `<summary>` 标签(有两个,XML doc 是坏的)。
- [x] 1.5 缺 `gameKey` 时三条路径都是 **400 + 点名字段**,不是 500。实测见 §6.2。

## 2. 前端:每个调用点指名棋种

- [x] 2.1 `list(gameKey)` / `create(name, gameKey)` 必填;`createAiRoom` 的 `humanSide` 与 `gameKey` 都提为必填。
- [x] 2.2 `createAiRoom` 的 body 不再按 `undefined` 拼装 —— 四个必填参数拼不出一个缺字段的 body。
- [x] 2.3 新增 `games/gomoku/game-key.ts` 导出 `GOMOKU_KEY`,与 `TICTACTOE_KEY` / `XIANGQI_KEY` 同形。**五子棋此前是唯一一个键没在客户端写下来的棋种** —— 服务端替它补,于是大厅「这是五子棋」这个决定在客户端无处可读。
- [x] 2.4 调用点全部指名:`lobby-data.service`(list + leaderboard)、`create-room-dialog`、`create-ai-room-dialog`;一字棋 / 象棋页面本来就传自己的键。
- [x] 2.5 `LeaderboardApiService` 的 `gameKey` 早已必填,无需改动。

## 3. AiSmoke

- [x] 3.1 给 `POST /api/rooms/ai` 补 `gameKey = "gomoku"`。
- [x] 3.2 实际跑了一次。**CLAUDE.md 说错了。**

  记录的是「`AiSmoke` 自 `add-leaderboard-pagination` 起就坏了:第 7 步把 `/api/leaderboard` 反序列化成 `List<LeaderboardEntry>`,而端点返回 `PagedResult<T>`」。实跑结果:

  ```
  === SUMMARY: 17 passed, 0 failed ===
  ```

  那个 bug **早就被修好了** —— 代码用的是 `PagedResult<LeaderboardEntry>`,旁边的注释还用过去时描述了这个 bug。它甚至长出了第 8 步(per-game rating),那是比笔记描述的时间点更晚的工作。笔记在写下时是对的,后来没人回来更新。

  笔记里仍然为真的部分:它不在 `Gewu.slnx`,CI 从不跑它,base URL 硬编码 `http://localhost:5145`。

  讽刺的地方值得记下来:那条笔记警告「CI 之外的冒烟测试会静静腐烂,然后谎报覆盖率」—— 结果**它自己**成了那句谎,只是方向相反:声称没有覆盖,而覆盖其实在。

## 4. 测试

- [x] 4.1 `rooms-api.service.spec.ts`:`GET /api/rooms?gameKey=gomoku`(含 `params.get` 断言)、另一个棋种的 list、`POST /api/rooms { name, gameKey }`。
- [x] 4.2 `createAiRoom` body 严格等于四字段对象;另一条断言两个键都**不可能**缺席。
- [x] 4.3 `lobby-data.service.spec` / 两个 dialog spec 按新签名更新。
- [x] 4.4 后端「缺 `gameKey` → 400」**没有**单元测试:这条行为在 controller 的模型绑定层,而本仓库没有 Api 级测试工程(CLAUDE.md 记的是"若将来加,叫 `Gewu.Api.Tests`")。它由 §6.2 的实调覆盖。不假装它有单测。

## 5. 规格

- [x] 5.1 `room-and-gameplay`:端点表 + 必填 + 「缺少棋种是 400」+ 「缺省的边仍被补全」。
- [x] 5.2 `web-lobby`:两处签名,并写明"位置不规范、必填才规范"。
- [x] 5.3 `openspec validate --strict` 通过。

## 6. 验证

- [x] 6.1 `dotnet build` 0 warning;`dotnet test` **871 passed**;`npm run lint` 全绿;`npm run test:ci` **453 passed**。
- [x] 6.2 实调(Development,真数据库):

  | 请求 | 结果 |
  | --- | --- |
  | `GET /api/rooms` | 400 `{"gameKey":["The gameKey field is required."]}` |
  | `POST /api/rooms {name}` | 400 `{"GameKey":["The GameKey field is required."]}` |
  | `POST /api/rooms/ai {name,difficulty}` | 400 `{"GameKey":[...]}` |
  | `GET /api/rooms?gameKey=gomoku` | 200 |
  | `POST /api/rooms` + gomoku | 201 |
  | `POST /api/rooms/ai` + gomoku / + xiangqi | 201 |
  | `POST /api/rooms/ai` 不带 `humanSide` | 201,black=真人、white=AI |

- [x] 6.3 浏览器实跑(自建 dev server,**没有动 4200 上属于主 worktree 的那个**):登录后大厅四个端点齐发,其中 `GET /api/rooms?gameKey=gomoku → 200`;"Create room" → `POST /api/rooms → 201`,列表随即刷新;"New AI game" → `POST /api/rooms/ai → 201`,响应体 `gameKey: "gomoku"`、`status: "Playing"`、white=`AI_Medium`。
- [x] 6.4 行为与变更前一致 —— 本变更不改行为,只改「谁来说出棋种」。

## 7. 明确不做的事

- [x] 7.1 `UsersController` 的 profile 缺省**留着,而且是唯一一个真被用到的**。`getProfile(userId, gameKey?)` 首屏就故意不传,那里省略是一个**有意义的值**(「看服务端的默认棋种」),它的 doc comment 早就写清楚了。删掉它会弄坏资料页首屏。
- [x] 7.2 `LeaderboardController` 的缺省留着:前端每个调用点都传,但 `AiSmoke` 不传;而且排行榜永远渲染在一个可见的棋种名之下,发错榜是**屏幕上**的错。房间建错键则是数据库里的错,看起来像什么都没发生。
- [x] 7.3 `gameKey` 的**参数位置**没有统一(`LeaderboardApiService` 放第一个,`RoomsApiService` 放最后)。本变更要的是"必填",不是"排列"。为对齐位置去动每一个排行榜调用点,是零行为的改动。
- [x] 7.4 大厅本身没动 —— 路由、卡片、页面结构是 `generalize-lobby`。
