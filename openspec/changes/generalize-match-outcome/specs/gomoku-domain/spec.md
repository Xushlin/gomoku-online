# gomoku-domain Specification Delta

## MODIFIED Requirements

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
