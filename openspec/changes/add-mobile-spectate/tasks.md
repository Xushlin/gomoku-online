# tasks

## 1. 传输

- [x] `RoomRepository.spectate(roomId)` —— `POST /spectate` → `JoinRoom` → `JoinSpectatorGroup`。
      **中间那步不可省**(探针第一版就是漏了它,量出来像个服务端 bug)。
- [x] `RoomRepository.unspectate(roomId)` —— `DELETE /spectate`。
- [x] `MatchHub.joinSpectatorGroup(roomId)`。
- [x] `room_route_contract_test` 仍然绿,且**两条新路由都进得了走查的样本**。

## 2. ViewModel

- [x] `isSpectator`:房间的 `spectators` 里有我(**按 id**)。
- [x] `tap` 在围观时**什么都不发** —— 在 ViewModel 上拦,不在 View 上拦。
- [x] 离开时按身份选路由:围观者 `DELETE /spectate`,玩家仍走原来的两条。
- [x] 聊天页签只对围观者出现;围观者发言可选频道。

## 3. 大厅

- [x] 判据是**「还坐得下吗」**(`takenSeats < totalSeats`),不是状态字面值。
- [x] 满员 / 进行中 → 围观;有空位 → 入座。

## 4. 判据

- [x] 单测:三步的**顺序**(不只是「都调了」)。
- [x] 单测:围观者 `tap` 不发走子,**配前置断言**证明玩家点同一个点会发。
- [x] 单测:离开的路由按身份分,两个方向都测。
- [x] 单测:玩家看不到围观页签、围观者看得到(**两个方向**)。
- [x] 单测:大厅按空位而不是状态分流(造一个**满员但 Waiting** 的房间)。
- [x] **正面对照:去掉 `JoinRoom` 那一步,看顺序断言红。**
- [x] **正面对照:让围观者的 `tap` 照常发走子,看那条红。**
- [x] **正面对照:把页签条件写成「客户端支持围观」,看玩家那条红。**
- [x] 集成测试:三个真用户,一个围观,**围观者屏幕上**看得到桌上的话;
      围观者点棋盘服务端没有多一步棋;离开之后房间的 `spectators` 空了。

## 5. 不回归

- [x] `flutter analyze` 零问题;`flutter test` 全绿;`shared_sync_test` 绿(零新增键)。
- [x] 既有集成测试逐个跑。

## 6. 收尾

- [x] `JOURNAL.md` 一条。
- [x] 开 PR,写明合并顺序。
