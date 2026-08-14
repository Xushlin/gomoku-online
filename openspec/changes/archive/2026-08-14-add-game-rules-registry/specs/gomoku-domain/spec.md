## MODIFIED Requirements

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

### Requirement: 同色棋子连成 5 颗或以上即获胜(基础规则,长连算赢)

系统 SHALL 在每次落子后,以该落子为中心沿水平、竖直、主对角、反对角四个方向做增量判胜:任一方向上同色连续子数(含中心)达到该棋盘的 `WinLength` 即判该色获胜。

连子长度 SHALL 取自棋盘构造参数而非常量 `5`。五子棋为 5,一字棋为 3。超过 `WinLength` 的长连仍然算赢(不实现禁手)。

#### Scenario: 横向连五获胜
- **WHEN** 黑子在同一行连续占据 5 个相邻位置
- **THEN** 最后一手返回 `GameResult.BlackWin`

#### Scenario: 竖向连五获胜
- **WHEN** 白子在同一列连续占据 5 个相邻位置
- **THEN** 最后一手返回 `GameResult.WhiteWin`

#### Scenario: 主对角连五获胜
- **WHEN** 黑子沿 ↘ 方向连续占据 5 个位置
- **THEN** 最后一手返回 `GameResult.BlackWin`

#### Scenario: 反对角连五获胜
- **WHEN** 白子沿 ↗ 方向连续占据 5 个位置
- **THEN** 最后一手返回 `GameResult.WhiteWin`

#### Scenario: 长连算赢
- **WHEN** 黑子连成 6 子
- **THEN** 判 `GameResult.BlackWin`,MUST NOT 因超长而判负或判无效

#### Scenario: 四子不算赢
- **WHEN** 黑子任一方向最多连成 4 子
- **THEN** 返回 `GameResult.Ongoing`

#### Scenario: 连子长度随棋种变化
- **WHEN** 在 `winLength = 3` 的棋盘上黑子连成 3 子
- **THEN** 判 `GameResult.BlackWin`;同样的 3 子在 `winLength = 5` 的棋盘上返回 `GameResult.Ongoing`
