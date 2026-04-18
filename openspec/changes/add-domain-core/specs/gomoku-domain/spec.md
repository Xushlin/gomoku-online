## ADDED Requirements

### Requirement: 棋盘尺寸是 15×15 的 `Position` 坐标系

系统 SHALL 以行 `Row` 和列 `Col` 表示棋盘坐标,两者皆为 0 至 14(含)的整数。`Position` 是不可变值对象,构造时 MUST 对行列范围进行校验;超出范围 MUST 抛出 `InvalidMoveException`。

#### Scenario: 合法坐标
- **WHEN** 以 `(Row=0, Col=0)`、`(Row=14, Col=14)`、`(Row=7, Col=7)` 等在 `[0..14]` 范围内的值构造 `Position`
- **THEN** 返回有效的 `Position` 值对象,`Row` / `Col` 与入参一致

#### Scenario: 行越界
- **WHEN** 以 `Row = -1` 或 `Row = 15` 构造 `Position`
- **THEN** 抛出 `InvalidMoveException`,异常消息 MUST 指出是哪个维度越界以及传入的值

#### Scenario: 列越界
- **WHEN** 以 `Col = -1` 或 `Col = 15` 构造 `Position`
- **THEN** 抛出 `InvalidMoveException`,异常消息 MUST 指出是哪个维度越界以及传入的值

#### Scenario: 值相等
- **WHEN** 两个 `Position` 的 `Row` 与 `Col` 都相等
- **THEN** `==`、`.Equals()`、`.GetHashCode()` 都 MUST 认定它们相等

---

### Requirement: `Stone` 枚举有三种状态,`Empty` 是默认值

系统 SHALL 定义 `Stone` 枚举,取值仅包含 `Empty`、`Black`、`White`。`Empty` 的底层值 MUST 为 `0`,以便未初始化的棋盘格自然为空。

#### Scenario: Empty 为默认
- **WHEN** 声明一个 `Stone` 变量但未赋值,或读取新建 `Board` 中任意未落子位置
- **THEN** 其值为 `Stone.Empty`

---

### Requirement: `Move` 是 `Position` + 非空 `Stone` 的不可变值对象

系统 SHALL 用 `Move` 表示一次落子,包含落点 `Position` 与棋色 `Stone`。`Move` MUST 拒绝 `Stone.Empty` 作为棋色;构造时如传入 `Stone.Empty`,MUST 抛出 `InvalidMoveException`。

#### Scenario: 合法落子
- **WHEN** 以 `Position(7, 7)` 与 `Stone.Black` 构造 `Move`
- **THEN** 返回有效 `Move`,其 `Position` 与 `Stone` 可被正确读取

#### Scenario: Stone 为 Empty
- **WHEN** 以 `Stone.Empty` 构造 `Move`
- **THEN** 抛出 `InvalidMoveException`,异常消息指明"落子棋色不能为 Empty"

---

### Requirement: `Board` 维护 15×15 的 `Stone` 网格

系统 SHALL 提供 `Board` 实体,内部维护一个 15×15 的 `Stone` 网格。新建的 `Board` 中所有位置 MUST 为 `Stone.Empty`。`Board` SHALL 支持按 `Position` 查询该位置的 `Stone`。

#### Scenario: 新建棋盘全为空
- **WHEN** 调用 `new Board()`
- **THEN** 对任意合法 `Position`,查询结果 MUST 是 `Stone.Empty`

#### Scenario: 查询越界位置
- **WHEN** 用 `Row` 或 `Col` 不在 `[0..14]` 的 `Position` 查询 `Board`
- **THEN** 抛出 `InvalidMoveException`(也可能是 `Position` 本身构造时就已抛出 —— 行为等效即可)

---

### Requirement: `Board.PlaceStone` 原子化地放子、判胜并返回结果

系统 SHALL 提供 `Board.PlaceStone(Move move)` 方法。该方法 MUST 原子化地完成:(a) 校验合法性;(b) 将 `move.Stone` 写入 `move.Position`;(c) 判定当前棋局是否结束;(d) 返回 `GameResult`。当 `move` 指向的格子已经有棋子时,MUST 抛出 `InvalidMoveException`,且**棋盘状态不得改变**。

#### Scenario: 合法落子且未形成五连
- **WHEN** 对空棋盘执行 `PlaceStone(Move((7,7), Black))`
- **THEN** 返回 `GameResult.Ongoing`;`Board` 在 `(7,7)` 处为 `Stone.Black`,其他位置仍为 `Stone.Empty`

