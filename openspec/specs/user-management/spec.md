# User Management

## Purpose

用户身份与账号生命周期能力:`UserId` / `Email` / `Username` 值对象的不变量,`User` 聚合根(含战绩字段 `Rating` / `GamesPlayed` / `Wins` / `Losses` / `Draws` 与启用状态 `IsActive`),`RefreshToken` 子实体,以及按 Id / Email / Username 查、按 token hash 追溯聚合、邮箱与用户名全局唯一这组规则。本能力**不**负责登录流程的凭据校验与 token 签发 —— 那些归 `authentication` 能力。

实现位于 `backend/src/Gomoku.Domain/Users/`(Domain)与 `backend/src/Gomoku.Infrastructure/Persistence/`(持久化适配)。
## Requirements
### Requirement: `UserId` 是 `Guid` 的强类型包装值对象

系统 SHALL 用 `UserId` 值对象承载用户主键,内部为 `Guid`。`UserId` MUST 不可变、基于值相等。任何 Domain / Application 的公共 API 在引用用户标识时 MUST 使用 `UserId` 而非裸 `Guid`。

#### Scenario: 构造与取值
- **WHEN** 以 `Guid.NewGuid()` 构造 `UserId`
- **THEN** 其 `Value` 属性等于传入的 `Guid`

#### Scenario: 值相等
- **WHEN** 两个 `UserId` 包装的 `Guid` 相同
- **THEN** `==`、`.Equals()` 与 `.GetHashCode()` 均认定它们相等

---

### Requirement: `Email` 值对象校验格式并规范化为小写

系统 SHALL 用 `Email` 值对象承载邮箱,构造时 MUST 校验合法性(借助 `System.Net.Mail.MailAddress` 的构造成功 + 总长 ≤ 254);非法格式 MUST 抛出 `InvalidEmailException`。`Email` MUST 将字符串规范化为小写后存储,且基于**规范化后的字符串**做值相等比较。`null` / 空字符串 / 空白字符串均 MUST 拒绝。

#### Scenario: 合法邮箱构造并小写化
- **WHEN** 以 `"Alice@Example.COM"` 构造 `Email`
- **THEN** 返回值对象,其 `Value` 等于 `"alice@example.com"`

#### Scenario: 非法格式抛异常
- **WHEN** 以 `"not-an-email"`、`""`、`null`、`"   "` 或超过 254 字符的字符串构造 `Email`
- **THEN** 抛出 `InvalidEmailException`,消息包含足以定位原因的描述

#### Scenario: 规范化后相等
- **WHEN** 以 `"Alice@Example.com"` 和 `"alice@EXAMPLE.COM"` 分别构造两个 `Email`
- **THEN** 两者 MUST 相等

---

### Requirement: `Username` 值对象校验长度、字符集与非全数字规则

系统 SHALL 用 `Username` 值对象承载用户名,构造时 MUST 同时满足:
- 长度 3–20 个 UTF-16 字符(含边界);
- 字符集限定为 `[a-zA-Z0-9_]` 与中文 `[\u4e00-\u9fff]`(BMP 内 CJK 基本区);
- 不得**全部由数字组成**。

任一条件不满足,MUST 抛出 `InvalidUsernameException`。`Username` MUST 不可变;比较时大小写不敏感,但存储保留原始大小写。

#### Scenario: 合法用户名
- **WHEN** 以 `"alice"`、`"Bob_2"`、`"小明"`、`"玩家123"`、`"a_b_c"` 构造 `Username`
- **THEN** 返回合法值对象

#### Scenario: 长度不在 [3..20]
- **WHEN** 以长度为 2 或 21 的字符串构造 `Username`
- **THEN** 抛出 `InvalidUsernameException`

#### Scenario: 字符集非法
- **WHEN** 以含空格、连字符、标点、emoji 或扩展 CJK 的字符串构造 `Username`(例如 `"alice bob"`、`"bad-name"`、`"🐱user"`)
- **THEN** 抛出 `InvalidUsernameException`

#### Scenario: 全数字用户名
- **WHEN** 以 `"12345"` 或 `"00000"` 构造 `Username`
- **THEN** 抛出 `InvalidUsernameException`

#### Scenario: 大小写不敏感的相等
- **WHEN** 以 `"Alice"` 和 `"ALICE"` 构造两个 `Username`
- **THEN** 它们 MUST 相等

