# in-room-chat Specification Delta

## ADDED Requirements

### Requirement: 围观与围观评论是对战内核的能力,不属于任何一个棋种

围观机制 SHALL 对**每一个有真人房的对战棋种**成立,MUST NOT 出现任何按 `GameKey` 分支的围观逻辑。

这一条不新建任何东西 —— 它把一件已经为真但从未写明的事写下来,并给它一个可检验的形状。整套机制早就在内核里:`ChatChannel`、`Room.PostChatMessage` 的成员与频道校验、`Room.JoinAsSpectator`、`IRoomNotifier` 的 `room:{id}:spectators` 子群、`POST /api/rooms/{id}/spectate`、大厅 `Playing` 行上的围观按钮、`ChatPanel` 的不对称可见性。**它们全都不点名棋种**,所以一个棋种够不到围观的唯一可能原因是它没有真人房。

写明它的理由是:「围观对所有对战棋种可用」此前是一个**推断** —— 由"代码里没有棋种分支"推出来的。而这个仓库反复付过同一种账:`SupportsHumanVsHuman` 被声明、被发布、被当作承重事实使用,却没有任何机制维持它。**一条没有断言的正确结论,与一条没人检查的结论,长得一模一样。**

围观人数 MUST NOT 有上限。`JoinAsSpectator` MUST 幂等(同一用户重复围观不产生第二条记录),且玩家 MUST NOT 能围观自己所在的局。

#### Scenario: 多名观众同时评论
- **WHEN** 一局真人对局有 ≥ 2 名围观者,各自在 `Spectator` 频道发言
- **THEN** 每条都入列 `Room.ChatMessages`;每个围观者都收到全部这些消息

#### Scenario: 玩家看不到围观频道
- **WHEN** 围观者在 `Spectator` 频道发言
- **THEN** 两名玩家 MUST NOT 收到该消息,也 MUST NOT 在自己的聊天面板里看到它

#### Scenario: 围观者与玩家共用房间频道
- **WHEN** 任一方在 `Room` 频道发言
- **THEN** 玩家与围观者**都**收到

#### Scenario: 围观不分棋种
- **WHEN** 对每一个 `SupportsHumanVsHuman == true` 的棋种各开一局真人对局并围观
- **THEN** 每一局都能围观、都能在围观频道评论;代码中 MUST NOT 存在任何按 `GameKey` 区分围观行为的分支

#### Scenario: 重复围观幂等
- **WHEN** 同一用户对同一房间调两次 `POST /api/rooms/{id}/spectate`
- **THEN** 围观者集合中只有一条该用户的记录

#### Scenario: 玩家不能围观自己的局
- **WHEN** 房间中的玩家调 `spectate`
- **THEN** 抛 `PlayerCannotSpectateException`
