## ADDED Requirements

### Requirement: 一局结束时,手机端 SHALL 说出结果

对局结束时客户端 SHALL 显示赢 / 输 / 和,以及结束原因,并给出「返回大厅」与「关掉再看棋盘」两条出路。

**在真机上量到的缺陷是「界面停在那」:** 棋盘还在,点哪儿都没反应(服务端在拒),没有任何一句话说结果。而数据到了**两次**都被扔了 —— 服务端每一份快照都带 `Result` / `WinnerUserId` / `EndReason`,客户端只解析 `moves` 和 `currentSeat`;`GameEnded` 推送订阅了,却推进一个没人消费的流。

赢还是输 MUST 按 **`WinnerUserId` 与自己的用户 id** 判定,MUST NOT 按用户名 —— 用户名是显示名,这个平台已经为「把显示名当身份」付过两次账。

#### Scenario: 三个结果都说得出来
- **WHEN** 一局以我获胜 / 我落败 / 和局结束
- **THEN** 分别 MUST 显示 `game.ended.title-win` / `title-lose` / `title-draw`
- **AND** 三个方向 MUST 同时被测:只测「赢」的话,一个「永远说你赢了」的实现同样通过

#### Scenario: 未结束时什么都不显示
- **WHEN** 对局仍在进行(`Result` 为 `Ongoing`,或者根本没有 `result` 字段)
- **THEN** MUST NOT 显示任何结果
- **AND** 这一条与上一条 MUST 同时存在:少了它,一个「一进房间就报结果」的实现同样通过

#### Scenario: 结束原因跟着结果一起说
- **WHEN** 结束原因是 `Decided` / `Resigned` / `TurnTimeout`
- **THEN** MUST 显示对应的 `game.ended.reason-*`
- **AND** 每一个键 MUST 在两个 locale 里都有文案 —— 一个渲成原始键的结果框比没有更糟

---

### Requirement: 对局结果 SHALL 只有一个来源:房间快照

客户端 SHALL 从 `RoomState` 携带的 `Result` / `WinnerUserId` / `EndReason` 得出结果,MUST NOT 另外依赖 `GameEnded` 推送。

**理由是从服务端源码量出来的顺序:** `MakeMoveCommandHandler` 与 `ResignCommandHandler` 都是 `SaveChangesAsync` → `RoomStateChangedAsync` → `GameEndedAsync`,而 `GameEndedDto` 是从**已经写好的** `room.Game` 上取的。所以结束时那份 `RoomState` 一定带着结果,`GameEnded` 对这件事是冗余的。

**两个来源描述同一件事正是这个仓库反复付账的形状**,所以这一笔**删掉** `GameEnded` 订阅和那条没人消费的 `_errors` 流 —— 最好的机制是能被删掉的那种。

#### Scenario: 只靠快照就够
- **WHEN** 一局真的下到结束
- **THEN** 结果 MUST 出现在**屏幕**上
- **AND** 判据 MUST 是屏幕而不是服务端:问服务端「有没有结果」只证明服务端结束了,
  而这个区别在 `fix-mobile-hub-inbound` 里刚付过一次学费

#### Scenario: 那条没人消费的流没了
- **WHEN** 检查 hub 服务
- **THEN** MUST NOT 再有 `GameEnded` 订阅,也 MUST NOT 再有一条没有消费者的错误流
- **AND** `hub_contract_test` 的订阅数从 3 回到 2,而走查 MUST 仍然绿
  (它断言的是「订阅的 ⊆ 服务端发的」,不是「订阅得越多越好」)
