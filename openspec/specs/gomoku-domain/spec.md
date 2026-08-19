# Gomoku Domain

## Purpose

五子棋核心领域能力:棋盘表达、落子合法性、五连判胜、对局结果。所有上层(Application / Api / 前端)对"一步棋是否合法、当前是否已分胜负"的判断,最终都通过这个能力来回答。

实现位于 `backend/src/Gewu.Domain/`,遵守 Clean Architecture 最内层铁律:零外部 NuGet 依赖、全同步(无 `async`/`Task`/`.Result`/`.Wait()`)、不与持久化/网络/UI 发生关系。
## Requirements
### Requirement: 棋盘尺寸是 15×15 的 `Position` 坐标系

系统 SHALL 以行 `Row` 和列 `Col` 表示棋盘坐标,两者皆为非负整数。`Position` 是不可变值对象。

构造时 MUST 拒绝**负**坐标并抛出 `InvalidMoveException` —— 负的行列在任何棋盘上都无意义。

构造时 MUST NOT 再校验上界:上界取决于棋种(五子棋 15×15、一字棋 3×3),因此 SHALL 由 `IGameRules.IsInBounds` 判定,并在 `Room.PlayMove` 触碰棋盘之前执行。

越界仍 SHALL 抛出 `InvalidMoveException`,异常类型保持不变 —— 因此 HTTP 409 的对外契约不动,变的只是抛出它的那一行。

#### Scenario: 合法坐标
- **WHEN** 以 `(Row=0, Col=0)`、`(Row=14, Col=14)`、`(Row=7, Col=7)` 等非负值构造 `Position`
- **THEN** 返回有效的 `Position` 值对象,`Row` / `Col` 与入参一致

#### Scenario: 负坐标在构造时即被拒绝
- **WHEN** 以 `Row = -1` 或 `Col = -1` 构造 `Position`
- **THEN** 抛出 `InvalidMoveException`,异常消息 MUST 指出是哪个维度以及传入的值

#### Scenario: 超出棋种上界由规则拒绝
- **WHEN** 在五子棋房间以 `(Row=15, Col=0)` 落子
- **THEN** `Room.PlayMove` 在触碰棋盘前抛出 `InvalidMoveException`,棋盘状态不变

#### Scenario: 同一坐标在不同棋种下界限不同
- **WHEN** 以 `(Row=5, Col=5)` 分别询问 15×15 与 3×3 的规则
- **THEN** 前者判定在界内,后者判定越界

#### Scenario: 值相等
- **WHEN** 两个 `Position` 的 `Row` 与 `Col` 都相等
- **THEN** `==`、`.Equals()`、`.GetHashCode()` 都 MUST 认定它们相等

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

系统 SHALL 提供 `Board` 实体,内部维护一个 `Stone` 网格。棋盘的**行数、列数与连子长度是构造参数**,不再是编译期常量 —— 这三个数是棋种属性,由 `IGameRules` 提供。

新建的 `Board` 中所有位置 MUST 为 `Stone.Empty`。`Board` SHALL 支持按 `Position` 查询该位置的 `Stone`。

五子棋 SHALL 由 `(rows: 15, cols: 15, winLength: 5)` 构造 —— 与本要求原先写死的常量完全一致,因此既有对局的行为逐位不变。

#### Scenario: 新建棋盘全为空
- **WHEN** 以任意合法尺寸构造 `Board`
- **THEN** 对任意界内 `Position`,查询结果 MUST 是 `Stone.Empty`

#### Scenario: 五子棋仍是 15×15 连五
- **WHEN** 以五子棋规则构造棋盘
- **THEN** `Rows == 15`、`Cols == 15`、`WinLength == 5`

#### Scenario: 查询越界位置
- **WHEN** 用超出该棋盘行列范围的 `Position` 查询 `Board`
- **THEN** 抛出 `InvalidMoveException`

#### Scenario: 非方形棋盘也成立
- **WHEN** 以 `(rows: 3, cols: 5, winLength: 3)` 构造棋盘并在 `(2, 4)` 落子
- **THEN** 落子成功 —— 索引换算 MUST 用列数而非"边长",方形假设不得残留

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