#### Scenario: 落子到已有棋子的位置
- **WHEN** 在 `(7,7)` 放黑子后,再对同一位置执行 `PlaceStone(Move((7,7), White))`
- **THEN** 抛出 `InvalidMoveException`,且 `(7,7)` 处仍为 `Stone.Black` 未被覆盖

#### Scenario: 落子位置越界
- **WHEN** 调用 `PlaceStone` 时 `Move.Position` 越界
- **THEN** 抛出 `InvalidMoveException`,棋盘无任何格子发生变化

---

### Requirement: 同色棋子连成 5 颗或以上即获胜(基础规则,长连算赢)

系统 SHALL 在每次 `PlaceStone` 之后,沿**水平、垂直、主对角、反对角**四个方向检测通过该落子的最长同色连续段。若该长度 ≥ 5,对应棋色获胜,返回 `GameResult.BlackWin` 或 `GameResult.WhiteWin`。

#### Scenario: 水平方向连五
- **WHEN** 依次放黑子于 `(7,3) (7,4) (7,5) (7,6)`,再放黑子于 `(7,7)`
- **THEN** 最后一步返回 `GameResult.BlackWin`

#### Scenario: 垂直方向连五
- **WHEN** 依次放白子于 `(3,7) (4,7) (5,7) (6,7)`,再放白子于 `(7,7)`
- **THEN** 最后一步返回 `GameResult.WhiteWin`

#### Scenario: 主对角线(↘)方向连五
- **WHEN** 依次放黑子于 `(3,3) (4,4) (5,5) (6,6)`,再放黑子于 `(7,7)`
- **THEN** 最后一步返回 `GameResult.BlackWin`

#### Scenario: 反对角线(↗)方向连五
- **WHEN** 依次放白子于 `(7,3) (6,4) (5,5) (4,6)`,再放白子于 `(3,7)`
- **THEN** 最后一步返回 `GameResult.WhiteWin`

#### Scenario: 连子超过 5(长连)也判胜
- **WHEN** 某方形成连续 6 颗同色子(例如黑子占据 `(7,2)..(7,7)`)
- **THEN** 形成该长连的最后一步 MUST 返回该方获胜的 `GameResult`

#### Scenario: 在棋盘边缘形成连五
- **WHEN** 依次放黑子于 `(0,0) (0,1) (0,2) (0,3) (0,4)`
- **THEN** 最后一步返回 `GameResult.BlackWin`

#### Scenario: 在棋盘另一条边形成连五
- **WHEN** 依次放白子于 `(14,10) (14,11) (14,12) (14,13) (14,14)`
- **THEN** 最后一步返回 `GameResult.WhiteWin`

#### Scenario: 四子而已,尚未连五
- **WHEN** 黑子形成 `(7,3) (7,4) (7,5) (7,6)` 但第 5 子未落
- **THEN** 每一步都返回 `GameResult.Ongoing`

#### Scenario: 被对方子打断的"四连"不算胜
- **WHEN** 棋盘上出现 `(7,3)=Black (7,4)=Black (7,5)=White (7,6)=Black (7,7)=Black`
- **THEN** 放下 `(7,7)` 那一步 MUST 返回 `GameResult.Ongoing`

---

### Requirement: 棋盘下满且无人连五时判定为平局

系统 SHALL 在 `PlaceStone` 返回前,若棋盘所有 225 个位置都已被占据且无一方达成五连,则返回 `GameResult.Draw`。

#### Scenario: 最后一子下满且无人赢
- **WHEN** 棋盘 224 格已占据,第 225 子落下后仍无任何方向 ≥ 5
- **THEN** 返回 `GameResult.Draw`

#### Scenario: 下满之前的步骤
- **WHEN** 棋盘上仍有至少一个 `Stone.Empty` 位置,且最新落子未连五
- **THEN** 返回 `GameResult.Ongoing`

---

### Requirement: `Board.Clone()` 返回完全独立的副本

系统 SHALL 提供 `Board.Clone()` 方法,返回一个与源棋盘**状态一致但内存独立**的新 `Board`。副本上的任何 `PlaceStone` 操作 MUST 不影响原棋盘;反之亦然。此方法是供 AI 搜索等"试走"场景使用的。