#### Scenario: `null` / 空白拒绝
- **WHEN** 以 `null`、空字符串或全空白字符串构造 `Username`
- **THEN** 抛出 `InvalidUsernameException`

---

### Requirement: `User` 聚合根承载身份、战绩、启用状态与注册时间

系统 SHALL 定义 `User` 作为聚合根,字段包含:`Id: UserId`、`Email: Email`、`Username: Username`、`PasswordHash: string`、`Rating: int`、`GamesPlayed: int`、`Wins: int`、`Losses: int`、`Draws: int`、`IsActive: bool`、`IsBot: bool`、**`RowVersion: byte[]`**(本次新增,乐观并发令牌,Domain 自管)、`CreatedAt: DateTime`、以及一个**只读**的 `RefreshTokens: IReadOnlyCollection<RefreshToken>`。外部 MUST NOT 直接修改这些字段;所有变更仅通过 `User` 提供的领域方法发生。

#### Scenario: 字段可读
- **WHEN** 访问 `User` 的任意上述属性(包括新增的 `RowVersion`)
- **THEN** MUST 返回相应的类型与当前值

#### Scenario: `RefreshTokens` 只读
- **WHEN** 外部尝试把 `User.RefreshTokens` 强转为 `List<RefreshToken>` 并调用 `Add`
- **THEN** 该修改 MUST NOT 影响 `User` 内部状态

#### Scenario: RowVersion 通过 Domain 方法变化
- **WHEN** 外部尝试 `user.RowVersion = new byte[16]`
- **THEN** 编译失败(`private set`);变更 MUST 通过 `RecordGameResult` 间接触发

> `RecordGameResult` 的 MODIFIED 版本(末尾调 `TouchRowVersion`)见 `elo-rating` delta —— 该
> Requirement 由 `add-elo-system` 归在 `elo-rating` 能力里,本次同样在那里修订,不在本 capability 重复。

### Requirement: `User.Register` 静态工厂方法创建新用户并设定初始状态

系统 SHALL 提供 `User.Register(UserId id, Email email, Username username, string passwordHash, DateTime createdAt)` 静态工厂方法。返回的 `User` 实例 MUST 具有:

- `Rating = 1200`
- `GamesPlayed = 0`,`Wins = 0`,`Losses = 0`,`Draws = 0`
- `IsActive = true`
- **`IsBot = false`**(本次新增不变量)
- `CreatedAt = createdAt`(由调用方通过 `IDateTimeProvider` 提供,不得取 `DateTime.UtcNow`)
- `RefreshTokens` 为空集合
- `Id` / `Email` / `Username` / `PasswordHash` 等于入参

`passwordHash` 为 `null` 或空字符串时,MUST 抛出 `ArgumentException`。

#### Scenario: 初始值正确
- **WHEN** 以合法入参调用 `User.Register(...)`
- **THEN** 返回的 `User` 的每个字段 MUST 等于上述初始值,且 `IsBot == false`

#### Scenario: 密码哈希缺失
- **WHEN** `passwordHash` 为 `null` 或空字符串
- **THEN** 抛出 `ArgumentException`

---

### Requirement: `User.IssueRefreshToken` 在聚合内添加一枚可用的 refresh token

系统 SHALL 提供 `User.IssueRefreshToken(string tokenHash, DateTime expiresAt, DateTime issuedAt)` 方法。调用后:新的 `RefreshToken` 子实体 MUST 以 `tokenHash` / `expiresAt` / `issuedAt` 写入,`RevokedAt = null`,并出现在 `User.RefreshTokens` 中。`tokenHash` 为 `null` / 空 MUST 抛 `ArgumentException`;`expiresAt <= issuedAt` MUST 抛 `ArgumentException`。

#### Scenario: 成功加入
- **WHEN** 对一个 `User` 调用 `IssueRefreshToken("hash1", expiresAt=now+7d, issuedAt=now)`
- **THEN** `user.RefreshTokens` 包含一枚 `TokenHash="hash1"` 的子实体,且其 `RevokedAt` 为 `null`

#### Scenario: 过期时间不合法
- **WHEN** `expiresAt <= issuedAt`
- **THEN** 抛 `ArgumentException`

---

