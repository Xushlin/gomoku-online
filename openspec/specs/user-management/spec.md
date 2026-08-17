# User Management

## Purpose

用户身份与账号生命周期能力:`UserId` / `Email` / `Username` 值对象的不变量,`User` 聚合根(含战绩字段 `Rating` / `GamesPlayed` / `Wins` / `Losses` / `Draws` 与启用状态 `IsActive`),`RefreshToken` 子实体,以及按 Id / Email / Username 查、按 token hash 追溯聚合、邮箱与用户名全局唯一这组规则。本能力**不**负责登录流程的凭据校验与 token 签发 —— 那些归 `authentication` 能力。

实现位于 `backend/src/Gewu.Domain/Users/`(Domain)与 `backend/src/Gewu.Infrastructure/Persistence/`(持久化适配)。
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

系统 SHALL 在 `Gewu.Domain.Exceptions` 命名空间下新增 `InvalidEmailException` 与 `InvalidUsernameException`,均继承 `System.Exception`,提供 `(string message)` 与 `(string message, Exception innerException)` 两个构造函数。异常消息 MUST 清晰指出违反的具体规则(例如 "length" / "character set" / "all digits"),以便日志定位与前端展示。

#### Scenario: 类型存在
- **WHEN** 审阅 `Gewu.Domain/Exceptions/`
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
工作,留给 `add-web-per-game-rating`;**`add-per-game-rating` 的代价是资料页此刻只能看到一个
棋种** —— 记为缺口,不是遗漏。

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
- **THEN** 返回她的五子棋战绩,数字与 `add-per-game-rating` 之前完全一致

#### Scenario: 没下过该棋种
- **WHEN** `GET /api/users/{aliceGuid}?gameKey=xiangqi` 而 Alice 从未下过象棋
- **THEN** HTTP 200,`Rating == 1200`、战绩全 0 —— MUST NOT 404

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

> `RecordGameResult` 现在住在 `UserGameStats` 上,其规范见 `elo-rating` —— 该 Requirement 由
> `add-elo-system` 归在那个能力里,`add-per-game-rating` 也在那里修订它,不在本 capability 重复。

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

### Requirement: 只读路径用 `FindGameStatsAsync` / `FindGameStatsForAsync`,MUST NOT 建行

Application 层 SHALL 在 `IUserRepository` 上再提供两个**只读**查询:

```
Task<UserGameStats?> FindGameStatsAsync(
    UserId userId, string gameKey, CancellationToken cancellationToken);

Task<IReadOnlyDictionary<Guid, UserGameStats>> FindGameStatsForAsync(
    IEnumerable<UserId> userIds, string gameKey, CancellationToken cancellationToken);
```

查询路径(资料页 / 搜索 / `/me` / 登录响应)MUST 用它们,MUST NOT 用
`GetOrCreateGameStatsAsync` —— 后者会**建行**,而"有行"就是"下完过这个棋种"、排行榜的成员资格
正是靠它。一次 GET 请求把人凭空登记进某个棋种的榜,会把"下过"的含义变成"被人看过资料"。

没有行时返回 `null`(单个)或不进字典(批量);调用方用初始值填 DTO。

`FindGameStatsForAsync` 存在的理由是一页搜索结果 20 个人 —— 逐个查就是 20 次往返。

#### Scenario: 读资料不建行
- **WHEN** 反复 `GET /api/users/{id}?gameKey=xiangqi`,而该用户从未下过象棋
- **THEN** 每次都返回初始值,且 `UserGameStats` 表中 MUST NOT 因此出现任何新行

#### Scenario: 批量查只取指定棋种
- **WHEN** 传入三个 id 查 `xiangqi`,其中只有一人有该棋种的行
- **THEN** 字典只含那一人;另外两人不进字典,MUST NOT 以初始值占位

### Requirement: `UserDto` 与登录 / 注册 / 刷新响应的战绩钉在五子棋

`UserDto`(`/api/users/me` 与 `AuthResponse.User`)的形状 MUST 一个字节不变,其战绩四项与 `Rating`
SHALL 取自该用户 `gomoku` 那一行的 `UserGameStats`。

理由:这三个端点都没有 `gameKey` 参数,而已发布的客户端在这里读的就是五子棋的数字 ——
换成别的(比如某种跨棋种聚合)会是一次无声的回归。给 `UserDto` 加棋种维度要改 DTO 形状,
属于 `add-web-per-game-rating`。

注册响应 MUST 用初始值填(`Rating = 1200`、战绩全 0)且 MUST NOT 创建战绩行 ——
一个刚注册的用户在每个棋种上都还没下过。

#### Scenario: 注册响应
- **WHEN** `POST /api/auth/register` 成功
- **THEN** `user.rating == 1200`、`user.gamesPlayed == 0`;`UserGameStats` 表中 MUST NOT 出现属于他的行

#### Scenario: `/me` 缺省向后兼容
- **WHEN** 一位有五子棋战绩的用户调 `GET /api/users/me`
- **THEN** 返回的数字与 `add-per-game-rating` 之前完全一致

