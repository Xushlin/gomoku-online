## Why

`add-domain-core` 已让 Domain 层可用,但 Application、Infrastructure、Api 三层至今仍只有脚手架(各一个 `Class1.cs`)。没有用户就没有房间、没有对局归属、没有 ELO —— 任何后续功能都需要一个 `User`。本次变更**第一次完整激活四层**,把"一个真实的请求从 HTTP 进入、穿过四层、命中 SQLite 数据库、再沿原路返回 JWT"打通,给后续所有 feature 铺一条已验证的工程主干道。

把"认证"作为第一个跨层能力是刻意的:它的外部依赖(EF Core、JWT、密码哈希、FluentValidation、MediatR Pipeline)正好覆盖了后续所有 CQRS handler 都会用到的基础设施,**一次搭好、后面复用**。

## What Changes

- **Domain**:新增 `UserId` / `Email` / `Username` 三个值对象、`User` 聚合根、`RefreshToken` 子实体、`InvalidEmailException` / `InvalidUsernameException` 领域异常。
- **Application**:引入 MediatR + FluentValidation。新增五个 feature(`Register` / `Login` / `RefreshToken` / `Logout` / `GetCurrentUser`),每个一组 Command/Query + Handler(+ Validator)。新增 Application 级抽象:`IUserRepository` / `IPasswordHasher` / `IJwtTokenService` / `IDateTimeProvider` / `IUnitOfWork`。新增 `ValidationBehavior` Pipeline Behavior。新增 `AuthResponse` / `UserDto` DTO。新增 Application 级异常族(凭据错误、重复邮箱等)。新增 `DependencyInjection.AddApplication`。
- **Infrastructure**:引入 EF Core + SQLite。新增 `GomokuDbContext`、`User` / `RefreshToken` 的 `IEntityTypeConfiguration`。实现 Application 的全部抽象:EF-backed `UserRepository` / `UnitOfWork`、基于 `Microsoft.AspNetCore.Identity.PasswordHasher<User>` 的 `PasswordHasher`、`JwtTokenService`、`SystemDateTimeProvider`。新增 `DependencyInjection.AddInfrastructure`。第一条 EF Core migration(`InitialIdentity`)。
- **Api**:新增 `AuthController`(`POST /api/auth/register|login|refresh|logout`)与 `UsersController`(`GET /api/users/me`)。改造 `Program.cs` 接入 DI、JWT Bearer、EF 自动 migrate、全局异常中间件。删除默认的 `WeatherForecast` sample。新增 `appsettings.json` 的 `Jwt` 节与 SQLite 连接串。新增 `HOW_TO_RUN.md` 说明本地启动步骤。
- **Tests**:`Gomoku.Domain.Tests/Users/` 覆盖值对象与 `User` 聚合;`Gomoku.Application.Tests/Features/Auth/` 覆盖全部 Handler(Moq mock 仓储与服务),成功与错误路径都要测。Infrastructure 暂不做集成测试(另开变更)。

**显式不做**(归属后续变更,以便 reviewer 不必担心):
- 头像上传、用户资料编辑、邮箱验证、找回密码、第三方 OAuth
- 角色/权限系统(本次所有已登录用户平权)
- ELO 积分变化逻辑与对局记录的联动(战绩字段先建立,`RecordWin/Loss/Draw` 方法留给 `add-elo-system`)
- Infrastructure 层的集成测试(另开 `add-integration-tests`)

## Capabilities

### New Capabilities

- `user-management`: 用户聚合及其不变量 —— 身份由 `UserId` 标识,`Email` / `Username` 有格式校验并全局唯一,新用户的初始 ELO / 战绩 / 启用状态有确定的默认值。涵盖"用户是什么、怎样创建、怎样查询当前用户"。
- `authentication`: 密码校验、JWT Access Token 签发、Refresh Token 发放与轮换、登出。涵盖"如何证明我是这个用户"。Register 接口在此能力下**产生副作用**(即刻签发一对 token),但"创建用户"的不变量属于 `user-management`。

### Modified Capabilities

(无 —— 两个都是新能力。`gomoku-domain` 本次零触碰。)

## Impact

- **代码规模**:大约 40–50 个新文件,跨四层 + 两个测试项目。
- **新增 NuGet(按层划分)**:
  - `Gomoku.Domain`:**零新增**(铁律不变)。
  - `Gomoku.Application`:`MediatR`、`FluentValidation`、`FluentValidation.DependencyInjectionExtensions`、`Microsoft.Extensions.DependencyInjection.Abstractions`。
  - `Gomoku.Infrastructure`:`Microsoft.EntityFrameworkCore`、`Microsoft.EntityFrameworkCore.Sqlite`、`Microsoft.EntityFrameworkCore.Design`、`Microsoft.AspNetCore.Identity` (仅为 `PasswordHasher<T>`)、`System.IdentityModel.Tokens.Jwt`、`Microsoft.Extensions.Options.ConfigurationExtensions`、`Microsoft.Extensions.Configuration.Abstractions`。
  - `Gomoku.Api`:`Microsoft.AspNetCore.Authentication.JwtBearer`。
  - `Gomoku.Application.Tests`:`Moq`、`FluentAssertions 7.2.0`(与 Domain.Tests 一致)。
- **项目引用变化**:`Infrastructure → Application`、`Infrastructure → Domain`、`Api → Application`、`Api → Infrastructure` 四条 `ProjectReference` 首次建立。
- **数据库**:`gomoku.db`(SQLite 文件)由应用启动时自动 migrate 创建;两张表 `Users` 与 `RefreshTokens`。
- **HTTP 表面**:新增 5 个端点 —— `POST /api/auth/register`、`POST /api/auth/login`、`POST /api/auth/refresh`、`POST /api/auth/logout`、`GET /api/users/me`。
- **后续变更将依赖**:所有需要"知道当前用户"的功能(房间、对局、聊天、ELO、排行榜),都建立在这次的 `User` 聚合与 JWT 约定之上。