### Requirement: `User.RevokeRefreshToken` 按 hash 吊销单枚 token

系统 SHALL 提供 `User.RevokeRefreshToken(string tokenHash, DateTime revokedAt)` 方法,找到 `TokenHash == tokenHash` 的子实体并把其 `RevokedAt` 设为 `revokedAt`。找不到该 hash 时,方法 MUST 返回 `false`(不抛);成功时返回 `true`。

#### Scenario: 吊销成功
- **WHEN** 用户有一枚 `"hash1"` 未撤销的 token,调用 `RevokeRefreshToken("hash1", now)`
- **THEN** 返回 `true`,该 token 的 `RevokedAt` 等于 `now`

#### Scenario: hash 不存在
- **WHEN** 调用 `RevokeRefreshToken("unknown-hash", now)`
- **THEN** 返回 `false`,其他 token 状态不变

#### Scenario: 已撤销不重复撤销
- **WHEN** 一枚 token 已经 `RevokedAt = t1`,再次调用 `RevokeRefreshToken(sameHash, t2)`
- **THEN** 实现可以返回 `true` 或 `false`,但该 token 的 `RevokedAt` MUST 保持为 `t1` 不变(避免覆盖首次吊销时间)

---

### Requirement: `User.RevokeAllRefreshTokens` 批量吊销当前用户所有未撤销 token

系统 SHALL 提供 `User.RevokeAllRefreshTokens(DateTime revokedAt)` 方法,将当前用户所有 `RevokedAt == null` 的 token 的 `RevokedAt` 设为 `revokedAt`。已撤销的保持不变。

#### Scenario: 批量吊销
- **WHEN** 用户有 3 枚未撤销 token,调用 `RevokeAllRefreshTokens(now)`
- **THEN** 三枚 token 的 `RevokedAt` 均等于 `now`

#### Scenario: 不覆盖已撤销
- **WHEN** 用户有 2 枚未撤销 + 1 枚 `RevokedAt = t_old` 的 token,调用 `RevokeAllRefreshTokens(t_new)`
- **THEN** 两枚变为 `t_new`,第三枚保持 `t_old`

---

### Requirement: `RefreshToken` 子实体承载 hash、过期时间与可吊销状态

系统 SHALL 定义 `RefreshToken` 子实体,字段:`Id: Guid`、`UserId: UserId`、`TokenHash: string`、`ExpiresAt: DateTime`、`CreatedAt: DateTime`、`RevokedAt: DateTime?`。`RefreshToken` SHALL 提供只读方法 `IsActive(DateTime now)`,当且仅当 `RevokedAt == null` 且 `ExpiresAt > now` 时返回 `true`。

#### Scenario: 活跃判定
- **WHEN** `RevokedAt == null` 且 `ExpiresAt > now`
- **THEN** `IsActive(now)` 返回 `true`

#### Scenario: 已撤销
- **WHEN** `RevokedAt != null`
- **THEN** `IsActive(now)` 返回 `false`(与过期时间无关)

#### Scenario: 已过期
- **WHEN** `RevokedAt == null` 且 `ExpiresAt <= now`
- **THEN** `IsActive(now)` 返回 `false`

---

### Requirement: `Email` 与 `Username` 在系统中全局唯一

系统 SHALL 在持久化层强制 `Users` 表的 `Email` 字段唯一、`Username` 字段唯一(大小写不敏感)。注册流程在持久化之前 MUST 通过 `IUserRepository.EmailExistsAsync` / `UsernameExistsAsync` 预检,发现冲突时分别抛 `EmailAlreadyExistsException` / `UsernameAlreadyExistsException`,返回客户端 HTTP 409。

#### Scenario: 邮箱已存在
- **WHEN** 客户端用已注册邮箱再次注册
- **THEN** 系统 MUST 返回 HTTP 409,错误类型 `EmailAlreadyExistsException`

#### Scenario: 用户名已存在(大小写不敏感)
- **WHEN** 已存在 `"Alice"` 账号,客户端用 `"ALICE"` 或 `"alice"` 注册
- **THEN** 系统 MUST 返回 HTTP 409,错误类型 `UsernameAlreadyExistsException`

---

### Requirement: `IUserRepository` 只暴露领域概念的查询与新增接口

