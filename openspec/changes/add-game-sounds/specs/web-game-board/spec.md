# web-game-board Specification Delta

## RENAMED Requirements

标题里的「5 个事件」现在是 6 个 —— 象棋的吃子是第六个。archive 的应用顺序是
RENAMED → REMOVED → MODIFIED → ADDED,所以下面 MODIFIED 用的是新标题。

- FROM: ### Requirement: RoomPage 在 5 个事件上 emit 声音
- TO: ### Requirement: RoomPage 在 6 个事件上 emit 声音

## MODIFIED Requirements

### Requirement: RoomPage 在 6 个事件上 emit 声音

`RoomPage` (`src/app/pages/rooms/room-page/room-page.ts`) SHALL 注入 `SoundService` 并在以下时机调 `sound.play(event)`(精确语义见每条 Scenario);所有 sound 调用都是 fire-and-forget 的副作用,MUST NOT 改变现有 SignalR / REST / 状态机的语义,MUST NOT 阻塞 UI。

声音事件清单与触发条件:

1. **`'move-place'`** —— 当 `state()?.game?.moves.length` 比上一次观察的值大 1 时(即 SignalR `MoveMade` 推到本地状态后)。**初次加载**(状态从无到有的第一次 hydration)MUST NOT 触发。
2. **`'capture'`** —— 同一个时机,但当这一手**吃掉了一个子**时代替 `'move-place'`。仅象棋:判定 MUST 走 `games/xiangqi/position.ts` 导出的 `lastMoveCaptured(moves)`,即把最后一手之前的局面算出来,看目标格原本有没有子。两个事件 MUST 互斥,一手只播一个。
3. **`'game-win'` / `'game-lose'` / `'game-draw'`** —— 当 `hub.gameEnded()` 从 null 翻为 non-null 时,根据 `(result, mySide())` 分派:
   - `result === 'Draw'` → `'game-draw'`
   - `result === 'BlackWin' && mySide === 'black'` 或 `result === 'WhiteWin' && mySide === 'white'` → `'game-win'`
   - 其它(包括观众视角)→ `'game-lose'`
4. **`'urge'`** —— 当 `hub.urged$` emit 时,与现有 urge toast 的触发同位置。

实现 SHALL 使用 RoomPage 已有的 `effect` 与 `subscribe`,不引入新的事件流;move-count 比对通过私有 `previousMoveCount = -1` 字段实现(初值 -1 是哨兵,首次观察时设为当前值,不 play)。

**吃子判定是一个按棋种的分支,不是注册表。** 有吃子概念的棋种就象棋一个 —— *一个只有一条分支的 switch 仍然是 switch*;`RoomPage` 本来就有 `isXiangqi()` / `isIdiomChain()` 在选棋盘组件。判定所依赖的局面是客户端**画棋盘本来就要算的**那一份(`INITIAL_POSITION` + `positionAfter`),没有引入第二份真相:目标格上原来有没有子,是它每一帧都在读的事实。

非象棋棋种 MUST 一律播 `'move-place'` —— 包括没有棋盘的成语接龙:那个事件的含义是「一手落定了」,听起来像什么由 pack 决定。

#### Scenario: 落子时播 move-place
- **WHEN** SignalR `MoveMade` 抵达,`state().game.moves.length` 从 5 变 6
- **THEN** `sound.play('move-place')` 被调一次

#### Scenario: 象棋吃子播 capture
- **WHEN** 房间 `gameKey === 'xiangqi'`,新到的一手落在一个原本有子的交叉点上
- **THEN** `sound.play('capture')` 被调一次;`'move-place'` MUST NOT 被调

#### Scenario: 象棋平移仍播 move-place
- **WHEN** 房间 `gameKey === 'xiangqi'`,新到的一手落在空交叉点上
- **THEN** `sound.play('move-place')` 被调一次;`'capture'` MUST NOT 被调

#### Scenario: 非象棋棋种不做吃子判定
- **WHEN** 房间 `gameKey === 'gomoku'` 或 `'idiom-chain'`,新到一手
- **THEN** 一律 `'move-place'`

#### Scenario: 初次 hydration 不播
- **WHEN** 用户进入 `/rooms/:id`,REST snapshot 返回已有 `moves.length === 12`
- **THEN** RoomPage 完成首次 state 写入,`sound.play(...)` 不被调用

#### Scenario: 重连 rehydration 不重复播
- **WHEN** 用户暂时断线又重连,REST snapshot 返回 `moves.length === 8`(与离线前相同)
- **THEN** rehydration 完成后 `sound.play('move-place')` MUST NOT 被调

#### Scenario: 我方胜利播 game-win
- **WHEN** `hub.gameEnded()` 翻为 `{ result: 'BlackWin', endReason: 'Decided' }`,`mySide() === 'black'`
- **THEN** `sound.play('game-win')` 被调一次

#### Scenario: 我方失败播 game-lose
- **WHEN** `hub.gameEnded()` 翻为 `{ result: 'BlackWin' }`,`mySide() === 'white'`
- **THEN** `sound.play('game-lose')` 被调一次

#### Scenario: 平局播 game-draw
- **WHEN** `hub.gameEnded()` 翻为 `{ result: 'Draw' }`(任意 mySide)
- **THEN** `sound.play('game-draw')` 被调一次
