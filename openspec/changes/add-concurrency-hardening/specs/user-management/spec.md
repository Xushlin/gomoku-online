## ADDED Requirements

### Requirement: `User.RowVersion` 乐观并发令牌保护战绩写入

`User` 聚合根 MUST 新增 `byte[] RowVersion` 只读属性。语义:

- 字段类型:`byte[]`,长度 16 字节(底层用 `Guid.NewGuid().ToByteArray()` 产生);
- 构造时自带非空值;
- `User.RecordGameResult(outcome, newRating)` 的末尾 MUST 调 `TouchRowVersion()`(私有方法)产生新值;
- `IssueRefreshToken` / `RevokeRefreshToken` / `RevokeAllRefreshTokens` MUST NOT 调 `TouchRowVersion()` —— refresh token 路径操作子集合,不改 User 父行业务属性,并发场景(并发登录、登出)本身无冲突,加保护反而把登录流程不必要地串行化。

数据库层 MUST 把 `Users.RowVersion` 列设为 `NOT NULL` 的 blob,`UserConfiguration` MUST 为该属性调 `.IsConcurrencyToken().IsRequired()`。这样 EF 在 UPDATE User 行时自动在 WHERE 子句里加 `RowVersion = @oldRowVersion`,若另一事务抢先把 RowVersion 换掉,本次 SaveChanges 命中 0 行 → 抛 `DbUpdateConcurrencyException`。

#### Scenario: 字段默认非空
- **WHEN** 调 `User.Register(...)` 或 `User.RegisterBot(...)`
- **THEN** 返回的 `User.RowVersion` 不为 `null`,长度为 16

#### Scenario: 两次 Register 得到不同 RowVersion
- **WHEN** 两次独立调用 `User.Register(...)`
- **THEN** 两个 User 的 RowVersion 字节数组 MUST 不相等(Guid 两次生成概率上不碰撞)

#### Scenario: RecordGameResult 改变 RowVersion
- **WHEN** 对同一 User 调 `RecordGameResult(GameOutcome.Win, 1220)`
- **THEN** `RowVersion` 与调用前**不相等**

#### Scenario: 多次 RecordGameResult 每次都变
- **WHEN** 连续对同一 User 调 RecordGameResult 三次(Win / Loss / Draw)
- **THEN** 每次调用后 RowVersion 都更新;三次之间两两不相等

#### Scenario: 刷新令牌路径不改 RowVersion
- **WHEN** 对同一 User 调 `IssueRefreshToken` / `RevokeRefreshToken` / `RevokeAllRefreshTokens` 中的任一个
- **THEN** `RowVersion` 保持不变 —— 这些方法只操作子集合,不参与 User 并发保护

---

### Requirement: 并发 `RecordGameResult` 冲突时后写者抛 `DbUpdateConcurrencyException`

两个事务并发加载同一 `User` 聚合后,**都**调用了 `RecordGameResult` 并尝试 `SaveChangesAsync`:第一个事务成功(数据库 RowVersion 更新为 V2),第二个事务的 UPDATE 语句因 `WHERE RowVersion = V1` 命中 0 行 MUST 抛 `DbUpdateConcurrencyException`。

上层(handler / worker / HTTP 客户端)MUST 决定重试策略;Api 层 MUST 通过全局异常中间件把该异常映射为 HTTP 409 + `ProblemDetails` 响应(沿用 `add-rooms-and-gameplay` 已建立的映射,不新增条目)。

#### Scenario: 并发 ELO 更新
- **WHEN** 两个 `ResignCommand` handler 对同一 Alice 几乎同时完成事务
- **THEN** 一者 SaveChanges 成功(数据库 Alice.Rating / GamesPlayed / Losses 按对应路径更新一次);另一者 SaveChanges 抛 `DbUpdateConcurrencyException`,**不会**默默覆盖第一者的更新

#### Scenario: HTTP 409 响应
- **WHEN** 客户端发出 `POST /api/rooms/{id}/resign`,恰好与 `TurnTimeoutWorker` 对同一用户另一房间的判负并发到达
- **THEN** 若自己输给 409,响应为 HTTP 409 + `ProblemDetails.title == "Concurrent modification."`,客户端应重拉 `GET /api/users/me` / `GET /api/rooms/{id}` 再决定是否重试

## MODIFIED Requirements

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

---

### Requirement: `User.RecordGameResult(GameOutcome, int newRating)` 原子更新战绩与 Rating

系统 SHALL 在 `User` 聚合根上提供 `RecordGameResult(GameOutcome outcome, int newRating)` 方法。调用后 MUST 原子完成:

- `GamesPlayed = GamesPlayed + 1`
- 根据 `outcome`:若 `Win` 则 `Wins++`,若 `Loss` 则 `Losses++`,若 `Draw` 则 `Draws++`
- `Rating = newRating`
- **`RowVersion` 通过 `TouchRowVersion()` 替换为新 16 字节值**(本次新增;保证乐观并发令牌推进)

`outcome` 传入未定义的枚举值时 MUST 抛 `ArgumentOutOfRangeException`,抛出时 User 状态 MUST 保持不变(包括 RowVersion)。

调用后 MUST 保持不变量:`Wins + Losses + Draws == GamesPlayed`。

#### Scenario: 胜场更新
- **WHEN** 新用户(`GamesPlayed=0, Wins=0, Rating=1200`)调用 `RecordGameResult(GameOutcome.Win, 1216)`
- **THEN** `GamesPlayed=1`,`Wins=1`,`Losses=0`,`Draws=0`,`Rating=1216`,`RowVersion` 不同于调用前

#### Scenario: 负场更新
- **WHEN** 新用户调用 `RecordGameResult(GameOutcome.Loss, 1184)`
- **THEN** `GamesPlayed=1`,`Losses=1`,`Rating=1184`,`RowVersion` 更新

#### Scenario: 平局更新
- **WHEN** 新用户调用 `RecordGameResult(GameOutcome.Draw, 1200)`
- **THEN** `GamesPlayed=1`,`Draws=1`,`Rating=1200`,`RowVersion` 更新

#### Scenario: 多局累积
- **WHEN** 同一用户连续调用 `RecordGameResult(Win, 1216) → RecordGameResult(Loss, 1200) → RecordGameResult(Draw, 1200)`
- **THEN** `GamesPlayed=3`,`Wins=1`,`Losses=1`,`Draws=1`,`Rating=1200`,且 `Wins+Losses+Draws == GamesPlayed`;三次调用间 RowVersion 两两不等

#### Scenario: 非法枚举值
- **WHEN** 传入 `(GameOutcome)99` 或其他非定义值
- **THEN** 抛 `ArgumentOutOfRangeException`;`User` 状态 MUST 保持不变,包括 `RowVersion`
