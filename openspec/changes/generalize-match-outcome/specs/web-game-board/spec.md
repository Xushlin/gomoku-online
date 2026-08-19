# web-game-board Specification Delta

## MODIFIED Requirements

### Requirement: RoomState 类型完整化 —— scaffold 留下的 `unknown` 被完整类型替换

`src/app/core/api/models/room.model.ts` SHALL 声明与后端 DTO 对齐的完整类型:

```ts
export type Stone = 'Empty' | 'Black' | 'White';
export type GameResult = 'Ongoing' | 'Decided' | 'Draw';
export type GameEndReason = 'Decided' | 'Resigned' | 'TurnTimeout';
export type ChatChannel = 'Room' | 'Spectator';

export interface MoveDto {
  readonly ply: number;
  readonly row: number | null;        // 无盘面棋种为 null
  readonly col: number | null;
  readonly text?: string | null;      // 文本类棋种的载荷
  readonly stone: Stone;
  readonly playedAt: string;
  readonly fromRow?: number | null;   // 走子类棋种的起点
  readonly fromCol?: number | null;
}

export interface GameSnapshot {
  readonly id: string;
  readonly currentTurn: Stone;
  readonly startedAt: string;
  readonly endedAt: string | null;
  readonly result: GameResult | null;
  readonly winnerUserId: string | null;
  readonly endReason: GameEndReason | null;
  readonly turnStartedAt: string;
  readonly turnTimeoutSeconds: number;
  readonly moves: readonly MoveDto[];
}

export interface GameEndedDto {
  readonly result: GameResult;
  readonly winnerUserId: string | null;
  readonly endedAt: string;
  readonly endReason: GameEndReason;
}
```

`RoomState.game` SHALL 为 `GameSnapshot | null`;`RoomState.chatMessages` SHALL 为 `readonly ChatMessage[]`。

所有字段名 MUST 与后端 System.Text.Json camelCase + `JsonStringEnumConverter` 产生的 wire 名完全对齐。**枚举类型 MUST 是字符串字面量并联类型**,不是数字 enum。

**`GameResult` MUST NOT 含带颜色的取值。** 服务端合并了 `BlackWin` / `WhiteWin`,理由是那两个值与 `winnerUserId` 说的是同一件事。客户端因此 MUST 用 `winnerUserId` 判断"谁赢了",MUST NOT 拿结果值去跟自己的棋色比 —— 后者在座位数超过两个时无从下手,而 `winnerUserId` 一直都在这两个 DTO 里。

`MoveDto` / `GameEndReason` 的取值在本条中一并订正:`generalize-match-payload` 与 `add-hub-error-codes` 之后,`row` / `col` 可空、`text` / `fromRow` / `fromCol` 存在、`Connected5` 已改名 `Decided`,而本 requirement 里的代码块此前仍是那几次改动之前的样子。**一条把源码整段抄进来的 requirement,会在每一次那段源码变化时静静过期**;订正它是本次改动的副产品,不是它的目的。

#### Scenario: 类型编译通过
- **WHEN** 用更新后的 `RoomState` 解析 `GET /api/rooms/:id` 的真实响应(在开发环境)
- **THEN** 无 TypeScript 错误,字段名逐一对应

#### Scenario: 带颜色的结果值不再存在
- **WHEN** 代码写 `result === 'BlackWin'`
- **THEN** TypeScript MUST 报错 —— 该取值已不在联合类型里

### Requirement: Game-ended CDK Dialog 由 `gameEnded` signal 驱动

RoomPage SHALL `effect(() => { ... })` 监听 `hub.gameEnded()`,当其从 null 变 non-null 时:

- 打开 `GameEndedDialog`(`src/app/pages/rooms/room-page/dialogs/game-ended-dialog.ts`)
- Dialog 数据 = `{ result, winnerUserId, endReason, roomId }` + 当前用户 id(供 dialog 计算"你赢了/你输了/平局"视角并支持回放跳转)
- Dialog 内容:
  - Title:
    - `result === 'Draw'` → `game.ended.title-draw`
    - `result === 'Decided' && winnerUserId === myUserId` → `game.ended.title-win`
    - 否则 → `game.ended.title-lose`
  - Reason:`game.ended.reason-connected-5` / `.reason-resigned` / `.reason-timeout`
  - 按钮:**主按钮** `game.ended.back-to-lobby` → 回到该棋种的入口;**次按钮** `game.ended.view-replay` → `/replay/<roomId>`;**收尾按钮** `game.ended.dismiss` 关弹窗留在只读 RoomPage
- Dialog 打开期间棋盘仍显示最终局面;背后的 RoomPage 仍响应 Resize / Leave 按钮
- 离开房间时 `hub.gameEnded` signal 被清

