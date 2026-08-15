# Tasks — add-web-per-game-rating

> 判据：**后端既有端点与 DTO 形状一个字节不变。** 本变更只加一个新的只读端点，
> 其余全是前端。若哪个既有 controller / DTO 需要改，说明范围跑偏了 —— 停下来看为什么。

> 第二条判据：**新棋种上线时，这套 UI 不需要改一行。** 榜的入口来自
> `GET /api/games` 的投影，资料页的切换列表也来自它。如果加中国象棋时要回来改
> 这里的任何组件，说明这一版做的是「两个棋种的 UI」而不是「多棋种的 UI」。

## 1. 后端：`GET /api/games`

- [x] 1.1 `GameDescriptorDto(GameKey, IsRated, SupportsHumanVsHuman, Rows, Cols)` 放 `Application/Common/DTOs/`。
- [x] 1.2 `GetGameDescriptorsQuery` + handler —— 直接读 `IGameRulesRegistry`，**无 DB 访问**。它是投影而不是第二份清单：注册表加一个棋种，端点自动多一条。按 `GameKey` 排序（注册表是 DI 集合 → 字典，顺序不作保证；每次刷新换序会让人以为数据在变，而让每个客户端各排一次，它们迟早排得不一样）。
- [x] 1.2a **提案漏了一件事：`IGameRulesRegistry` 此前只有 `For(key)`，不能枚举。** 加了 `All`。没有它，这个端点只能拿一份手写清单去逐个 `For()` —— 那正是本变更要消灭的第二份清单，换了个位置而已。`IPuzzleRulesRegistry` / `IGameAiRegistry` **不顺手加**：今天没有消费者，而「三个注册表长得一样」不构成给两个接口加未使用成员的理由。
- [x] 1.3 `GamesController.Get()`，`[Authorize]`，`GET /api/games`。
- [x] 1.4 `WinLength` **不进 DTO**。它在 `IGameRules` 上确实存在，但那是因为今天的棋种恰好都是「连 N 子」—— 中国象棋没有这个概念。把一个对将来的棋种无意义的字段放进对外契约，只会让客户端学着去读它。有反射测试钉住 DTO 恰好五个字段。
- [x] 1.5 7 条测试：**逐条对着注册表比**，不写死「应该有 gomoku 和 tictactoe 两条」—— 写死清单的测试会在加中国象棋时静静通过，而那正是这个端点存在的意义失效的时刻。另有一字棋 `isRated == false`、五子棋 15×15、排序、单棋种注册表、DTO 字段集。

## 2. 前端：类型与服务

- [x] 2.1 `core/api/models/game-descriptor.model.ts` —— 镜像 DTO。
- [x] 2.2 `GamesApiService`（抽象类 DI token + 默认实现），一次 `GET /api/games`。
- [x] 2.3 **改为独立的 `GameCapabilitiesService`，不并入 `GameCatalogService`。**
      提案写的是「合并进 `GameCatalogService`」，实现时发现那是错的：目录服务读的是静态 import
      —— 同步、不会失败、不会为空，好几个组件和它们的 spec 都依赖这一点；为了两个布尔把它变成
      异步，就要把 loading / error 状态推进每一个消费者。两层分开、在调用点组合：
      **manifest 说「有哪些游戏、怎么进去」，能力服务说「服务端允许它们做什么」。**
      合不上的键是**「不适用」而不是 `false`** —— 谜题类根本没有 `IGameRules`，折叠成
      `isRated: false` 会让「一字棋不计分」和「成语纵横不是对战游戏」再也分不开。
      加载失败退化为「全部不适用」= 本变更之前的界面：**失败要退化成少一个入口，不是一个错的入口。**
- [x] 2.4 `LeaderboardApiService.getPage(gameKey, page, pageSize)` —— `gameKey` **必填**（design D4）。`top()` 同样。
- [x] 2.5 `UsersApiService.getProfile(userId, gameKey?)` —— 这里可选：省略是一个**有意义的值**（走后端 gomoku 缺省），而资料页首屏就是这个语义。
- [x] 2.6 测试：三个 service 的 URL / query 参数；`GameCapabilitiesService` 的六条（按 key 查、`ratedKeys()` 只列计分、未提及的键是 `undefined`、只拉一次、失败退化、未启动前什么都不报）。

## 3. 前端：`/g/:gameKey/leaderboard`

- [x] 3.1 懒加载路由 + 鉴权守卫。构建产物 **2.21 kB gzipped**，远在 200 KB 之下。
- [x] 3.2 复用现有 `LeaderboardEntry` 与分页；**Rank 用服务端返回的全局名次**，不按页内下标重算。
- [x] 3.3 四态齐全：骨架（与真实行等高，落数据时不跳动）/ empty / error / data。**empty 说人话** —— 「还没有人下过这个棋种」，不是「暂无数据」。
- [x] 3.4 不计分 / 未登记的棋种：后端返回 200 + 空榜，前端渲染说明性空态，**不翻译成错误**。只有请求真失败才是 error 态。
- [x] 3.5 375px 无页面级横向滚动（表格在自己的 `overflow-x: auto` 容器里滚）；按钮键盘可达且有 `focus-visible` 环。
- [x] 3.6 11 条测试：按 gameKey 请求、行渲染、**page 2 的 rank 是 21 不是 1**、空态文案、不计分棋种走空态、未登记键有兜底标题、只有失败才报错、retry、next 翻页、标题取 manifest 名。

