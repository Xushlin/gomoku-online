## 1. Domain 层 — 异常与值对象

- [x] 1.1 在 `backend/src/Gomoku.Domain/Exceptions/` 新增 `InvalidEmailException.cs`,继承 `Exception`,带 `(string)` 与 `(string, Exception)` 两个构造函数 + XML 注释
- [x] 1.2 同目录新增 `InvalidUsernameException.cs`(同上)
- [x] 1.3 创建子目录 `backend/src/Gomoku.Domain/Users/`(可选;若不建,用 `Entities/` 与 `ValueObjects/` 根目录也可)。以下 User 相关文件都放这里,保持聚合内聚
- [x] 1.4 `ValueObjects/UserId.cs`:`public readonly record struct UserId(Guid Value)`,XML 注释说明"封装 Guid 提供类型安全"
- [x] 1.5 `ValueObjects/Email.cs`:`sealed record Email`,构造函数用 `System.Net.Mail.MailAddress` 校验 + 长度 ≤ 254 + 非空;规范化为小写;非法抛 `InvalidEmailException`
- [x] 1.6 `ValueObjects/Username.cs`:`sealed record Username`,构造函数校验长度 `[3..20]` + 正则 `^[a-zA-Z0-9_\u4e00-\u9fff]+$` + `!value.All(char.IsDigit)`;非法抛 `InvalidUsernameException`;相等比较用 `StringComparer.OrdinalIgnoreCase`,`GetHashCode` 用小写版本
- [x] 1.7 `Entities/RefreshToken.cs`:字段 `Id:Guid`、`UserId:UserId`、`TokenHash:string`、`ExpiresAt:DateTime`、`CreatedAt:DateTime`、`RevokedAt:DateTime?`。提供 `IsActive(DateTime now)`。构造函数为 `internal`(仅聚合根能创建),EF Core 通过 `#pragma warning disable` 或 `InternalsVisibleTo` 访问
- [x] 1.8 `Entities/User.cs` 骨架:字段 + 私有 List<RefreshToken>;`RefreshTokens` 属性返回 `IReadOnlyCollection<RefreshToken>`;所有 setter 私有
- [x] 1.9 `User.Register(UserId, Email, Username, string passwordHash, DateTime createdAt)` 静态工厂:设置 `Rating=1200`、战绩字段=0、`IsActive=true`、`CreatedAt=createdAt`;`passwordHash` 空抛 `ArgumentException`
- [x] 1.10 `User.IssueRefreshToken(string tokenHash, DateTime expiresAt, DateTime issuedAt)`:参数校验 + 创建 RefreshToken + 加入内部列表
- [x] 1.11 `User.RevokeRefreshToken(string tokenHash, DateTime revokedAt) : bool`:找到则设 RevokedAt 返回 true;找不到返回 false;已撤销不覆盖
- [x] 1.12 `User.RevokeAllRefreshTokens(DateTime revokedAt)`:遍历未撤销的,逐个 `Revoke`
- [x] 1.13 `RefreshToken.Revoke(DateTime revokedAt)` 内部方法:幂等(已撤销则 no-op)
- [x] 1.14 全部新增类型加 XML `<summary>`;检查 Domain 层**仍然**零 NuGet 依赖、零 async/Task/.Result

## 2. Domain 测试

- [x] 2.1 `Gomoku.Domain.Tests/Users/UserIdTests.cs`:构造 + 值相等 + `GetHashCode` 一致
- [x] 2.2 `Gomoku.Domain.Tests/Users/EmailTests.cs`:合法构造(大小写转小写)、非法格式抛异常(5+ 用例)、超长、`null`/空/空白、规范化相等
- [x] 2.3 `Gomoku.Domain.Tests/Users/UsernameTests.cs`:合法 5 例(含中文)、长度越界 2 例、字符集非法 3 例、全数字、`null`/空、大小写不敏感相等
- [x] 2.4 `Gomoku.Domain.Tests/Users/UserRegisterTests.cs`:`Register` 初始值完整断言;`passwordHash` 空抛异常
- [x] 2.5 `Gomoku.Domain.Tests/Users/UserRefreshTokenTests.cs`:`IssueRefreshToken` 添加成功 + 参数校验;`RevokeRefreshToken` 四种场景(成功 / hash 不存在 / 已撤销不覆盖 / 多枚并存只撤一枚);`RevokeAllRefreshTokens` 批量吊销 + 不覆盖已撤销
- [x] 2.6 `Gomoku.Domain.Tests/Users/RefreshTokenIsActiveTests.cs`:`IsActive` 三种路径(活跃 / 已吊销 / 已过期),边界时间 `ExpiresAt == now` 算过期
- [x] 2.7 运行 `dotnet test tests/Gomoku.Domain.Tests`,全部绿灯

