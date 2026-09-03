## ADDED Requirements

### Requirement: 围观入场 SHALL 是三步,而 `JoinRoom` 是其中一步

以围观者身份进入房间,客户端 SHALL 依次执行:

1. `POST /api/rooms/{id}/spectate`;
2. hub `JoinRoom(roomId)`;
3. hub `JoinSpectatorGroup(roomId)`。

**第二步不可省。** 房间频道的推送发给**房间组**,而进房间组的方法是 `JoinRoom`;
`JoinSpectatorGroup` 只加围观子群。少了第二步的表现是「围观者收不到房间里的消息」,而那
读起来和一个服务端缺陷一模一样 —— 这个平台的探针第一版就是这么错的。

第三步对非围观者是服务端侧的静默无操作,所以客户端 MUST NOT 为它加一个「我是不是围观者」
的前置判断:那是一个会过期的判断,而服务端已经查过聚合了。

#### Scenario: 三步都发生,顺序正确
- **WHEN** 用户围观一个进行中的房间
- **THEN** 客户端先 `POST /api/rooms/{id}/spectate`,再 `JoinRoom`,再 `JoinSpectatorGroup`

#### Scenario: 围观者收得到房间频道
- **WHEN** 围观期间桌上有人说话
- **THEN** 围观者的屏幕上出现那条消息

---

### Requirement: 围观者离开 SHALL 走 `DELETE /api/rooms/{id}/spectate`

围观者退出房间 SHALL 调用 `DELETE /api/rooms/{id}/spectate`,MUST NOT 调用
`POST /api/rooms/{id}/leave`(那是玩家的路由)。

**哪条路由由服务端的规则决定**,与「主持人退等待中的房间要走 `DELETE /api/rooms/{id}`」
是同一类。客户端 MUST NOT 按「哪条更顺手」选。

#### Scenario: 围观者退出
- **WHEN** 围观者离开房间
- **THEN** 客户端调用 `DELETE /api/rooms/{id}/spectate`

#### Scenario: 玩家退出仍走玩家的路由
- **WHEN** 坐在座位上的玩家离开一个进行中的房间
- **THEN** 客户端调用 `POST /api/rooms/{id}/leave`,MUST NOT 调用围观的那条

---

### Requirement: 围观者的棋盘 SHALL 是只读的,而 MUST NOT 靠界面隐藏来实现

围观者点棋盘 MUST NOT 向服务端发出任何走子。这条 SHALL 在 ViewModel 上成立,而不是靠
View 不画棋盘或不接收点击 —— 一个只在界面层拦住的规则,会在下一个进入这块棋盘的路径上
失效。

认输与催促的入口对围观者 MUST NOT 出现(它们的条件已经要求「坐在座位上」)。

#### Scenario: 围观者点棋盘什么都不发
- **WHEN** 围观者在棋盘上点一个空点
- **THEN** MUST NOT 调用 `MakeMove` 或 `MovePiece`

#### Scenario: 玩家点棋盘照常
- **WHEN** 轮到自己的玩家点一个空点
- **THEN** 照常发出走子(否则上一条是因为整条路断了才成立的)

---

### Requirement: 大厅 SHALL 给坐不下的房间一个围观入口,而判据是空位不是状态

大厅列表 SHALL 按「这个房间还坐得下吗」决定点击的去向:

- 还有空位的房间 → 入座(`POST /join`);
- 没有空位或已经开打的房间 → 围观。

**判据是「这个房间还坐得下吗」,不是房间状态的字面值** —— 一个满员但仍在 `Waiting` 的房间
坐不下,而客户端按状态判断会给出一个必然被服务端拒绝的入座按钮。

**而「还坐得下吗」的座位总数 SHALL 取自棋种描述符,MUST NOT 取自房间摘要。** 这是量出来的:
`GET /api/rooms` 返回的 `RoomSummaryDto` **不含 `seatCount`**,且 `seats` **只列已坐下的
座位** —— 于是「已坐 < 总数」在摘要上退化成 `1 < 1`,每个房间(包括空房间)都会被判成坐不下。
大厅是按棋种打开的,所以描述符就在手边。

**一份用完整房间 JSON 造的夹具证明不了这件事**:它带着 `seatCount`,于是无论实现读哪一个都
绿。这个缺陷是集成测试抓到的,而单测夹具此后 SHALL 用**摘要的形状**。

#### Scenario: 进行中的房间给的是围观
- **WHEN** 大厅里有一个进行中的房间
- **THEN** 点它进入围观,而不是尝试入座

#### Scenario: 有空位的房间给的是入座
- **WHEN** 大厅里有一个还有空位的房间
- **THEN** 点它尝试入座

## RENAMED Requirements

- FROM: `### Requirement: 手机端的聊天 SHALL 只有房间频道,MUST NOT 显示一个到不了的围观频道`
- TO: `### Requirement: 手机端的聊天频道页签 SHALL 只对到得了那个频道的人出现`

## MODIFIED Requirements

### Requirement: 手机端的聊天频道页签 SHALL 只对到得了那个频道的人出现

聊天面板 SHALL 只把一个频道的入口显示给到得了那个频道的人。

围观频道**只有围观者收得到、也只有围观者发得出**。

因此聊天面板 SHALL 只对**围观者**显示频道页签(房间 / 围观);对坐在座位上的玩家 SHALL
只显示房间频道,且 MUST NOT 显示围观页签。

判据是**「谁到得了这个频道」**,不是「这个客户端支不支持围观」。前者在围观落地之后对玩家
仍然成立,后者不成立 —— 一个写成后者的条件会在围观落地当天把一个永远空的页签放到玩家
面前,而一个永远空的页签看起来像坏了。

#### Scenario: 玩家看不到围观页签
- **WHEN** 坐在座位上的玩家打开聊天面板
- **THEN** 界面上 MUST NOT 出现围观频道的入口

#### Scenario: 围观者两个频道都看得到
- **WHEN** 围观者打开聊天面板
- **THEN** 房间与围观两个频道都可选
