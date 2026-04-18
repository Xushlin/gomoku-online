## Context

这是项目**第一次**让 Application / Infrastructure / Api 三层真正运行起来的变更。它的副产品是一整套 CQRS 基础设施(MediatR + FluentValidation Pipeline + 异常映射中间件 + EF Core + JWT 管线),后面每个功能都会复用这些主干。因此本次 design 的重点不在"怎么做登录",而在"**选什么样的基础件,让未来每个 feature 都能照着抄**"。

现状:Application/Infrastructure 两个项目各有一个 `Class1.cs` 占位,Api 还是 .NET 模板的 `WeatherForecast`;四层之间**尚无 `ProjectReference`**。Clean 架构的依赖方向在 CLAUDE.md 已铁律化 —— Domain ← Application ← Infrastructure ← Api。

用户已对齐关键决策(见你发来的需求),本 design 把它们落成可执行的技术选型,并为每个非显然的选择写下理由与否决的备选。

## Goals / Non-Goals

**Goals**

- 一条请求能从 HTTP 进入、穿过 `Controller → MediatR → Handler → Repository → EF Core → SQLite`,再沿原路返回 JWT。整套链路可用、可测、可扩展。
- 密码与 refresh token 的存储安全:明文密码不落盘,refresh token 只存 hash。
- Domain 层零外部 NuGet(铁律);Application 只依赖轻量契约(MediatR、FluentValidation、DI 抽象);Infrastructure 隔离所有"脏"依赖(EF Core、Identity PasswordHasher、JWT 库)。
- 每个 Command/Query 一个 Handler 一个文件;Validator 单文件;输入输出都是 `record`。
- 错误处理策略统一(领域/应用异常 → 全局中间件 → HTTP 状态码 + ProblemDetails),在本变更定型。

**Non-Goals**

- ELO 的实际计算(战绩字段先立,`RecordWin/Loss/Draw` 方法留给 `add-elo-system`)。
- 用户资料编辑、头像、邮箱验证、找回密码、第三方 OAuth、角色/权限。
- `Result<T>` 函数式错误模式 —— 本次用"领域异常 + 全局中间件"简单模式,将来若 handler 数量膨胀或错误路径变复杂再重构。
- Infrastructure 层集成测试(单独开变更,届时才引入 Testcontainers / 真 SQLite 文件)。
- "登出所有设备"(本次 logout 只吊销当前那一枚 refresh token)。

## Decisions

### D1. 能力划分:`user-management` + `authentication` 两个 spec

"用户是什么"与"如何证明我是这个用户"是两件分离的事,未来会分别演进(用户资料 / 身份联合 等)。划分两份 spec 让未来变更能精确引用受影响的能力,避免整包 MODIFIED。

- **备选**:合成一份 `user-auth` 单 spec。否决 —— 两者演进节奏不同,耦合只有在本次变更里显得"都要做",长期会分叉。
- Register 接口本质跨两个能力(创建 `User` + 即刻颁发 token),spec 会在两边各写一条 Requirement,交由文字说明其协作。

### D2. 密码哈希:只借用 `Microsoft.AspNetCore.Identity.PasswordHasher<User>`,不引入 Identity 全家桶

- **为什么**:Identity 的全家桶(`UserManager`、`SignInManager`、`IdentityDbContext`、cookie schemes 等)会强行占据 DbContext 的建模权与控制器层的约定,与 Clean Architecture 的依赖方向相悖。只取 `PasswordHasher<T>` 意味着我们只用到它的 PBKDF2+HMACSHA512(V3 格式)算法实现,其余一律自理。
- **放哪**:在 Infrastructure 层实现 `IPasswordHasher`(Application 的抽象),内部 `new PasswordHasher<User>()`。`<User>` 在 `PasswordHasher<T>` 里只是占位泛型参数,不会触发 Identity 配置。
- **备选**:自写 PBKDF2。否决 —— 重造轮子,还得自己升级格式。

### D3. Refresh Token 存 **hash** 不存明文

