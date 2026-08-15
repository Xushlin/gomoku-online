# Tasks — add-per-game-rating

> **本变更拆成 expand / contract 两步实现。** 提案里我写过「它没法再小」—— 那句话是错的。
> 删 `User` 那五列确实会强制所有读者同时改，但**建表 + 回填**这一半完全可以先走：它是纯增量，
> 树保持绿，而且把风险最高的迁移单独暴露出来被测。这是 schema 变更的标准做法，我该一开始就想到。
>
> - **expand（commit 1）**：`UserGameStats` 实体 + EF 配置 + `AddUserGameStats`（建表 +
>   回填到 `GameKey='gomoku'`，**不删列**）+ 迁移测试。`User` 原封不动，读者原封不动。
> - **contract（commit 2）**：切全部读者到 `UserGameStats`、从 `User` 删掉五个字段与
>   `RecordGameResult`、`DropUserRatingColumns` 删列。

> 判据：**已发布的 Web 客户端零改动，且它看到的每个数字与本变更之前一分不差。**
> 三个 DTO 的形状一个字节不变，只是数据来源从 `User` 换成 `UserGameStats`（§6）。
> 若哪个前端文件需要改，说明范围跑偏了 —— 停下来看为什么。

> 迁移是本仓库唯一会在别人机器上按原样跑一遍的东西。§3 是本变更风险最高的一节，
> 它有专门的测试，不是可选项。

## 1. Domain：`UserGameStats`

- [x] 1.1 新实体 `UserGameStats`，复合主键 `(UserId, GameKey)`，字段 `Rating`(初始 1200) / `GamesPlayed` / `Wins` / `Losses` / `Draws` / `RowVersion`。
- [x] 1.2 `RecordGameResult` 写在新实体上（含推进 `RowVersion`）。
- [x] 1.3 从 `User` **删除** `Rating` / `GamesPlayed` / `Wins` / `Losses` / `Draws` 与 `RecordGameResult`。**不留镜像字段**（design D2）。
- [x] 1.4 `User.Register` / `RegisterBot` 不再初始化战绩，也**不创建** `UserGameStats` 行。
- [x] 1.5 `User.RowVersion` 保留，但现在只由 `ChangePassword` 推动。
- [x] 1.6 测试（`UserGameStatsTests`，13 条）：复合主键两行互不影响；初始值；空 `GameKey` 被拒；`RecordGameResult` 只推自己那行的令牌；**反射断言 `User` 上已无那五个属性**。`UserRowVersionTests` 换成「改密码推、下棋不推」的边界。

## 2. Infrastructure：EF 配置与仓储

- [x] 2.1 `UserGameStatsConfiguration`：复合主键、`RowVersion` 走 `.IsConcurrencyToken().IsRequired()`、索引 `(GameKey, Rating)`。
- [x] 2.2 `UserConfiguration` 删掉那五列的映射。
- [x] 2.3 `IUserRepository.GetOrCreateGameStatsAsync(userId, gameKey, ct)` —— 不存在则以初始值新建并加入跟踪，**不自行 SaveChanges**。
- [x] 2.4 `GetLeaderboardPagedAsync(gameKey, page, pageSize, ct)` 返回 `(IReadOnlyList<UserGameStats>, int)`；`GameKey` 谓词**下推到 EF**，bot 过滤靠 join 回 `Users`。
- [x] 2.5 **新增**：`FindGameStatsAsync` / `FindGameStatsForAsync` —— 只读，**不建行**。提案里漏了这两个，而没有它们就只能在查询路径上用 get-or-create，那会让一次 GET 请求把人登记进排行榜。批量版本存在的理由是一页搜索结果 20 个人。
- [x] 2.6 测试（`UserGameStatsRepositoryTests`，14 条，打真 SQLite）：按棋种隔离；没有行的人不上榜；bot 即使分最高也被挡在榜外；`get-or-create` 不重置既有行、不自行提交、提交后落库；只读查询不建行；未登记棋种返回空而不报错；三级排序。