## 3. Application 层 — 项目引用与 NuGet

- [x] 3.1 在 `Gomoku.Application.csproj` 加 `<ProjectReference>` 指向 `Gomoku.Domain`
- [x] 3.2 加 `PackageReference`:`MediatR`、`FluentValidation`、`FluentValidation.DependencyInjectionExtensions`、`Microsoft.Extensions.DependencyInjection.Abstractions`(全部用当前稳定版)
- [x] 3.3 删除 `Gomoku.Application/Class1.cs`

## 4. Application 层 — 抽象契约

- [x] 4.1 `Abstractions/IDateTimeProvider.cs`:只含 `DateTime UtcNow { get; }`
- [x] 4.2 `Abstractions/IUnitOfWork.cs`:只含 `Task<int> SaveChangesAsync(CancellationToken ct)`
- [x] 4.3 `Abstractions/IUserRepository.cs`:设计中 D12 列出的 7 个方法;不出现 EF 类型
- [x] 4.4 `Abstractions/IPasswordHasher.cs`:`string Hash(string plainPassword)` + `bool Verify(string plainPassword, string hashed)`
- [x] 4.5 `Abstractions/IJwtTokenService.cs`:`string GenerateAccessToken(User user, out DateTime expiresAtUtc)`(或返回一个小 record `AccessToken(string Token, DateTime ExpiresAt)`,便于单元测试)、`string GenerateRefreshToken()`、`string HashRefreshToken(string raw)`;并定义 `JwtOptions` record(Issuer / Audience / SigningKey / AccessTokenLifetimeMinutes / RefreshTokenLifetimeDays)
- [x] 4.6 所有接口带 XML 注释,签名使用领域类型

## 5. Application 层 — 公共基础设施

- [x] 5.1 `Common/Exceptions/ValidationException.cs`:`IDictionary<string, string[]> Errors` 字段;构造函数接收 FluentValidation 的 `IEnumerable<ValidationFailure>` 并聚合
- [x] 5.2 `Common/Exceptions/EmailAlreadyExistsException.cs`、`UsernameAlreadyExistsException.cs`、`InvalidCredentialsException.cs`、`InvalidRefreshTokenException.cs`、`UserNotFoundException.cs`、`UserNotActiveException.cs` —— 各继承 `Exception`,带 `(string)` 构造函数 + XML 注释
- [x] 5.3 `Common/Behaviors/ValidationBehavior.cs`:`IPipelineBehavior<TRequest, TResponse>`,构造函数注入 `IEnumerable<IValidator<TRequest>>`;无 validator → 直通;多 validator → 聚合所有 failures → 若非空抛 `ValidationException`
- [x] 5.4 `Common/DTOs/UserDto.cs`:`public record UserDto(Guid Id, string Email, string Username, int Rating, int GamesPlayed, int Wins, int Losses, int Draws, DateTime CreatedAt)`
- [x] 5.5 `Common/DTOs/AuthResponse.cs`:`public record AuthResponse(string AccessToken, string RefreshToken, DateTime AccessTokenExpiresAt, UserDto User)`
- [x] 5.6 `Common/Mapping/UserMapping.cs`(或扩展方法):`User → UserDto` 的静态转换,**不**暴露敏感字段

## 6. Application 层 — Auth Feature: Register

