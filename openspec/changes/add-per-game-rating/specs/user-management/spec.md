## RENAMED Requirements

两条 requirement 的标题不再成立:`User` 不再承载战绩,而并发保护的对象从 `User` 变成了
`UserGameStats`。应用顺序 RENAMED → REMOVED → MODIFIED → ADDED。

- FROM: ### Requirement: `User` 聚合根承载身份、战绩、启用状态与注册时间
- TO: ### Requirement: `User` 聚合根承载身份、启用状态与注册时间

- FROM: ### Requirement: `User.RowVersion` 乐观并发令牌保护战绩写入
- TO: ### Requirement: `UserGameStats.RowVersion` 乐观并发令牌保护战绩写入,`User.RowVersion` 保护密码

## ADDED Requirements

### Requirement: `UserGameStats` 是战绩与 Rating 的唯一真源

`Gewu.Domain` SHALL 定义实体 `UserGameStats`,主键为 `(UserId, GameKey)` 复合键,字段:

- `UserId: UserId` / `GameKey: string` —— 复合主键。
- `Rating: int` —— 该玩家在该棋种上的 ELO,初始 1200。
- `GamesPlayed` / `Wins` / `Losses` / `Draws: int` —— 该棋种上的战绩,初始全 0。
- `RowVersion: byte[]` —— 乐观并发令牌,语义与原 `User.RowVersion` 在战绩路径上的语义一致。

`User` MUST NOT 再持有 `Rating` / `GamesPlayed` / `Wins` / `Losses` / `Draws`,**也 MUST NOT 保留
它们的镜像或聚合值**(例如"主棋种的分"或"所有棋种加权")。镜像是第二份真源,而它与本实体漂移之后
的症状是**排行榜与资料页显示不同的分**,且没有任何东西会拦住 —— 与建房校验不许内联棋种白名单
是同一条理由。

一个 `(UserId, GameKey)` 行 MUST 只在该玩家**下完**该棋种的第一局时才被创建(见 `elo-rating` 的
`MakeMoveCommandHandler`)。"没有行"就是"没在这个棋种上下过",而排行榜的成员资格正是靠它 ——
所以为一局尚未结束的棋提前建行会把"下过"的含义变成"点开过"。

#### Scenario: 复合主键
- **WHEN** 同一玩家在 `gomoku` 与 `xiangqi` 上各有战绩
- **THEN** 存在两行,`UserId` 相同、`GameKey` 不同;写其中一行 MUST NOT 影响另一行

#### Scenario: 初始值
- **WHEN** 新建一行 `UserGameStats`
- **THEN** `Rating == 1200`,`GamesPlayed == Wins == Losses == Draws == 0`,`RowVersion` 非空且长度 16

#### Scenario: `User` 上不再有战绩字段
- **WHEN** 反射检查 `User` 的 public 属性
- **THEN** 属性集合 MUST NOT 含 `Rating` / `GamesPlayed` / `Wins` / `Losses` / `Draws` 中的任何一个

### Requirement: `IUserRepository.GetOrCreateGameStatsAsync` 取或建某棋种的战绩行

Application 层 SHALL 在 `IUserRepository` 上提供:

```
Task<UserGameStats> GetOrCreateGameStatsAsync(
    UserId userId, string gameKey, CancellationToken cancellationToken);
```

不存在时 MUST 以初始值新建并加入变更跟踪(**MUST NOT** 自行 `SaveChangesAsync` —— 由调用方合并到
对局结束的同一事务)。

是 get-or-**create** 而不是 find-or-throw:"第一次下这个棋种"是常态而不是异常。

#### Scenario: 首次取即新建
- **WHEN** 对一个没有 `xiangqi` 战绩的玩家调用
- **THEN** 返回一行初始值的 `UserGameStats`;在调用方 `SaveChangesAsync` 之后它 MUST 已落库

