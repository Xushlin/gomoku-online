# mobile-shell Specification

## Purpose
TBD - created by archiving change add-mobile-shell. Update Purpose after archive.
## Requirements
### Requirement: SignalR 连通性 SHALL 在写任何 UI 之前被证伪

本变更 SHALL 先用一个不带 UI 的最小 Dart 脚本证明 `signalr_netcore` 能与本平台的 hub 通信:连上 `/hubs/match`、带查询串 JWT、`JoinRoom` 与 `MakeMove` 各成功一次。

**这不是流程洁癖,是这个变更最大的风险。** `signalr_netcore` 是社区包(1.4.4,最近发布 2025-09-05),而我们的 hub 走查询串 JWT + JSON 协议 + 具名方法。它**可能根本不通**,而那时要做的决定是**换传输方案**(自研协议层,或给 hub 加 REST 落子路径)—— 一个比 UI 重写更大的决定。

铺完 UI 再发现,等于那些 UI 白做。所以顺序是规格的一部分。

#### Scenario: 不通就停下来
- **WHEN** 最小脚本无法完成 `JoinRoom` 或 `MakeMove`
- **THEN** 本变更 MUST 停止并汇报,MUST NOT 继续写 UI

#### Scenario: 通了才继续
- **WHEN** 脚本两个调用都成功
- **THEN** 把「能通」这件事写进 JOURNAL,再开始外壳

### Requirement: 手机端 SHALL 读 web 端那份 i18n 产物,MUST NOT 建第二套翻译

手机端 SHALL 使用 `frontend-web/public/i18n/{zh-CN,en}.json` 作为翻译真源。

**547 个键 × 2 个 locale。** 手抄第二套的漂移表现是「同一句话在两个端不一样」,而没有任何东西会报告它 —— 这个仓库为「手抄清单冒充注册表」已经付过**八次**账。

MUST 有一条测试断言两端的键集合**完全一致**(不是「包含」)。漏一个键的表现是界面上出现原文键。

#### Scenario: 键集合一致
- **WHEN** 比较手机端加载的翻译键与 web 端产物
- **THEN** 两个 locale 的键集合 MUST 完全相等
- **AND** 判据 MUST 是相等而不是包含 —— 「包含」在手机端只用了一半键时也成立

### Requirement: Android 上的默认服务器地址 SHALL 是 `10.0.2.2`,而这不是笔误

Android 目标的默认服务器地址 SHALL 是 `http://10.0.2.2:5145`,并 MUST 在代码注释里写明理由。

模拟器里的 `localhost` 是**模拟器自己**;宿主机的回环在模拟器里是 `10.0.2.2`。写 `localhost` 的表现是每个请求都连接被拒,而屏幕上只是登录失败 —— 看起来像后端没起。

这与桌面壳「宿主给地址」是同一个问题,只是答案不同。

#### Scenario: 模拟器连得上宿主的后端
- **WHEN** 在模拟器里登录,后端跑在宿主的 5145
- **THEN** 请求 MUST 到达后端

### Requirement: 手机端 MUST NOT 自行判定走子合法性

棋盘 SHALL 把落子请求发给服务端并接受它的裁决,MUST NOT 在客户端预判合法性。

与 web 端象棋同一条(设计 D2),理由也同一个:客户端持一份规则就是第二份真源,而两份不一致时玩家读到的是「这一步明明能走」。

#### Scenario: 非法落子由服务端拒绝
- **WHEN** 在已有子的交叉点落子
- **THEN** 请求 MUST 被发出,并由服务端的错误码驱动界面提示

### Requirement: 手机端 SHALL 按 View → ViewModel → Repository → Service 分层

`lib/` SHALL 分成 `data/`(models / services / repositories)与 `ui/<feature>/`(view / view_model),并遵守:

1. **View MUST NOT 直接使用 Service 或 Dio。** 它只与自己的 ViewModel 说话。
2. **模型 MUST 不可变,且只解析。** 模型里出现网络调用或业务规则,说明它该是别的东西。
3. **ViewModel MUST 是 `ChangeNotifier`,且 MUST NOT 持有 `BuildContext`。** 持有了就不能在没有 widget 的情况下测,而那正是它存在的理由。
4. **JSON → 模型 MUST 只在 Repository 里发生。**
5. **Repository 之外 MUST NOT 有人知道 Dio 存在。**

分层照 Flutter 官方架构指南的 MVVM,不自创。

