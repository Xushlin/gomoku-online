# Tasks — add-web-per-game-rating

> 判据：**后端既有端点与 DTO 形状一个字节不变。** 本变更只加一个新的只读端点，
> 其余全是前端。若哪个既有 controller / DTO 需要改，说明范围跑偏了 —— 停下来看为什么。

> 第二条判据：**新棋种上线时，这套 UI 不需要改一行。** 榜的入口来自
> `GET /api/games` 的投影，资料页的切换列表也来自它。如果加中国象棋时要回来改
> 这里的任何组件，说明这一版做的是「两个棋种的 UI」而不是「多棋种的 UI」。

## 1. 后端：`GET /api/games`

- [ ] 1.1 `GameDescriptorDto(string GameKey, bool IsRated, bool SupportsHumanVsHuman, int Rows, int Cols, int WinLength)` 放 `Application/Common/DTOs/`。
- [ ] 1.2 `GetGameDescriptorsQuery` + handler —— 直接读 `IGameRulesRegistry`，**无 DB 访问**。它是投影而不是第二份清单：注册表加一个棋种，端点自动多一条。
- [ ] 1.3 `GamesController.Get()`，`[Authorize]`，`GET /api/games`。
- [ ] 1.4 `WinLength` 只对 n-in-a-row 有意义 —— 确认 `IGameRules` 上有没有这个概念；没有就**不要为了 DTO 好看而加**，宁可 DTO 少一个字段。（象棋没有「连几子」。）
- [ ] 1.5 测试:注册表有几个棋种就返回几条;一字棋 `isRated == false` / `supportsHumanVsHuman == false`;五子棋两者都 true 且 15×15；**遍历注册表断言无遗漏**（写死清单的测试会在加象棋时静静通过）。

## 2. 前端：类型与服务

- [ ] 2.1 `core/api/models/game-descriptor.model.ts` —— 镜像 DTO。
- [ ] 2.2 `GamesApiService`（抽象类 DI token + 默认实现），一次 `GET /api/games`。
- [ ] 2.3 `GameCatalogService` 把服务端能力按 `key` 合并进 `GAME_REGISTRY`。合不上的（谜题、规划中的游戏）**没有**能力信息 —— 那是正确的，它们没有 `IGameRules`，MUST NOT 用缺省值填。
- [ ] 2.4 `LeaderboardApiService.getPage(gameKey, page, pageSize)` —— `gameKey` **必填**（design D4）。`top(count)` 同样加参数。
- [ ] 2.5 `UsersApiService.getProfile(userId, gameKey?)` —— 这里 `gameKey` 可选，缺省不带参数（让后端的 `gomoku` 缺省生效），因为资料页首屏就是这么进来的。
- [ ] 2.6 测试:三个 service 的 URL / query 参数;`GameCatalogService` 合并逻辑(合不上时不填缺省)。

## 3. 前端：`/g/:gameKey/leaderboard`

- [ ] 3.1 懒加载路由 + 鉴权守卫，与 `/g/tictactoe` 同一形状。
- [ ] 3.2 页面复用现有 `LeaderboardEntry` 模型与分页（prev/next），前三名图标沿用 `web-lobby` 已定的 rank 驱动规则。
- [ ] 3.3 四态齐全:loading 骨架 / empty / error / data。**empty 要说人话** —— 「还没有人下过这个棋种」而不是「暂无数据」;一个新棋种刚上线时这就是常态,不是故障。
- [ ] 3.4 `gameKey` 不是计分棋种（或根本不存在）时：不 404，显示一个说明性的空态。后端对未登记的键返回 200 + 空榜，前端别把它翻译成错误。
- [ ] 3.5 375px 可用;键盘可达;`prefers-reduced-motion` 尊重。
- [ ] 3.6 测试:按 gameKey 请求正确的 URL;空态与错误态各自渲染;分页 Rank 是全局名次不是页内序号。

## 4. 前端：目录页入口

