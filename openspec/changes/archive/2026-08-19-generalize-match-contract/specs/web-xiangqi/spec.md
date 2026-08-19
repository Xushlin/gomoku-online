# web-xiangqi 的规格变化

## MODIFIED Requirements

### Requirement: 两步交互 —— 选子、落点，且第一步可以反悔

`XiangqiBoard` SHALL 维护一个本地的「已选中起点」状态：

- 点自己的子 → 选中它并高亮。
- 已有选中时点一个非自己子的交叉点 → emit `(pieceMove)`，载荷为 `{ from, to }`。
- 点**同一枚**已选中的子 → 取消选择，MUST NOT emit。
- 点**另一枚**自己的子 → 改选它，MUST NOT emit（那既不是吃自己，也不该发一个必然失败的请求）。
- `Escape` → 取消选择。

组件 MUST NOT 判定着法是否合法（design D2）。它只做两件不需要规则的事：只能拿起自己的子，以及非本方回合时整块盘只读。

以下任一为真时全盘 `disabled`：`readonly`、`submitting`、`mySide() === 'spectator'`、`state.status !== 'Playing'`、非本方回合。

每个交叉点 MUST 有可翻译的 `aria-label`，含行列与该点的棋子（或「空」）；选中的子 MUST 以 `aria-pressed` 表达，MUST NOT 只靠颜色。

#### Scenario: 选子再落点
- **WHEN** 红方回合，点 `(9,0)` 的俥，再点 `(8,0)`
- **THEN** `(pieceMove)` emit 一次，载荷为 `{ from: {row:9,col:0}, to: {row:8,col:0} }`

#### Scenario: 再点一次取消
- **WHEN** 点 `(9,0)` 后再点 `(9,0)`
- **THEN** MUST NOT emit；选中态清空

#### Scenario: 改选另一枚子
- **WHEN** 点 `(9,0)` 后点 `(9,1)`（也是自己的子）
- **THEN** MUST NOT emit；选中态变为 `(9,1)`

#### Scenario: 拿不起对方的子
- **WHEN** 红方回合，点一枚黑子且当前无选中
- **THEN** MUST NOT emit，MUST NOT 进入选中态

#### Scenario: 非本方回合只读
- **WHEN** `currentSeat` 是对方的座位
- **THEN** 全部 90 个按钮 `disabled`；点击不触发任何事件

#### Scenario: 观众只读
- **WHEN** `mySide() === 'spectator'`
- **THEN** 全部按钮 `disabled`

#### Scenario: 组件不判定合法性
- **WHEN** 检索组件源码
- **THEN** MUST NOT 存在任何棋子走法规则（马走日、象飞田、炮翻山、九宫、河界限制等）

