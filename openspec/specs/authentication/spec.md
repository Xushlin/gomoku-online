# Authentication

## Purpose

如何证明我是这个用户。涵盖密码哈希(`PasswordHasher<User>` V3 格式)、JWT Access Token(HS256,15 分钟)的签发与校验、Refresh Token 的高熵生成与 SHA-256 哈希入库、基于轮换(rotation)的 7 天刷新流程、幂等登出、FluentValidation 管道、全局异常到 HTTP 状态码的映射表、以及 `IDateTimeProvider` 作为系统唯一现在来源。

HTTP 端点:`POST /api/auth/{register,login,refresh,logout}`。实现位于 `backend/src/Gomoku.Infrastructure/Authentication/` 与 `backend/src/Gomoku.Api/`。

## Requirements


### Requirement: 密码以 ASP.NET Identity `PasswordHasher` 的 V3 格式哈希,不落盘明文

系统 SHALL 在 Infrastructure 层用 `Microsoft.AspNetCore.Identity.PasswordHasher<Gomoku.Domain.Entities.User>` 实现 `IPasswordHasher` 接口,接口契约仅包含 `string Hash(string plainPassword)` 与 `bool Verify(string plainPassword, string hashed)`。明文密码 MUST NOT 写入数据库或任何持久化形态;日志 / 异常消息中也 MUST NOT 出现明文密码。

#### Scenario: 哈希成功后可验证
- **WHEN** 对 `"Password1"` 调 `Hash`,再用 `Verify("Password1", result)`
- **THEN** 返回 `true`

#### Scenario: 错误密码验证失败
- **WHEN** 对 `"Password1"` 哈希后用 `Verify("wrong", hashed)`
- **THEN** 返回 `false`

#### Scenario: 相同明文两次哈希结果不同
- **WHEN** 对同一明文连续调两次 `Hash`
- **THEN** 两次返回的 hash MUST 不相等(Identity `PasswordHasher` 每次产生不同 salt)

---

### Requirement: 密码规则要求至少 8 位、含字母与数字

系统 SHALL 在 `RegisterCommandValidator` 中对明文密码强制:

- 长度 ≥ 8;
- 至少包含一个字母(`a-z` 或 `A-Z`);
- 至少包含一个数字(`0-9`)。

不强制大小写区分或特殊字符。违反规则 MUST 通过 `ValidationBehavior` 转为 `ValidationException`,客户端收到 HTTP 400 + `ProblemDetails`,`errors["Password"]` 包含具体说明。

#### Scenario: 合法密码
- **WHEN** 密码为 `"Password1"`、`"abc12345"`、`"Zzz99999"`
- **THEN** Validator MUST 通过

#### Scenario: 长度不足
- **WHEN** 密码长度 < 8
- **THEN** Validator MUST 失败,`errors["Password"]` 指出长度问题

#### Scenario: 缺字母
- **WHEN** 密码为纯数字(如 `"12345678"`)
- **THEN** Validator MUST 失败,`errors["Password"]` 指出缺字母

#### Scenario: 缺数字
- **WHEN** 密码为纯字母(如 `"password"`)
- **THEN** Validator MUST 失败,`errors["Password"]` 指出缺数字

---

### Requirement: Access Token 为 HS256 JWT,包含 `sub` 和 `preferred_username`,15 分钟过期

系统 SHALL 用 `System.IdentityModel.Tokens.Jwt` 签发 Access Token,算法 `HS256`,claims 至少包含:
- `sub` = `UserId` 的 `Guid` 字符串形式
- `preferred_username` = `Username` 的原始字符串
- `jti` = 每次签发唯一的 `Guid`
- `iat` / `exp` 标准字段

`iss`(Issuer)MUST = `"gomoku-online"`,`aud`(Audience)MUST = `"gomoku-online-clients"`。过期时间 MUST = 签发时刻 + 15 分钟。签名密钥长度 MUST ≥ 32 字节。

Api 层 JWT Bearer 中间件 MUST 同时校验 `Issuer`、`Audience`、`Lifetime`、`SigningKey`;`ClockSkew` MUST = 30 秒。

#### Scenario: Token 可被自身 Bearer 中间件接受
- **WHEN** 对一个新注册用户签发 Access Token,随后以该 token 请求 `GET /api/users/me`
- **THEN** 请求 MUST 成功(HTTP 200)

#### Scenario: 篡改 token 被拒
- **WHEN** 以修改过 payload / signature 的 token 请求任何受保护端点
- **THEN** MUST 返回 HTTP 401

