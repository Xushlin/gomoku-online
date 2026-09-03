# tasks

## 1. 存

- [x] 加 `shared_preferences`。**不复用 `flutter_secure_storage`** —— 那是放刷新令牌的,
      主题名不是秘密。
- [x] `SettingsRepository`:主题名 + 深浅,读一次、写即存,暴露成一个可监听的值。
- [x] 未存过时的缺省 = 今天写死的那两个(`ink` + 深色),**这样升级的人看不出变化**。

## 2. 切

- [x] `GewuApp` 在 `MaterialApp.router` 外面套一个监听器。**外壳仍然是 `StatelessWidget`** ——
      那条由 `shell_state_test.dart` 的 tear-off 用编译器钉着,别搬状态回去。
- [x] 主题与深浅是**两个正交的轴**,不是八选一。
- [x] 主题列表从 `themeTokens.keys` 派生,**页面里不写名单**。

## 3. 设置页

- [x] 路由 `/settings`,嵌在 `/` 底下;入口放目录页 AppBar(`header.settings.label`)。
- [x] 主题一组、深浅一个开关;文案 `header.theme.*`。
- [x] 退出确认:标题 `header.auth.logout`,按钮 `lobby.ai-game.cancel` / `header.auth.logout`。
      **一个键都不新增** —— 新增会让 `shared_sync_test` 红。
- [x] 两处退出(目录页、大厅)都走同一条确认 —— **两条路径会分叉,而分叉的表现是
      某一条悄悄不问了**。

## 4. 判据

- [x] 走查:`themeTokens` 的每一个键都有 `header.theme.<key>` 文案,两个 locale。
      **从 keys 派生,不手写。**
- [x] 单测:切主题不动深浅、切深浅不动主题(**两个方向**);缺省值;持久化后读回。
- [x] 单测:取消不登出(**配前置断言**证明当时是登录状态)、确认才登出。
- [x] 集成测试:目录 → 设置 → `canPop` 为 true → 返回回到目录;换一个主题看
      `MaterialApp` 的 `ThemeData` 真的变了。
- [x] **正面对照:往 `themeTokens` 里塞一个没有文案的键,看那条走查红。**
- [x] **正面对照:让切深浅时顺手重置主题名,看那两个方向里对应的红。**

## 5. 走查与不回归

- [x] `flutter analyze` 零问题;`flutter test` 全绿。
- [x] 既有集成测试**匹配器与期望值逐字未变**(目录页 AppBar 多一个图标,不该影响它们)。
- [x] `shared_sync_test` MUST 仍然绿 —— 一个新增的翻译键会让它红,那是对的。
- [ ] **没做:装到真机上换一次主题。** `adb devices` 此刻没有设备(线拔了),而这三条
      正是真机提的 —— 所以这一条是这一笔唯一没有真机确认的部分。桌面上的集成测试挂的是
      **同一个外壳**,量到的是同一个 `ThemeData`,**是近似不是替代**。

## 6. 收尾

- [x] `JOURNAL.md` 一条。
- [x] `CLAUDE.md`:手机端那节加**一行**(主题两轴、列表从同步产物派生、棋盘颜色不是独立轴)。
- [x] PR 里报净改动行数。