## 3. 迁移（本变更风险最高的一节）

- [x] 3.1 `AddUserGameStats`（expand）：建表 → **显式 SQL** 回填到 `GameKey='gomoku'`。**不删列** —— 于是这一半是可逆的（`Down` 只丢表，战绩仍在 `Users` 原处）。
- [x] 3.2 `DropUserRatingColumns`（contract）：删那五列。顺序由时间戳保证，压缩迁移时**不能颠倒**。
- [x] 3.3 **手工检查生成的 migration**。EF 生成的 `Down` 只 `AddColumn(defaultValue: 0)` —— 回滚后每个人的分变成 0，与 `AddRoomGameKey` 那次 `defaultValue: ""` 是同一类 bug，手工补了一段搬回来的 SQL，`Rating` 缺省改成 1200。
- [x] 3.4 8 条测试，跑**真实 `IMigrator.MigrateAsync`**（不是 `EnsureCreated` —— 后者按当前模型建库、完全跳过迁移脚本）。**停在 expand 那一站取快照**：删完列源数据就不在了，再断言只是在跟自己核对。逐字段保真、bot 账号同样搬迁、`RowVersion` 互不相同、回填可重放、expand 后列还在、contract 后列没了、回滚恢复真实数值而不是零。
- [x] 3.5 `dotnet ef migrations script` 能跑通（`--idempotent` 在 SQLite 上不支持，那是另一条已记录的文档 bug）。

## 4. ELO 应用路径

- [x] 4.1 `GameEloApplier` 改用 `GetOrCreateGameStatsAsync` 取两行；`EloRating.Calculate` 吃**该棋种**的 `GamesPlayed`（K 因子因此按该棋种资历分段）。
- [x] 4.2 `IsRated == false` 的守卫**原样保留** —— 那是 `add-game-capabilities` 的成果，本变更不碰。
- [x] 4.3 未结束局 MUST NOT 创建战绩行。
- [x] 4.4 测试：首局建行且只建该棋种那行（`stats.Count == 2`）；未结束局一行不建；不计分棋种 `GetOrCreateGameStatsAsync` `Times.Never`；K 因子取该棋种局数（30 局 → 跌幅落在 K=20 区间而不是 K=40 区间）。

## 5. 排行榜

- [x] 5.1 `GetLeaderboardQuery(GameKey, Page, PageSize)`；validator 加 `GameKey` 非空，但**不校验是否已登记**（未登记返回空榜而不是 400）。
- [x] 5.2 Handler 映射 `UserGameStats` → `LeaderboardEntryDto`，用户名经 `LookupUsernamesAsync` 另取；Rank 公式不变。
- [x] 5.3 `GET /api/leaderboard?gameKey=`，缺省 `gomoku`，**缺省只在 controller**。
- [x] 5.4 测试：按棋种隔离；未登记棋种 200 + 空；一字棋 200 + 空；不带 `gameKey` 与本变更前完全一致。

## 6. 资料页与搜索（形状不变）

- [x] 6.1 `GetUserProfileQuery(UserId, GameKey)`；`GET /api/users/{id}?gameKey=` 缺省 `gomoku`。
- [x] 6.2 该用户在该棋种上没有行时返回**初始值**填的 DTO，**不是** 404。
- [x] 6.3 `SearchUsers` 钉在 `gomoku`，**不加参数**。没有战绩行的人**仍然出现在搜索结果里** —— 搜索的是「人」，不是「上过榜的人」，这与排行榜的成员资格规则刻意不同。
- [x] 6.4 三个 DTO 的字段列表**逐一核对未变**（`UserDto` / `UserPublicProfileDto` / `LeaderboardEntryDto`），有反射测试钉住。
- [x] 6.5 **提案漏了 `UserDto`**：`/api/users/me` 与登录 / 注册 / 刷新的 `AuthResponse.User` 都带战绩，且都没有 `gameKey` 参数。全部钉在五子棋 —— 已发布客户端在这里读的就是五子棋的数字。注册响应用初始值填且不建行。
- [x] 6.6 测试：缺省棋种的数字与变更前一致；没下过该棋种返回初始值而非 404；读资料不建行；搜索一页只查一次战绩。