#### Scenario: 过期 token 被拒
- **WHEN** token 的 `exp` 小于当前时间减 30 秒
- **THEN** MUST 返回 HTTP 401

#### Scenario: claims 完整性
- **WHEN** 解析已签发的 Access Token
- **THEN** MUST 能读到 `sub`、`preferred_username`、`jti`、`iat`、`exp` 五个 claim

---

### Requirement: Refresh Token 为高熵随机字符串,**仅以 SHA-256 哈希存库**

系统 SHALL 在 `IJwtTokenService` 中提供 `string GenerateRefreshToken()`,返回 ≥ 256 bit 熵的 base64url 字符串。`string HashRefreshToken(string rawToken)` MUST 返回 `SHA-256(rawToken)` 的十六进制或 base64 表达。数据库 `RefreshTokens.TokenHash` 列 MUST 仅存 hash,**MUST NOT** 存原始 token。明文 token 仅出现在 HTTP 响应体中(只此一次,客户端自存)。

#### Scenario: 熵充足
- **WHEN** 连续生成 1000 个 refresh token
- **THEN** MUST 两两互不相同(概率上,256 bit 空间下无碰撞)

#### Scenario: 哈希稳定
- **WHEN** 对同一原始 token 多次调用 `HashRefreshToken`
- **THEN** MUST 返回同一字符串

#### Scenario: 库内无明文
- **WHEN** 审阅 `RefreshTokenConfiguration` 与 EF 生成的表结构
- **THEN** `RefreshTokens` 表 MUST NOT 有存储明文 token 的列

---

### Requirement: Refresh Token 7 天过期,每次刷新执行**轮换 + 旧 token 吊销**

系统 SHALL 让 Refresh Token 在签发后 7 天过期。`POST /api/auth/refresh` 成功时 MUST 执行:
1. 把请求中传入的 refresh token 对应的子实体 `RevokedAt = now`;
2. 生成一枚**新**的 refresh token,存入同一 `User` 的 `RefreshTokens`;
3. 签发一枚**新**的 Access Token;
4. 在响应中返回这对新 token。

若传入的 refresh token 已被吊销 / 已过期 / 不存在,MUST 抛 `InvalidRefreshTokenException`,客户端收到 HTTP 401。

#### Scenario: 正常轮换
- **WHEN** 以尚未过期且未吊销的 refresh token 请求刷新
- **THEN** 响应中 MUST 返回一对新 token;旧 token 的 `RevokedAt` MUST 被设为 `now`

#### Scenario: 旧 token 无法再用
- **WHEN** 用刚刷新过的**旧** refresh token 再次请求 `/api/auth/refresh`
- **THEN** MUST 返回 HTTP 401

#### Scenario: 已过期
- **WHEN** 用 `ExpiresAt < now` 的 refresh token 请求
- **THEN** MUST 返回 HTTP 401

#### Scenario: 不存在
- **WHEN** 用服务端从未签发过的随机字符串请求
- **THEN** MUST 返回 HTTP 401

---

### Requirement: `POST /api/auth/register` 创建用户、即刻登录、返回 `AuthResponse`

Api 层 SHALL 暴露 `POST /api/auth/register`,接收 JSON `{ email, username, password }`。成功时 MUST:
- 以 `Email` / `Username` / `PasswordHasher` 构造 `User` 聚合,调用 `User.Register`;
- 签发一对 Access + Refresh Token,refresh token 的 hash 存入 `User.RefreshTokens`;
- 单次 `SaveChangesAsync` 持久化;
- 返回 HTTP 201 + `AuthResponse { accessToken, refreshToken, accessTokenExpiresAt, user }`;`user` 是不含敏感字段的 `UserDto`。

Register MUST 同时满足 `user-management` 能力中的字段校验与唯一性检查。

#### Scenario: 成功
- **WHEN** 客户端以合法入参注册
- **THEN** MUST 返回 HTTP 201 + `AuthResponse`,数据库新增一条 `User` 与一枚 `RefreshToken`

#### Scenario: 邮箱重复
- **WHEN** 邮箱已存在
- **THEN** MUST 返回 HTTP 409,错误类型 `EmailAlreadyExistsException`,MUST NOT 创建任何行

#### Scenario: 用户名重复(大小写不敏感)
- **WHEN** 用户名已存在(比较忽略大小写)
- **THEN** MUST 返回 HTTP 409,错误类型 `UsernameAlreadyExistsException`