**胜负视角 MUST 由 `winnerUserId === myUserId` 判定**,MUST NOT 由 `result` 与 `mySide` 的组合判定。后者需要客户端同时持有"我是哪一色"与"哪一色赢了"两份镜像,而围观者两份都没有。按 `winnerUserId` 判定时,围观者不等于赢家,仍然落到非胜文案 —— 但那是因为他确实不是赢家,不是因为分支漏了他。

#### Scenario: Gameover 自动弹框
- **WHEN** `hub.gameEnded()` 从 null 变为 `{ result: 'Decided', winnerUserId: myId, endReason: 'Decided' }`
- **THEN** CDK Dialog 自动打开,title = `game.ended.title-win` 翻译文案

#### Scenario: 对手赢
- **WHEN** `hub.gameEnded()` 翻为 `{ result: 'Decided', winnerUserId: opponentId }`
- **THEN** title = `game.ended.title-lose`

#### Scenario: 平局
- **WHEN** `result === 'Draw'`
- **THEN** title = `game.ended.title-draw`

#### Scenario: 返回大厅按钮
- **WHEN** 弹窗中点主按钮
- **THEN** 导航到该棋种的入口;dialog 关闭;RoomPage 被销毁

#### Scenario: 关闭保留只读视图
- **WHEN** 弹窗中点 `dismiss` 按钮
- **THEN** dialog 关闭;RoomPage 仍在 `/rooms/:id`;棋盘只读

#### Scenario: 跳转回放
- **WHEN** 弹窗中点 `view-replay` 按钮
- **THEN** `router.navigateByUrl('/replay/<currentRoomId>')` 被调一次;dialog 关闭

### Requirement: RoomPage 在 6 个事件上 emit 声音

`RoomPage` (`src/app/pages/rooms/room-page/room-page.ts`) SHALL 注入 `SoundService` 并在以下时机调 `sound.play(event)`;所有 sound 调用都是 fire-and-forget 的副作用,MUST NOT 改变现有 SignalR / REST / 状态机的语义,MUST NOT 阻塞 UI。

声音事件清单与触发条件:

1. **`'move-place'`** —— 当 `state()?.game?.moves.length` 比上一次观察的值大 1 时。**初次加载** MUST NOT 触发。
2. **`'capture'`** —— 同一个时机,但当这一手**吃掉了一个子**时代替 `'move-place'`。仅象棋:判定 MUST 走 `games/xiangqi/position.ts` 导出的 `lastMoveCaptured(moves)`。两个事件 MUST 互斥,一手只播一个。
3. **`'game-win'` / `'game-lose'` / `'game-draw'`** —— 当 `hub.gameEnded()` 从 null 翻为 non-null 时,根据 `(result, winnerUserId)` 分派:
   - `result === 'Draw'` → `'game-draw'`
   - `result === 'Decided' && winnerUserId === myUserId` → `'game-win'`
   - 其它(包括观众视角)→ `'game-lose'`
4. **`'urge'`** —— 当 `hub.urged$` emit 时,与现有 urge toast 的触发同位置。

实现 SHALL 使用 RoomPage 已有的 `effect` 与 `subscribe`;move-count 比对通过私有 `previousMoveCount = -1` 字段实现(初值 -1 是哨兵,首次观察时设为当前值,不 play)。

**吃子判定是一个按棋种的分支,不是注册表。** 有吃子概念的棋种就象棋一个 —— *一个只有一条分支的 switch 仍然是 switch*。判定所依赖的局面是客户端**画棋盘本来就要算的**那一份,没有引入第二份真相。

非象棋棋种 MUST 一律播 `'move-place'` —— 包括没有棋盘的成语接龙。

胜负音效与弹窗文案 MUST 用**同一个**判据(`winnerUserId === myUserId`)。两处各写一遍"我赢了没有",是同一个判断的两份实现,而它们分歧的那一天表现为**弹窗说你赢了、音效放的是败音**。

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
- **WHEN** 用户暂时断线又重连,REST snapshot 返回 `moves.length === 8`
- **THEN** rehydration 完成后 `sound.play('move-place')` MUST NOT 被调

#### Scenario: 我方胜利播 game-win
- **WHEN** `hub.gameEnded()` 翻为 `{ result: 'Decided', winnerUserId: myId }`
- **THEN** `sound.play('game-win')` 被调一次

#### Scenario: 我方失败播 game-lose
- **WHEN** `hub.gameEnded()` 翻为 `{ result: 'Decided', winnerUserId: opponentId }`
- **THEN** `sound.play('game-lose')` 被调一次

#### Scenario: 平局播 game-draw
- **WHEN** `hub.gameEnded()` 翻为 `{ result: 'Draw' }`
- **THEN** `sound.play('game-draw')` 被调一次
