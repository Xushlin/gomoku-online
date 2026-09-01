## RENAMED Requirements

`refactor-mobile-mvvm` 归档时把一条**主语是那一次变更**的要求留在了 live spec 里。规则通用,标题不通用 —— 一条写着「重构 MUST NOT…」的 live 要求,下一个人读到时无从判断它管不管自己。这一笔是第二次用到同一条规则,所以在这里改名,而不是记一句「以后再说」。

先 RENAMED 再 MODIFIED:archive 的应用顺序是 RENAMED → REMOVED → MODIFIED → ADDED,只写 MODIFIED 会因为在现行 spec 里找不到新标题而失败。

- FROM: ### Requirement: 重构 MUST NOT 改变任何行为,而判据是既有的端到端切片
- TO: ### Requirement: 手机端的重构 SHALL 由既有的端到端切片判定,而期望值 MUST 逐字未变

## MODIFIED Requirements

**下面这段是从 live spec 里抽出来改的,不是重打的** —— 重打是让一句无关的话被静默改回去的方式。改动限于:标题、以及那两句主语是「上一次」的话。

### Requirement: 手机端的重构 SHALL 由既有的端到端切片判定,而期望值 MUST 逐字未变

`integration_test/play_a_move_test.dart` MUST 通过,且它的**每一个匹配器与期望值 MUST 逐字未变**。

**这条对每一次手机端重构都成立,不只对写下它的那一次。** 那条测试(注册 → 建房 → 对手加入 → 落子 → 服务端记下 (7,7))已经存在,它就是「什么都没变」的可执行形式 —— 与 `play-from-position` 当初用「既有象棋测试一条不改地通过」是同一手,理由也相同:自己写的新断言证明不了自己没改坏东西。

**这条要求原本的标题是「重构 MUST NOT 改变任何行为」,主语是一次已经过去的变更。** 规则是通用的,标题却把它钉在了 `refactor-mobile-mvvm` 上 —— 下一个人读到时无从判断它还管不管自己。改名的时机不是「以后」,而是**第二次用到它的时候**,也就是 `add-mobile-router`。

**判据写的是「匹配器与期望值」而不是「一个字都不改」,因为后者做不到,而写一条做不到的判据只会让人绕过它。** 类型搬了家,取同一个事实的**路径**就得跟着搬:`services.username` → `deps.auth.currentUser?.username`。变的是接收者,不变的是断言什么、期望是什么。两次实测:`refactor-mobile-mvvm` 改了**三行接收者、零个期望值**;`add-mobile-router` 改了 **零行** —— 路由化整个发生在 widget 树的对外面之下,连接收者都没动。**「做不到」指的是不保证为零,不是保证不为零。**

#### Scenario: 期望值未被改动
- **WHEN** 对比本变更与基线
- **THEN** 该文件里 MUST 没有任何匹配器或期望值被修改
- **AND** 接收者路径的改名 MUST 只在类型确实搬家的地方发生,并且逐条能说得出搬到哪了

#### Scenario: 结果一致
- **WHEN** 重构后运行该切片
- **THEN** MUST 仍然是:房间属于本人、状态 `Playing`、**恰好一步且坐标为 (7,7)**

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