#### Scenario: 入参校验失败
- **WHEN** 邮箱 / 用户名 / 密码中任一字段不合法
- **THEN** MUST 返回 HTTP 400 + `ProblemDetails`,`errors` 字典列出各字段的失败原因

---

### Requirement: `POST /api/auth/login` 校验凭据并签发 token,错误信息模糊

Api 层 SHALL 暴露 `POST /api/auth/login`,接收 JSON `{ email, password }`。成功路径:查找 `User` by `Email` → `PasswordHasher.Verify` → 校验 `IsActive` → 签发新 refresh token 加入 `User.RefreshTokens` → 签发 access token → `SaveChangesAsync` → 返回 HTTP 200 + `AuthResponse`。

失败路径 MUST 不泄漏"邮箱是否存在"。无论"邮箱不存在"或"密码不对"都 MUST 抛同一个 `InvalidCredentialsException`,消息统一为 `"Email or password is incorrect."`,HTTP 401。仅当邮箱 / 密码均正确但 `IsActive == false` 时,抛 `UserNotActiveException`,HTTP 403。

#### Scenario: 成功
- **WHEN** 邮箱与密码均正确且 `IsActive == true`
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

---

### Requirement: `POST /api/auth/refresh` 用 refresh token 换一对新 token

Api 层 SHALL 暴露 `POST /api/auth/refresh`,接收 JSON `{ refreshToken }`(不要求 `Authorization` 头 —— refresh token 本身就是凭据)。成功路径见上文"Refresh Token 轮换"要求。返回体形状 MUST 与 `/api/auth/register` / `/api/auth/login` 一致(`AuthResponse`)。

#### Scenario: 成功
- **WHEN** 传入合法 refresh token
- **THEN** MUST 返回 HTTP 200 + 新 `AuthResponse`

#### Scenario: 非法 / 过期 / 已撤销
- **WHEN** refresh token 不合法
- **THEN** MUST 返回 HTTP 401,错误类型 `InvalidRefreshTokenException`

#### Scenario: 请求体缺失字段
- **WHEN** body 里缺少 `refreshToken` 或为空字符串
- **THEN** MUST 返回 HTTP 400,由 Validator 产出的错误

---

### Requirement: `POST /api/auth/logout` 幂等地吊销当前 refresh token

Api 层 SHALL 暴露 `POST /api/auth/logout`,接收 JSON `{ refreshToken }`。成功路径:hash 该 token,在对应 `User` 聚合上调用 `RevokeRefreshToken(hash, now)`,`SaveChangesAsync`,返回 HTTP 204 No Content。

若 token 不存在 / 已过期 / 已撤销,MUST 同样返回 HTTP 204(**幂等**),MUST NOT 返回 401/404。Handler 内部 MUST NOT 打印包含原始 token 的日志。

#### Scenario: 吊销成功
- **WHEN** 传入合法 refresh token
- **THEN** MUST 返回 HTTP 204;该 token 的 `RevokedAt` MUST 被写为 `now`

#### Scenario: token 不存在(幂等)
- **WHEN** 传入一个服务端从未签发过的字符串
- **THEN** MUST 返回 HTTP 204,不抛异常

#### Scenario: token 已撤销
- **WHEN** 传入已 `RevokedAt != null` 的 token
- **THEN** MUST 返回 HTTP 204,且该 token 的 `RevokedAt` 时间戳 MUST NOT 被覆盖

---

### Requirement: FluentValidation 通过 `ValidationBehavior` 自动触发

系统 SHALL 在 Application 层实现 `ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>`,在 handler 执行**之前**,用 DI 容器中已注册的所有 `IValidator<TRequest>` 对入参校验,任一 validator 失败即 MUST 抛 `ValidationException`(自定义 Application 异常,内含 `IDictionary<string, string[]> Errors`)。Handler 层 MUST NOT 手动调用 validator。

#### Scenario: 非法入参被拦截
- **WHEN** 对某 Command 存在对应 `AbstractValidator<TCommand>` 且入参违反规则
- **THEN** Handler MUST NOT 被调用;MUST 抛 `ValidationException`

#### Scenario: 无 validator 的 Command 直通
- **WHEN** 某 Command 没有对应 validator
- **THEN** Behavior MUST 直接交给 handler 处理

#### Scenario: 多个 validator
- **WHEN** 同一 Command 被注册了多个 validator
- **THEN** 所有 validator MUST 参与校验,失败信息合并后抛出

---

### Requirement: 全局异常中间件按异常类型映射 HTTP 状态码并返回 `ProblemDetails`

