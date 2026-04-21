## ADDED Requirements

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

## MODIFIED Requirements

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