- [x] 6.1 `Features/Auth/Register/RegisterCommand.cs`:`public record RegisterCommand(string Email, string Username, string Password) : IRequest<AuthResponse>`
- [x] 6.2 `Features/Auth/Register/RegisterCommandValidator.cs`:`AbstractValidator<RegisterCommand>`,字段规则 —— Email 非空、最大 254;Username 非空、长度 3–20;Password 长度 ≥ 8、含字母、含数字(用 `Matches` + 两条规则;**不**重复做域级正则,由 Email/Username 值对象再次兜底)
- [x] 6.3 `Features/Auth/Register/RegisterCommandHandler.cs`:
  - 注入 `IUserRepository` / `IPasswordHasher` / `IJwtTokenService` / `IDateTimeProvider` / `IUnitOfWork` / `IOptions<JwtOptions>`
  - 构造 `Email` / `Username` 值对象(会校验)
  - 预检唯一性 → 冲突抛 `*AlreadyExistsException`
  - `Hash` 密码 → `User.Register` → 生成 refresh token + hash → `user.IssueRefreshToken(hash, expiresAt, now)` → `repo.AddAsync` → `uow.SaveChangesAsync` → 生成 access token → 返回 `AuthResponse`(含原始 refresh token,不是 hash)

## 7. Application 层 — Auth Feature: Login

- [x] 7.1 `Features/Auth/Login/LoginCommand.cs`:`(string Email, string Password)`
- [x] 7.2 `Features/Auth/Login/LoginCommandValidator.cs`:Email 非空,Password 非空(**不**在 login 侧复用密码复杂度规则,避免把"我之前注册时的密码现在不合规"卡住老用户)
- [x] 7.3 `Features/Auth/Login/LoginCommandHandler.cs`:
  - 构造 `Email` 值对象
  - `FindByEmailAsync` → 用户不存在抛 `InvalidCredentialsException`
  - `Hasher.Verify` 失败也抛同一异常(消息完全一致)
  - `!IsActive` 抛 `UserNotActiveException`
  - 生成 refresh + hash → `IssueRefreshToken` → `SaveChanges` → 生成 access token → 返回

## 8. Application 层 — Auth Feature: RefreshToken / Logout

- [x] 8.1 `Features/Auth/RefreshToken/RefreshTokenCommand.cs`:`(string RefreshToken)`
- [x] 8.2 `Features/Auth/RefreshToken/RefreshTokenCommandValidator.cs`:`RefreshToken` 非空
- [x] 8.3 `Features/Auth/RefreshToken/RefreshTokenCommandHandler.cs`:
  - `HashRefreshToken(raw)` → `FindByRefreshTokenHashAsync` → 找不到抛 `InvalidRefreshTokenException`
  - 在聚合内找到对应 `RefreshToken`,`!IsActive(now)` → 抛同样异常
  - `user.RevokeRefreshToken(oldHash, now)` → 生成新 refresh + hash → `IssueRefreshToken` → `SaveChanges` → 新 access token → `AuthResponse`
- [x] 8.4 `Features/Auth/Logout/LogoutCommand.cs`:`(string RefreshToken) : IRequest<Unit>`
- [x] 8.5 `Features/Auth/Logout/LogoutCommandHandler.cs`:
  - hash token → 查 user → 找到就 `RevokeRefreshToken(hash, now)` + `SaveChanges`
  - 任何"找不到 / 已撤销"场景**静默成功**,返回 `Unit.Value`
  - MUST NOT 输出含原始 token 的日志

## 9. Application 层 — Users Feature

- [x] 9.1 `Features/Users/GetCurrentUser/GetCurrentUserQuery.cs`:`(UserId UserId) : IRequest<UserDto>`
- [x] 9.2 `Features/Users/GetCurrentUser/GetCurrentUserQueryHandler.cs`:
  - `FindByIdAsync` → 找不到 `UserNotFoundException`
  - `!IsActive` → `UserNotActiveException`
  - 映射 `UserDto` 返回

## 10. Application 层 — DI

- [x] 10.1 `DependencyInjection.cs`:`public static IServiceCollection AddApplication(this IServiceCollection services)`:
  - `AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly))`
  - `AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly)`
  - `AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>))`
- [x] 10.2 Application 项目 build 通过(不需可运行,此时 Infrastructure 未实现)

## 11. Application 测试