系统 SHALL 在 Application 层定义 `IUserRepository` 接口,方法签名只接受 / 返回领域类型(`UserId`、`Email`、`Username`、`User`),不得暴露 `IQueryable`、`Expression` 或 EF Core 实体。接口 MUST 包含:

- `Task<User?> FindByIdAsync(UserId id, CancellationToken ct)`
- `Task<User?> FindByEmailAsync(Email email, CancellationToken ct)`
- `Task<User?> FindByUsernameAsync(Username username, CancellationToken ct)`
- `Task<User?> FindByRefreshTokenHashAsync(string tokenHash, CancellationToken ct)`
- `Task<bool> EmailExistsAsync(Email email, CancellationToken ct)`
- `Task<bool> UsernameExistsAsync(Username username, CancellationToken ct)`
- `Task AddAsync(User user, CancellationToken ct)`

所有"按 refresh token 查找"场景 MUST 返回聚合根 `User`,而不是单独的 `RefreshToken`,以遵守"只通过聚合根修改"的 DDD 约束。

#### Scenario: 接口纯净性
- **WHEN** 审阅 `IUserRepository.cs`
- **THEN** 签名中 MUST NOT 出现 `IQueryable`、`Expression<Func<...>>`、`DbSet<...>`、EF Core 或 Microsoft.EntityFrameworkCore 相关类型

---

### Requirement: `GET /api/users/me` 返回当前登录用户的 `UserDto`

Api 层 SHALL 暴露 `GET /api/users/me`,仅接受持合法 Access Token 的请求。Controller 从 JWT 的 `sub` claim 解出 `UserId`,发起 `GetCurrentUserQuery(UserId)`,返回形状为 `UserDto` 的 JSON,字段:`Id: Guid`、`Email: string`、`Username: string`、`Rating: int`、`GamesPlayed: int`、`Wins: int`、`Losses: int`、`Draws: int`、`CreatedAt: DateTime`。**不得**返回 `PasswordHash` 或 `RefreshTokens`。

#### Scenario: 成功
- **WHEN** 客户端以合法 Access Token 请求 `GET /api/users/me`
- **THEN** MUST 返回 HTTP 200 + 对应 `UserDto` JSON,**不**包含 `PasswordHash` 或 token 相关字段

#### Scenario: 缺失 / 非法 token
- **WHEN** 请求不带 `Authorization` 头或 token 验证失败
- **THEN** MUST 返回 HTTP 401(由 JWT Bearer 中间件处理,不进入 handler)

#### Scenario: JWT 合法但用户已被删除
- **WHEN** token 合法但 `UserId` 在库里找不到
- **THEN** MUST 返回 HTTP 404,错误类型 `UserNotFoundException`

#### Scenario: 用户被禁用
- **WHEN** token 合法但 `IsActive == false`
- **THEN** MUST 返回 HTTP 403,错误类型 `UserNotActiveException`

---

### Requirement: 新增领域异常 `InvalidEmailException` / `InvalidUsernameException`

系统 SHALL 在 `Gomoku.Domain.Exceptions` 命名空间下新增 `InvalidEmailException` 与 `InvalidUsernameException`,均继承 `System.Exception`,提供 `(string message)` 与 `(string message, Exception innerException)` 两个构造函数。异常消息 MUST 清晰指出违反的具体规则(例如 "length" / "character set" / "all digits"),以便日志定位与前端展示。

#### Scenario: 类型存在
- **WHEN** 审阅 `Gomoku.Domain/Exceptions/`
- **THEN** MUST 存在 `InvalidEmailException.cs` 与 `InvalidUsernameException.cs`,两类型均继承 `Exception`

#### Scenario: 异常消息可读
- **WHEN** 以非法格式触发两类异常
- **THEN** 异常消息 MUST 指出违反的是哪条规则,避免仅有 "Invalid value" 之类模糊文本

### Requirement: `User` 新增 `IsBot` 只读字段

`User` 聚合根 MUST 新增 `IsBot: bool` 只读属性(`get; private set;`),表达"该账号是系统机器人,不可登录,不上排行榜"。现有用户(真人)默认 `IsBot = false`;通过 `User.Register` 创建的用户 MUST 保持 `IsBot = false`。

数据库层 MUST 为 `Users.IsBot` 列设置 `NOT NULL DEFAULT 0`,以便老行在 migration 后自动为真人。

