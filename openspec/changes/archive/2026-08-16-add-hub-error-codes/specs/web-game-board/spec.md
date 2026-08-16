## RENAMED Requirements

一条 requirement 连标题一起改:客户端不再匹配服务端的散文,它匹配一个码。

应用顺序 RENAMED → REMOVED → MODIFIED → ADDED,所以下面 MODIFIED 用的是新标题。

- FROM: ### Requirement: 错误处理 —— `HubException` 消息到翻译键的映射
- TO: ### Requirement: 错误处理 —— 服务端错误码到翻译键的映射

## MODIFIED Requirements

### Requirement: 错误处理 —— 服务端错误码到翻译键的映射

RoomPage / Board / ChatPanel SHALL 把从 hub 命令 promise 抛出的 `HubException` 处理为用户可见的翻译文案。`HubException` 的消息携带服务端的错误码(见 room-and-gameplay「领域错误带稳定错误码」)。SignalR 会把它包成 `…on the server. HubException: <码>`,所以客户端 MUST **取出**那个码再查表,MUST NOT 拿整串去比。映射为一张**穷举的**码 → 键表:

| 码 | 翻译键 |
| --- | --- |
| `not-your-turn` | `game.errors.not-your-turn` |
| `invalid-move` | `game.errors.invalid-move` |
| `self-check` | `game.errors.self-check` |
| `room-not-in-play` | `game.errors.room-not-in-play` |
| `not-a-player` | `game.errors.not-a-player` |
| `urge-too-frequent` | `game.errors.urge-cooldown` |
| `not-opponents-turn` | `game.errors.not-opponents-turn` |
| `invalid-chat-content` | `game.errors.invalid-chat` |
| `spectator-channel-forbidden` | `game.chat.forbidden-error` |
| `concurrent-modification` | `game.errors.concurrent-move-refetched`(并**必须**跟进一次 `roomsApi.getById → applySnapshot`) |

未在表内的码 → `game.errors.generic`。网络层错误(Promise rejection 不是 `HubException`,而是 connection 已断)→ `game.errors.network`。

**此前这里是对服务端英文散文的关键字模糊匹配,而那个方案在 Development 之外根本不工作。** 普通异常的消息只有在 `EnableDetailedErrors` 打开时才送到客户端,而它被设成 `IsDevelopment()`;生产环境下每一条都落到 generic。这不是推演,是实测出来的:同一次非法象棋着法,Development 显示「That move isn't allowed.」,Production 显示「Something went wrong. Please try again.」。

客户端 MUST NOT 展示服务端消息的任何部分 —— 负载里本来也只有码。这条不靠自觉:`HubException` 里没有散文可显示。

#### Scenario: 并发错误走 rehydration
- **WHEN** `hub.makeMove` reject,码为 `concurrent-modification`
- **THEN** 显示 `game.errors.concurrent-move-refetched` 翻译 toast;`roomsApi.getById(id)` 被调一次;state 被 `applySnapshot` 替换

#### Scenario: 未识别的码走 generic
- **WHEN** `HubException` 消息是一个表里没有的码
- **THEN** toast 显示 `game.errors.generic` 翻译

#### Scenario: 线上的包装形式也认得
- **WHEN** 收到 `"An unexpected error occurred invoking 'MovePiece' on the server. HubException: invalid-move"`
- **THEN** toast 显示 `game.errors.invalid-move`

#### Scenario: 象棋的走法拒绝读得懂
- **WHEN** `HubException` 消息是 `invalid-move`
- **THEN** toast 显示 `game.errors.invalid-move`,MUST NOT 显示 `game.errors.generic`

#### Scenario: 自将有自己的说法
- **WHEN** `HubException` 消息是 `self-check`
- **THEN** toast 显示 `game.errors.self-check`

#### Scenario: 生产环境下也读得懂
- **WHEN** 服务端以 `EnableDetailedErrors = false` 运行,玩家走一步非法着法
- **THEN** toast 显示 `game.errors.invalid-move` —— 与 Development 下**完全相同**

#### Scenario: 散文不再参与判定
- **WHEN** `HubException` 消息是一句英文句子而不是码(例如某个未迁移的路径)
- **THEN** toast 显示 `game.errors.generic`,MUST NOT 靠关键字猜测它的含义

### Requirement: i18n —— `game.*` 翻译树同步扩充

`public/i18n/en.json` 与 `public/i18n/zh-CN.json` SHALL 同步新增 `game.*` 键集合,包含但不限于:

- `game.room.{name-label, host-label, seat-black, seat-white, status-waiting, status-playing, status-finished}`
- `game.board.{cell-aria-label, last-move-label}`(cell-aria-label 带 `{{row}}` / `{{col}}` 插值占位符)
- `game.turn.{your-turn, opponent-turn, black-turn, white-turn, countdown-label}`
- `game.actions.{resign, resign-confirm-title, resign-confirm-body, resign-confirm-ok, leave, urge}`
- `game.chat.{title, tab-room, tab-spectator, send, placeholder, empty, max-length-error, forbidden-error}`
- `game.urge.{toast, button-disabled-own-turn, button-disabled-cooldown}`
- `game.ended.{title-win, title-lose, title-draw, reason-connected-5, reason-resigned, reason-timeout, back-to-lobby, dismiss}`
- `game.errors.{generic, network, not-your-turn, invalid-move, self-check, room-not-in-play, not-a-player, not-opponents-turn, invalid-chat, concurrent-move-refetched, urge-cooldown}`
- `game.connection.{reconnecting, disconnected, retry, connected}`

键集合 MUST 两份 JSON 完全相等;已有 flattener parity check 持续 0 drift。

模板 MUST 零硬编码 CJK / 长英文显示字符串;按 scaffold / auth / lobby 已立规则。

#### Scenario: parity
- **WHEN** 对比 `en.json` 与 `zh-CN.json` flatten 后的 key 集合
- **THEN** 差集为空

#### Scenario: 模板零硬编码
- **WHEN** 在 `src/app/pages/rooms/room-page/**/*.html` 下搜索 CJK 字符或 ≥3 字母英文显示字符串
- **THEN** 0 匹配(Brand / test-id / 技术字符串豁免)

#### Scenario: 每个映射到的键都有文案
- **WHEN** 遍历码 → 键表里的每一个翻译键
- **THEN** 两份 locale 中都存在且非空