- [x] 11.1 在 `Gomoku.Application.Tests.csproj` 加 `ProjectReference` 指向 `Gomoku.Application`(以及 Domain 已通过 Application 传递)
- [x] 11.2 加 `PackageReference`:`Moq`、`FluentAssertions 7.2.0`;`global using FluentAssertions; using Moq; using Xunit;`
- [x] 11.3 删除 `Gomoku.Application.Tests/UnitTest1.cs`
- [x] 11.4 `Common/Behaviors/ValidationBehaviorTests.cs`:无 validator 直通;单 validator 成功 / 失败;多 validator 合并错误
- [x] 11.5 `Features/Auth/Register/RegisterCommandValidatorTests.cs`:每条规则一个 Theory
- [x] 11.6 `Features/Auth/Register/RegisterCommandHandlerTests.cs`:
  - 成功:验证 `AddAsync` 被调用一次、`SaveChangesAsync` 被调用一次、返回的 `AuthResponse` 各字段正确
  - 邮箱已存在 → `EmailAlreadyExistsException`,`AddAsync` 未被调用
  - 用户名已存在 → `UsernameAlreadyExistsException`
  - Mock `IDateTimeProvider` 返回固定 UTC,断言 `AuthResponse.AccessTokenExpiresAt == now + 15min`
- [x] 11.7 `Features/Auth/Login/LoginCommandHandlerTests.cs`:
  - 成功:`IssueRefreshToken` 在聚合上发生、返回正确 token
  - 用户不存在 / 密码错误 → `InvalidCredentialsException`(两条消息一致)
  - `IsActive=false` → `UserNotActiveException`
- [x] 11.8 `Features/Auth/RefreshToken/RefreshTokenCommandHandlerTests.cs`:
  - 成功轮换:旧 token `RevokedAt` 被设置,新 token 加入,返回值 token 不等于旧 token
  - 不存在 / 已吊销 / 已过期 → `InvalidRefreshTokenException`
- [x] 11.9 `Features/Auth/Logout/LogoutCommandHandlerTests.cs`:
  - 成功吊销:`SaveChangesAsync` 被调用一次
  - token 不存在:**不抛**,`SaveChangesAsync` 不被调用
  - token 已撤销:**不抛**,`RevokedAt` 不被覆盖
- [x] 11.10 `Features/Users/GetCurrentUser/GetCurrentUserQueryHandlerTests.cs`:成功 / 用户不存在 / 用户未启用

## 12. Infrastructure 层 — 项目引用与 NuGet

- [x] 12.1 在 `Gomoku.Infrastructure.csproj` 加 `<ProjectReference>` 指向 `Gomoku.Application` 与 `Gomoku.Domain`
- [x] 12.2 加 NuGet:`Microsoft.EntityFrameworkCore`、`Microsoft.EntityFrameworkCore.Sqlite`、`Microsoft.EntityFrameworkCore.Design`、`Microsoft.AspNetCore.Identity`、`System.IdentityModel.Tokens.Jwt`、`Microsoft.Extensions.Options.ConfigurationExtensions`、`Microsoft.Extensions.Configuration.Abstractions`
- [x] 12.3 删除 `Gomoku.Infrastructure/Class1.cs`

## 13. Infrastructure 层 — Persistence

- [x] 13.1 `Persistence/GomokuDbContext.cs`:继承 `DbContext`,`DbSet<User> Users` + `DbSet<RefreshToken> RefreshTokens`;`OnModelCreating` 中 `ApplyConfigurationsFromAssembly(typeof(GomokuDbContext).Assembly)`
- [x] 13.2 `Persistence/Configurations/UserConfiguration.cs`:
  - PK `Id`,`HasConversion(id => id.Value, guid => new UserId(guid))`
  - `Email.Value` 列 `VARCHAR(254)`、`HasConversion`、唯一索引
  - `Username.Value` 列 `VARCHAR(20)` + `COLLATE NOCASE` + 唯一索引
  - 战绩字段 `int not null` 默认 0
  - `RefreshTokens` 为导航属性,级联删除
- [x] 13.3 `Persistence/Configurations/RefreshTokenConfiguration.cs`:
  - PK `Id`、FK `UserId`
  - `TokenHash` 列 `VARCHAR(128) not null` + 唯一索引
  - `ExpiresAt` / `CreatedAt` / `RevokedAt` 类型 `DATETIME`
- [x] 13.4 `Persistence/Repositories/UserRepository.cs`:实现 `IUserRepository` 的 7 个方法;所有带 token 场景用 `Include(u => u.RefreshTokens)`;查询 `AsNoTracking()` 按需
- [x] 13.5 `Persistence/UnitOfWork.cs`:注入 `GomokuDbContext`,`SaveChangesAsync` 直接委托