## 7. 验收

- [x] 7.1 `dotnet build` 干净（0 warning）、`dotnet test` 全绿：**702 通过 / 0 失败**（Domain 425 + Application 203 + Infrastructure 74；变更前 669）。
- [x] 7.2 **`frontend-web/` 零改动** —— `git diff --name-only -- frontend-web` 为空。
- [x] 7.3 HTTP 冒烟：拿**本地真实的迁移前 `gewu.db`**（6 个用户，含 `AI_Easy` 1220/1胜、`smoke1786688301` 1180/1负）复制一份 → 用原生 SQL 记下五列的值 → 启 API 让它跑迁移 → 逐字段核对 `/api/leaderboard` 与 `/api/users/{id}`。**19 项全过，数字一分不差。** 再下一局确认分数照常变动、bot 的战绩也照常推进。
- [x] 7.4 冒烟：一字棋对局结束后，`/api/leaderboard?gameKey=tictactoe` 仍为空、`/api/users/{me}?gameKey=tictactoe` 仍是 0 局、五子棋那行未被触碰。
- [x] 7.5 `openspec validate add-per-game-rating --strict` 通过。
- [x] 7.6 PR 描述写明体积、§3 的迁移风险与它的测试、以及「资料页此刻只能看一个棋种」这个已知缺口。

## 8. 已知缺口（记录，不在本变更修）

- [ ] 8.0 **回填包含 `GamesPlayed = 0` 的用户**，于是他们在五子棋榜上仍以 1200 分出现 —— 与 design D4（「没下过该棋种的人不上榜」）表面冲突。选保真是因为今天的排行榜查询只过滤 `!IsBot`，这些人**现在就在榜上**；只回填 `GamesPlayed > 0` 会让他们消失，那大概是个改进，但它是一个**产品决定**，不该作为一次迁移的副作用悄悄发生。
  **副作用要说清楚**：从此**新**注册的用户在下完第一局之前不上榜（D4 生效），而迁移搬过来的零局用户还在榜上。这个不一致是保真的代价，会在「要不要把零局用户清出排行榜」那个变更里一并消掉。

- [ ] 8.1 资料页只显示一个棋种的战绩（缺省五子棋）；排行榜没有棋种切换入口；`/me` 与登录响应同样钉在五子棋。三者都是纯前端 + DTO 形状工作 → `add-web-per-game-rating`。
- [ ] 8.2 搜索排序钉在五子棋 → 随大厅泛化解决。
- [ ] 8.3 谜题阶梯（星数 + 用时）与将来的分数榜**依旧各自独立**，本变更不碰（design D5）。「per-game rating」这个名字听起来像要统一什么，它不统一任何东西。
- [ ] 8.4 `DropUserRatingColumns` 在 SQLite 上**不是原子的**：`DROP COLUMN` 被 EF 降级成「建新表 → 拷贝 → 换名」，需要 `PRAGMA foreign_keys = 0`，而那不能在事务里执行（跑迁移时会打一条警告）。本地无所谓，真上生产之前得先备份。迁移文件里记了。
- [ ] 8.5 `backend/smoke/AiSmoke` 已经**坏了，且不是本变更弄坏的**：它第 7 步 `GetFromJsonAsync<List<LeaderboardEntry>>("/api/leaderboard")`，而端点在 `add-leaderboard-pagination` 之后返回的是 `PagedResult<T>`，反序列化会抛。它不在 `Gewu.slnx` 里、CI 也不跑它，所以一直没人发现 —— 这本身说明一个不在 CI 里的冒烟工具会静静烂掉。
- [ ] 8.6 连接串的配置键是 `ConnectionStrings:Default`,不是 `ConnectionStrings:DefaultConnection`。环境变量因此是 `ConnectionStrings__Default`（本次冒烟验证过，确实生效）。此前记成 `__DefaultConnection` 不生效是**记错了键名**,不是功能缺失。
