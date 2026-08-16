# Tasks — generalize-match-domain

> 判据：**五子棋与一字棋的行为一个字节不变。** 这是一次纯重构 —— 盘面语义从聚合根搬进规则，
> 没有任何棋种的玩法改变。已有的 Domain / Application 测试除了签名适配之外**不该需要改断言**；
> 哪条断言的期望值变了，就说明重构改了行为，停下来看为什么。

> 第二条判据：**做完之后，加象棋不需要再碰 `Room` / `Game` / `Move`。**
> 如果 `add-xiangqi` 还要回来改聚合根，说明这次抽象抽错了地方。

## 1. Domain：值对象

- [x] 1.1 `MoveIntent(Position? From, Position To)`、`PlayedMove(Position? From, Position To, Stone Side)`、`MoveApplication(GameResult Result)`。
- [x] 1.2 `From` 可空的理由写进 doc comment —— 不许用 `From == To` 表示「没有起点」。

## 2. Domain：`IGameRules` 拆分

- [x] 2.1 `IGameRules.Apply(history, intent, side)`；非法走子由规则抛 `InvalidMoveException`。
- [x] 2.2 `CreateBoard()` / `WinLength` 下沉到 `INInARowRules : IGameRules`。
- [x] 2.3 `IsInBounds` 从公开接口移除 —— 它现在是 `Apply` 的内部步骤。（`Position` 的 doc comment 也要跟着改。）
- [x] 2.4 `NInARowRules` 实现 `Apply`：形状校验（`From` 必须为 null）→ 越界 → 重放 → `PlaceStone`。
- [x] 2.5 测试：合法/越界/重复/带 `From` 的落子；无状态（同实例两段历史互不影响）；反射断言 `IGameRules` 上已无 `WinLength` / `CreateBoard`。

## 3. Domain：`Room` / `Game` / `Move`

- [x] 3.1 `Move` 加 `FromRow` / `FromCol`（可空），两者同为空或同为非空。
- [x] 3.2 `Game.RecordMove` 接 `MoveIntent`；`Game.ReplayBoard` **删除** —— 重放属于规则了。
- [x] 3.3 `Room.PlayMove(userId, MoveIntent, now, rules)`：只留房间态 / 玩家 / 回合三道校验，其余交给 `Apply`。
- [x] 3.4 `GameEndReason.Connected5` → `Decided`，底层值保持 `0`。
- [x] 3.5 测试：非玩家 / 非回合时 MUST NOT 调 `Apply`；规则抛异常时聚合状态不变。

## 4. Application / Api

- [x] 4.1 `MakeMoveCommand` 加可空 `FromRow` / `FromCol`；构造 `MoveIntent` 传下去。
- [x] 4.2 **这一条按提案的写法是错的,是 AiSmoke 跑出来的。** 原计划给 `MakeMove` 加两个可选参数,
      但 **SignalR 不套用 C# 的可选参数默认值** —— 三参调用打到五参方法上,服务端直接回
      `InvalidDataException: Invocation provides 3 argument(s) but target expects 5`,
      每一个已发布客户端会当场下不了棋。改成:`MakeMove(roomId, row, col)` **一个字不动**,
      新增 `MovePiece(roomId, fromRow, fromCol, row, col)`,两者分派到同一条命令。
      三层单元测试一条都没发现它 —— 它们都不经过 SignalR 的参数绑定。
- [x] 4.3 `MoveDto` 加可空 `fromRow` / `fromCol`。
- [x] 4.4 AI 路径：n-in-a-row 的工厂改接 `INInARowRules`。

## 5. Infrastructure

- [x] 5.1 `MoveConfiguration` 映射两个可空列。
- [x] 5.2 迁移 `AddMoveOrigin`：两列可空 → 纯增量，`Down` 只丢列。
- [x] 5.3 测试（真 SQLite）：既有 `Moves` 行的 `Ply` / `Row` / `Col` / `Stone` 一字不变，新列为 `NULL`。

## 6. Web

- [x] 6.1 `GameEndReason` 联合类型 `'Connected5'` → `'Decided'`；四处 `switch` 与一个 i18n key 跟着改。
- [x] 6.2 i18n `game.ended.reason-connected-5` → `reason-decided`，中英文案改成游戏中立的说法。
- [x] 6.3 `MoveDto` 类型加两个可选字段。棋盘组件**不用改** —— 五子棋不发起点。

## 7. 验收

- [x] 7.1 `dotnet build` 0 warning、`dotnet test` 全绿；`npm run lint` + `npm run test:ci` 全绿。
- [x] 7.2 **既有测试的断言期望值零改动**（只允许签名适配）。改了就是重构改了行为。
- [x] 7.3 冒烟：AiSmoke **17 项全过**,含一整局 SignalR 对局(黑方连五获胜)。它不知道本次重构
      存在,所以是独立佐证 —— 也正因如此它抓到了 §4.2 那个所有单元测试都漏掉的 bug。
- [x] 7.4 `openspec validate generalize-match-domain --strict`。

## 8. 已知缺口（记录，不在本变更修）

- [ ] 8.1 **前端还没有走子类棋种的客户端。** `MovePiece` 已经在 Hub 上,但 `GameHubService`
      没有包装它 —— 包装一个没有调用方的方法,等于写一段今天验证不了的代码。象棋的棋盘组件
      落地时一起加。
- [ ] 8.2 **`game.ended.reason-decided` 的文案是所有棋种共用的一句**（「棋局本身分出了胜负」）。
      对五子棋来说不如原来的「连成一线」具体。要按棋种给不同措辞,需要每棋种一个 i18n key +
      一个中立兜底 —— 那是显示层的事,留给第一个真正需要它的棋种。
- [ ] 8.3 **`GomokuHub` / `/hubs/gomoku` 仍然叫五子棋。** 本变更把 `GameEndReason` 的棋种专名
      清掉了,但 hub 的名字还在。改它要动 localStorage 之外的五份 web spec 与客户端连接串,
      与大厅泛化一起做更省。
- [ ] 8.4 **`Room` 仍叫 `BlackPlayerId` / `WhitePlayerId`。** 象棋是红黑两方 —— 那是**显示层**
      把黑/白读成红/黑的事,座位结构不用动(见 proposal「不做的事」)。真要改名是纯改名变更。
