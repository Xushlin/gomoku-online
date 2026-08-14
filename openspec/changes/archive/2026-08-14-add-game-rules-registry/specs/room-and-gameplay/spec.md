## ADDED Requirements

### Requirement: `Room` 记录自己是哪一种棋

`Room` SHALL 持有 `GameKey`(非空字符串),标识该房间玩的是哪个棋种。既有房间一律为 `'gomoku'`。

`GameKey` MUST 是字符串而非枚举 —— 新增棋种的全部意义就在于不必修改一个共享类型,与游戏目录、`IPuzzleRules` 注册表的选择一致。

本变更中创建房间的路径**不接受调用方指定棋种** —— handler 一律写入 `'gomoku'`,
所以"未注册的键"此刻无从产生。等到 `add-tictactoe` 让调用方能选棋种时,那条路径 MUST
先校验键已登记再建房。

落子路径 SHALL 在解析规则失败时返回 404 —— 那是"房间的 `GameKey` 指向一个本构建不认识的
棋种"的唯一可能来源(手工改过的数据,或降级过的构建)。

#### Scenario: 既有房间是五子棋
- **WHEN** 读取迁移前创建的任意房间
- **THEN** `GameKey == "gomoku"`

#### Scenario: 新建房间写入已登记的棋种
- **WHEN** 通过 `CreateRoom` 或 `CreateAiRoom` 建房
- **THEN** `GameKey == "gomoku"`,且该键能在规则注册表中解析出规则

#### Scenario: 房间指向未知棋种时落子返回 404
- **WHEN** 某房间的 `GameKey` 在注册表中不存在,玩家尝试落子
- **THEN** handler 返回 404,MUST NOT 抛未处理异常

### Requirement: 落子入参校验只管与棋种无关的那一半

落子入参的校验 SHALL 只在应用层校验器里保留**与棋种无关**的那一半:行列非负,违反返回 400。

上界属于棋种,而校验器跑在解析房间(因而也是棋种)之前,所以超界 SHALL 由 `Room.PlayMove` 经 `IGameRules.IsInBounds` 判定,抛 `InvalidMoveException`,映射为 **409**。

这是相对本变更之前的一处**有意的状态码变更**:`(20, 20)` 这类坐标此前返回 400。改后更准确 —— 它是一个格式良好的请求,只是在五子棋里不合法而在假想的 21×21 棋种里合法,那属于"这一步在本局不合规"而非"请求有语法错"。Web 客户端只渲染实际存在的格子,因此触及不到这条路径。

#### Scenario: 负坐标仍是 400
- **WHEN** 提交 `row = -1`
- **THEN** 校验器拒绝,返回 400

#### Scenario: 超出棋种上界是 409
- **WHEN** 在五子棋房间提交 `row = 20`
- **THEN** `Room.PlayMove` 抛 `InvalidMoveException`,返回 409,`Move` 未被 append

## MODIFIED Requirements

### Requirement: `Room.PlayMove` 以原子事务落子、判胜并推进状态

系统 SHALL 提供 `Room.PlayMove(UserId userId, Position position, DateTime now, IGameRules rules)`,按顺序:

1. `Status != Playing` → MUST 抛 `RoomNotInPlayException`
2. `userId != BlackPlayerId && userId != WhitePlayerId` → MUST 抛 `NotAPlayerException`
3. 根据 `userId` 推断棋色 `Stone`,若不等于 `Game.CurrentTurn` → MUST 抛 `NotYourTurnException`
4. `rules.IsInBounds(position)` 为假 → MUST 抛 `InvalidMoveException`
5. 调 `Board.PlaceStone(new Move(position, stone))`,棋盘由 `rules.CreateBoard()` 经 replay 得到(重复落子判定仍由 `Board` 负责)
6. Append 一条 `Move` 子实体:`Ply = 上一 Ply + 1`、`Position`、`Stone`、`PlayedAt = now`
7. `Game.CurrentTurn = oppositeColor(stone)`
8. 若 Board 返回的 `GameResult != Ongoing`:
   - `Game.FinishWith(result, winnerUserId, GameEndReason.Connected5, now)`
   - `Room.Status = Finished`
9. 返回的 `MoveOutcome` MUST 包含新 `Move` 实体引用与 `GameResult`,供调用方决定发哪些事件。

规则 MUST 由调用方**作为参数传入**,MUST NOT 由聚合自行解析注册表 —— `Domain` 因此保持零外部依赖,`Room` 也仍然是其入参的纯函数,不需要一个注册表才能在测试里构造。调用方 SHALL 依据 `room.GameKey` 解析规则。

#### Scenario: 最后一子连五
- **WHEN** 黑方已在 `(7,3)..(7,6)` 连四,调 `PlayMove(aliceId, (7,7), now, gomokuRules)`
- **THEN** 返回 `GameResult.BlackWin`;`Game.EndedAt == now`;`Game.WinnerUserId == aliceId`;`Game.EndReason == Connected5`;`Room.Status == Finished`

#### Scenario: 合法落子且未连五
- **WHEN** `Playing` 状态,轮到 Alice(黑方),调 `PlayMove(aliceId, (7,7), now, gomokuRules)`,棋盘此前为空
- **THEN** 返回 `GameResult.Ongoing`;`Game.Moves` 新增一条 `Ply=1` 的 Move;`Game.CurrentTurn == White`;`Room.Status == Playing`;`Game.EndReason == null`

#### Scenario: 非玩家尝试落子
- **WHEN** 围观者或非成员调 `PlayMove`
- **THEN** 抛 `NotAPlayerException`

#### Scenario: 不是你的回合
- **WHEN** 白方在 `CurrentTurn == Black` 时调 `PlayMove`
- **THEN** 抛 `NotYourTurnException`

#### Scenario: 非 `Playing` 状态
- **WHEN** `Status == Waiting` 或 `Finished`,调 `PlayMove`
- **THEN** 抛 `RoomNotInPlayException`

#### Scenario: 超出该棋种边界
- **WHEN** 在五子棋房间以 `(15, 0)` 落子
- **THEN** 抛 `InvalidMoveException`;`Move` 未被 append,状态保持不变

#### Scenario: 底层棋盘规则违反
- **WHEN** 正确玩家在正确回合,但该位置已有子
- **THEN** `Board` 抛 `InvalidMoveException`,`Room` MUST 让其原样冒泡;`Move` 未被 append,状态保持不变

### Requirement: 从 `Moves` 在内存 replay 得到当前 `Board`

`Game` MUST NOT 冗余存储盘面。需要当前 `Board` 时,SHALL 由 `Game.ReplayBoard(IGameRules rules)` 从 `Moves` 按 `Ply` 升序重放得到 —— 棋盘的尺寸与连子长度来自传入的规则,因此同一段落子序列在不同棋种下重放出对应尺寸的棋盘。

规则同样 MUST 由调用方传入,理由与 `PlayMove` 一致。

#### Scenario: replay 还原盘面
- **WHEN** `Game.Moves` 含 10 步,调 `ReplayBoard(gomokuRules)`
- **THEN** 返回的 `Board` 上这 10 个位置的 `Stone` 与 `Moves` 一致,其余为 `Empty`

#### Scenario: replay 尺寸随规则
- **WHEN** 以五子棋规则重放
- **THEN** 得到 15×15 的棋盘
