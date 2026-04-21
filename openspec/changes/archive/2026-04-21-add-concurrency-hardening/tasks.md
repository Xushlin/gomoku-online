## 1. Domain — `User.RowVersion` + `TouchRowVersion`

- [x] 1.1 `Gomoku.Domain/Users/User.cs`:
  - 加 `public byte[] RowVersion { get; private set; } = Guid.NewGuid().ToByteArray();`
  - 加 `private void TouchRowVersion() => RowVersion = Guid.NewGuid().ToByteArray();`
  - 在 `RecordGameResult` **末尾**(所有字段写入后)调 `TouchRowVersion();`
  - XML 注释在 `RowVersion` 属性上说明"SQLite 无原生 rowversion,由 Domain 自管,EF 以 IsConcurrencyToken 使用"
- [x] 1.2 `Register` / `RegisterBot` 返回的 User 已通过字段默认值自带非空 RowVersion(无需改代码)。

## 2. Domain 测试

- [x] 2.1 `Gomoku.Domain.Tests/Users/UserRowVersionTests.cs`:
  - `User.Register` 生成的 RowVersion 非空、长度 16
  - `User.RegisterBot` 生成的 RowVersion 非空
  - 两次 Register 生成不同 RowVersion(Guid 碰撞概率 ~ 2^-122,测试里取等视为 0 概率)
  - `RecordGameResult(Win, 1220)` 改变 RowVersion
  - 连续 3 次 RecordGameResult 生成 3 个两两不等的 RowVersion
  - `IssueRefreshToken` 不改 RowVersion(反向不变量)
  - `RevokeRefreshToken` 不改 RowVersion
  - `RevokeAllRefreshTokens` 不改 RowVersion
- [x] 2.2 `dotnet test tests/Gomoku.Domain.Tests` 全绿(预期 208 + 8 = 216)。

## 3. Infrastructure — EF 配置 + migration

- [x] 3.1 `UserConfiguration.cs` 的 `Configure` 加 `builder.Property(u => u.RowVersion).IsConcurrencyToken().IsRequired();`
- [x] 3.2 `dotnet ef migrations add AddUserRowVersion --project src/Gomoku.Infrastructure --startup-project src/Gomoku.Api --output-dir Persistence/Migrations`
- [x] 3.3 修改生成的 migration 使用 `defaultValueSql: "randomblob(16)"` (SQLite);Down 正常 DropColumn。
- [x] 3.4 `dotnet ef database update` 在本地 SQLite 应用;`SELECT hex(RowVersion), Username_Value FROM Users` 所有行都非 NULL / 非同值。
- [x] 3.5 `dotnet build Gomoku.slnx` 0 警告 0 错。

## 4. 端到端冒烟

- [x] 4.1 启动 Api,注册 Alice,进入 Playing 局并赢棋(走连五或让 Bob 认输)。
- [x] 4.2 `GET /api/users/me`:Rating 增长、Wins=1(说明 `RecordGameResult` 生效且 SaveChanges 成功,RowVersion 检查通过)。
- [x] 4.3 **并发场景手工验证**(可选,观感用):用一个简单 Python 脚本同时发 2 个 `POST /api/rooms/{id}/resign`(同一 Alice 对两个 bob 同时认输,需 2 个房间) —— 一者 200,一者 409 `Concurrent modification`;前端客户端按 409 提示重拉即可。**若脚本写起来太复杂,跳过此步**,`DbUpdateConcurrencyException` 路径本身由 EF + Room/Game 的既有验证保证。

## 5. 归档前置检查

- [x] 5.1 `dotnet build Gomoku.slnx`:0 警告 0 错。
- [x] 5.2 `dotnet test Gomoku.slnx`:全绿(Domain 208 → 216;Application 89 不变)。
- [x] 5.3 Domain csproj 仍 0 PackageReference / 0 ProjectReference。
- [x] 5.4 `openspec validate add-concurrency-hardening --strict`:valid。
- [x] 5.5 分支 `feat/add-concurrency-hardening`,按层分组 commit(Domain / Infrastructure / docs-openspec 三条 —— Application / Api 零改动,不单独 commit)。