### Requirement: `SearchUsers` 的棋种钉在五子棋且不接受参数

`SearchUsersQuery` 的形状 MUST 不变(`Search` / `Page` / `PageSize`),handler SHALL 用
`FindGameStatsForAsync(..., "gomoku", ...)` 一次批量取这一页用户的战绩。

**不加 `gameKey` 参数**。此前给的理由是「找人卡片是五子棋大厅的一个组件」,那条理由自
`generalize-lobby` 起不再成立 —— 找人卡片在 `/home`,而 `/home` 是**平台主页**,不属于任何棋种。
结论没变,而且现在更直接:搜索的对象是**人**,不是某个棋种里的人。顺带显示的战绩固定取
`gomoku`,那是一个可以商榷的展示选择,不是本契约的一部分。

没有五子棋战绩行的用户 MUST 仍然出现在搜索结果里(显示初始值)—— 搜索的是"人",
不是"上过榜的人";找人卡片要能找到刚注册的人。这与排行榜的成员资格规则**刻意不同**。

#### Scenario: 刚注册的人能被搜到
- **WHEN** 搜索一位注册后从未下完过一局的用户
- **THEN** 他出现在结果里,`Rating == 1200`、战绩全 0

#### Scenario: 一页只查一次战绩
- **WHEN** 一页返回 20 位用户
- **THEN** handler MUST 只调一次 `FindGameStatsForAsync`,MUST NOT 逐个调 `FindGameStatsAsync`

### Requirement: 迁移把既有战绩归给五子棋,且顺序不可颠倒

迁移 SHALL 按此顺序完成三步,分两条 EF migration(expand / contract):

1. `AddUserGameStats`(expand)——建 `UserGameStats` 表(复合主键 + 索引 `(GameKey, Rating DESC)` 供排行榜使用)。
2. `AddUserGameStats`(同一条)——用**显式 SQL** 把每个 `Users` 行的五个战绩列搬成一行 `UserGameStats`,`GameKey = 'gomoku'`。
3. `DropUserRatingColumns`(contract,时间戳更晚)——从 `Users` 删除那五列。

**拆成两条是有意的。** 提案原文要求三步"在同一个 migration 内",那多余:顺序的保证来自迁移
时间戳,而不是来自它们挤在同一个文件里。拆开之后 expand 那一半是**可逆的**(`Down` 只丢表,
战绩仍在 `Users` 原处),而且它是纯增量、可以在读者还没切过来时先落地、单独被审。

顺序 MUST NOT 颠倒 —— 先删列再搬数据就把数据搬没了,而 EF 的自动生成不知道这几步有依赖关系。
本仓库在同一处被咬过一次:`AddRoomGameKey` 那次 EF 生成了 `defaultValue: ""`,会让每个既有房间的
`GameKey` 变成空串、进而解析不出规则、房间全部不可玩;那次是手工改成 `'gomoku'` 加一条显式
`UPDATE` 才对的。将来若有人压缩迁移,这个先后同样 MUST NOT 颠倒。

`DropUserRatingColumns` 的 `Down` MUST 显式把数据搬回来。EF 自动生成的版本只
`AddColumn(defaultValue: 0)`,回滚之后每个人的分会变成 0 —— 与上面那次 `defaultValue: ""` 是同一类
bug,而回滚这条路平时没人走,坏了不会有人立刻发现。

两条 migration MUST 各有针对"迁移前形状"的库跑一遍的测试,断言战绩一行不丢、数值一分不差。
断言 MUST 在 expand 之后、contract 之前取一次快照 —— 删完列源数据就不在了,再断言只是在跟自己核对。
本地 SQLite 没有生产数据是事实,但迁移是本仓库**唯一会在别人机器上按原样跑一遍**的东西。

#### Scenario: 既有战绩不丢
- **WHEN** 在一个含若干 `Users` 行(各有非零战绩)的迁移前库上跑迁移
- **THEN** 每个用户得到一行 `GameKey == 'gomoku'` 的 `UserGameStats`,五个数值与迁移前逐一相等

#### Scenario: expand 之后源列还在
- **WHEN** 只迁到 `AddUserGameStats` 为止
- **THEN** `Users` 上那五列 MUST 仍然存在 —— 这一半是可逆的,回滚只需丢掉新表

#### Scenario: 列已删除
- **WHEN** 迁到最新后检视 `Users` 表结构
- **THEN** MUST NOT 再有 `Rating` / `GamesPlayed` / `Wins` / `Losses` / `Draws` 列

#### Scenario: 回滚 contract 恢复真实数值而不是零
- **WHEN** 迁到最新后再回滚到 `AddUserGameStats`
- **THEN** `Users` 的五列 MUST 恢复成 `UserGameStats` 里 `gomoku` 那行的数值,MUST NOT 全是缺省值

#### Scenario: bot 账号同样被搬迁
- **WHEN** 迁移前库中含三个 seeded bot 账号
- **THEN** 它们各自也得到 `gomoku` 那行 —— bot 对局计分(反套利约束),所以它们的战绩同样是真数据

