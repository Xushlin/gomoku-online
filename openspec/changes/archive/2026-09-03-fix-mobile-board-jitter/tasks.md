# tasks

- [x] **先写能红的测试**:挂 `GameView`,比较回合易手前后棋盘的 `Rect`。
      两个棋种都走(它与 renderer 无关,只跑一个会让人以为是某一个棋种的问题)。
- [x] 理由行改为 `Visibility(maintainSize: true)` —— **预留而不是删除**。
- [x] 错误文案改为 `Stack` 叠加。**不用 SnackBar**(它会自己消失,而集成测试断言它在屏幕上)。
- [x] **正面对照:理由行改回条件插入 → 两条棋种测试红。**
- [x] **正面对照:错误行改回列里的一行 → 那条红。**
- [x] `flutter analyze` 零问题;`flutter test` 全绿。
- [x] `integration_test/xiangqi_test.dart` 仍然绿(它断言拒绝文案在屏幕上)。
- [x] `play_a_move_test` / `game_actions_test` 仍然绿。
- [ ] **没做:真机上再看一眼。** 判据是矩形,不是眼睛。
