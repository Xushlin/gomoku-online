## Why

项目骨架已就位,但 `Gomoku.Domain` 项目只有一个占位 `Class1.cs`,没有任何业务逻辑。任何后续工作 —— Application 层的 CQRS handler、Infrastructure 的持久化、Api 的 SignalR hub、前端的棋盘组件 —— 都需要一个稳定、已被测试覆盖的领域核心作为基础。先把领域核心做扎实,可以避免上层在对"什么是合法落子""什么时候算赢"这类问题反复改写时被动返工。

## What Changes

- 在 `Gomoku.Domain` 中新增:
  - `Enums/Stone.cs` — 棋子枚举(`Empty` / `Black` / `White`)
  - `ValueObjects/Position.cs` — 坐标值对象,行/列 0–14,构造时强制校验
  - `ValueObjects/Move.cs` — 一次落子(`Position` + `Stone`),`Stone` 不能为 `Empty`
  - `ValueObjects/GameResult.cs` — 对局状态(`Ongoing` / `BlackWin` / `WhiteWin` / `Draw`)
  - `Entities/Board.cs` — 15×15 棋盘聚合实体,提供 `PlaceStone` / 查询 / 判胜 / `Clone` / `Reset`
  - `Exceptions/InvalidMoveException.cs` — 领域异常(越界、重复落子、空 `Stone` 落子等)
- 在 `Gomoku.Domain.Tests` 中新增全面的 xUnit 测试,覆盖判胜的边界与正常路径、非法落子、平局、克隆独立性等
- 基础五子棋规则:达成**恰好或超过**五连即获胜(长连算赢);禁手规则本次不实现,留给后续变更
- 仍然遵守铁律:Domain 层零外部 NuGet 依赖,零数据库/IO/UI 痕迹

不是这次变更的范围(显式列出以便后续变更引用):
- 禁手规则(三三 / 四四 / 长连禁手)
- 对局计时、悔棋、认输
- 棋手身份、用户、房间、聊天
- ELO 积分
- 持久化与棋谱回放

## Capabilities

### New Capabilities

- `gomoku-domain`: 覆盖棋盘表达、落子合法性、五连判胜、对局结果这一组核心不变量。所有上层(Application / Api / 前端)对"一步棋是否合法、当前是否已分胜负"的判断,最终都通过这个能力来回答。

### Modified Capabilities

(无 —— 这是第一个规范。)

## Impact

- **代码**:新增 6 个源文件于 `backend/src/Gomoku.Domain/` 及其子目录,新增若干测试类于 `backend/tests/Gomoku.Domain.Tests/`。`Class1.cs` 占位文件一并删除。
- **依赖**:不新增任何 NuGet 引用。测试项目已有的 xUnit 足够;`FluentAssertions` 若尚未添加,在 tasks 里补。
- **API / SignalR / 数据库**:零影响 —— 本变更不触达 Application / Infrastructure / Api 任何一层。
- **后续变更将依赖**:`add-application-core`(CQRS handler 将消费 `Board` 与 `InvalidMoveException`)、`add-game-persistence`(棋谱记录)、`add-ai-opponent`(AI 在内存里复用 `Board`)。