#### Scenario: `User.Register` 的产物是真人
- **WHEN** 用 `User.Register(...)` 注册新用户
- **THEN** `user.IsBot == false`

#### Scenario: 字段对外只读
- **WHEN** 外部尝试直接 `user.IsBot = true`
- **THEN** 编译失败;修改 MUST 通过领域方法

---

### Requirement: `User.RegisterBot` 工厂创建机器人账号

系统 SHALL 提供 `User.RegisterBot(UserId id, Email email, Username username, DateTime createdAt)` 静态工厂。返回的 `User` 实例:

- `PasswordHash = "__BOT_NO_LOGIN__"`(常量;Domain 层暴露为 `User.BotPasswordHashMarker` 静态只读字段供 Infrastructure migration 与测试引用)
- `Rating = 1200`、所有战绩计数器 0
- `IsActive = true`
- **`IsBot = true`**
- `CreatedAt = createdAt`

调用方 MUST NOT 在 bot 账号上调用 `User.IssueRefreshToken`。领域不显式阻止(签名不随 IsBot 而变),但**约定**:bot 没有刷新令牌。

#### Scenario: Bot 字段正确
- **WHEN** 调 `User.RegisterBot(id, email, username, now)`
- **THEN** 返回的 `User` 满足:`IsBot == true`、`PasswordHash == User.BotPasswordHashMarker`、`IsActive == true`、`Rating == 1200`

#### Scenario: `PasswordHash` 标记常量
- **WHEN** 读取 `User.BotPasswordHashMarker`
- **THEN** 值为 `"__BOT_NO_LOGIN__"`(这一常量用来让 migration seed 与"bot 不可登录"检查对得上)

---

### Requirement: `IUserRepository` 新增两个 AI 支持查询

Application 层 SHALL 在 `IUserRepository` 追加两个方法(已在 `ai-opponent` spec 定义其契约,这里将其登记为 `user-management` 的仓储能力扩展):

```
Task<User?> FindBotByDifficultyAsync(BotDifficulty difficulty, CancellationToken cancellationToken);
Task<IReadOnlyList<RoomId>> GetRoomsNeedingBotMoveAsync(CancellationToken cancellationToken);
```

签名 MUST 不出现 `IQueryable` / `Expression<>` / EF Core 类型。

#### Scenario: 签名纯净
- **WHEN** 审阅 `IUserRepository.cs`
- **THEN** 新增签名中出现的类型只有 `BotDifficulty` / `RoomId` / `User` / `CancellationToken` / `Task<>` / `IReadOnlyList<>`

### Requirement: `User.RowVersion` 乐观并发令牌保护战绩写入

`User` 聚合根 MUST 定义 `byte[] RowVersion` 只读属性。语义:

- 字段类型:`byte[]`,长度 16 字节(底层用 `Guid.NewGuid().ToByteArray()` 产生);
- 构造时自带非空值;
- 以下**写入 User 父行业务属性**的方法 MUST 调 `TouchRowVersion()`:
  - `RecordGameResult(outcome, newRating)` —— ELO / 战绩字段更新(由 `add-elo-system` + `add-concurrency-hardening` 引入);
  - **`ChangePassword(newPasswordHash)`** —— 密码更新(本次 `add-change-password` 引入)。
- `IssueRefreshToken` / `RevokeRefreshToken` / `RevokeAllRefreshTokens` MUST NOT 调 `TouchRowVersion()` —— refresh token 路径只操作子集合,不改 User 父行业务属性;并发场景(并发登录、并发登出)本身无冲突,加保护反而把登录 / 登出流程不必要地串行化。

数据库层 MUST 把 `Users.RowVersion` 列设为 `NOT NULL` 的 blob,`UserConfiguration` MUST 为该属性调 `.IsConcurrencyToken().IsRequired()`。

规则总结:**凡改 User 业务属性(Rating / GamesPlayed / Wins / Losses / Draws / PasswordHash)的路径调 `TouchRowVersion`;凡只改子集合(RefreshTokens)的不调。**

#### Scenario: 字段默认非空
- **WHEN** 调 `User.Register(...)` 或 `User.RegisterBot(...)`
- **THEN** 返回的 `User.RowVersion` 不为 `null`,长度为 16

