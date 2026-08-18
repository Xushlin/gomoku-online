# web-tetris Specification Delta

## ADDED Requirements

### Requirement: 俄罗斯方块在锁定 / 消行 / 升级 / 结束时发声

`TetrisPlay` (`src/app/games/tetris/play/play.ts`) SHALL 注入 `SoundService`,并在每次重力步之后按下面的规则播放**至多一个**事件。所有 sound 调用都是 fire-and-forget 的副作用,MUST NOT 改变引擎状态、提交流程或计时器语义。

**引擎 MUST NOT 知道声音存在。** `TetrisGame` 是无 Angular 的纯状态机,被引擎测试压着;组件通过**观察**它的快照 `{locks, lines, level, over}` 来推断发生了什么,与 `RoomPage` 的 `previousMoveCount` 是同一个模式。判定 MUST 是一个纯函数 `soundForStep(before, after): SoundEventName | null`,与组件分开,以便逐种组合断言。

事件映射:

| 发生了什么 | 事件 |
| --- | --- |
| 方块锁定(`locks` 增加) | `move-place` |
| 消掉 1–3 行 | `line-clear` |
| 消掉 4 行 | `line-clear-quad` |
| 等级上升 | `level-up` |
| 顶到天花板,一局结束 | `game-lose` |

**锁定复用 `move-place`、结束复用 `game-lose`,这是刻意的。** 一次锁定就是「一次落子生效了」,pack 决定它听起来像什么;而一局 score-attack 只会以爆顶结束,`game-lose` 的下扫音色正是「结束了」。为同一件事再造一个 `game-over` 只是同一件事的第二个名字。

**四行同时消 MUST 与 1–3 行不同声。** `LINE_SCORES` 的 100 vs 800 那个差值「是整个『攒一个 tetris』的决定」;声音不区分它,音频就在和计分表唱反调。

一次落子只播一个声音,优先级 **`over` > `level-up` > `line-clear-quad` > `line-clear` > `move-place`**:同时响两个是浑的。升级排在四行消之前,因为**升级改变游戏**(`gravityIntervalMs` 立刻变快),而四行消只是奖励,且奖励玩家已经在计分板上看见了。

按键本身 MUST NOT 发声 —— 左右移动、旋转、软降、硬降、暂停都不播。**声音播报「发生了什么」,不播报「你按了什么」**:玩家自己按下的东西不需要被告知。

#### Scenario: 锁定一个方块播 move-place
- **WHEN** 从 UI 硬降一次
- **THEN** `sound.play('move-place')` 被调一次

#### Scenario: 消行播 line-clear 而不是 move-place
- **WHEN** 一次落子消掉 1–3 行
- **THEN** `sound.play('line-clear')` 被调一次,同一次落子 MUST NOT 再播 `move-place`

#### Scenario: 四行同时消播 line-clear-quad
- **WHEN** 一次落子消掉 4 行
- **THEN** 播 `line-clear-quad`,MUST NOT 播 `line-clear`

#### Scenario: 升级压过消行
- **WHEN** 一次落子既消了行又让 `level` 上升
- **THEN** 只播 `level-up`

#### Scenario: 爆顶播 game-lose
- **WHEN** 新方块无处生成,`over` 变 true
- **THEN** 播 `game-lose`;同一步 MUST NOT 再播锁定或消行的声音

#### Scenario: 移动与旋转不发声
- **WHEN** 从 UI 连续左移、右移、旋转、暂停、继续
- **THEN** `sound.play` MUST NOT 被调用

#### Scenario: 开局不发声
- **WHEN** 一局开始,第一个方块出现但还没落下
- **THEN** `sound.play` MUST NOT 被调用