- [ ] 4.1 `/games` 上 `status === 'available'` **且**服务端说 `isRated` 的卡片，多一个「排行榜」次级入口指向 `/g/<key>/leaderboard`。
- [ ] 4.2 一字棋卡片 MUST NOT 出现这个入口（它不计分）。这条要有测试 —— 它是 D1 那份论证的唯一可执行形式。
- [ ] 4.3 谜题类卡片同样没有（它们没有 `IGameRules`，合并后没有能力信息）。

## 5. 前端：资料页棋种切换

- [ ] 5.1 header card 上一排切换，**只列服务端说计分的棋种**，缺省五子棋。
- [ ] 5.2 切换重新拉 `GET /api/users/{id}?gameKey=`；切换时有 loading 态，不闪烁旧数据。
- [ ] 5.3 `gamesPlayed === 0` 渲染「尚无对局」空态,**不显示 1200** —— 那个数字此刻不承载信息,
      直接渲染会让「没下过」看起来像「下过的新手」(design D3)。中国象棋刚上线时这对几乎每个
      用户都成立,不是边角。
- [ ] 5.4 胜率计算在 `denom === 0` 时已经返回 `—`，确认切换后仍然正确（现有 `winRateLabel` 逻辑）。
- [ ] 5.5 只有一个计分棋种时,切换器**仍然渲染**还是隐藏?——建议渲染(它是「这是哪个棋种的分」
      这个信息的唯一载体,今天正好只有一个选项)。写进 spec，别留给实现随手定。
- [ ] 5.6 测试:切换触发带 `gameKey` 的请求;0 局显示空态而不是 1200;切换器只列计分棋种。

## 6. i18n

- [ ] 6.1 `leaderboard.*`（新页面：标题、空态、错误态、分页）。
- [ ] 6.2 `profile.game-switcher.*` + `profile.no-games-in-game`。
- [ ] 6.3 `catalog.leaderboard-link`。
- [ ] 6.4 `zh-CN` 与 `en` 键集合**逐一对齐**（有现成的对齐测试就跑它）。

## 7. 验收

- [ ] 7.1 `dotnet build` / `dotnet test` 全绿；`npm run lint` / `npm run test:ci` 全绿。
- [ ] 7.2 **既有后端端点与 DTO 形状零改动** —— 只新增 `GET /api/games`。
- [ ] 7.3 HTTP 冒烟：`GET /api/games` 返回三条（gomoku / tictactoe / 及注册表里的其余），一字棋 `isRated: false`。
- [ ] 7.4 手动:`/g/gomoku/leaderboard` 有人;`/g/tictactoe/leaderboard` 直接访问显示说明性空态;
      资料页切到一个没下过的棋种显示「尚无对局」而不是 1200。
- [ ] 7.5 `openspec validate add-web-per-game-rating --strict`。

## 8. 已知缺口（记录，不在本变更修）

- [ ] 8.1 **`/api/users/me` 与登录 / 注册 / 刷新响应仍然钉死五子棋。** 给 `UserDto` 加棋种维度要
      改 DTO 形状，而「header 上那个分该显示哪个棋种」是产品问题（主棋种？当前所在棋种？全部？），
      不该顺手定。
- [ ] 8.2 **搜索排序仍然钉死五子棋** —— 找人卡片是五子棋大厅的组件，随大厅泛化一起走。
- [ ] 8.3 **`/home` 的排行榜卡片与 `/g/gomoku/leaderboard` 会重复** —— 两个入口看同一个榜。
      比现在就动五份 web spec 便宜，大厅泛化那一步会自然消掉（design D2）。
- [ ] 8.4 **`GameManifest.board` 仍在**，但删除条件已经从「等 `generalize-match-contract`」降级成
      「随时」—— `GET /api/games` 已经在传 `Rows` / `Cols`。下一个碰 manifest 的变更可以顺手做掉。
- [ ] 8.5 谜题阶梯（星数 + 用时）与将来的分数榜**不进这个 UI**。三个阶梯刻意不统一。