- **为什么**:Refresh token 是一张"7 天内可换新 Access Token"的通行证,等同于弱化的密码。数据库一旦泄漏,明文 refresh token 可直接使用。存 hash 后,攻击者即使拿到整张表,也只能得到"这个用户曾有过某些 session"而已。
- **算法**:`SHA-256(token)`。**不用** bcrypt/PBKDF2,因为:(a) 每次刷新都要 verify,慢哈希带来性能开销;(b) refresh token 是 256 bit 高熵随机,不存在字典/穷举攻击前景,快哈希已足够。
- **用户侧感知为零**:明文只在 HTTP 响应里出现一次,客户端自行保存。
- 用户提到的"随机字符串,存数据库"在本 design 里落实为:**生成时**得到高熵 base64url 字符串返回给客户端,**入库前**算 SHA-256 存 `TokenHash` 列。

### D4. Refresh Token 采用 **轮换(rotation)** 策略

- 每次 `/api/auth/refresh` 成功都会:(a) 把旧 refresh token 标记为 `RevokedAt = now`,(b) 签发一枚新的,(c) 连同新 access token 一起返回。
- 好处:若 refresh token 泄漏并被攻击者先用一次,正主再刷新就会失败(旧 token 已被撤销),能及时察觉被盗。
- 成本:多一次写库。对于 15 min access token 的节奏可接受。
- 备选:不轮换。否决 —— 失去对盗用的可见性,与"基础安全"目标不符。

### D5. 值对象形态:`UserId` 用 `readonly record struct`,`Email` 和 `Username` 用 `sealed record class`

- `UserId` 只包一个 `Guid`,轻量,高频出现(JWT claim / repo 查找 / 对局归属 …),`record struct` 零分配。
- `Email` / `Username` 持有 `string`,本身就是引用类型字段 + 校验逻辑。`record class` 比 `record struct` 在 boxing 场景与 EF Core `HasConversion` 的泛型推断上更稳,代价可忽略。
- 格式校验写在构造函数里;非法值抛 `InvalidEmailException` / `InvalidUsernameException`。Email 构造时调用 `ToLowerInvariant()` 统一大小写,以"规范化存储、不区分大小写比较"为准。
- **备选**:全部用 struct。否决 —— 对字符串值对象没收益,反而带来"struct 装箱进接口"的潜在问题。

### D6. Email 格式校验:**`MailAddress` 构造 + 长度上限**

- 不写正则。`System.Net.Mail.MailAddress` 构造成功 ≈ 合法邮箱(RFC 5321/5322 不完整但足够实用)。
- 额外限制:总长 ≤ 254(RFC 5321 事实上限);本地部分不为空。
- 统一小写入库;做相等比较时就是字符串相等。

### D7. Username 规则:长度 3–20,字母 / 数字 / 中文 / 下划线,**不能全数字**

- 正则:`^[a-zA-Z0-9_\u4e00-\u9fff]{3,20}$`。长度按 **UTF-16 `char`** 计 —— 中文在 BMP 内一个 char 一个字符;扩展 CJK 与 emoji 会被字符集正则直接拒绝(好事,用户名不应含 emoji)。
- "不能全数字"单独一道校验:`value.All(char.IsDigit)` 为 true 则拒。
- 入库按原大小写存,比较大小写**不敏感**(避免 `alice` 和 `Alice` 同时存在)—— 通过在 EF 配置里把列声明为 `COLLATE NOCASE`(SQLite)实现;仓储查询时不需要显式 `ToLower()`。
- **备选**:长度按 rune / grapheme 计。放弃,过度工程;本次拒绝扩展字符就够了。

### D8. JWT 构造:**原生 `System.IdentityModel.Tokens.Jwt`**,不上 OpenIddict / Duende 之类

- 本次只需要"签发 Access Token + 在 Api 层校验",依赖越少越好。
- Claims:`sub` = `UserId`(Guid 字符串)、`preferred_username` = `Username`、`jti` = `Guid.NewGuid()`、`iat` / `exp` 标准字段。**不把 Refresh Token 的任何信息塞进 JWT**。
- 签名算法:`HS256`(对称密钥)。单体后端场景下对称密钥最简单;密钥长度 ≥ 32 bytes,appsettings 里存 base64,生产走环境变量。
- `Issuer` = `"gomoku-online"`,`Audience` = `"gomoku-online-clients"`;验证时两项都校验。
- **clock skew** 统一 30 秒(`TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(30)`),比默认 5 分钟更严、比 0 更宽容。
- **备选**:RS256。否决 —— 单体后端无密钥分发问题,HS256 更简单,未来要拆分到多服务再升级。