#### Scenario: 两次 Register 得到不同 RowVersion
- **WHEN** 两次独立调用 `User.Register(...)`
- **THEN** 两个 User 的 RowVersion 字节数组 MUST 不相等

#### Scenario: RecordGameResult 改变 RowVersion
- **WHEN** 对同一 User 调 `RecordGameResult(GameOutcome.Win, 1220)`
- **THEN** `RowVersion` 与调用前**不相等**

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

两个事务并发加载同一 `User` 聚合后,**都**调用了 `RecordGameResult` 并尝试 `SaveChangesAsync`:第一个事务成功(数据库 RowVersion 更新为 V2),第二个事务的 UPDATE 语句因 `WHERE RowVersion = V1` 命中 0 行 MUST 抛 `DbUpdateConcurrencyException`。

上层(handler / worker / HTTP 客户端)MUST 决定重试策略;Api 层 MUST 通过全局异常中间件把该异常映射为 HTTP 409 + `ProblemDetails` 响应(沿用 `add-rooms-and-gameplay` 已建立的映射,不新增条目)。

#### Scenario: 并发 ELO 更新
- **WHEN** 两个 `ResignCommand` handler 对同一 Alice 几乎同时完成事务
- **THEN** 一者 SaveChanges 成功(数据库 Alice.Rating / GamesPlayed / Losses 按对应路径更新一次);另一者 SaveChanges 抛 `DbUpdateConcurrencyException`,**不会**默默覆盖第一者的更新

#### Scenario: HTTP 409 响应
- **WHEN** 客户端发出 `POST /api/rooms/{id}/resign`,恰好与 `TurnTimeoutWorker` 对同一用户另一房间的判负并发到达
- **THEN** 若自己输给 409,响应为 HTTP 409 + `ProblemDetails.title == "Concurrent modification."`,客户端应重拉 `GET /api/users/me` / `GET /api/rooms/{id}` 再决定是否重试

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

#### Scenario: 反射检查无敏感字段
- **WHEN** 审阅 `UserPublicProfileDto` 的 public properties
- **THEN** 属性集合精确为 `{Id, Username, Rating, GamesPlayed, Wins, Losses, Draws, CreatedAt}`

---

### Requirement: `GET /api/users/{id}` 按 Id 返回公开用户主页

Api 层 SHALL 暴露 `GET /api/users/{id:guid}`(`[Authorize]`):

- Controller 调 `GetUserProfileQuery(new UserId(id))`;
- Handler Load user;null 抛 `UserNotFoundException` → HTTP 404;
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

---

### Requirement: `GET /api/users?search=&page=&pageSize=` 按用户名前缀搜索真人

Api 层 SHALL 暴露 `GET /api/users`(`[Authorize]`),接受 query:

- `search: string?` —— 可选;非空时按 Username **前缀**(大小写不敏感)过滤;空则返回所有真人。
- `page: int`(默认 1,`≥ 1`)
- `pageSize: int`(默认 20,`[1, 100]`)

Validator `SearchUsersQueryValidator`:`Page ≥ 1`、`PageSize ∈ [1, 100]`、`Search` 非空时 `Length ≤ 20`(与 `Username` 最大长度对齐);非法 HTTP 400。

Handler 调 `IUserRepository.SearchByUsernamePagedAsync(Search, Page, PageSize, ct)`,映射 `UserPublicProfileDto`,包 `PagedResult` 返回。

仓储实现 MUST:
- `Where(u => !u.IsBot)` —— bot **不**出现在搜索结果;
- 若 `prefix` 非空 → `Username LIKE prefix%`(case-insensitive,SQLite 靠 NOCASE collation;EF 翻译 `ToLower().StartsWith`);
- `OrderBy(Username ASC)`;
- `CountAsync` + `Skip((page-1)*pageSize).Take(pageSize)`;
- 返回 `(IReadOnlyList<User>, int Total)` tuple。

#### Scenario: 前缀匹配
- **WHEN** 数据库有 Alice / AliceB / Bob / Carol + 3 bot;调 `GET /api/users?search=Ali`
- **THEN** HTTP 200;`Items` 含 Alice + AliceB(Username ASC);**不**含 Bob / Carol / bot;`Total == 2`

