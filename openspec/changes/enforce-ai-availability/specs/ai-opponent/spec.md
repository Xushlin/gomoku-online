# ai-opponent Specification Delta

## ADDED Requirements

### Requirement: AI 房只开给真的有 AI 的棋种

`CreateAiRoomCommandValidator` SHALL 校验目标棋种能在 `IGameAiRegistry` 中解析出一个工厂;解析不出则 MUST 校验失败(HTTP **400**),MUST NOT 建房。

判据 MUST 取自 `IGameAiRegistry.For(gameKey)`,MUST NOT 是 `IGameRules` 上一个手写的 `SupportsAi` 布尔。理由与 `IsRated` 当初被约束成不变量是同一条:**一个复述结构性事实的手写布尔是一个判断,而判断会过期且不报错。** 注册表自己就是那个事实,给某个棋种登记 AI 的那一刻,本校验自动放行 —— 没有第二处要记得改。

本条只挂在 **AI 房**路径上。`POST /api/rooms`(真人房)MUST NOT 受它约束:成语接龙开放人人对战,那正是它该有的玩法。

键解析不出**规则**时本条 MUST 静默通过 —— 那种情况由「必须是已登记棋种」报出。同一个字段为同一件事报两条错误,只会让调用方以为要改两处。

**这条规则此前不存在,后果是一个计分漏洞而不只是一个多余的房间。** 实测:`POST /api/rooms/ai { gameKey: "idiom-chain", humanSide: White }` 返回 201,房间进入 `Playing` 且轮到机器人;`AiMoveWorker` 每 1500 ms 抛一次 `RoomNotFoundException`;60 秒后 `TurnTimeoutWorker` 判那个走不了的一方超时告负,而成语接龙 `IsRated == true`,于是真人凭零手棋拿到一场胜利与约 +46 ELO,可无限重复。

`ExecuteBotMoveCommandHandler` 里那句「一个棋种可以先有规则、后有 AI」的注释说的正是这个局面,却把它与「房间指向一个本构建不认识的棋种」归为同一种失败。两者不同:后者是不该写进库的脏数据,前者是平台**当前就成立**的正常状态。把可达状态当成数据损坏处理,结果就是它只被一个后台 worker 永远地记在日志里。

#### Scenario: 没有 AI 的棋种被拒
- **WHEN** `POST /api/rooms/ai` 指定 `gameKey: "idiom-chain"`
- **THEN** HTTP 400,错误点名 `GameKey` 字段;MUST NOT 建出房间

#### Scenario: 有 AI 的棋种照常
- **WHEN** `POST /api/rooms/ai` 指定 `gomoku` / `tictactoe` / `xiangqi`
- **THEN** 建房成功 —— 三者都有已登记的工厂

#### Scenario: 真人房不受影响
- **WHEN** `POST /api/rooms` 指定 `gameKey: "idiom-chain"`
- **THEN** 建房成功

#### Scenario: 未登记的键只报一条错
- **WHEN** `POST /api/rooms/ai` 指定一个既无规则也无 AI 的键
- **THEN** `GameKey` 上恰好一条错误

#### Scenario: 判定遍历 AI 注册表
- **WHEN** 走遍 `IGameRulesRegistry` 中每个棋种,对每个都试建 AI 房
- **THEN** 放行与否 MUST 与 `IGameAiRegistry.For(key) is not null` 逐一相符,且两种结果 MUST 都出现过 —— 只走到一边的遍历会全绿地什么都不验