### D9. 错误处理策略:**领域/应用异常 + 全局中间件**,不用 `Result<T>`

- **领域异常**(Domain 层):`InvalidEmailException`、`InvalidUsernameException`、以及已有的 `InvalidMoveException`。聚合不变量的守门员。
- **应用异常**(Application 层):`EmailAlreadyExistsException`、`UsernameAlreadyExistsException`、`InvalidCredentialsException`、`InvalidRefreshTokenException`、`UserNotFoundException`、`UserNotActiveException`、`ValidationException`(包住 FluentValidation 的失败)。这些异常**只从 Handler 里冒出来**,Controller 不捕获。
- **全局异常中间件**(Api 层):按异常类型映射 HTTP:
  | 异常 | HTTP |
  | --- | --- |
  | `ValidationException` | 400 + ProblemDetails + `errors` 字典 |
  | `InvalidEmailException` / `InvalidUsernameException` / `InvalidMoveException` | 400 |
  | `InvalidCredentialsException` / `InvalidRefreshTokenException` | 401 |
  | `UserNotActiveException` | 403 |
  | `UserNotFoundException` | 404 |
  | `EmailAlreadyExistsException` / `UsernameAlreadyExistsException` | 409 |
  | 其他 | 500,消息不外泄 |
- **响应体**使用 `ProblemDetails`(RFC 7807),便于前端统一处理。
- 本策略给后续变更提供"新建一个 `*Exception` + 在中间件里加一条 switch 分支"的简单扩展路径。
- **备选**:`Result<T>` 模式。否决本次 —— 所有 handler 都要写返回值分支,短期成本高;等错误类型膨胀到"分支 switch 明显笨重"再重构。

### D10. MediatR Pipeline:只加 **`ValidationBehavior`** 一个

- FluentValidation 的 `AbstractValidator<TCommand>` 注册为 DI 单例,`ValidationBehavior<TRequest, TResponse>` 在 handler 执行**前**统一调用,失败抛 `ValidationException`。
- 本次**不加**:日志 Behavior(Serilog 的请求级日志由 Api 层处理即可,Behavior 里加会跟 Serilog middleware 重复)、缓存 Behavior(没有可缓存的场景)、事务 Behavior(`IUnitOfWork` 手动 SaveChanges 更直观,且本次只有单聚合写入)。
- **备选**:零 Behavior,validator 手动调用。否决 —— 每个 handler 加 4 行重复代码,违反 DRY。

### D11. `IUnitOfWork` 作为"显式保存点"而非"事务跨越 handler"

- 契约极简:`Task<int> SaveChangesAsync(CancellationToken ct)`。Infrastructure 实现里包 `dbContext.SaveChangesAsync()`。
- Handler 内"先改内存、最后 SaveChanges",与 EF Core 的变更追踪自然契合。
- **不跨 handler 做事务** —— 本次没有"多个 handler 组合"的场景,保持简单。
- **备选**:在 Behavior 里自动 SaveChanges。否决 —— 查询 Handler 也会进 Behavior,容易误触发保存;显式调用更安全。

### D12. Repository 契约:`IUserRepository` 只暴露**领域概念**,返回聚合或 `null`

```
FindByIdAsync(UserId) / FindByEmailAsync(Email) / FindByUsernameAsync(Username)
FindByRefreshTokenHashAsync(string hash)
EmailExistsAsync(Email) / UsernameExistsAsync(Username)
AddAsync(User)
```

- 不暴露 `IQueryable` / 过滤表达式 / EF 实体。保持 Application 对 EF 零感知。
- `FindByRefreshTokenHashAsync` 内部 `Include(u => u.RefreshTokens)` 以便 handler 直接操作子实体。
- 无 `UpdateAsync` / `DeleteAsync`:EF 变更追踪自动感知;本次没有用户删除场景。

### D13. 聚合边界:`User` 是聚合根,`RefreshToken` 是**子实体**,客户端不得直接获取