#### Scenario: 大小写不敏感
- **WHEN** `search=ALI`
- **THEN** 同上(仍匹配 Alice / AliceB)

#### Scenario: 空 search 返回所有真人
- **WHEN** `GET /api/users`(不带 search)
- **THEN** HTTP 200;Items 含所有真人按 Username ASC;bot 不在

#### Scenario: 分页
- **WHEN** 5 个真人匹配某前缀,`page=2&pageSize=2`
- **THEN** Items.Count == 2(第 3、4 个);Total == 5

#### Scenario: 非法参数
- **WHEN** `pageSize=101` 或 `page=0` 或 `search=超过 20 字符的字符串...`
- **THEN** HTTP 400 `ValidationException`

#### Scenario: 未登录
- **WHEN** 无 Bearer token
- **THEN** HTTP 401

---

### Requirement: `IUserRepository.SearchByUsernamePagedAsync` 分页 + 前缀 + bot 过滤

Application 层 SHALL 在 `IUserRepository` 上新增:

```
Task<(IReadOnlyList<User> Users, int Total)> SearchByUsernamePagedAsync(
    string? prefix, int page, int pageSize, CancellationToken cancellationToken);
```

实现 MUST:
1. 过滤 `!IsBot`(搜索不应出现 bot)。
2. 若 `prefix` 非空,按 Username 大小写不敏感的**前缀匹配**过滤。
3. 按 `Username ASC` 排序。
4. `CountAsync` → Total;`Skip((page-1)*pageSize).Take(pageSize)` → Users 物化。
5. 返回 `(Users, Total)` tuple。

签名 MUST 不暴露 `IQueryable` 等 EF 类型。

#### Scenario: Bot 过滤
- **WHEN** 库里有 3 真人(含 Alice)+ 3 bot(AI_Easy/Medium/Hard),调 `SearchByUsernamePagedAsync(null, 1, 100, ct)`
- **THEN** Users.Count == 3(仅真人);Total == 3

#### Scenario: 前缀 + 分页
- **WHEN** 库里有 5 个 "Al" 前缀真人,调 `SearchByUsernamePagedAsync("Al", 2, 2, ct)`
- **THEN** Users.Count == 2(第 3、4 个);Total == 5

### Requirement: `User.ChangePassword` 替换密码哈希并推进并发令牌

系统 SHALL 在 `User` 聚合根上新增 `ChangePassword(string newPasswordHash)` 方法。规则:

- `newPasswordHash` 为 `null` / 空 / 全空白 MUST 抛 `ArgumentException`;
- `IsBot == true` 时 MUST 抛 `InvalidOperationException("Bot accounts cannot change password.")` —— bot 账号由 migration seed 写入 `__BOT_NO_LOGIN__` marker,不应被改密。
- 校验通过:`PasswordHash = newPasswordHash`;
- 方法末尾 MUST 调 `TouchRowVersion()` —— `PasswordHash` 是 User 父行的业务属性,并发改密应被 EF 乐观并发捕获(与 `RecordGameResult` 同一 RowVersion 纪律)。

调用方(handler)MUST 先验证当前密码、自己调 `IPasswordHasher.Hash(newPassword)` 产出 hash,再调本方法 —— Domain 不做密码字符串校验(validator 层负责复杂度)。

#### Scenario: 成功改密
- **WHEN** 对真人 User 调 `ChangePassword("newhashedvalue")`
- **THEN** `PasswordHash == "newhashedvalue"`;`RowVersion` 与调用前不等

#### Scenario: 空 hash 拒绝
- **WHEN** 调 `ChangePassword(null)` 或 `ChangePassword("")` 或 `ChangePassword("   ")`
- **THEN** 抛 `ArgumentException`;`PasswordHash` / `RowVersion` 保持不变

#### Scenario: Bot 拒绝
- **WHEN** 对 `RegisterBot` 创建的 User 调 `ChangePassword("any")`
- **THEN** 抛 `InvalidOperationException`,消息含 "Bot accounts cannot change password";`PasswordHash` 仍为 `User.BotPasswordHashMarker`

#### Scenario: 连续多次改密
- **WHEN** 同一 User 调 `ChangePassword("h1")` → `ChangePassword("h2")` → `ChangePassword("h3")`
- **THEN** 每次 `RowVersion` 推进;3 次调用后三个 RowVersion 两两不等

