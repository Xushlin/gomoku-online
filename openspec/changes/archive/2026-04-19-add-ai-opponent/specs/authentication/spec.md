## MODIFIED Requirements

### Requirement: `POST /api/auth/login` 校验凭据并签发 token,错误信息模糊

Api 层 SHALL 暴露 `POST /api/auth/login`,接收 JSON `{ email, password }`。成功路径:查找 `User` by `Email` → `PasswordHasher.Verify` → 校验 `IsActive` → **校验 `IsBot == false`**(本次新增)→ 签发新 refresh token 加入 `User.RefreshTokens` → 签发 access token → `SaveChangesAsync` → 返回 HTTP 200 + `AuthResponse`。

失败路径 MUST 不泄漏"邮箱是否存在"。无论"邮箱不存在"、"密码不对"**或"命中的账号是 bot"**都 MUST 抛同一个 `InvalidCredentialsException`,消息统一为 `"Email or password is incorrect."`,HTTP 401。仅当邮箱 / 密码均正确但 `IsActive == false` 时,抛 `UserNotActiveException`,HTTP 403。

**理由**:`User.RegisterBot` 会把 `PasswordHash` 置为常量 `"__BOT_NO_LOGIN__"`,Identity `PasswordHasher.Verify` 永远返回 `Failed`,流程自然不会走到"成功签发 token"分支。此显式检查是**深度防御** —— 防止任何未来对 Hash 的改动或 seed 数据污染意外让 bot 账号可登录;同时保证失败响应的**时延**与"密码错"场景一致(否则攻击者可能通过"比其他 401 返回得慢"来嗅探 bot 邮箱是否存在)。

#### Scenario: 成功
- **WHEN** 邮箱与密码均正确且 `IsActive == true` 且 `IsBot == false`
- **THEN** MUST 返回 HTTP 200 + `AuthResponse`,数据库新增一枚 `RefreshToken`

#### Scenario: 邮箱不存在
- **WHEN** 提交的邮箱从未注册
- **THEN** MUST 返回 HTTP 401,消息与"密码错误"场景一致

#### Scenario: 密码错误
- **WHEN** 邮箱存在但密码不匹配
- **THEN** MUST 返回 HTTP 401,消息与"邮箱不存在"场景一致

#### Scenario: 用户被禁用
- **WHEN** 凭据正确但 `IsActive == false`
- **THEN** MUST 返回 HTTP 403,错误类型 `UserNotActiveException`

#### Scenario: 命中 bot 账号
- **WHEN** 提交的邮箱精确匹配某 bot 账号(例如 `easy@bot.gomoku.local`),密码任意
- **THEN** MUST 返回 HTTP 401,`InvalidCredentialsException`,响应体与消息与"邮箱不存在"场景**完全一致**,不泄漏该邮箱对应的是 bot

---

### Requirement: `POST /api/auth/refresh` 用 refresh token 换一对新 token

Api 层 SHALL 暴露 `POST /api/auth/refresh`,接收 JSON `{ refreshToken }`(不要求 `Authorization` 头 —— refresh token 本身就是凭据)。成功路径见"Refresh Token 轮换"要求。返回体形状 MUST 与 `/api/auth/register` / `/api/auth/login` 一致(`AuthResponse`)。

**追加约束**(本次新增):加载到的 `User` 若 `IsBot == true`,MUST 抛 `InvalidRefreshTokenException`(HTTP 401)。由于 `User.RegisterBot` 不调 `IssueRefreshToken`,bot 聚合的 `RefreshTokens` 集合为空,正常情况下 token hash 查找不会命中 bot;此检查是防御性兜底,以免未来有代码误在 bot 聚合上发放 refresh token。

#### Scenario: 成功
- **WHEN** 传入合法 refresh token(属于真人用户)
- **THEN** MUST 返回 HTTP 200 + 新 `AuthResponse`

#### Scenario: 非法 / 过期 / 已撤销
- **WHEN** refresh token 不合法
- **THEN** MUST 返回 HTTP 401,错误类型 `InvalidRefreshTokenException`

#### Scenario: 请求体缺失字段
- **WHEN** body 里缺少 `refreshToken` 或为空字符串
- **THEN** MUST 返回 HTTP 400,由 Validator 产出的错误

#### Scenario: token hash 意外匹配到 bot 聚合
- **WHEN** 某 refresh token hash 查出的 `User.IsBot == true`(未来回归防御)
- **THEN** MUST 返回 HTTP 401,`InvalidRefreshTokenException`,不签发新 token
