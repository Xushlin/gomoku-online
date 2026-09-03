# 手机端的围观

## 为什么

大厅里一局已经开打的房间,手机端点进去只有一条路:`POST /join` —— 而服务端会拒绝(座位满了)。于是「已开局的房间」在这个客户端上等于**看得见、进不去**。

服务端的围观从第一天就在:`POST /api/rooms/{id}/spectate`、`DELETE .../spectate`、`SpectatorJoined` / `SpectatorLeft` 推送、`RoomStateDto.spectators`、以及一个只有围观者收发的聊天频道。

## 做什么

### 入场是三步,而中间那步是会被跳过的那一步

**`POST /spectate` → `JoinRoom` → `JoinSpectatorGroup`。**

这不是推测:`test/room_social_probe_test.dart` 第一版只调了后面那一个,量出来的结果是「围观者收不到房间频道的消息」—— **读起来和一个服务端 bug 一模一样**。房间频道发给**房间组**,而进房间组的方法是 `JoinRoom`;`JoinSpectatorGroup` 只加围观子群。

服务端会自己查聚合确认身份,所以 `JoinSpectatorGroup` 对非围观者是**静默无操作**,不是错误。客户端因此可以无条件调它。

### 离开走另一条路由

围观者离开是 `DELETE /api/rooms/{id}/spectate`,不是 `POST /leave`。这条与「主持人退等待中的房间要走 `DELETE /api/rooms/{id}`」是同一类:**哪条路由由服务端的规则决定,不由客户端觉得哪条更顺**。`room_route_contract_test` 从控制器的属性派生合法路由集,所以走查会覆盖它。

### 围观者的屏幕是只读的,而这一半已经写好了

认输和催促的显示条件都要求 `mySeat != null`,所以围观者天然看不到动作条。落子也要拦:`GameViewModel.tap` 目前只看 `sending`。

### 围观频道的页签**只对围观者出现**

`add-mobile-room-chat` 里写着「围观能力落地之前只显示房间频道」,理由是「一个只有玩家到得了的屏幕上的围观页签是一个永远空的页签」。现在围观落地了,而那条理由**只对玩家仍然成立** —— 所以判据不是「围观落地了就都显示」,而是**「谁到得了这个频道,谁才看得到这个页签」**。

## 不做

- **从围观切换成玩家**(空位出现时坐下)。服务端有 `/join`,但那牵扯到「已经在围观的人要先 `DELETE /spectate` 吗」这类顺序问题,而现在没有需求逼它。
- 围观者名单的实时增删动画;显示人数与名单就够。
- 回放(`GET /api/rooms/{id}/replay` 存在,是另一笔账)。
