## MODIFIED Requirements

### Requirement: `CreateAiRoomCommand` 一步创建房间并让机器人加入

Application 层 SHALL 新增:

```
public sealed record CreateAiRoomCommand(
    UserId HostUserId, string Name, BotDifficulty Difficulty, Stone HumanSide)
    : IRequest<RoomStateDto>;
```

Handler 流程 MUST 按顺序:

1. `FindByIdAsync(HostUserId)` 加载 Host;未找到抛 `UserNotFoundException`。
2. **断言 `host.IsBot == false`**;若为 true 抛 `ValidationException`("AI cannot host an AI room.")。
3. 按 `BotAccountIds.For(Difficulty)` 定位 bot UserId;`FindByIdAsync` 加载;未找到抛 `UserNotFoundException`(提示检查 migration seed)。
4. `Room.Create(new RoomId(Guid.NewGuid()), Name, HostUserId, _clock.UtcNow)`。
5. `room.JoinAsPlayer(botUserId, _clock.UtcNow)` —— 状态从 Waiting 进 Playing,`Game` 实例化。
6. **如果 `HumanSide == Stone.White`,调 `room.SwapPlayers(_clock.UtcNow)`**(本次新增)。结果:`BlackPlayerId == botUserId`,`WhitePlayerId == HostUserId`,host 仍是真人;`Game.CurrentTurn` 仍是 Black,即立刻轮到 bot 走第 1 步。如果 `HumanSide == Stone.Black`,跳过这步(默认行为)。
7. `IRoomRepository.AddAsync(room, ct)`。
8. `IUnitOfWork.SaveChangesAsync(ct)` —— **一次**事务内提交房间、Game 与潜在的 swap。
9. 拉 username 字典(Host + Bot),用 `room.ToState(usernames)` 组装 `RoomStateDto` 返回。

Validator(FluentValidation)独立校验 `Name` 规则(和现有 `CreateRoomCommandValidator` 一致:3–50 非空白)。`Difficulty` 与 `HumanSide` 由 enum 类型系统保证。`HumanSide` MUST 仅接受 `Stone.Black` 或 `Stone.White`(`Stone.Empty` 抛 ValidationException)。

#### Scenario: 成功创建 AI 房间(默认 humanSide=Black)
- **WHEN** 真人 Alice 发 `CreateAiRoomCommand(alice, "quick practice", Medium, Stone.Black)`
- **THEN** 返回 `RoomStateDto`,`Status == Playing`,`BlackPlayerId == alice`,`WhitePlayerId == BotAccountIds.Medium`,`Game.CurrentTurn == Black`,`Game.Moves` 空

#### Scenario: 真人选 White 反转座位
- **WHEN** 真人 Alice 发 `CreateAiRoomCommand(alice, "defense practice", Medium, Stone.White)`
- **THEN** 返回 `RoomStateDto`,`Status == Playing`,`BlackPlayerId == BotAccountIds.Medium`,`WhitePlayerId == alice`,`HostUserId == alice`,`Game.CurrentTurn == Black`(轮到 bot 先走),`Game.Moves` 空;后续 AI worker 轮询会触发 bot 的第 1 步

#### Scenario: 机器人不存在(migration 未应用)
- **WHEN** 库里不存在 `BotAccountIds.Easy` 对应 User,调 `CreateAiRoomCommand(alice, "x", Easy, Stone.Black)`
- **THEN** 抛 `UserNotFoundException`

#### Scenario: AI-vs-AI 被拒
- **WHEN** 某调用方传入 `HostUserId = BotAccountIds.Easy`(即以机器人身份 Host)
- **THEN** 抛 `ValidationException`

#### Scenario: HumanSide=Empty 被拒
- **WHEN** 调 `CreateAiRoomCommand(alice, "x", Easy, Stone.Empty)`
- **THEN** 抛 `ValidationException`(Empty 不是合法落座选择)

---

### Requirement: `POST /api/rooms/ai` 端点暴露 AI 房间创建

Api 层 SHALL 暴露 `POST /api/rooms/ai`(`[Authorize]`),请求 body `CreateAiRoomRequest { Name: string, Difficulty: BotDifficulty, HumanSide?: Stone }`(JSON 中 `Difficulty` 和 `HumanSide` 接受 `"Easy"` / `"Medium"` / `"Hard"` 与 `"Black"` / `"White"` 字符串,由 `JsonStringEnumConverter` 转换)。`HumanSide` **MUST 是可选字段**(`Stone?` 可空):缺省 / null 时 controller 默认填 `Stone.Black`(向后兼容,旧客户端继续工作)。行为:

- Controller 从 JWT `sub` 取 `UserId host`;
- 把 `request.HumanSide ?? Stone.Black` 当作 effective side;
- 发 `CreateAiRoomCommand(host, request.Name, request.Difficulty, effectiveSide)`;
- 成功 201 Created,`Location = /api/rooms/{id}`,body `RoomStateDto`。

MUST NOT 接受 URL 参数或 query string;MUST NOT 对未 `[Authorize]` 的调用放行。

#### Scenario: 成功(默认 Black)
- **WHEN** 登录用户 `POST /api/rooms/ai { name: "test", difficulty: "Easy" }`(不传 humanSide)
- **THEN** HTTP 201,响应体是 `RoomStateDto`,`BlackPlayerId == 调用方 userId`,`WhitePlayerId == BotAccountIds.Easy`,`Status == Playing`

#### Scenario: 成功(显式 humanSide=White)
- **WHEN** 登录用户 `POST /api/rooms/ai { name: "test", difficulty: "Easy", humanSide: "White" }`
- **THEN** HTTP 201,`BlackPlayerId == BotAccountIds.Easy`,`WhitePlayerId == 调用方 userId`,`HostUserId == 调用方 userId`

#### Scenario: 名字非法
- **WHEN** `name == ""`
- **THEN** HTTP 400(validator 拒绝)

#### Scenario: humanSide 字符串非法
- **WHEN** `POST /api/rooms/ai { ..., humanSide: "Empty" }` 或其它非 Black/White 值
- **THEN** HTTP 400(validator 拒绝;`Stone.Empty` 不允许)

#### Scenario: 未登录
- **WHEN** 不带 Bearer token 请求
- **THEN** HTTP 401
