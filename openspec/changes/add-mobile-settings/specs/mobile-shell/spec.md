## ADDED Requirements

### Requirement: 主题与深色模式 SHALL 可切换,而主题列表 SHALL 从同步产物派生

设置页 SHALL 让人选主题与深浅色,并持久化到本地。

可选主题 SHALL 从 `themeTokens` 的键派生,MUST NOT 在页面里手写一份名单。那份产物由 `tool/sync_shared.dart` 从 web 同步、由 `shared_sync_test` 钉住;**「手写清单假装成注册表」是这个仓库修过八次的缺陷**,而四个主题名字看起来足够稳定,正是它容易再犯一次的地方。

主题与深浅 SHALL 是**两个正交的轴**,MUST NOT 合并成一个八选一的列表 —— 与 web 端同一个模型。

#### Scenario: 每一个主题都有名字
- **WHEN** 走查遍历 `themeTokens` 的键
- **THEN** 每一个 MUST 有 `header.theme.<key>` 的文案,两个 locale 都要有
- **AND** 这条走查 MUST 从 `themeTokens` 派生 —— 下次 web 加一套主题同步过来,
  它 MUST 红,而不是页面上多一个渲成原始键的选项

#### Scenario: 两个轴各自独立
- **WHEN** 切换深色模式
- **THEN** 主题名 MUST 不变;反之切换主题时深浅 MUST 不变
- **AND** 两个方向 MUST 同时被测:少了任何一半,一个「切一个就重置另一个」的实现
  都能通过剩下那半

#### Scenario: 选择留得住
- **WHEN** 选好之后重启应用
- **THEN** MUST 仍然是那个主题和那个深浅
- **AND** MUST NOT 存进放刷新令牌的那个安全存储 —— 主题名不是秘密

#### Scenario: 棋盘颜色跟着主题走,而这不是第三件事
- **WHEN** 主题改变
- **THEN** 棋盘底色 MUST 跟着变(`AppTheme.boardBackground` 读的就是主题 token 的
  `color-well`)
- **AND** 手机端 MUST NOT 另建一条独立的棋盘皮肤轴 —— 那是 web 的 `BoardSkinService`
  那一摊,没有同步过来,而**换主题已经换了棋盘颜色**

---

### Requirement: 退出登录 SHALL 先确认,而 MUST NOT 为此新增翻译键

点退出 SHALL 先弹确认,取消则 MUST NOT 退出。

文案 SHALL 由既有的键拼出(标题 `header.auth.logout`,按钮 `lobby.ai-game.cancel` 与 `header.auth.logout`)。**MUST NOT 新增手机端专属的键** —— 手机端那两份 i18n 是 web 产物的同步副本,`shared_sync_test` 会红,而那条走查存在的理由就是不许有第二套翻译。

#### Scenario: 取消不退出
- **WHEN** 弹出确认后选「取消」
- **THEN** MUST NOT 调用登出,MUST 仍然停在当前页
- **AND** 这条负面断言 MUST 配一条前置断言证明**当时确实是登录状态** ——
  否则它对「根本没弹窗」也是绿的

#### Scenario: 确认才退出
- **WHEN** 选「退出登录」
- **THEN** MUST 登出,并 MUST 回到登录页
- **AND** 回登录页 MUST 由既有的 `redirect` 完成,MUST NOT 另写一次导航 ——
  两个答案回答同一个问题,第一次改动就会分叉

---

### Requirement: 设置页 SHALL 是既有三层栈里的一层,而外壳 SHALL 保持无状态

设置页 SHALL 是嵌在 `/` 底下的一条路由,MUST NOT 自造导航。

`GewuApp` SHALL 仍然是 `StatelessWidget`。**这一条由编译器钉着**(`test/shell_state_test.dart` 里一个类型为「返回 `StatelessWidget` 的构造函数」的 tear-off),所以主题改变时的重建 SHALL 由 `MaterialApp.router` 外面的一个监听器完成,MUST NOT 把状态搬回外壳。

#### Scenario: 返回键照旧
- **WHEN** 从目录进设置页
- **THEN** `canPop()` MUST 为 true,一次 `popRoute` MUST 回到目录
- **AND** 判据仍然是 `canPop` —— `add-mobile-router` 里量过:改成顶层路由会编译通过、
  分析零问题、`redirect` 照旧,而 `canPop()` 立刻变 false