#### Scenario: 已存在则取回
- **WHEN** 该玩家在该棋种上已有战绩
- **THEN** 返回既有那行,MUST NOT 重置它的任何字段

#### Scenario: 不自行提交
- **WHEN** 调用后不执行 `SaveChangesAsync`
- **THEN** 数据库中 MUST NOT 出现新行

### Requirement: 迁移把既有战绩归给五子棋,且顺序不可颠倒

一条 EF migration SHALL 按此顺序完成三步,**在同一个 migration 内**:

1. 建 `UserGameStats` 表(复合主键 + 索引 `(GameKey, Rating DESC)` 供排行榜使用)。
2. 用**显式 SQL** 把每个 `Users` 行的五个战绩列搬成一行 `UserGameStats`,`GameKey = 'gomoku'`。
3. 从 `Users` 删除那五列。

顺序 MUST NOT 颠倒 —— 先删列再搬数据就把数据搬没了,而 EF 的自动生成不知道这三步有依赖关系。
本仓库在同一处被咬过一次:`AddRoomGameKey` 那次 EF 生成了 `defaultValue: ""`,会让每个既有房间的
`GameKey` 变成空串、进而解析不出规则、房间全部不可玩;那次是手工改成 `'gomoku'` 加一条显式
`UPDATE` 才对的。

本 migration MUST 有一个针对"迁移前形状"的库跑一遍的测试,断言战绩一行不丢、数值一分不差。
本地 SQLite 没有生产数据是事实,但迁移是本仓库**唯一会在别人机器上按原样跑一遍**的东西。

#### Scenario: 既有战绩不丢
- **WHEN** 在一个含若干 `Users` 行(各有非零战绩)的迁移前库上跑迁移
- **THEN** 每个用户得到一行 `GameKey == 'gomoku'` 的 `UserGameStats`,五个数值与迁移前逐一相等

#### Scenario: 列已删除
- **WHEN** 迁移完成后检视 `Users` 表结构
- **THEN** MUST NOT 再有 `Rating` / `GamesPlayed` / `Wins` / `Losses` / `Draws` 列

#### Scenario: bot 账号同样被搬迁
- **WHEN** 迁移前库中含三个 seeded bot 账号
- **THEN** 它们各自也得到 `gomoku` 那行 —— bot 对局计分(反套利约束),所以它们的战绩同样是真数据

## MODIFIED Requirements

### Requirement: `User` 聚合根承载身份、启用状态与注册时间


系统 SHALL 定义 `User` 作为聚合根,字段包含:`Id: UserId`、`Email: Email`、`Username: Username`、`PasswordHash: string`、`IsActive: bool`、`IsBot: bool`、**`RowVersion: byte[]`**(本次新增,乐观并发令牌,Domain 自管)、`CreatedAt: DateTime`、以及一个**只读**的 `RefreshTokens: IReadOnlyCollection<RefreshToken>`。外部 MUST NOT 直接修改这些字段;所有变更仅通过 `User` 提供的领域方法发生。

#### Scenario: 字段可读
- **WHEN** 访问 `User` 的任意上述属性(包括新增的 `RowVersion`)
- **THEN** MUST 返回相应的类型与当前值

#### Scenario: `RefreshTokens` 只读
- **WHEN** 外部尝试把 `User.RefreshTokens` 强转为 `List<RefreshToken>` 并调用 `Add`
- **THEN** 该修改 MUST NOT 影响 `User` 内部状态

战绩(`Rating` / `GamesPlayed` / `Wins` / `Losses` / `Draws`)**不再在此聚合上** —— 它们随棋种分开,
住在 `UserGameStats`。`User` MUST NOT 保留它们的镜像。

#### Scenario: RowVersion 通过 Domain 方法变化
- **WHEN** 外部尝试 `user.RowVersion = new byte[16]`
- **THEN** 编译失败(`private set`);变更 MUST 通过 `ChangePassword` 间接触发

