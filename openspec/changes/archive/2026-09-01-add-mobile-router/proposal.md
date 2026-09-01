# add-mobile-router

引入 `go_router`,把手机端的三屏变成三个真正的路由,并把「去登录页」这个决定收到一个地方。

## 为什么 —— 四个量出来的读数,不是「更规范」

现在切屏是 `GewuApp` 里对 `(_authenticated, _openRoomId)` 做 `switch`,整个 `Navigator` 上永远只有 `MaterialApp.home` 一个路由([app.dart:114](../../../frontend-mobile/lib/app.dart)）。在 Windows 桌面上跑真实 widget 树 + 真后端量到:

| 读数 | 值 |
| --- | --- |
| 房间里 `Navigator.canPop()` | **false** —— 游戏屏不是一个路由 |
| 发一次 `popRoute`(安卓返回键/手势走的就是这条)后还在房间里 | **true** |
| 屏幕上那个返回箭头能离开房间 | **true**(对照组) |
| 会话被弄死之后落在登录页 | **false**,`still-at-lobby=true` |

第三行是对照组,而它把结论收窄成了对的那句:**不是「离不开房间」,是「系统返回键什么也不做」**。`WidgetsApp` 问 `Navigator.maybePop()`,拿到 false 就告诉引擎自己没处理,于是安卓自己处理 —— finish activity。屏幕上的箭头照样能用,所以这不是「不可用」,是「每个安卓用户按下去都会得到错的结果」。

第四行是另一个洞:`_authenticated` 变 false 只有一处 —— 大厅的登出按钮。刷新令牌过期时 `RefreshInterceptor.refresh()` 返回 false、请求 401、ViewModel 弹一个 `errorKey`,**人就留在大厅上**。没有任何一条路把「会话死了」变成「回登录页」。

**为什么是现在而不是第二个棋种落地时:** `lib/` 里 `Navigator.` 出现 **0 次**,路由表 **0 个**。今天迁移的调用点数量是零。第十个棋种的时候不是。

## 改什么

三条路由,一个重定向:

```
/                 大厅
/login            登录 / 注册
/rooms/:id        一局棋
```

- `redirect` 一个地方决定去哪:未认证 → `/login`;已认证还停在 `/login` → `/`;会话中途死掉 → `/login`(靠 `refreshListenable` 收一个「认证状态变了」的信号)。
- `GewuApp` 里的 `_authenticated` 与 `_openRoomId` **删掉**。**最好的机制是能被删掉的那种** —— 它俩消失就是路由表真的接手了的证据。
- `GewuApp` 从 `StatefulWidget` 变成 `StatelessWidget`。这不是收拾屋子:**一个 `StatelessWidget` 拿不到 `setState`,所以「哪一屏」这个状态在编译期就没地方藏了。** 一条源码规则守不住的东西,让类型去守。
- `LoginView` / `LobbyView` / `GameView` 的回调(`onSignedIn` / `onOpenRoom` / `onLeave`)换成一次 `context.go(...)`。View 仍然不认识 repository。

## 一个必须先修的缺陷,而它是这次量出来的

`GameViewModel.open()` 在 `await` 之后无条件 `notifyListeners()`([game_view_model.dart:31](../../../frontend-mobile/lib/ui/game/view_model/game_view_model.dart));`place()` 在 `finally` 里也是同一个形状。探针跑出来一条真实断言:

```
A GameViewModel was used after being disposed.
GameViewModel.open (game_view_model.dart:31)
```

触发条件是 `open()` 还在飞的时候树被拆掉 —— 在应用里就是「点进房间、立刻返回」。**路由化会让它更容易触发**,因为路由被 pop 就 dispose,比现在整棵子树换掉的频率高得多。所以它在这次里修,不单开一笔。

release 构建里 `debugAssertNotDisposed` 是空的,所以这个 bug **只在 debug 崩,在用户手上静默** —— 那是更糟的一种,不是更轻的一种。

## 不做,以及理由

- **深链接**(邀请链接 / 通知直达房间)。`go_router` 让它便宜,但它要动 `AndroidManifest` 的 intent-filter、要一套 link 的 host 约定,而现在没有产生邀请链接的地方。**触发条件:第一个会发出房间链接的功能落地。**
- **`freezed` / `json_serializable` / `retrofit` / `get_it` / `drift`。** 数字不支持:`copyWith` 在 `lib/` 里 **0 次调用**(hub 推整个房间的 JSON,repository 直接重新解析),端点 **6 个**,模型 **7 个**。freezed / json_serializable 的触发条件已经写在 `refactor-mobile-mvvm` 的提案里(模型 > ~12 个,或第一次手写 `fromJson` 漏字段出错),且它俩 **MUST 同时落地** —— 分两次等于模型写两遍。
- **换 Riverpod。** Provider 是 Flutter 官方架构指南自己用的,刚落地、有测试守着。为了「更主流」换掉一个能跑的状态管理不划算。
- **底部导航 / 嵌套路由 shell。** 三屏还不需要一个 shell。触发条件:第二个顶层目的地(比如「我的记录」)落地。

## 归档顺序

`refactor-mobile-mvvm` 还没归档,而这一笔也只往 `mobile-shell` 里 **ADDED**,不 MODIFIED 任何要求 —— 所以不需要手工合并。但仍然**按合并顺序归档**:先 `refactor-mobile-mvvm`,再这一笔。

## 验收

四个读数逐条翻面,且**两个方向都在样本里** —— 「返回键离开了房间」单独成立时,一个「什么都 pop」的实现也能通过,所以大厅是栈底这件事必须同时被断言。
