## ADDED Requirements

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

系统 SHALL 定义 `User` 作为聚合根,字段包含:`Id: UserId`、`Email: Email`、`Username: Username`、`PasswordHash: string`、`Rating: int`、`GamesPlayed: int`、`Wins: int`、`Losses: int`、`Draws: int`、`IsActive: bool`、`CreatedAt: DateTime`、以及一个**只读**的 `RefreshTokens: IReadOnlyCollection<RefreshToken>`。外部 MUST NOT 直接修改这些字段;所有变更仅通过 `User` 提供的领域方法发生。

#### Scenario: 字段可读
- **WHEN** 访问 `User` 的任意上述属性
- **THEN** MUST 返回相应的类型与当前值

#### Scenario: `RefreshTokens` 只读
- **WHEN** 外部尝试把 `User.RefreshTokens` 强转为 `List<RefreshToken>` 并调用 `Add`
- **THEN** 该修改 MUST NOT 影响 `User` 内部状态(即内部集合与暴露的只读视图相互隔离,或暴露的就是 `IReadOnlyCollection<RefreshToken>` 本身)

---

### Requirement: `User.Register` 静态工厂方法创建新用户并设定初始状态

系统 SHALL 提供 `User.Register(UserId id, Email email, Username username, string passwordHash, DateTime createdAt)` 静态工厂方法。返回的 `User` 实例 MUST 具有:

- `Rating = 1200`
- `GamesPlayed = 0`,`Wins = 0`,`Losses = 0`,`Draws = 0`
- `IsActive = true`
- `CreatedAt = createdAt`(由调用方通过 `IDateTimeProvider` 提供,不得取 `DateTime.UtcNow`)
- `RefreshTokens` 为空集合
- `Id` / `Email` / `Username` / `PasswordHash` 等于入参

`passwordHash` 为 `null` 或空字符串时,MUST 抛出 `ArgumentException`。

#### Scenario: 初始值正确
- **WHEN** 以合法入参调用 `User.Register(...)`
- **THEN** 返回的 `User` 的每个字段 MUST 等于上述初始值

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