- `User.RefreshTokens` 暴露为 `IReadOnlyCollection<RefreshToken>`;改动只通过 `User.IssueRefreshToken(...)` / `User.RevokeRefreshToken(...)` / `User.RevokeAllRefreshTokens()` 领域方法。
- EF 映射:`RefreshToken.UserId` FK,`User → RefreshToken` 一对多,级联删除(用户被删 → token 随走。本次无删除用户场景,但规则定好)。
- 查询 refresh token 时:`IUserRepository.FindByRefreshTokenHashAsync` 返回 `User`(而非 `RefreshToken`),保持"永远通过聚合根操作"。

### D14. Register 流程由 Handler 协调"创建用户 + 签发一对 token"

- 注册接口**一次完成创建 + 登录**。流程:Validator → 唯一性检查 → 构造值对象(会触发领域异常) → hash 密码 → `User.Register(...)` 静态工厂 → `user.IssueRefreshToken(...)` → `repo.AddAsync` → `uow.SaveChangesAsync` → 生成 access token → 返回 `AuthResponse`。
- 这样客户端不需要"注册完再打一次 login",一次往返就能进入已登录态。

### D15. Login 的错误信息**故意模糊**

- "邮箱不存在" 与 "邮箱对但密码错" 均返回同一个 `InvalidCredentialsException`,消息统一为 `"Email or password is incorrect."`。
- 防止攻击者用此接口枚举哪些邮箱已注册。本次约束严格遵守,别在日志/异常消息里偷偷加区分。

### D16. Logout 是**幂等**的,找不到 token 也返回 204

- 传入的 refresh token 若在库里不存在 / 已过期 / 已撤销,一律**静默成功**(HTTP 204)。
- 理由:客户端本地已经清凭据,再向服务端抛 4xx 反而制造噪音;攻击者也无法通过"撤销返回 404"来探测 token 有效性。
- Handler 内部:hash 传入 → 查 user → 若找到对应 RefreshToken 则 `user.RevokeRefreshToken(hash, now)`;否则 no-op。

### D17. `IDateTimeProvider` 全项目唯一"现在"的来源

- 所有需要 `DateTime.UtcNow` 的地方(注册 `CreatedAt`、token 过期、撤销时间戳)都注入 `IDateTimeProvider`。
- Infrastructure 实现 `SystemDateTimeProvider { DateTime UtcNow => DateTime.UtcNow; }`。
- 测试中可以注入固定时间 mock,确保"token 过期判定"之类有可重复验证的断言。
- 这是 Application 层抽象,Domain 层**不引**(Domain 方法接收 `DateTime now` 参数,调用方负责注入)。Domain 保持零外部依赖。

### D18. EF Core:**Code First + 值对象转换器**,启动自动 migrate

- 值对象映射:`UserId (Guid)`、`Email (string)`、`Username (string)` 通过 `HasConversion` 转成列基元类型。
- 索引:`Users.Email` 唯一、`Users.Username` 唯一(`NOCASE` 排序)、`RefreshTokens.TokenHash` 唯一。
- 迁移目录:`backend/src/Gomoku.Infrastructure/Persistence/Migrations/`(与 CLAUDE.md 里给的命令一致)。
- 启动时在 `Program.cs` 调 `db.Database.Migrate()`(开发便利;生产环境将来通过 CI/部署脚本显式执行)。
- 连接串:`Data Source=gomoku.db`(相对 `Api` 项目工作目录)。
- **备选**:Database First / DbUp。否决 —— EF Core 的 code-first migration + 值对象转换器已足够。

### D19. JWT Bearer 校验:`Api` 层按约定**信任 JWT、不查库**

- Access Token 只要签名合法 + 未过期,就认为请求者是那个 `UserId`。不在每个请求里查数据库确认用户还存在 / 仍启用 —— 那样每个 API 都多一次查询,且 15 分钟足够短,用户被禁用后最多等 15 分钟就失效。
- **例外**:`GetCurrentUser` 本来就要查用户,顺便在查不到 / `IsActive = false` 时分别返回 404 / 403。
- **未来升级**:如果要"立即踢掉",再引入"每个请求查 Redis 黑名单"机制,本次不做。

### D20. DI 组装:每层一个 `DependencyInjection` 静态类暴露 `AddXxx(IServiceCollection)`

