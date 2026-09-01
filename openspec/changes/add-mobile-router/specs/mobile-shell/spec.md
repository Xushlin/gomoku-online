## ADDED Requirements

### Requirement: 手机端的每一屏 SHALL 是一个真正的路由,判据是 `canPop`

导航 SHALL 由 `go_router` 承担,三条路由:`/login`、`/`(大厅)、`/rooms/:id`。

判据写成 `canPop` 而不是「用了 go_router」,因为**装了包不等于屏变成了路由** —— 一个把 `MaterialApp.home` 换成 `GoRouter` 却仍然在一个路由里 `switch` 的实现会通过后者、通不过前者。

**这不是收拾屋子,是量出来的:** 房间里 `Navigator.canPop()` 为 **false**,发一次 `popRoute`(安卓返回键走的就是这条)之后**还在房间里**。而屏幕上那个返回箭头**能**离开房间 —— 对照组把结论收窄成对的那句:不是「离不开房间」,是「系统返回键什么也不做」。

#### Scenario: 房间是一个可以被弹出的路由
- **WHEN** 一局棋的界面在屏上
- **THEN** `Navigator.canPop()` MUST 为 true
- **AND** 一次 `popRoute` MUST 回到大厅

#### Scenario: 大厅是栈底
- **WHEN** 大厅在屏上
- **THEN** `Navigator.canPop()` MUST 为 false —— 已登录的人按返回 MUST NOT 退回登录页
- **AND** 这一条与上一条 MUST 同时存在:只断言「返回键离开了房间」的话,一个「什么都 pop」的实现同样通过

#### Scenario: 「哪一屏」这个状态没地方藏
- **WHEN** 检查应用外壳
- **THEN** `GewuApp` MUST 是 `StatelessWidget`,且 MUST NOT 再有 `_authenticated` / `_openRoomId` 这类字段
- **AND** 判据是类型而不是走查:**一个 `StatelessWidget` 拿不到 `setState`**,所以这条由编译器守,不由下一个读代码的人守

---

### Requirement: 「回登录页」SHALL 由一个地方决定,MUST NOT 由每一屏各自决定

会话失效 SHALL 通过路由的 `redirect` 落到 `/login`,而不是靠某一屏自己发现 401 后自己跳。

**量到的现状:** 把两个 token 都弄成不可用之后点大厅的刷新,`at-login=false`、`still-at-lobby=true` —— `_authenticated` 变 false 只有一处(登出按钮),所以刷新令牌过期时人留在大厅上看一个错误提示。

重定向 MUST 是双向的:未认证的人进不去大厅,**已认证的人也 MUST NOT 停在登录页**。只写前一半的话,一个「永远重定向到 `/login`」的实现会通过。

#### Scenario: 会话死了就回登录页
- **WHEN** 刷新令牌不可用,且此后任意一次受保护请求得到 401
- **THEN** MUST 落到 `/login`
- **AND** MUST NOT 停在原来那一屏只显示一个错误提示

#### Scenario: 已登录的人不停在登录页
- **WHEN** 已经认证,而目标是 `/login`
- **THEN** MUST 重定向到 `/`

#### Scenario: 未登录的人进不去房间
- **WHEN** 未认证,而目标是 `/rooms/:id`
- **THEN** MUST 重定向到 `/login`

---

### Requirement: ViewModel MUST NOT 在被 dispose 之后通知

一个 `ChangeNotifier` 在 `await` 之后 SHALL 先确认自己还活着,再 `notifyListeners()`。

**这是这次量出来的一条真实断言,不是防御性编程:**

```
A GameViewModel was used after being disposed.
GameViewModel.open (game_view_model.dart:31)
```

触发条件是 `open()` 还在飞的时候树被拆掉 —— 在应用里就是「点进房间、立刻返回」。**路由化会让它更容易触发**,因为一个路由被 pop 就 dispose,比整棵子树被换掉频繁得多。

而它 **只在 debug 崩:** release 构建里 `debugAssertNotDisposed` 是空的。**一个只在 debug 崩的 bug 是更糟的一种,不是更轻的一种** —— 在用户手上它表现为一次没有发生的界面更新。

#### Scenario: 飞行中的请求落地时视图已经没了
- **WHEN** `GameViewModel.open()`(或 `place()`)的 `await` 还没返回,而它已经被 dispose
- **THEN** MUST NOT 抛 `A GameViewModel was used after being disposed`
- **AND** 该场景 MUST 有一个**看见过它红**的测试:一条从没红过的守卫等于没有,而这一条的正面对照就是把守卫删掉再跑一次
