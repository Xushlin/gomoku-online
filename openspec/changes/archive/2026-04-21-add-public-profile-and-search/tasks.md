## 1. Application — DTO

- [x] 1.1 `Common/DTOs/UserPublicProfileDto.cs`:`record UserPublicProfileDto(Guid Id, string Username, int Rating, int GamesPlayed, int Wins, int Losses, int Draws, DateTime CreatedAt);` + XML 注释强调不含敏感字段。

## 2. Application — GetUserProfile feature

- [x] 2.1 `Features/Users/GetUserProfile/GetUserProfileQuery.cs`:`record GetUserProfileQuery(UserId UserId) : IRequest<UserPublicProfileDto>`。
- [x] 2.2 `Features/Users/GetUserProfile/GetUserProfileQueryHandler.cs`:
  - Load user 按 Id;null → `UserNotFoundException`;
  - 不过滤 bot(详见 proposal);
  - 映射 UserPublicProfileDto 返回。

## 3. Application — SearchUsers feature

- [x] 3.1 `Features/Users/SearchUsers/SearchUsersQuery.cs`:
  `record SearchUsersQuery(string? Search, int Page, int PageSize) : IRequest<PagedResult<UserPublicProfileDto>>`。
- [x] 3.2 `Features/Users/SearchUsers/SearchUsersQueryValidator.cs`:
  - `Page >= 1`
  - `PageSize ∈ [1, 100]`
  - `Search` 可空;非空时 `Length <= 20`
- [x] 3.3 `Features/Users/SearchUsers/SearchUsersQueryHandler.cs`:
  - 调 `IUserRepository.SearchByUsernamePagedAsync(Search, Page, PageSize, ct)`;
  - 映射 `UserPublicProfileDto[]`;
  - 包 PagedResult 返回。

## 4. Application — `IUserRepository` 扩展

- [x] 4.1 新签名:
  ```csharp
  Task<(IReadOnlyList<User> Users, int Total)> SearchByUsernamePagedAsync(
      string? prefix, int page, int pageSize, CancellationToken cancellationToken);
  ```
  XML 注释:过滤 bot;若 `prefix` 非空按 StartsWith 大小写不敏感;按 Username ASC 排序;Count 在 Skip/Take 之前。

## 5. Infrastructure

- [x] 5.1 `UserRepository.SearchByUsernamePagedAsync`:
  ```csharp
  var baseQuery = _db.Users.Where(u => !u.IsBot);
  if (!string.IsNullOrEmpty(prefix))
  {
      var lower = prefix.ToLowerInvariant();
      baseQuery = baseQuery.Where(u => u.Username.Value.ToLower().StartsWith(lower));
  }
  var total = await baseQuery.CountAsync(ct);
  var users = await baseQuery
      .OrderBy(u => u.Username.Value)
      .Skip((page - 1) * pageSize)
      .Take(pageSize)
      .ToListAsync(ct);
  return (users, total);
  ```
  (SQLite Username 列已有 NOCASE collation,ToLower 可省;保留保险)

## 6. Api

- [x] 6.1 `UsersController`:
  ```csharp
  [HttpGet("{id:guid}")]
  public async Task<ActionResult<UserPublicProfileDto>> GetProfile(Guid id, CancellationToken ct)
  {
      var dto = await _mediator.Send(new GetUserProfileQuery(new UserId(id)), ct);
      return Ok(dto);
  }

  [HttpGet("")]  // search & list
  public async Task<ActionResult<PagedResult<UserPublicProfileDto>>> Search(
      [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
      CancellationToken ct = default)
  {
      var result = await _mediator.Send(new SearchUsersQuery(search, page, pageSize), ct);
      return Ok(result);
  }
  ```
  注意路由:`{id:guid}` 的 `guid` constraint 确保 "me" 不被拦到这个 action。

## 7. 测试

- [x] 7.1 `GetUserProfileQueryHandlerTests`(3):
  - 成功(真人) → DTO 字段齐;DTO 上**没有** `Email` / `PasswordHash` property(反射断言)。
  - 成功(bot) → 也返回,`Username` 形如 `AI_Easy`(印证 proposal 的 non-filter 决定)。
  - 找不到 → `UserNotFoundException`。
- [x] 7.2 `SearchUsersQueryHandlerTests`(4):
  - 空 search → 真人全表 + sort by Username ASC。
  - prefix "Ali" → 只匹配 Username 以 Ali 开头者(不含 bot)。
  - 分页:total=5 / page=2 / pageSize=2 → 返回 2 条。
  - Bot 被过滤:仓储返回包含 bot 的话……handler 依赖仓储过滤。测试只 mock 仓储返回(真人)结果,断 handler 不再叠加过滤;**仓储层**过滤的正确性由其他测试(或 smoke)保证。
- [x] 7.3 `SearchUsersQueryValidatorTests`(5):
  - 默认 Page=1 PageSize=20 + null search → 通过;
  - Page=0 → 失败;
  - PageSize=101 → 失败;
  - Search 空字符串 → 通过(被 handler 当作"无过滤"处理);
  - Search 长 21 字符 → 失败。

## 8. 端到端冒烟

- [x] 8.1 启动 Api,注册 Alice + Bob + Carol。
- [x] 8.2 `GET /api/users/{aliceId}` → 200 + DTO,含 Rating / 战绩 / Username,不含 Email。
- [x] 8.3 `GET /api/users/{不存在 guid}` → 404。
- [x] 8.4 `GET /api/users?search=Ali` → PagedResult;Items 中 Alice 出现,Bob / Carol / 3 bot 都不出现。
- [x] 8.5 `GET /api/users` 不带 search → 所有真人 + 按 Username ASC(Alice, Bob, Carol 顺序);Total == 真人数。
- [x] 8.6 `GET /api/users?pageSize=101` → 400。
- [x] 8.7 `GET /api/users/me` 仍正常(不被 guid route 误拦)。

## 9. 归档前置检查

- [x] 9.1 `dotnet build Gomoku.slnx`:0 警告 0 错。
- [x] 9.2 `dotnet test Gomoku.slnx`:全绿(Domain 230 不变;Application ~112 → 约 124)。
- [x] 9.3 `openspec validate add-public-profile-and-search --strict`:valid。
- [x] 9.4 分支 `feat/add-public-profile-and-search`,按层 Application / Infrastructure / Api / docs 四条 commit。
