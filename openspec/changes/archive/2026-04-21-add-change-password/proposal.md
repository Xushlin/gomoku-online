## Why

`add-user-authentication` 装了注册 / 登录 / 刷新 / 登出,但没装**改密**。前端的"个人设置 → 修改密码"功能需要一个端点。补齐基础账号管理的最后一块。

安全纪律三条:
1. 要**当前密码**作凭据验证(避免 session 被 XSS 劫持后改密 → 接管账号)。
2. 改密后**吊销全部 refresh token**(其它设备 / 其它浏览器的 session 立即失效)。
3. bot 账号禁止改密(bot 不可登录,不可改密;防御)。

## What Changes

- **Domain**:
  - `User.ChangePassword(string newPasswordHash)` 新方法:
    - bot 抛 `InvalidOperationException`("Bot accounts cannot change password.");
    - `newPasswordHash` null / 空 抛 `ArgumentException`;
    - 替换 `PasswordHash`;
    - **末尾调 `TouchRowVersion()`** —— PasswordHash 是 User 父行的业务属性,并发改密应被 EF 乐观并发捕获,与 `RecordGameResult` 同一 RowVersion 纪律(修订 `user-management` 的 RowVersion 调用点约定)。
- **Application**:
  - 新 feature `Features/Auth/ChangePassword/`:
    - `ChangePasswordCommand(UserId UserId, string CurrentPassword, string NewPassword) : IRequest<Unit>`;
    - Validator:`CurrentPassword` 非空;`NewPassword` 复用 `RegisterCommandValidator` 的三条规则(最短 8 字符、至少含一个字母、至少含一个数字)。
    - Handler:Load user(null → `UserNotFoundException`)→ `PasswordHasher.Verify(currentPwd, user.PasswordHash)` 失败抛 `InvalidCredentialsException`(401,和 login 的错误形一致)→ `user.ChangePassword(_hasher.Hash(newPwd))` → `user.RevokeAllRefreshTokens(_clock.UtcNow)` → SaveChanges → 返回 `Unit.Value`。
    - **新旧密码相同**时:validator 层不拦(过度限制 UX),handler 层也不拦 —— 行为等价于"改成一样的";`RevokeAllRefreshTokens` 仍生效(用户主动"登出所有设备"的副作用)。
- **Api**:
  - `AuthController` 新 action:`POST /api/auth/change-password`(`[Authorize]`)body `{ currentPassword, newPassword }` → 204 No Content。
  - 错误:`CurrentPassword` 不对 → 401(`InvalidCredentialsException`,复用映射);非法密码格式 → 400(validator);未登录 → 401(JWT 中间件)。
- **Tests**:
  - Domain `UserChangePasswordTests`(~5):成功改密 / bot 抛异常 / 空 hash 抛异常 / RowVersion 变化 / 连续两次都 Touch 成 3 个不同 RowVersion。
  - Application `ChangePasswordCommandHandlerTests`(~4):成功(Verify 调用 + ChangePassword 调用 + RevokeAll 调用 + SaveChanges 一次)/ 当前密码错 → InvalidCredentialsException / 用户不存在 → UserNotFoundException / bot 用户试改 → 透传 Domain 的 InvalidOperationException。
  - Application `ChangePasswordCommandValidatorTests`(~5):CurrentPassword 空 / NewPassword 空 / NewPassword 短于 8 / 无数字 / 无字母 → 失败;valid 通过。

**显式不做**(留给后续):
- 改密后发邮件通知:需要邮件基础设施,归 `add-email-notifications`。
- 密码历史(最近 5 次不得重复):增复杂度,不在 MVP。
- Forgot password / reset flow(通过邮件 token 重置):需要邮件 + 独立路径,归 `add-password-reset`。
- 2FA / TOTP:独立能力,归 `add-two-factor-auth`。
- 强密码打分(zxcvbn):前端职责。

## Capabilities

### Modified Capabilities

- **`authentication`** — 新 `POST /api/auth/change-password` 端点 + `ChangePasswordCommand` feature;改密要求验证当前密码、吊销全部 refresh token、对 bot 账号拒绝。
- **`user-management`** — 扩展 `User.ChangePassword` 方法;更新 `RowVersion` 的调用点纪律(从"仅 RecordGameResult"扩展到"RecordGameResult + ChangePassword")。

### New Capabilities

(无)

## Impact

- **代码规模**:~8 新 / 修改文件。
- **NuGet**:零。
- **HTTP 表面**:+1 端点。
- **SignalR**:零。
- **数据库**:零 schema;改密时一次 UPDATE Users(PasswordHash + RowVersion)+ 多次 UPDATE RefreshTokens(RevokedAt)。
- **运行时**:PasswordHasher.Verify ~100ms(PBKDF2 设计如此);改密后用户其它设备需要重新登录。
- **后续变更依赖**:前端"个人设置 → 修改密码"表单;`add-two-factor-auth`(复用 ChangePassword 的"要当前密码"模式);`add-password-reset`(独立路径)。
