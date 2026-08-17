# web-game-board Specification Delta

## MODIFIED Requirements

### Requirement: 错误处理 —— 服务端错误码到翻译键的映射

RoomPage / Board / ChatPanel SHALL 把从 hub 命令 promise 抛出的 `HubException` 处理为用户可见的翻译文案。`HubException` 的消息携带服务端的错误码(见 room-and-gameplay「领域错误带稳定错误码」)。SignalR 会把它包成 `…on the server. HubException: <码>`,所以客户端 MUST **取出**那个码再查表,MUST NOT 拿整串去比。映射为一张**穷举的**码 → 键表:

| 码 | 翻译键 |
| --- | --- |
| `not-your-turn` | `game.errors.not-your-turn` |
| `invalid-move` | `game.errors.invalid-move` |
| `self-check` | `game.errors.self-check` |
| `idiom-not-found` | `game.errors.idiom-not-found` |
| `idiom-does-not-link` | `game.errors.idiom-does-not-link` |
| `idiom-already-used` | `game.errors.idiom-already-used` |
| `room-not-in-play` | `game.errors.room-not-in-play` |
| `not-a-player` | `game.errors.not-a-player` |
| `urge-too-frequent` | `game.errors.urge-cooldown` |
| `not-opponents-turn` | `game.errors.not-opponents-turn` |
| `invalid-chat-content` | `game.errors.invalid-chat` |
| `spectator-channel-forbidden` | `game.chat.forbidden-error` |
| `concurrent-modification` | `game.errors.concurrent-move-refetched`(并**必须**跟进一次 `roomsApi.getById → applySnapshot`) |

三条接龙的码各有自己的一行,而不是共用 `invalid-move`。象棋能共用是因为玩家看着盘面能自己想明白；接龙不能:**「不是成语」「接不上」「说过了」是三种完全不同的纠正,而「这一步不合法」一种都说不出。**这一点尤其要紧,因为词链界面**故意不在客户端判合法性**(见 `web-idiom-chain`),所以服务端的拒绝是玩家了解规则的**唯一**途径。

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

#### Scenario: 不在词典里有自己的说法
- **WHEN** `HubException` 消息是 `idiom-not-found`
- **THEN** toast 显示 `game.errors.idiom-not-found`,MUST NOT 显示 `game.errors.invalid-move` 或 generic

#### Scenario: 接不上有自己的说法
- **WHEN** `HubException` 消息是 `idiom-does-not-link`
- **THEN** toast 显示 `game.errors.idiom-does-not-link`

#### Scenario: 说过了有自己的说法
- **WHEN** `HubException` 消息是 `idiom-already-used`
- **THEN** toast 显示 `game.errors.idiom-already-used`
