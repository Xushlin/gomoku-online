## ADDED Requirements

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

## MODIFIED Requirements

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

### Requirement: `User` 聚合根承载身份、战绩、启用状态与注册时间

系统 SHALL 定义 `User` 作为聚合根,字段包含:`Id: UserId`、`Email: Email`、`Username: Username`、`PasswordHash: string`、`Rating: int`、`GamesPlayed: int`、`Wins: int`、`Losses: int`、`Draws: int`、`IsActive: bool`、**`IsBot: bool`**、`CreatedAt: DateTime`、以及一个**只读**的 `RefreshTokens: IReadOnlyCollection<RefreshToken>`。外部 MUST NOT 直接修改这些字段;所有变更仅通过 `User` 提供的领域方法发生。

#### Scenario: 字段可读
- **WHEN** 访问 `User` 的任意上述属性(包括新增的 `IsBot`)
- **THEN** MUST 返回相应的类型与当前值

#### Scenario: `RefreshTokens` 只读
- **WHEN** 外部尝试把 `User.RefreshTokens` 强转为 `List<RefreshToken>` 并调用 `Add`
- **THEN** 该修改 MUST NOT 影响 `User` 内部状态(即内部集合与暴露的只读视图相互隔离,或暴露的就是 `IReadOnlyCollection<RefreshToken>` 本身)
