## ADDED Requirements

### Requirement: `POST /api/auth/change-password` 允许登录用户修改密码

Api 层 SHALL 暴露 `POST /api/auth/change-password`(`[Authorize]`),接收 JSON `{ currentPassword, newPassword }`。成功路径:

1. 从 JWT `sub` claim 取 `UserId`;
2. 调 `ChangePasswordCommand(userId, currentPassword, newPassword)`;
3. Handler:
   - Load user(null → `UserNotFoundException` → 404,实际登录用户应存在,此为防御);
   - `IPasswordHasher.Verify(currentPassword, user.PasswordHash)` **失败**时抛 `InvalidCredentialsException` → HTTP 401(与 login 的"密码错"完全同形响应);
   - `IPasswordHasher.Hash(newPassword)` → `user.ChangePassword(newHash)`;
   - `user.RevokeAllRefreshTokens(_clock.UtcNow)` —— **所有**已发 refresh token 失效,其它设备 / 其它浏览器 session 立即失效;
   - `SaveChangesAsync`;
4. 成功 HTTP 204 No Content(无 body)。

Validator:
- `currentPassword` 非空;
- `newPassword` 复用 `RegisterCommandValidator` 的密码规则:≥ 8 字符、至少一个字母、至少一个数字;
- 任一失败 HTTP 400 `ValidationException`。

安全纪律:
- 即使 JWT 有效也**必须**验证当前密码(防 session 被劫后改密)。
- 改密成功后**吊销全部** refresh token(防旧 token 持续有效)。
- Bot 账号的改密由 Domain 层防御拒绝(见 `user-management` 能力的 `User.ChangePassword` 修订)。

#### Scenario: 成功改密
- **WHEN** 登录用户 Alice(密码 `pa55w0rd!`)调 `POST /api/auth/change-password { "pa55w0rd!", "newP@ss123" }`
- **THEN** HTTP 204 No Content;`Alice.PasswordHash` 已更新;`Alice.RefreshTokens` 中所有未撤销的 token MUST 均被标记 `RevokedAt = now`

#### Scenario: 当前密码错
- **WHEN** `currentPassword` 与 `Alice.PasswordHash` 不匹配
- **THEN** HTTP 401 `InvalidCredentialsException`;MUST NOT 修改数据库任何字段

#### Scenario: 新密码不满足复杂度
- **WHEN** `newPassword = "abc123"`(7 字符)或 `"12345678"`(无字母)或 `"abcdefgh"`(无数字)
- **THEN** HTTP 400 `ValidationException`(errors 指向 `NewPassword` 字段)

#### Scenario: 未登录
- **WHEN** 不带 Bearer token 请求
- **THEN** HTTP 401(JWT 中间件)

#### Scenario: 改密后旧 refresh token 立即失效
- **WHEN** Alice 改密前拿到 `refreshA`;改密成功后用 `refreshA` 调 `POST /api/auth/refresh`
- **THEN** HTTP 401 `InvalidRefreshTokenException`(因 token 已在 RevokeAllRefreshTokens 中被吊销)

#### Scenario: 改密后旧密码登录失败
- **WHEN** 改密后用 `currentPassword("pa55w0rd!")` 调 `POST /api/auth/login`
- **THEN** HTTP 401 `InvalidCredentialsException`;必须用新密码才能登录

#### Scenario: 新旧密码相同
- **WHEN** `currentPassword == newPassword`(两者皆合法且匹配)
- **THEN** HTTP 204 —— 视为合法操作;副作用仍包含"吊销全部 refresh token"(等价于"登出所有其它设备")