Api 层 SHALL 实现一个异常处理中间件,在 pipeline 顶端包住所有请求。异常 → HTTP 映射表(严格遵守):

| 异常 | HTTP 状态 |
| --- | --- |
| `ValidationException` | 400 |
| `InvalidEmailException` / `InvalidUsernameException` / `InvalidMoveException` | 400 |
| `InvalidCredentialsException` / `InvalidRefreshTokenException` | 401 |
| `UserNotActiveException` | 403 |
| `UserNotFoundException` | 404 |
| `EmailAlreadyExistsException` / `UsernameAlreadyExistsException` | 409 |
| 其他 `Exception` | 500,响应体的 `detail` 字段 MUST 为静态文案 `"An unexpected error occurred."`,原异常细节 MUST NOT 外泄 |

所有响应体 MUST 是 RFC 7807 `ProblemDetails` 形态:`type` / `title` / `status` / `detail`;`ValidationException` 额外携带 `errors: { "field": ["msg", ...] }`。中间件 MUST 对 5xx 使用 Serilog `LogError`,4xx 使用 `LogInformation`(不含栈)。

#### Scenario: 映射正确
- **WHEN** handler 抛出上表任一异常
- **THEN** 响应的 HTTP 状态码 MUST 与表一致;响应体 MUST 为 `ProblemDetails` 形状

#### Scenario: 5xx 不泄漏内部细节
- **WHEN** handler 抛出表外异常(例如 `NullReferenceException`)
- **THEN** MUST 返回 HTTP 500,`detail` MUST 为 `"An unexpected error occurred."`,响应体 MUST NOT 包含栈信息或消息

#### Scenario: ValidationException 携带字段级错误
- **WHEN** Validator 失败抛 `ValidationException`
- **THEN** 响应 JSON MUST 有 `errors` 对象,键为字段名,值为字符串数组

---

### Requirement: `IDateTimeProvider` 作为全系统唯一"现在"来源

Application 层 SHALL 定义 `IDateTimeProvider { DateTime UtcNow { get; } }`;Infrastructure MUST 提供 `SystemDateTimeProvider` 实现 `DateTime.UtcNow`。所有 Handler / 领域方法调用处 MUST 通过该抽象获取当前时间,MUST NOT 在 Application / Domain 层直接读 `DateTime.UtcNow`。

#### Scenario: 代码扫描
- **WHEN** 在 `Gomoku.Application/` 与 `Gomoku.Domain/` 目录下搜索 `DateTime.UtcNow`
- **THEN** MUST 零匹配(Handler 的 `CreatedAt` / token 过期时间均来自 `IDateTimeProvider`)

#### Scenario: 测试中可注入固定时间
- **WHEN** 单元测试 mock `IDateTimeProvider.UtcNow` 返回固定值
- **THEN** 所有依赖"当前时间"的 handler 行为 MUST 基于该固定值判断(例如测试"7 天过期 + 1 秒"路径)

---

### Requirement: Application / Infrastructure 层通过 `DependencyInjection` 扩展方法暴露 DI 注册

系统 SHALL 提供:
- `Gomoku.Application.DependencyInjection.AddApplication(IServiceCollection)` —— 注册 MediatR、FluentValidation 的 validator assembly、`ValidationBehavior`;
- `Gomoku.Infrastructure.DependencyInjection.AddInfrastructure(IServiceCollection, IConfiguration)` —— 注册 `GomokuDbContext`(SQLite)、`IUserRepository` / `IUnitOfWork` 实现、`IPasswordHasher` 实现、`IJwtTokenService` 实现、`IDateTimeProvider` 实现,并绑定 `JwtOptions`。

`Program.cs` MUST 只调用这两个扩展方法完成 Application / Infrastructure 的接线,MUST NOT 直接 `AddScoped<IUserRepository, ...>`。

#### Scenario: DI 装配自洽
- **WHEN** `Program.cs` 只调用 `builder.Services.AddApplication().AddInfrastructure(builder.Configuration)` + JWT Bearer 配置
- **THEN** 应用 MUST 能完整启动;每个 handler 的依赖均能解析,且不依赖具体 Infrastructure 类型

#### Scenario: 未来替换 Infrastructure 实现
- **WHEN** 新 Infrastructure 变更要把 `SqliteUserRepository` 换成 `SqlServerUserRepository`
- **THEN** 修改范围 MUST 局限在 `Gomoku.Infrastructure/DependencyInjection.cs` 与新实现类内部,Application / Api 层不需要改动