#### Scenario: 分层边界由走查强制,不是由注释
- **WHEN** 任意 `ui/**` 下的文件 import `data/services/**` 或 `package:dio`
- **THEN** 走查测试 MUST 红
- **AND** 该走查 MUST 有过一次**正面对照**(故意加一条这样的 import 并看到它红)——
  一条没见过红的边界检查等于没有,而写在文档里的分层规则是下一个赶时间的人
  第一个绕过的东西

#### Scenario: 模型不依赖上层
- **WHEN** 任意 `data/models/**` 下的文件 import service 或 repository
- **THEN** 走查测试 MUST 红

### Requirement: token 逻辑 SHALL 住在 Dio 拦截器里,并按路径豁免

认证 SHALL 由 Dio 拦截器实现:附加 token、401 时静默刷新并**只重试一次**。

豁免名单(login / register / refresh)MUST 按**路径**匹配,MUST NOT 按整个 URL 前缀 —— base url 一非空,地址就是绝对的,而 `startsWith('/api/auth/login')` 对绝对地址**恒假**。那会给「本身就是凭据」的三个端点挂上 token,并拿刷新令牌去重试刷新本身。

**这一条 web 端与桌面壳各踩过一次**,所以它是继承来的教训,不是新发现。

重试 MUST NOT 成环:一次刷新、一次重试,失败即失败。成环会把一个过期会话变成对登录端点的请求风暴。

#### Scenario: 凭据端点不带 token
- **WHEN** 请求 login / register / refresh,且地址是绝对的
- **THEN** MUST NOT 带 `Authorization` 头

#### Scenario: 其余请求带 token
- **WHEN** 请求任意其它端点
- **THEN** MUST 带 `Authorization`
- **AND** 这一条与上一条 MUST 同时存在 —— 少了它,一个「从不带 token」的实现也能通过

#### Scenario: 401 只重试一次
- **WHEN** 一个受保护请求连续两次收到 401
- **THEN** MUST 只发生一次刷新与一次重试,之后失败

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

### Requirement: 棋种目录 SHALL 从 `GET /api/games` 读,客户端 MUST NOT 存第二份

盘面行列数、座位数、支不支持人人对战、支不支持 AI、是否计分,SHALL 全部取自 `GameDescriptorDto`。手机端 MUST NOT 出现一份手写的棋种表。

**理由不是洁癖,是失配的症状看不见。** `GameDescriptorDto` 自己的文档写着:一份 `rated` 副本错了,症状是**一个永远空着的榜** —— 与「新棋种还没人下过」在屏幕上一模一样。而「手写清单假装成注册表」在这个仓库里已经修过**八次**,其中一次还出现在为了防这件事而新写的检查脚本里。

`Rows` / `Cols` 可空、`SeatCount` 不可空,这个区别 MUST 在模型上保留:每个有规则的棋种都有座位数,而成语接龙真的没有盘面。

走查 MUST 从 `GET /api/games` 的响应派生,MUST NOT 迭代一份手打的键清单。

#### Scenario: 目录的条目来自服务端,而唯一的过滤器是翻译包
- **WHEN** 棋种目录在屏上
- **THEN** 条目 MUST 来自 `GET /api/games`,MUST NOT 来自任何客户端清单
- **AND** 唯一允许的过滤是「翻译包里有没有 `games.<key>.title`」——
  这不是第二份表,而是从 **web 同一份 i18n 产物**派生的判据,
  而那份产物已经被 `test/shared_sync_test.dart` 钉住
- **AND** 这条过滤是实测出来的,不是设计出来的：端点返回 **7** 个,
  其中 `xiangqi-endgame` 在两个 locale 里都**没有标题也没有描述** ——
  它在 web 端也不是一个可浏览的棋种(不在 `GAME_REGISTRY` 里,
  是从象棋古谱页「摆此局对弈」进的)。
  **不过滤它会在一个已发布的屏上渲出一行 `games.xiangqi-endgame.title`。**
- **AND** 测试 MUST 钉住三个实测数字：服务端 **7**、有标题 **6**、可进入 **1**。
  三个数字而不是一个,因为它们分别会在三种不同的变更里变

#### Scenario: 画不出来的棋种显示为禁用,而「画得出来」是派生的
- **WHEN** 某个棋种手机端还没有棋盘
- **THEN** 该条目 MUST 是禁用态,MUST NOT 可点进去
- **AND** 「哪些画得出来」MUST 从棋盘注册表派生,MUST NOT 是一份手写名单