## 4. 前端：目录页入口

- [x] 4.1 `status === 'available'` **且**服务端 `isRated` 的卡片，多一个「排行榜」次级入口。
      **卡片标记要改**：此前整张卡是一个 `<a>`，而 `<a>` 里套 `<a>` 是非法 HTML —— 浏览器会拆开，
      键盘顺序和屏幕阅读器都会坏掉。改成容器 + 靠 `after:inset-0` 伸展的启动链接：整张卡仍然可点，
      榜入口靠 `z-10` 赢得重叠区域。
- [x] 4.2 一字棋卡片 **没有**这个入口，有测试。它是「为什么用服务端投影而不是 manifest 上一个布尔副本」那份论证的唯一可执行形式 —— 这条测试挂掉或被删，就说明那份副本又爬回来了。
- [x] 4.3 谜题类同样没有（没有能力信息）；规划中的游戏没有；能力未加载 / 加载失败时一个都没有。

## 5. 前端：资料页棋种切换

- [x] 5.1 header card 上一排切换，只列服务端说计分的棋种，缺省五子棋。
- [x] 5.2 切换重新拉 `GET /api/users/{id}?gameKey=`；切换时清空旧数字（留着会把一个棋种的战绩挂在另一个棋种的标签下）。**presence 不跟着重拉** —— 在不在线是人的属性，不是棋种的。
- [x] 5.3 `gamesPlayed === 0` 渲染「尚无对局」，**不显示 1200**（design D3）。
- [x] 5.4 胜率在零对局时仍然是 `—`。
- [x] 5.5 只有一个计分棋种时切换器**仍然渲染** —— 它是「这是哪个棋种的分」这个信息的唯一载体，今天正好只有一个选项。已写进 spec 并有测试。
- [x] 5.6 9 条测试：只列计分棋种、单选项也渲染、首屏不带 `gameKey`、切换带上、点当前项不重复请求、切换不重拉 presence、0 局显示空态且**屏幕上没有 1200**、有战绩正常显示、能力未加载时无切换器。

## 6. i18n

- [x] 6.1 `leaderboard.*`（标题、空态、错误态、表头、分页）。
- [x] 6.2 `profile.game-switcher.*` + `profile.no-games-in-game`；`catalog.leaderboard-link`。
- [x] 6.3 `profile.games-title` 改成「对局记录（全部棋种）」/「Games (all types)」—— 上方战绩按棋种、下方列表是全部，不加限定词两者读起来像在互相矛盾（dev 库里正好是「1 局」配「9 条记录」）。改的是**既有键的值**而不是加一个兄弟键，免得留下孤儿。
- [x] 6.4 `zh-CN` 与 `en` 键集合逐一对齐（各 313 个，差集为空）；中文串逐条与预期字面比对过 —— 这台机器的控制台会把中文显示成乱码，用眼睛看输出会看错。

## 7. 验收

- [x] 7.1 `dotnet build` 0 warning、`dotnet test` **709 通过**（原 702）；`npm run lint` 干净、`npm run test:ci` **361 通过**（原 326）。
- [x] 7.2 既有后端端点与 DTO 形状零改动 —— 只新增 `GET /api/games`。
- [x] 7.3 `GET /api/games` 未登录 401；登录后返回注册表投影。
- [x] 7.4 浏览器实测（真数据）：`/games` 只有五子棋卡片有榜入口（一字棋、成语纵横都没有）；
      `/g/gomoku/leaderboard` 5 人、名次与前三图标正确；`/g/tictactoe/leaderboard` 与
      `/g/a-game-nobody-registered/leaderboard` 都是说明性空态、不报错；`AI_Medium`（0 局）
      的资料页显示「尚无对局」且**屏幕上没有 1200**；zh-CN 文案正确；375px 无页面级横向滚动
      （表格在自己的容器里滚，341 → 512）；dark 模式配色正常反转。
- [x] 7.5 `openspec validate add-web-per-game-rating --strict` 通过。

## 8. 已知缺口（记录，不在本变更修）

- [ ] 8.1 **`/api/users/me` 与登录 / 注册 / 刷新响应仍然钉死五子棋。** 给 `UserDto` 加棋种维度要
      改 DTO 形状，而「header 上那个分该显示哪个棋种」是产品问题（主棋种？当前所在棋种？全部？），
      不该顺手定。
- [ ] 8.2 **搜索排序仍然钉死五子棋** —— 找人卡片是五子棋大厅的组件，随大厅泛化一起走。
- [ ] 8.3 **`/home` 的排行榜卡片与 `/g/gomoku/leaderboard` 重复** —— 两个入口看同一个榜。
      比现在就动五份 web spec 便宜，大厅泛化那一步会自然消掉（design D2）。
- [ ] 8.4 **`GameManifest.board` 仍在**，但删除条件已经从「等 `generalize-match-contract`」降级成
      「随时」—— `GET /api/games` 已经在传 `Rows` / `Cols`。下一个碰 manifest 的变更可以顺手做掉。
- [ ] 8.5 **对局列表不按棋种过滤** —— `GET /api/users/{id}/games` 没有 `gameKey` 参数。已经把标题
      改成「全部棋种」把话说清楚，但那是说明而不是解决。给那个端点加参数是独立的一件事。
- [ ] 8.6 谜题阶梯（星数 + 用时）与将来的分数榜**不进这个 UI**。三个阶梯刻意不统一。
