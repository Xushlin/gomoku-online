# Tasks — add-per-game-rating

> 判据：**已发布的 Web 客户端零改动，且它看到的每个数字与本变更之前一分不差。**
> 三个 DTO 的形状一个字节不变，只是数据来源从 `User` 换成 `UserGameStats`（§6）。
> 若哪个前端文件需要改，说明范围跑偏了 —— 停下来看为什么。

> 迁移是本仓库唯一会在别人机器上按原样跑一遍的东西。§3 是本变更风险最高的一节，
> 它有一条专门的测试，不是可选项。

## 1. Domain：`UserGameStats`

- [ ] 1.1 新实体 `UserGameStats`，复合主键 `(UserId, GameKey)`，字段 `Rating`(初始 1200) / `GamesPlayed` / `Wins` / `Losses` / `Draws` / `RowVersion`。
- [ ] 1.2 `RecordGameResult(GameOutcome, int newRating)` 从 `User` **搬到** 这里，含 `TouchRowVersion()`。不变量 `Wins + Losses + Draws == GamesPlayed` 保留，现在每个棋种各自成立。
- [ ] 1.3 从 `User` **删除** `Rating` / `GamesPlayed` / `Wins` / `Losses` / `Draws` 与 `RecordGameResult`。**不留镜像字段**（design D2）。
- [ ] 1.4 `User.Register` / `RegisterBot` 不再初始化战绩，也**不创建** `UserGameStats` 行。
- [ ] 1.5 `User.RowVersion` 保留，但现在只由 `ChangePassword` 推动。
- [ ] 1.6 测试：复合主键两行互不影响；初始值；反射断言 `User` 上已无那五个属性；`RecordGameResult` 只推自己那行的令牌。

## 2. Infrastructure：EF 配置与仓库

- [ ] 2.1 `UserGameStatsConfiguration`：复合主键、`RowVersion` 走 `.IsConcurrencyToken().IsRequired()`、索引 `(GameKey, Rating DESC)`。
- [ ] 2.2 `UserConfiguration` 删掉那五列的映射。
- [ ] 2.3 `IUserRepository.GetOrCreateGameStatsAsync(userId, gameKey, ct)` —— 不存在则以初始值新建并加入跟踪，**不自行 SaveChanges**。
- [ ] 2.4 `GetLeaderboardPagedAsync(gameKey, page, pageSize, ct)` 返回 `(IReadOnlyList<UserGameStats>, int)`；`GameKey` 谓词**下推到 EF**。
- [ ] 2.5 测试（打真 SQLite）：按棋种隔离；没有行的人不上榜；`get-or-create` 不重置既有行；不自行提交。

## 3. 迁移（本变更风险最高的一节）

- [ ] 3.1 一条 migration，**同一个文件内**按序：建表 → **显式 SQL** 搬数据到 `GameKey='gomoku'` → 删列。
- [ ] 3.2 **手工检查生成的 migration**。EF 不知道这三步有依赖关系，顺序反了数据就没了。`AddRoomGameKey` 那次 EF 生成的 `defaultValue: ""` 会让所有房间不可玩，是手工改对的 —— 同一类风险。
- [ ] 3.3 测试：在一个"迁移前形状"、含非零战绩的库上跑迁移，断言每个用户得到 `gomoku` 那行且**五个数值逐一相等**；`Users` 表已无那五列；三个 bot 账号同样被搬迁。
- [ ] 3.4 `dotnet ef migrations script` 能跑通（注意 `--idempotent` 在 SQLite 上不支持，那是另一条已记录的文档 bug）。

## 4. ELO 应用路径

- [ ] 4.1 `GameEloApplier` 改用 `GetOrCreateGameStatsAsync` 取两行；`EloRating.Calculate` 吃**该棋种**的 `GamesPlayed`（K 因子因此按该棋种资历分段）。
- [ ] 4.2 `IsRated == false` 的守卫**原样保留** —— 那是 `add-game-capabilities` 的成果，本变更不碰。
- [ ] 4.3 未结束局 MUST NOT 创建战绩行。"没有行"就是"没在这个棋种上下过"，而排行榜成员资格靠它 —— 提前建行会把"下过"变成"点开过"。
- [ ] 4.4 测试：首局建行且只建该棋种那行；跨棋种互不影响（五子棋老手在象棋上按 K=40 从 1200 起）；未结束局不建行；不同棋种并发不再互撞 409。

## 5. 排行榜

- [ ] 5.1 `GetLeaderboardQuery(GameKey, Page, PageSize)`；validator 加 `GameKey` 非空，但**不校验是否已登记**（未登记返回空榜而不是 400）。
- [ ] 5.2 Handler 映射 `UserGameStats` → `LeaderboardEntryDto`，用户名经 `LookupUsernamesAsync` 另取；Rank 公式不变。
- [ ] 5.3 `GET /api/leaderboard?gameKey=`，缺省 `gomoku`，**缺省只在 controller**。
- [ ] 5.4 测试：按棋种隔离；未登记棋种 200 + 空；一字棋 200 + 空（不计分所以没有行）；不带 `gameKey` 与本变更前完全一致。

## 6. 资料页与搜索（形状不变）

- [ ] 6.1 `GetUserProfileQuery(UserId, GameKey)`；`GET /api/users/{id}?gameKey=` 缺省 `gomoku`。
- [ ] 6.2 该用户在该棋种上没有行时返回**初始值**填的 DTO，**不是 404** —— "存在但没下过"是正常答案，404 会被前端误报成"用户不存在"。
- [ ] 6.3 `SearchUsers` 钉在 `gomoku`，**不加参数**。找人卡片是五子棋大厅的组件，泛化它属于大厅泛化那一步。
- [ ] 6.4 三个 DTO 的字段列表**逐一核对未变**。这是本变更不跑偏的判据。
- [ ] 6.5 测试：缺省棋种的数字与变更前一致；没下过该棋种返回初始值而非 404。

## 7. 验收

- [ ] 7.1 `dotnet build` 干净、`dotnet test` 全绿。
- [ ] 7.2 **`frontend-web/` 零改动** —— `git diff --name-only -- frontend-web` 必须为空。若不为空，§6 的形状承诺破了。
- [ ] 7.3 HTTP 冒烟：迁移前建几局五子棋 → 跑迁移 → 确认 `/api/leaderboard` 与 `/api/users/{id}` 的数字**一分不差**；再下一局确认分数照常变动。
- [ ] 7.4 冒烟：一字棋对局仍不产生任何 `UserGameStats` 行。
- [ ] 7.5 `openspec validate add-per-game-rating --strict`。
- [ ] 7.6 PR 描述写明体积、§3 的迁移风险与它的测试、以及"资料页此刻只能看一个棋种"这个已知缺口。

## 8. 已知缺口（记录，不在本变更修）

- [ ] 8.1 资料页只显示一个棋种的战绩（缺省五子棋）；排行榜没有棋种切换入口。两者都是纯前端工作 → `add-web-per-game-rating`。
- [ ] 8.2 搜索排序钉在五子棋 → 随大厅泛化解决。
- [ ] 8.3 谜题阶梯（星数 + 用时）与将来的分数榜**依旧各自独立**，本变更不碰（design D5）。「per-game rating」这个名字听起来像要统一什么，它不统一任何东西。
