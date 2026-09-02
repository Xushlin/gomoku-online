# tasks

## 1. 传输

- [x] `MatchHub.urge(roomId)` —— hub 方法 `Urge`,**一个参数**。
      SignalR 两个方向都不套用 C# 可选参数默认值,多一个少一个都在绑定层被拒。
- [x] 订阅 `UrgeReceived`,推给一个 `ValueListenable` 计数器 + 最近一条 payload。
      **计数器而不是 bool** —— 「又被催了一次」必须可观测。
- [x] `RoomRepository.resign(roomId)` —— `POST /api/rooms/{id}/resign`。
- [x] `hub_contract_test` 仍然绿:`UrgeReceived` 必须在服务端源码派生出的名单里。
- [x] `room_route_contract_test` 仍然绿:resign 那条路由必须进得了走查的样本
      (**上次这条走查按行扫,漏掉了多行调用**)。

## 2. ViewModel

- [x] `canResign`:我在座位上 **且** 进行中 **且** `descriptor.seatCount == 2`。
      座位数**读房间自己的 `seatCount`**,不绕道棋种目录。
- [x] `canUrge` / `urgeDisabledReasonKey`:玩家 + 进行中 → 显示;不是我的回合 → 可点。
- [x] 429 → `game.errors.urge-cooldown`,**不落通用错误**。
- [x] 认输成功之后 **什么都不做** —— 结果由既有那条路(`outcome`)显示。

## 3. 界面

- [x] 对局页底部一条动作条:认输 / 催促。**离开已经有了**(AppBar 的返回),不重复。
- [x] 认输确认框,复用 `game.actions.resign-confirm-*`。
- [x] 收到催促 → `SnackBar`(`game.urge.toast`)。
- [x] 一个键都不新增。

## 4. 判据

- [x] 单测:`canResign` 的**四个**分支各一条(可以 / 不是玩家 / 未开始 / 座位数 3)。
      **座位数 3 那条要造一个三座位的房间** —— 今天的两个棋种都是 2,所以这条判据在
      真实数据上恒真,而恒真的判据是**空循环**。
- [x] 单测:取消不发请求(**配前置断言**证明当时可以认输)、确认才发。
- [x] 单测:认输成功后 ViewModel **没有**自己写结果 —— 结果仍然只来自快照。
- [x] 单测:催促的三种状态(可点 / 轮到自己 / 冷却),以及 429 的映射。
- [x] 单测:`UrgeReceived` 的计数器被推送推动。
- [x] **正面对照:把 `seatCount == 2` 换成 `>= 2`,看那条三座位的测试红。**
- [x] **正面对照:让认输成功时自己写一个「你输了」,看「结果只来自快照」那条红。**
- [x] 集成测试:两个真实玩家,一个认输,**另一个屏幕上**出现结果;
      一个催促,**另一个屏幕上**出现提示。判据是屏幕不是服务端。

## 5. 不回归

- [x] `flutter analyze` 零问题;`flutter test` 全绿;`shared_sync_test` 绿(零新增键)。
- [x] 既有集成测试**逐个**跑(整目录跑会 6 个 `Unable to start the app on the device`,
      那是实例互抢,不是失败)。

## 6. 收尾

- [x] `JOURNAL.md` 一条。
- [x] PR 里写清**依赖 `fix-urge-user-routing`**,否则催促按钮天生是死的。