#### Scenario: 副本初始状态一致
- **WHEN** 在已放若干子的棋盘上调用 `Clone()`
- **THEN** 副本在每个位置的 `Stone` 与原盘一致

#### Scenario: 副本改动不影响原盘
- **WHEN** 克隆后在副本上调用 `PlaceStone`
- **THEN** 原盘上对应位置仍保持克隆时刻的状态

#### Scenario: 原盘改动不影响副本
- **WHEN** 克隆后在原盘上调用 `PlaceStone`
- **THEN** 副本上对应位置仍保持克隆时刻的状态

---

### Requirement: `Board.Reset()` 把棋盘恢复为初始空盘

系统 SHALL 提供 `Board.Reset()` 方法,调用后棋盘所有位置 MUST 回到 `Stone.Empty`,并可再次从头开始对局。

#### Scenario: Reset 后查询
- **WHEN** 在已落若干子的棋盘上调用 `Reset()`,然后查询任意位置
- **THEN** 所有位置都返回 `Stone.Empty`

#### Scenario: Reset 后可重新落子
- **WHEN** `Reset()` 后对 `(7,7)` 执行 `PlaceStone(Move((7,7), Black))`
- **THEN** 落子成功,返回 `GameResult.Ongoing`

---

### Requirement: `GameResult` 枚举有四种状态

系统 SHALL 定义 `GameResult` 枚举,取值仅包含 `Ongoing`、`BlackWin`、`WhiteWin`、`Draw`。`Board.PlaceStone` 的返回值 MUST 只取其中之一。

#### Scenario: 未结束
- **WHEN** 落子未连五且棋盘未满
- **THEN** 返回 `GameResult.Ongoing`

#### Scenario: 黑方胜
- **WHEN** 落子导致黑子连五或长连
- **THEN** 返回 `GameResult.BlackWin`

#### Scenario: 白方胜
- **WHEN** 落子导致白子连五或长连
- **THEN** 返回 `GameResult.WhiteWin`

#### Scenario: 平局
- **WHEN** 最后一格落下仍无人连五
- **THEN** 返回 `GameResult.Draw`

---

### Requirement: 非法落子通过 `InvalidMoveException` 抛出

系统 SHALL 用 `InvalidMoveException`(继承自 `System.Exception`)承载所有领域级非法落子错误,至少覆盖:位置越界、落子到已有棋子的格子、以 `Stone.Empty` 构造 `Move`。异常消息 MUST 明确原因。Domain 层 MUST NOT 用返回 `bool` 或 `null` 的方式表达这些错误。

#### Scenario: 异常类型
- **WHEN** 触发任意非法落子场景
- **THEN** 抛出的异常类型 MUST 是 `InvalidMoveException`

#### Scenario: 异常信息可读
- **WHEN** 因位置已有棋子而失败
- **THEN** 异常消息 MUST 包含冲突的位置坐标,便于上层展示与日志定位

---

### Requirement: Domain 项目零外部 NuGet 依赖

`Gomoku.Domain.csproj` MUST NOT 引用任何第三方 NuGet 包,也 MUST NOT 引用其他项目。`Gomoku.Domain` 只能依赖 .NET 基类库。

#### Scenario: 依赖检查
- **WHEN** 审阅 `Gomoku.Domain.csproj`
- **THEN** `<PackageReference>` 与 `<ProjectReference>` 节点数量 MUST 为零

---

### Requirement: `PlaceStone` 的异常仅用于保护不变量,不得作为常规流程控制

`InvalidMoveException` MUST 仅在调用方违反 Domain 不变量时抛出(越界、重复落子、空色落子)。调用方 MUST 在调用 `PlaceStone` 之前自行校验合法性,不得将异常当作"落子是否合法"的查询手段。`Board` 的公共 API MUST 在 XML 注释中明确说明这一约定,以便上层(Application、AI、SignalR hub)遵循。

#### Scenario: API 文档包含约定
- **WHEN** 审阅 `Board.PlaceStone` 的 XML 注释
- **THEN** 注释 MUST 指出"调用方需先确保位置合法",以及异常仅用于保护不变量

#### Scenario: AI 搜索遵循该约定
- **WHEN** AI 枚举候选走法
- **THEN** AI MUST 从已知的空格集合选择候选走法,而非对每个 `(row, col)` 尝试 `PlaceStone` 并捕获异常