系统 SHALL 在每次落子后,以该落子为中心沿水平、竖直、主对角、反对角四个方向做增量判胜:任一方向上同色连续子数(含中心)达到该棋盘的 `WinLength` 即判该色获胜。

连子长度 SHALL 取自棋盘构造参数而非常量 `5`。五子棋为 5,一字棋为 3。超过 `WinLength` 的长连仍然算赢(不实现禁手)。

判出胜负时返回 `GameResult.Decided`。**赢的是哪一方 MUST 由调用方从 `move.Stone` 得知**,而 MUST NOT 从返回值读 —— 见 `GameResult` 那条。

#### Scenario: 横向连五获胜
- **WHEN** 黑子在同一行连续占据 5 个相邻位置
- **THEN** 最后一手返回 `GameResult.Decided`

#### Scenario: 竖向连五获胜
- **WHEN** 白子在同一列连续占据 5 个相邻位置
- **THEN** 最后一手返回 `GameResult.Decided`

#### Scenario: 主对角连五获胜
- **WHEN** 黑子沿 ↘ 方向连续占据 5 个位置
- **THEN** 最后一手返回 `GameResult.Decided`

#### Scenario: 反对角连五获胜
- **WHEN** 白子沿 ↗ 方向连续占据 5 个位置
- **THEN** 最后一手返回 `GameResult.Decided`

#### Scenario: 长连算赢
- **WHEN** 黑子连成 6 子
- **THEN** 判 `GameResult.Decided`,MUST NOT 因超长而判负或判无效

#### Scenario: 四子不算赢
- **WHEN** 黑子任一方向最多连成 4 子
- **THEN** 返回 `GameResult.Ongoing`

#### Scenario: 连子长度随棋种变化
- **WHEN** 在 `winLength = 3` 的棋盘上黑子连成 3 子
- **THEN** 判 `GameResult.Decided`;同样的 3 子在 `winLength = 5` 的棋盘上返回 `GameResult.Ongoing`

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

`Gewu.Domain.csproj` MUST NOT 引用任何第三方 NuGet 包,也 MUST NOT 引用其他项目。`Gewu.Domain` 只能依赖 .NET 基类库。

#### Scenario: 依赖检查
- **WHEN** 审阅 `Gewu.Domain.csproj`
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

### Requirement: `GameResult` 枚举有三种状态

系统 SHALL 定义 `GameResult` 枚举,取值仅包含 `Ongoing`、`Decided`、`Draw`。`Board.PlaceStone` 的返回值 MUST 只取其中之一。

底层值 MUST 为 `Ongoing = 0`、`Decided = 1`、`Draw = 3`。`Draw` 保持 `3` 是为了让历史数据只需要重映射一个值(`2 → 1`);`1` 复用给 `Decided` 是因为落子类棋种里先手赢占绝大多数,重映射的行数因此最少。

**MUST NOT 存在带颜色的胜负取值。** `Board.PlaceStone(move)` 已经被告知 `move.Stone`,而落子类棋种里落子的一方不可能因为落子而输 —— 所以返回值里的颜色恒等于入参里的颜色,是同一个事实的第二份。哪一方赢由**调用方**从它自己刚走的那一步得知,规则层则通过 `MoveApplication.WinnerSeat` 向上说明。

#### Scenario: 未结束
- **WHEN** 落子未连五且棋盘未满
- **THEN** 返回 `GameResult.Ongoing`

#### Scenario: 落子的一方连五
- **WHEN** 落子导致该色连五或长连
- **THEN** 返回 `GameResult.Decided` —— 赢家就是刚落子那一方,MUST NOT 由返回值再说一遍

#### Scenario: 平局
- **WHEN** 最后一格落下仍无人连五
- **THEN** 返回 `GameResult.Draw`

#### Scenario: 返回值里不提颜色
- **WHEN** 检查 `GameResult` 的成员
- **THEN** 没有任何成员名包含 `Black` 或 `White`

  这一条是可执行的:一条测试断言枚举的成员名集合恰好是 `{Ongoing, Decided, Draw}`。**它防的不是打错字,是有人为了"方便"把颜色加回来** —— 而加回来的那一刻,`Board.PlaceStone` 就又能返回一个与入参矛盾的值,且没有任何测试会红。

