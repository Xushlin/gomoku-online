## 1. Domain — `User.ChangePassword`

- [x] 1.1 `Gomoku.Domain/Users/User.cs` 新方法 `public void ChangePassword(string newPasswordHash)`:
  - `string.IsNullOrWhiteSpace(newPasswordHash)` → `ArgumentException`
  - `IsBot == true` → `InvalidOperationException("Bot accounts cannot change password.")`
  - `PasswordHash = newPasswordHash;`
  - `TouchRowVersion();`
- [x] 1.2 XML 注释指出:bot 拒绝 / 要 RowVersion 推进 / 外部调用方必须先验证当前密码。

## 2. Domain 测试

- [x] 2.1 `Gomoku.Domain.Tests/Users/UserChangePasswordTests.cs`:
  - 成功改密 → `PasswordHash == newHash`;`RowVersion` 变化。
  - `null` / 空 / 空白 `newPasswordHash` → `ArgumentException`,字段不变。
  - Bot(`RegisterBot`)调 ChangePassword → `InvalidOperationException`;字段不变。
  - 连续两次 ChangePassword → 3 个不同 RowVersion(包括初始)。

## 3. Application — ChangePassword feature

- [x] 3.1 `Features/Auth/ChangePassword/ChangePasswordCommand.cs`:
  `record ChangePasswordCommand(UserId UserId, string CurrentPassword, string NewPassword) : IRequest<Unit>`。
- [x] 3.2 `Features/Auth/ChangePassword/ChangePasswordCommandValidator.cs`:
  - `CurrentPassword` NotEmpty;
  - `NewPassword` NotEmpty + MinimumLength(8) + Matches("[A-Za-z]") + Matches("[0-9]") —— 与 RegisterCommandValidator 一致。
- [x] 3.3 `Features/Auth/ChangePassword/ChangePasswordCommandHandler.cs`:
  - 依赖 `IUserRepository` / `IPasswordHasher` / `IUnitOfWork` / `IDateTimeProvider`;
  - Load user(null → `UserNotFoundException`);
  - `_hasher.Verify(CurrentPassword, user.PasswordHash)` false → `InvalidCredentialsException`(复用 login 的映射)。
  - `_hasher.Hash(NewPassword)` → `user.ChangePassword(newHash)`;
  - `user.RevokeAllRefreshTokens(_clock.UtcNow)`;
  - `await _uow.SaveChangesAsync(ct)`;
  - 返回 `Unit.Value`。
  - Domain 抛出的 `InvalidOperationException`(bot)直接冒泡;全局中间件需映射(见下)。

## 4. Api

- [x] 4.1 `AuthController` 新 action:
  ```csharp
  [HttpPost("change-password")]
  [Authorize]
  public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest body, CancellationToken ct)
  {
      var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
          ?? User.FindFirst("sub")?.Value
          ?? throw new UnauthorizedAccessException("Missing sub claim.");
      var userId = new UserId(Guid.Parse(sub));
      await _mediator.Send(new ChangePasswordCommand(userId, body.CurrentPassword, body.NewPassword), ct);
      return NoContent();
  }
  ```
- [x] 4.2 `AuthController.cs` 文件末尾或同文件加 `public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);`。
- [x] 4.3 `ExceptionHandlingMiddleware`:把 `InvalidOperationException` 的"Bot accounts cannot change password"映射 —— 这不是正常用户错误(bot 本就不该能登录),属于防御路径。**策略**:不特判,让它走 500(现有默认)。理由:bot 登录已经被 login handler 拒;真到这里说明路径异常,500 是合理告警。**或者**:引入专用应用异常 `BotOperationNotAllowedException` → 403。选后者更干净。

  → 实际选:不加应用异常,让 Domain 抛的 `InvalidOperationException` 走 500 — bot 改密这条路径攻击面可忽略(bot 不可能持有 valid JWT)。**若**将来发现有 false positive 再重构。

- [x] 4.4 验证:`InvalidCredentialsException` 已有 401 映射(来自 login),无需新增。

## 5. Application 测试

- [x] 5.1 `Features/Auth/ChangePasswordCommandHandlerTests.cs`(~4):
  - 成功:mock Verify true,mock Hash → "newhash";断言 `ChangePassword` 被调、`RevokeAllRefreshTokens` 被调、`SaveChangesAsync` 一次。
  - 当前密码错:`Verify` 返回 false → InvalidCredentialsException;SaveChangesAsync 未调。
  - 用户不存在 → UserNotFoundException。
  - Bot 改密:Mock User 为 bot 对象 → Domain `ChangePassword` 抛 `InvalidOperationException`,透传。
- [x] 5.2 `Features/Auth/ChangePasswordCommandValidatorTests.cs`(~5):
  - valid 组合通过。
  - CurrentPassword 空 → 失败。
  - NewPassword 空 → 失败。
  - NewPassword 长 7 → 失败。
  - NewPassword 只字母无数字 → 失败。
  - NewPassword 只数字无字母 → 失败。

## 6. 端到端冒烟

- [x] 6.1 启动 Api,注册 Alice;拿 ALICE_TOKEN + ALICE_REFRESH。
- [x] 6.2 `POST /api/auth/change-password { currentPassword:"pa55w0rd!", newPassword:"newP@ss123" }` → 204。
- [x] 6.3 用旧密码 login → 401。用新密码 login → 200 + 新 access + refresh tokens。
- [x] 6.4 用**初始 ALICE_REFRESH**(改密前拿的)调 `/api/auth/refresh` → 401(RevokeAll 生效)。
- [x] 6.5 wrong currentPassword 调 change-password → 401。
- [x] 6.6 newPassword 长 7 字符 → 400。
- [x] 6.7 无 JWT 调 → 401。

## 7. 归档前置检查

- [x] 7.1 `dotnet build Gomoku.slnx`:0 警告 0 错。
- [x] 7.2 `dotnet test Gomoku.slnx`:全绿(Domain +4、Application +9)。
- [x] 7.3 `openspec validate add-change-password --strict`:valid。
- [x] 7.4 分支 `feat/add-change-password`,按层 Domain / Application / Api / docs 四条 commit。