## 14. Infrastructure 层 — Auth 实现

- [x] 14.1 `Authentication/PasswordHasher.cs`:实现 `Application.Abstractions.IPasswordHasher`,内部 `private readonly PasswordHasher<User> _inner = new()`;`Hash` 直接调 `_inner.HashPassword(null!, plain)`;`Verify` 用 `_inner.VerifyHashedPassword(null!, hashed, plain)` 返回 `Success || SuccessRehashNeeded`
- [x] 14.2 `Authentication/JwtOptions.cs`(或直接用 Application 里定义的):`Issuer` / `Audience` / `SigningKey` / `AccessTokenLifetimeMinutes=15` / `RefreshTokenLifetimeDays=7`
- [x] 14.3 `Authentication/JwtTokenService.cs`:实现 `IJwtTokenService`:
  - `GenerateAccessToken`:用 `JwtSecurityTokenHandler`,HS256,claims 按 design D8 列表,`iat` / `exp` 取 `IDateTimeProvider.UtcNow`,expires 加上 15 分钟;返回 `AccessToken(string, DateTime)`
  - `GenerateRefreshToken`:`RandomNumberGenerator.Fill(buf[32])` → `Base64UrlEncoder.Encode`
  - `HashRefreshToken`:`SHA256.HashData(Encoding.UTF8.GetBytes(raw))` → `Convert.ToHexStringLower` 或 base64
- [x] 14.4 `Common/SystemDateTimeProvider.cs`:`DateTime UtcNow => DateTime.UtcNow`

## 15. Infrastructure 层 — DI 与 Migration

- [x] 15.1 `DependencyInjection.cs`:`AddInfrastructure(IServiceCollection, IConfiguration)`:
  - `AddDbContext<GomokuDbContext>(o => o.UseSqlite(configuration.GetConnectionString("Default")))`
  - `AddScoped<IUserRepository, UserRepository>()`
  - `AddScoped<IUnitOfWork, UnitOfWork>()`
  - `AddSingleton<IPasswordHasher, PasswordHasher>()`
  - `AddSingleton<IDateTimeProvider, SystemDateTimeProvider>()`
  - `AddScoped<IJwtTokenService, JwtTokenService>()`(scoped 是因为依赖 `IDateTimeProvider`,其实可 singleton;设计上 scoped 更保守)
  - `services.Configure<JwtOptions>(configuration.GetSection("Jwt"))`
- [x] 15.2 生成初始 migration:`dotnet ef migrations add InitialIdentity --project src/Gomoku.Infrastructure --startup-project src/Gomoku.Api --output-dir Persistence/Migrations`
- [x] 15.3 本地 `dotnet ef database update`,确认 `backend/gomoku.db` 生成,两张表建立
- [x] 15.4 把 `gomoku.db` 写进 `.gitignore`(单行 `*.db`);确认 migration 文件进仓库

## 16. Api 层 — 项目引用、NuGet、Program.cs

- [x] 16.1 在 `Gomoku.Api.csproj` 加 `<ProjectReference>` 指向 `Gomoku.Application` 与 `Gomoku.Infrastructure`
- [x] 16.2 加 NuGet:`Microsoft.AspNetCore.Authentication.JwtBearer`
- [x] 16.3 改 `Program.cs`:删除 `WeatherForecast` 相关代码;添加 `builder.Services.AddApplication().AddInfrastructure(builder.Configuration)`;`AddControllers()`;`AddEndpointsApiExplorer()`;保留 `AddOpenApi()`
- [x] 16.4 添加 JWT Bearer:`AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o => { o.TokenValidationParameters = ... })`,绑定 `JwtOptions`,`ClockSkew = TimeSpan.FromSeconds(30)`
- [x] 16.5 `app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();`
- [x] 16.6 启动时 `using (var scope = app.Services.CreateScope()) { scope.ServiceProvider.GetRequiredService<GomokuDbContext>().Database.Migrate(); }`(仅 Development)
- [x] 16.7 `appsettings.json` 加 `"ConnectionStrings": { "Default": "Data Source=gomoku.db" }` 与 `"Jwt": { "Issuer": "gomoku-online", "Audience": "gomoku-online-clients", "SigningKey": "", "AccessTokenLifetimeMinutes": 15, "RefreshTokenLifetimeDays": 7 }`(SigningKey 为空字符串占位)
- [x] 16.8 `appsettings.Development.json` 加 **开发用 base64** 签名密钥(≥ 32 字节随机 `RandomNumberGenerator.GetBytes(32)` 生成,仅限本地)
- [x] 16.9 `Program.cs` 启动时校验:若 `app.Environment.IsProduction() && string.IsNullOrWhiteSpace(jwtOptions.SigningKey)` 则抛 `InvalidOperationException`,拒绝以空密钥启动生产环境(D22)

