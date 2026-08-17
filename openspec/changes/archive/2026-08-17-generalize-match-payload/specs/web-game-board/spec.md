# web-game-board Specification Delta

## ADDED Requirements

### Requirement: 客户端区分"没有盘面"与"还不知道盘面"

`boardSizeFor(capabilities, gameKey)` SHALL 区分三种情况,MUST NOT 把它们折成一种:

1. **描述符已到,且带尺寸** → 返回该尺寸。
2. **描述符已到,尺寸为 `null`** → 这个棋种**没有盘面**(成语接龙)。返回 `null`,调用方 MUST NOT 渲染棋盘。
3. **客户端不认识这个键 / 描述符里没有它** → 返回 `DEFAULT_BOARD`,理由不变:一个比服务端旧的客户端遇到没听说过的棋种时,画错尺寸好过白屏,而服务端会挡住越界落子。

而"描述符还没到"是**第四种**、也是既有的一种:调用方 MUST 在 `capabilities.loaded()` 为 false 时保持骨架,不调用本函数下结论。

第 2 种此前不存在,而且不能用第 3 种代替:`rows: null` 走到 `DEFAULT_BOARD` 会把一个没有棋盘的游戏描述成 **15×15 的五子棋盘**。`remove-manifest-board` 已经为「两件不同的事共用一个回落值」写过一次判词,这是同一条:**回落值只该覆盖它真正说得清的那一种情况。**

#### Scenario: 有盘面
- **WHEN** 描述符里 `gomoku` 是 `{ rows: 15, cols: 15 }`
- **THEN** 返回 `{ rows: 15, cols: 15 }`

#### Scenario: 没有盘面
- **WHEN** 描述符里某棋种的 `rows` / `cols` 为 `null`
- **THEN** 返回 `null`;调用方 MUST NOT 渲染棋盘,也 MUST NOT 代入 `DEFAULT_BOARD`

#### Scenario: 不认识的键
- **WHEN** 描述符里根本没有这个键
- **THEN** 返回 `DEFAULT_BOARD`

#### Scenario: 三者互不冒充
- **WHEN** 审阅 `boardSizeFor` 的实现
- **THEN** "没有盘面"与"不认识的键"MUST 走不同的分支并返回不同的值