#### Scenario: 战绩字段已移出
- **WHEN** 反射检查 `User` 的 public 属性
- **THEN** MUST NOT 含 `Rating` / `GamesPlayed` / `Wins` / `Losses` / `Draws`

> `RecordGameResult` 现在住在 `UserGameStats` 上,其 MODIFIED 版本见 `elo-rating` delta —— 该
> Requirement 由 `add-elo-system` 归在 `elo-rating` 能力里,本次同样在那里修订,不在本 capability 重复。

### Requirement: `User.Register` 静态工厂方法创建新用户并设定初始状态


系统 SHALL 提供 `User.Register(UserId id, Email email, Username username, string passwordHash, DateTime createdAt)` 静态工厂方法。返回的 `User` 实例 MUST 具有:

- `IsActive = true`
- **`IsBot = false`**(本次新增不变量)
- `CreatedAt = createdAt`(由调用方通过 `IDateTimeProvider` 提供,不得取 `DateTime.UtcNow`)
- `RefreshTokens` 为空集合
- `Id` / `Email` / `Username` / `PasswordHash` 等于入参

`passwordHash` 为 `null` 或空字符串时,MUST 抛出 `ArgumentException`。

注册 MUST NOT 创建任何 `UserGameStats` 行 —— 一个新用户在**每个**棋种上都还没下过,
而"没有行"正是那个意思。行在他下完某棋种第一局时才出现(见 `elo-rating`)。

#### Scenario: 初始值正确
- **WHEN** 以合法入参调用 `User.Register(...)`
- **THEN** 返回的 `User` 的每个字段 MUST 等于上述初始值,且 `IsBot == false`

#### Scenario: 注册不建战绩行
- **WHEN** 注册一个新用户并提交
- **THEN** `UserGameStats` 表中 MUST NOT 出现属于他的任何行

#### Scenario: 密码哈希缺失
- **WHEN** `passwordHash` 为 `null` 或空字符串
- **THEN** 抛出 `ArgumentException`

### Requirement: `UserGameStats.RowVersion` 乐观并发令牌保护战绩写入,`User.RowVersion` 保护密码


`UserGameStats` 与 `User` MUST **各自**定义 `byte[] RowVersion` 只读属性 —— 两者保护的是不同的
东西,合成一个会让"改密码"和"下完一局棋"互相冲突。语义:

- 字段类型:`byte[]`,长度 16 字节(底层用 `Guid.NewGuid().ToByteArray()` 产生);
- 构造时自带非空值;
- `UserGameStats.RecordGameResult(outcome, newRating)` MUST 调该行的 `TouchRowVersion()`
  —— 保护的是**该棋种那一行**的战绩写入。
- `User.ChangePassword(newPasswordHash)` MUST 调 `User` 自己的 `TouchRowVersion()`。
- `IssueRefreshToken` / `RevokeRefreshToken` / `RevokeAllRefreshTokens` MUST NOT 调 `TouchRowVersion()` —— refresh token 路径只操作子集合,不改 User 父行业务属性;并发场景(并发登录、并发登出)本身无冲突,加保护反而把登录 / 登出流程不必要地串行化。

数据库层 MUST 把 `Users.RowVersion` 与 `UserGameStats.RowVersion` 两列都设为 `NOT NULL` 的 blob,
各自的 EF 配置 MUST 为该属性调 `.IsConcurrencyToken().IsRequired()`。

规则总结:**改战绩的路径推 `UserGameStats` 那行的令牌;改密码的路径推 `User` 的令牌;
只改子集合(RefreshTokens)的都不推。**

分成两个令牌的收益是具体的:一个玩家一边在下棋、一边改密码,此前会撞 409;现在不会。

#### Scenario: 字段默认非空
- **WHEN** 调 `User.Register(...)` 或 `User.RegisterBot(...)`
- **THEN** 返回的 `User.RowVersion` 不为 `null`,长度为 16