## 17. Api 层 — 异常中间件

- [x] 17.1 `Middleware/ExceptionHandlingMiddleware.cs`:`async Task InvokeAsync(HttpContext ctx, RequestDelegate next)`;按 design D9 映射表输出 `ProblemDetails`;`ValidationException` 响应体额外带 `errors` 字典
- [x] 17.2 Register 到 pipeline 顶端(在 `UseAuthentication` **前**);用 Serilog(若未启用 Serilog,用 `ILogger<T>`)在 5xx 时 `LogError`,4xx 时 `LogInformation`
- [x] 17.3 未知异常的 `detail` 固定为 `"An unexpected error occurred."`,避免内部细节外泄

## 18. Api 层 — Controllers

- [x] 18.1 `Controllers/AuthController.cs`:`[ApiController] [Route("api/auth")]`;注入 `ISender`(MediatR);四个 endpoint:
  - `POST register` → `RegisterCommand` → 201 + `AuthResponse`
  - `POST login` → `LoginCommand` → 200 + `AuthResponse`
  - `POST refresh` → `RefreshTokenCommand` → 200 + `AuthResponse`
  - `POST logout` → `LogoutCommand` → 204
- [x] 18.2 `Controllers/UsersController.cs`:`[ApiController] [Route("api/users")] [Authorize]`;`GET me` 从 `User.FindFirst("sub")` 解 `UserId` 发 `GetCurrentUserQuery`,返回 `UserDto`
- [x] 18.3 所有 action 方法签名带 `CancellationToken`,透传给 `ISender.Send`

## 19. Api 层 — 文档与冒烟

- [x] 19.1 在仓库根或 `backend/` 下加 `HOW_TO_RUN.md`:如何本地启动后端、如何获得 dev JWT、如何 curl 测 5 个 endpoint(含 register / login / me / refresh / logout 的示例)
- [x] 19.2 本地运行 `dotnet run --project src/Gomoku.Api`,用 `curl` 或 `.http` 文件走完一整条链路:register → me → refresh → me(with new token)→ logout
- [x] 19.3 `gomoku.db` 里手工核对:`Users` 有 1 行,`RefreshTokens` 初始 1 行,refresh 后 2 行(旧的 `RevokedAt` 非 null),logout 后末条 `RevokedAt` 非 null
- [x] 19.4 删除 `Gomoku.Api/Gomoku.Api.http` 里 WeatherForecast 相关行;若写新 `.http` 示例,覆盖这 5 个 endpoint

## 20. 归档前置检查

- [x] 20.1 `dotnet build Gomoku.slnx`:0 警告 / 0 错误
- [x] 20.2 `dotnet test Gomoku.slnx`:Domain + Application 测试全部通过(按计划应新增 ~20 个 Domain 测试 + ~15 个 Application 测试)
- [x] 20.3 `grep -r "DateTime.UtcNow" backend/src/Gomoku.Application backend/src/Gomoku.Domain`:**零命中**(除 `IDateTimeProvider` 自身的默认实现,但那在 Infrastructure)
- [x] 20.4 `grep -r "async void\|\.Result\b\|\.Wait(" backend/src/Gomoku.Application backend/src/Gomoku.Domain`:零命中
- [x] 20.5 Domain 层 csproj 扫描:`<PackageReference>` 和 `<ProjectReference>` 均为 0
- [x] 20.6 Application 层 csproj 扫描:`<ProjectReference>` 只有 `Gomoku.Domain`(不得出现 EF Core / Infrastructure 的引用)
- [x] 20.7 `openspec validate add-user-authentication`:无错误
- [x] 20.8 按 CLAUDE.md 的作者自检清单逐项过;分支命名 `feat/add-user-authentication`;分组 commit(按 D→A→I→Api 四组),每组遵守 Conventional Commits
