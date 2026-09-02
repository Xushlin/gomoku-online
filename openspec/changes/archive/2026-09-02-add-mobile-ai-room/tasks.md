# tasks

## 1. 一字棋的棋盘(不是新棋盘)

- [x] 注册表里加一字棋,指向**同一个** `GomokuRenderer` 实例。
- [x] 断言两个键指向同一个实例,并断言目录的「启用条数 == 键数」仍然成立。
      **「一个 renderer 只服务一个棋种」此前是巧合** —— 别让巧合变成下一个人的保证。
- [x] 3×3 的越界与星位判据:复用 `xiangqi_board_test` 那套**采样像素**的做法,
      而不是再推导一遍坐标。

## 2. 大厅的两个入口

- [x] 建房入口按 `supportsHumanVsHuman`,人机入口按 `supportsAi`,**都从描述符来**。
- [x] `supportsHumanVsHuman == false` 时显示 `lobby.game-lobby.unavailable.ai-only-*`。
- [x] **两个方向都测**:一字棋没有建房入口、五子棋两个都有。少了后半条,
      「永远隐藏建房」也能通过。
- [x] **正面对照:把入口改成无条件显示,看一字棋那条红。**

## 3. 人机房

- [x] `RoomRepository.createAiRoom(name, gameKey, difficulty, humanSide)` —— 走
      `POST /api/rooms/ai`,**先对着 controller 的特性核过再写**。
- [x] 把这条新路由纳入 `room_route_contract_test.dart`(它就是上一笔猜错路由后补的机制)。
- [x] 弹窗:难度三选一 + 执边二选一,`showDialog`,文案走 `lobby.ai-game.*`。
- [x] 难度错误 MUST NOT 试图从 400 里取字段级消息 —— 那是**绑定层**错误,
      文案是 `lobby.ai-game.errors.generic`。

## 4. 判据(真后端)

- [x] 集成测试:建一个**执白**的人机房 → **一下都不碰屏幕** → AI 的那一子出现在屏幕上、
      回合变成我。**判据是屏幕,不是服务端有那一步。**
- [x] 集成测试:建一个**执黑**的人机房 → 回合是我、盘面是空的。
- [x] 集成测试:一字棋的大厅**没有**建房入口,五子棋的大厅**有**。
- [x] **正面对照:把执白那条的等待去掉(或把入向订阅摘掉),看它红** ——
      这一条钉的正是上一轮那个静默死掉的入向。

## 5. 走查与不回归

- [x] `flutter analyze` 零问题;`flutter test` 全绿;`i18n_keys_test` 覆盖新增文案键。
- [x] `catalog_test` 里那个「恰好 2 个启用」会变成 3。**那正是它该红的时刻。**
- [x] 既有的 4 条集成测试(路由 / 五子棋切片 / 象棋 / 离开)MUST 通过,
      **每一个匹配器与期望值逐字未变**。

## 6. 收尾

- [x] `JOURNAL.md` 一条。
- [x] `CLAUDE.md`:手机端那节**加一行** —— 人机入口按 `supportsAi` 派生,
      而执白时 AI 的第一步是异步来的。
- [x] PR 里报净改动行数。