#### Scenario: 两次 Register 得到不同 RowVersion
- **WHEN** 两次独立调用 `User.Register(...)`
- **THEN** 两个 User 的 RowVersion 字节数组 MUST 不相等

#### Scenario: RecordGameResult 改变该棋种那行的 RowVersion
- **WHEN** 对某 `UserGameStats` 行调 `RecordGameResult(GameOutcome.Win, 1220)`
- **THEN** 该行 `RowVersion` 与调用前**不相等**;同一玩家其它棋种那行以及 `User.RowVersion` MUST 不变

#### Scenario: ChangePassword 改变 RowVersion(本次新增)
- **WHEN** 对同一 User 调 `ChangePassword("newhash")`
- **THEN** `RowVersion` 与调用前**不相等**

#### Scenario: 多次 RecordGameResult 每次都变
- **WHEN** 连续对同一 User 调 RecordGameResult 三次(Win / Loss / Draw)
- **THEN** 每次调用后 RowVersion 都更新;三次之间两两不相等

#### Scenario: 刷新令牌路径不改 RowVersion
- **WHEN** 对同一 User 调 `IssueRefreshToken` / `RevokeRefreshToken` / `RevokeAllRefreshTokens` 中的任一个
- **THEN** `RowVersion` 保持不变 —— 这些方法只操作子集合,不参与 User 并发保护

### Requirement: 并发 `RecordGameResult` 冲突时后写者抛 `DbUpdateConcurrencyException`


两个事务并发加载**同一棋种的同一 `UserGameStats` 行**后都调用 `RecordGameResult` 并尝试 `SaveChangesAsync` 时,第一个成功(该行 RowVersion 更新为 V2),第二个的 UPDATE 因 `WHERE RowVersion = V1` 命中 0 行 MUST 抛 `DbUpdateConcurrencyException`。

**不同棋种之间不冲突**:同一玩家的两局棋若属于不同棋种,写的是不同行,MUST 都成功。这是把令牌下移到 `UserGameStats` 的直接收益 —— 此前它们会互相撞 409。

上层(handler / worker / HTTP 客户端)MUST 决定重试策略;Api 层 MUST 通过全局异常中间件把该异常映射为 HTTP 409 + `ProblemDetails` 响应(沿用 `add-rooms-and-gameplay` 已建立的映射,不新增条目)。

#### Scenario: 并发 ELO 更新
- **WHEN** 两个 `ResignCommand` handler 对同一 Alice 几乎同时完成事务
- **THEN** 一者 SaveChanges 成功(数据库 Alice.Rating / GamesPlayed / Losses 按对应路径更新一次);另一者 SaveChanges 抛 `DbUpdateConcurrencyException`,**不会**默默覆盖第一者的更新

#### Scenario: HTTP 409 响应
- **WHEN** 客户端发出 `POST /api/rooms/{id}/resign`,恰好与 `TurnTimeoutWorker` 对同一用户另一房间的判负并发到达
- **THEN** 若自己输给 409,响应为 HTTP 409 + `ProblemDetails.title == "Concurrent modification."`,客户端应重拉 `GET /api/users/me` / `GET /api/rooms/{id}` 再决定是否重试

#### Scenario: 不同棋种并发不冲突
- **WHEN** 同一玩家的一局 `gomoku` 与一局 `xiangqi` 几乎同时结束
- **THEN** 两次 `SaveChangesAsync` MUST 都成功,两行各自更新一次

### Requirement: `UserPublicProfileDto` 是他人可见的用户资料快照


Application 层 SHALL 在 `Common/DTOs/UserPublicProfileDto.cs` 定义:

```
public sealed record UserPublicProfileDto(
    Guid Id,
    string Username,
    int Rating,
    int GamesPlayed,
    int Wins,
    int Losses,
    int Draws,
    DateTime CreatedAt);
```

