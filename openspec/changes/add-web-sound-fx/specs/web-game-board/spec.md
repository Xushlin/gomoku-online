## ADDED Requirements

### Requirement: RoomPage 在 5 个事件上 emit 声音

`RoomPage` (`src/app/pages/rooms/room-page/room-page.ts`) SHALL 注入 `SoundService` 并在以下时机调 `sound.play(event)`(精确语义见每条 Scenario);所有 sound 调用都是 fire-and-forget 的副作用,MUST NOT 改变现有 SignalR / REST / 状态机的语义,MUST NOT 阻塞 UI。

声音事件清单与触发条件:

1. **`'move-place'`** —— 当 `state()?.game?.moves.length` 比上一次观察的值大 1 时(即 SignalR `MoveMade` 推到本地状态后)。**初次加载**(状态从无到有的第一次 hydration)MUST NOT 触发。
2. **`'game-win'` / `'game-lose'` / `'game-draw'`** —— 当 `hub.gameEnded()` 从 null 翻为 non-null 时,根据 `(result, mySide())` 分派:
   - `result === 'Draw'` → `'game-draw'`
   - `result === 'BlackWin' && mySide === 'black'` 或 `result === 'WhiteWin' && mySide === 'white'` → `'game-win'`
   - 其它(包括观众视角)→ `'game-lose'`
3. **`'urge'`** —— 当 `hub.urged$` emit 时,与现有 urge toast 的触发同位置。

实现 SHALL 使用 RoomPage 已有的 `effect` 与 `subscribe`,不引入新的事件流;move-count 比对通过私有 `previousMoveCount = -1` 字段实现(初值 -1 是哨兵,首次观察时设为当前值,不 play)。

#### Scenario: 落子时播 move-place
- **WHEN** SignalR `MoveMade` 抵达,`state().game.moves.length` 从 5 变 6
- **THEN** `sound.play('move-place')` 被调一次

#### Scenario: 初次 hydration 不播
- **WHEN** 用户进入 `/rooms/:id`,REST snapshot 返回已有 `moves.length === 12`
- **THEN** RoomPage 完成首次 state 写入,`sound.play(...)` 不被调用

#### Scenario: 重连 rehydration 不重复播
- **WHEN** 用户暂时断线又重连,REST snapshot 返回 `moves.length === 8`(与离线前相同)
- **THEN** rehydration 完成后 `sound.play('move-place')` MUST NOT 被调

#### Scenario: 我方胜利播 game-win
- **WHEN** `hub.gameEnded()` 翻为 `{ result: 'BlackWin', endReason: 'Connected5' }`,`mySide() === 'black'`
- **THEN** `sound.play('game-win')` 被调一次

#### Scenario: 我方失败播 game-lose
- **WHEN** `hub.gameEnded()` 翻为 `{ result: 'BlackWin' }`,`mySide() === 'white'`
- **THEN** `sound.play('game-lose')` 被调一次

#### Scenario: 平局播 game-draw
- **WHEN** `hub.gameEnded()` 翻为 `{ result: 'Draw' }`(任意 mySide)
- **THEN** `sound.play('game-draw')` 被调一次

#### Scenario: 观众视角默认 game-lose
- **WHEN** `hub.gameEnded()` 翻为 `{ result: 'BlackWin' }`,`mySide() === 'spectator'`
- **THEN** `sound.play('game-lose')` 被调一次(中性兜底,观众可整体静音)

#### Scenario: 被催促时播 urge
- **WHEN** `hub.urged$` emit
- **THEN** `sound.play('urge')` 被调一次,与现有 `urgeToast.set(true)` 同步

#### Scenario: muted 时所有事件静默
- **WHEN** `sound.muted() === true`,任何上述触发条件发生
- **THEN** `sound.play` 仍按上述次数被"调"(spy 可断言),但 service 内部早返,无音频输出