#### Scenario: 两个方向都在样本里
- **WHEN** 跑那条走查
- **THEN** 启用的条数 MUST 等于棋盘注册表的条目数
- **AND** MUST 至少有一个禁用的条目 —— 少了这一半,一个「全部启用」的实现同样通过;
  少了前一半,一个「全部禁用」的实现同样通过
- **AND** 测试 MUST 另钉一个**当下的具体数字**,并在下一个棋种落地时**变红**:
  一个派生的不变量证明形状对,一个具体数字才让「数字变了」有人看见

#### Scenario: 盘面尺寸取自房间的 gameKey,不取自路由
- **WHEN** 路由路径里的 `:key` 与房间快照的 `gameKey` 不一致(手打的 URL 能做到)
- **THEN** MUST 按房间快照的 `gameKey` 取尺寸
- **AND** 理由在 `RoomStateDto` 的文档里:进房间有四条路,**只有「刚建完房跳转」那一条上
  客户端知道棋种**,另外三条它手上只有一个房间 id

---

### Requirement: 棋盘尺寸 SHALL 是行与列两个数,MUST NOT 是一个「边长」

棋盘组件 SHALL 收 `rows` 与 `cols`,MUST NOT 收单个 `size`。

**旧的 `size: int` 有两处不成立,而它读起来像「任意尺寸都行」:** 它假设正方形,而且星位写死成 `[3, 7, 11]`,只对 15 路有意义 —— 传别的值会在盘外画点。**一个只有一个调用方、而那个调用方永远传同一个值的参数,不是参数,是一句没人验证过的承诺。**

顺带修一处注释:类文档说间距是 `size / (n - 1)`,代码是 `side / size` 加半格内缩。**代码是对的,注释是错的。**

**这条要求在本变更之后仍然没有生产调用方**,而这是明写下来的欠账:`rows != cols` 只有测试在用。它与一句空承诺的区别是**下一笔已经指名了那个调用方**(象棋 10×9),且是同一轮里的下一笔。**若 `add-mobile-xiangqi` 不做,这一半 SHALL 退回。**

#### Scenario: 非正方形按行列画
- **WHEN** 以 `rows: 10, cols: 9` 渲染
- **THEN** 交叉点 MUST 是 10×9,MUST NOT 有任何装饰落在盘面之外
- **AND** 盘面比例 MUST 保持 10:9,MUST NOT 被拉成正方形

#### Scenario: 15 路仍然照旧
- **WHEN** 以 `rows: 15, cols: 15` 渲染
- **THEN** 星位 MUST 仍在 15 路棋盘该在的位置
- **AND** 这一条与上一条 MUST 同时存在:只断言非正方形的话,
  一个「永不画装饰」的实现同样通过

---

### Requirement: 手机端的每一级导航 SHALL 是一层路由

路由为 `/login`、`/`(棋种目录)、`/games/:key`(大厅)、`/games/:key/rooms/:id`(一局),后三者 SHALL 嵌套。

**判据是 `canPop`,不是「用了嵌套写法」。** `add-mobile-router` 里量过:把子路由改成顶层路由,**编译通过、分析零问题、`redirect` 照旧**,而房间里 `canPop()` 立刻变 false、`AppBar` 一个返回按钮都不画。这一条是那个形状的第一次复用,所以判据照抄。

#### Scenario: 三层栈,每一级都能返回
- **WHEN** 从目录进大厅、再进一局
- **THEN** 一局里 `canPop()` MUST 为 true,一次 `popRoute` MUST 回到大厅
- **AND** 大厅里 `canPop()` MUST 为 true,一次 `popRoute` MUST 回到目录

#### Scenario: 目录是栈底
- **WHEN** 棋种目录在屏上
- **THEN** `canPop()` MUST 为 false —— 已登录的人按返回 MUST NOT 退回登录页
- **AND** 这一条与上一条 MUST 同时存在:只断言「能返回」的话,
  一个「什么都 pop」的实现同样通过

#### Scenario: 那行硬编码的棋种键没了
- **WHEN** 检查大厅的 ViewModel
- **THEN** MUST NOT 再有 `const gameKey = 'gomoku'`,棋种键 MUST 来自路由参数
- **AND** 它当初的注释写着「一个只有一项的选择器是假装成平台的选择器」——
  那句话的兑现日就是这一笔