- `Gomoku.Application.DependencyInjection.AddApplication(IServiceCollection)` —— 注册 MediatR、FluentValidation、`ValidationBehavior`。
- `Gomoku.Infrastructure.DependencyInjection.AddInfrastructure(IServiceCollection, IConfiguration)` —— 注册 `GomokuDbContext`、仓储、`IPasswordHasher`、`IJwtTokenService`、`IDateTimeProvider`,绑定 `JwtOptions` / `ConnectionStrings`。
- `Program.cs` 只调这两个扩展方法 + 加 JWT Bearer 验证配置。三层解耦、易于写测试时替换。

## Risks / Trade-offs

- **启动时自动 `Migrate`** → 若多个实例同时启动会竞争 migration。Mitigation:本次单体部署不存在此问题;未来部署到多实例时,在 CI 跑 `dotnet ef database update`,Program 里关掉自动 migrate。
- **Refresh token 存 hash 后无法"查用户的所有活跃 session"列表带原文** → 不影响本次功能;未来若做"已登录设备管理",在 RefreshToken 上加 `DeviceLabel` 列即可,无需回退到存明文。
- **`PasswordHasher<User>` 的泛型参数耦合了 User 类型** → 若将来 Domain 的 `User` 重命名或搬动命名空间,Infrastructure 层一处需要同步。可控,记在这里。
- **Login 错误信息模糊会让用户在"忘记密码"之前得不到提示** → 一旦有"忘记密码"功能,这条平衡自然解决;当前权衡偏向安全。
- **JWT claim 不包含 roles / permissions** → 本次所有已登录用户平权,满足需求;未来加权限系统时,要么在 claim 里加 roles(需要重新签发),要么服务端每次查库。记下来,避免将来误以为 JWT 里有角色。
- **EF Core 自动 migrate 在集成测试里可能重复执行** → 集成测试变更里用内存 / 每测试一库,不走这条路径;本次仅为 Api 启动用。
- **`IUserRepository` 没有"批量"接口** → 本次只单条操作,够用;排行榜 / 战绩分页等将来用 `GetLeaderboardQuery` 这类**读模型专用**的 Query + 直接查 DbContext,不走 Repository,避免把 repo 变成全能查询器。

## Migration Plan

纯新增、无需数据迁移。按 `tasks.md` 顺序分四组 PR(Domain / Application / Infrastructure / Api)提交审阅,每组可独立编译并通过自身测试。`main` 分支上合并顺序严格:Domain → Application → Infrastructure → Api。

回滚策略:任一环节被驳回,该层的 commit 可单独 revert,不影响先前已合并层。数据库:`gomoku.db` 是本地文件,删除即回滚;无生产数据。

### D21. 密码哈希升级:本次**不做**自动 rehash(`SuccessRehashNeeded` 忽略)

- `PasswordHasher<User>.VerifyHashedPassword` 返回 `PasswordVerificationResult.SuccessRehashNeeded` 时,Login handler **仅**视其为验证成功,不触发重哈希 + 写库。
- 理由:当前密码哈希格式已是 Identity V3(PBKDF2+HMACSHA512+iter=100000),近期不会过时;每次登录额外 SaveChanges 带来的性能和复杂度不划算。
- 未来当 Identity 发布新默认格式,或项目决定提升迭代次数时,开独立变更 `upgrade-password-hash`,届时在 `LoginCommandHandler` 里加一个 if 分支即可,改动范围极小。

### D22. 开发环境 JWT 密钥:**明文 base64 写入 `appsettings.Development.json`**,生产走环境变量

- `appsettings.Development.json` 中 `Jwt:SigningKey` 填一段 ≥ 32 字节的 **base64 字符串**(仅开发)。目标是"克隆仓库即能跑通",不让新成员卡在"先生成密钥"这一步。
- 生产部署通过环境变量 `GOMOKU_JWT__SIGNINGKEY` 覆盖(ASP.NET Core 配置系统天然支持 `__` 分层)。CI 密钥使用 CI 提供的 secrets 机制。
- `appsettings.json`(生产默认)中 `Jwt:SigningKey` MUST 留占位符(例如空字符串),并在 `Program.cs` 启动校验:**生产环境读到空密钥时直接抛异常拒绝启动**,避免以弱密钥跑生产。
- 备选"启动生成临时密钥写内存"被否决:每次重启旧 access token 全部失效,开发体验差;且与"15 分钟 access token + 7 天 refresh"的设计冲突。

## Open Questions

(无 —— 提案讨论中的两个悬置问题已落为 D21、D22。)