DTO MUST NOT 含 `Email` / `PasswordHash` / `RefreshTokens` / `IsActive` / `IsBot` 字段。比起
`UserSummaryDto`(仅 Id + Username)更完整;比起 `UserDto`(`/me`)少 Email。

**形状 MUST 一个字节不变**,变的只是数据来源:战绩四项与 `Rating` 取自该用户在**某一个棋种**上的
`UserGameStats`,由查询的 `GameKey` 决定(见下一条 requirement)。该用户在该棋种上没有行时,
MUST 返回初始值(`Rating = 1200`、战绩全 0)而不是 404 —— "这个人存在但没下过这个棋种"是一个
正常答案,而 404 会让前端把它误报成"用户不存在"。

保持形状不变是刻意的:已发布的 Web 客户端因此零改动。"资料页同时展示所有棋种的战绩"是纯前端
工作,留给 `add-web-per-game-rating`;**本变更的代价是资料页此刻只能看到一个棋种** —— 记为缺口,
不是遗漏。

#### Scenario: 反射检查无敏感字段
- **WHEN** 审阅 `UserPublicProfileDto` 的 public properties
- **THEN** 属性集合精确为 `{Id, Username, Rating, GamesPlayed, Wins, Losses, Draws, CreatedAt}`

### Requirement: `GET /api/users/{id}` 按 Id 返回公开用户主页


Api 层 SHALL 暴露 `GET /api/users/{id:guid}`(`[Authorize]`):

- 端点 SHALL 接受 query `gameKey`,缺省 `gomoku`。缺省 MUST 只发生在 Api 层;
  `GetUserProfileQuery.GameKey` MUST 是必填非空字段 —— Application 层不猜自己在被问哪个棋种。
- Controller 调 `GetUserProfileQuery(new UserId(id), gameKey ?? "gomoku")`;
- Handler Load user;null 抛 `UserNotFoundException` → HTTP 404;
- Handler 取该用户在该棋种上的 `UserGameStats`;没有则用初始值填 DTO,**MUST NOT** 404;
- **不过滤 bot**:允许查询 bot 账号(`BotAccountIds.Easy` / `Medium` / `Hard`)返回其资料,让前端回放中对 `AI_Hard` 的链接能正常展示战绩。
- 成功 HTTP 200 + `UserPublicProfileDto`。

路由约束 `{id:guid}` 保证 `GET /api/users/me` **不**被该 action 拦截 —— "me" 不是合法 Guid。

#### Scenario: 真人主页
- **WHEN** 登录用户 `GET /api/users/{aliceGuid}`,alice 是真人
- **THEN** HTTP 200;Body 含 Rating / 战绩 / CreatedAt;**不**含 Email

#### Scenario: Bot 主页也可查
- **WHEN** `GET /api/users/{BotAccountIds.Easy}`
- **THEN** HTTP 200;Username == "AI_Easy";战绩字段正常反映 bot 历史对局

#### Scenario: 找不到
- **WHEN** 请求不存在的 `Guid`
- **THEN** HTTP 404 `UserNotFoundException`

#### Scenario: `/me` 不被误拦
- **WHEN** `GET /api/users/me`(调用者登录)
- **THEN** HTTP 200;走既有 `Me` action,返回 `UserDto`(含 Email)—— 路由约束 `{id:guid}` 确保 "me" 不匹配

#### Scenario: 未登录
- **WHEN** 无 Bearer token
- **THEN** HTTP 401

#### Scenario: 缺省棋种向后兼容
- **WHEN** 已发布的客户端调 `GET /api/users/{aliceGuid}`(不带 `gameKey`)
- **THEN** 返回她的五子棋战绩,数字与本变更之前完全一致

#### Scenario: 没下过该棋种
- **WHEN** `GET /api/users/{aliceGuid}?gameKey=xiangqi` 而 Alice 从未下过象棋
- **THEN** HTTP 200,`Rating == 1200`、战绩全 0 —— MUST NOT 404
