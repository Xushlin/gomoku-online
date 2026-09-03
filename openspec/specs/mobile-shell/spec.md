# mobile-shell Specification

## Purpose
TBD - created by archiving change add-mobile-shell. Update Purpose after archive.
## Requirements
### Requirement: SignalR 连通性 SHALL 在写任何 UI 之前被证伪

本平台的每一个实时客户端 SHALL 先用一个不带 UI 的最小脚本证明它能与本平台的 hub 通信,而那份证明 MUST 覆盖**两个方向**:连上 `/hubs/match`、带查询串 JWT、`JoinRoom` 与一次落子各成功一次(**出向**),**并且收到至少一条服务端推送、断言它的内容**(**入向**)。

**一个只测发送的连通性证明是半个证明,而这笔账已经付过了。** 手机端订阅的是 `RoomStateChanged`,服务端发的是 `RoomState` —— SignalR 对「订阅了一个没人调用的名字」不报错也不警告,于是**入向从第一天起就是死的**,而这条要求是满足的、每一条测试是绿的。症状是对手加入后屏幕还写「等待中」、自己落了子盘面一动不动。

**这不是流程洁癖,是这个变更最大的风险。** `signalr_netcore` 是社区包(1.4.4,最近发布 2025-09-05),而我们的 hub 走查询串 JWT + JSON 协议 + 具名方法。它**可能根本不通**,而那时要做的决定是**换传输方案**(自研协议层,或给 hub 加 REST 落子路径)—— 一个比 UI 重写更大的决定。

铺完 UI 再发现,等于那些 UI 白做。所以顺序是规格的一部分。

#### Scenario: 不通就停下来
- **WHEN** 最小脚本无法完成出向调用,**或者一条推送都没收到**
- **THEN** 该变更 MUST 停止并汇报,MUST NOT 继续写 UI

#### Scenario: 入向的判据是内容,不是「连上了」
- **WHEN** 客户端已经进了房间,且服务端广播了一次房间状态
- **THEN** 脚本 MUST 收到那条推送,并 MUST 断言它的内容(那一步棋在里面)
- **AND** 拿 REST 查「服务端有没有这一步」**MUST NOT** 算作入向的证明 —— 它证明的是**服务端收到了**,不是**客户端收得到**。旧版探针里那一条自称「正面对照」的断言正是这个形状

#### Scenario: 通了才继续
- **WHEN** 两个方向都成功
- **THEN** 把「两个方向都能通」这件事写进 JOURNAL,再开始外壳

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

### Requirement: 象棋局面 SHALL 由客户端重放走子得出,而重放 MUST NOT 含任何规则

服务端不下发局面(`GameSnapshotDto` 只有 `Moves`),所以客户端 SHALL 由初始摆法逐步应用 `from → to` 得出盘面。

重放 MUST 只做两件事:把 `from` 上的子搬到 `to`,`to` 上原有的子消失。它 MUST NOT 知道任何一种子怎么走。

**这与「手机端 MUST NOT 自行判定走子合法性」不冲突,而是同一条要求的另一面:** 重放连「马走日」都不知道,它不判断任何一步该不该被允许。这条要求存在,是因为「客户端画得出盘」很容易滑成「客户端算得出合法着法」,而后者是平台明确不要的。

初始摆法 MUST 由「后排次序 + 关于中路的镜像对称」推出,MUST NOT 手打 32 个格子。

#### Scenario: 初始摆法拿真实开局对着服务端校验,不跟另一份副本对照
- **WHEN** 在一局真实象棋里下出一步真实开局(炮二平五)
- **THEN** 服务端 MUST 接受它
- **AND** 判据就是这一条:客户端对「那个炮在哪」的看法一旦与服务端不一致,这一步会被拒。
  **跟权威比,不跟另一份客户端副本比** —— 两份副本可以一起错

#### Scenario: 吃子会让被吃的子消失
- **WHEN** 一步走子的落点上原有敌子
- **THEN** 重放后该格 MUST 只有走过来的那个子
- **AND** 全盘子数 MUST 减一

#### Scenario: 重放里没有规则
- **WHEN** 检查重放的实现
- **THEN** MUST NOT 出现任何一种子的走法判断

---

### Requirement: 象棋的座位 SHALL 读作红 / 黑,座位 0 是**红**,且这个读法按棋种分

象棋房里座位 0 SHALL 显示为红方、座位 1 为黑方。`Stone.Black` 在象棋里读作**红**。读法 SHALL 按 `gameKey` 分派,MUST NOT 按座位数分派。

**为什么必须有 Scenario:** `Game` 从 `Stone.Black` 开局,象棋红先。web 端把这个读法做成两个命名常量,注释写明「不让任何地方出现裸的 `=== 'Black'`,因为那正是将来有人来『修正』它的地方」。而这条规则栽过一次 —— `web-game-board` 曾把「象棋读作红 / 黑」写在**括号里**,没有实现读它、没有测试守它,同一条要求下面的 Scenario 还明说「侧栏说『黑方 / 白方』」,三处代码照着 Scenario 写。**一个写在括号里的例外和没写是一样的。**

**为什么必须按棋种分:** 当初那条判据被写成「座位数大于二 → 说座位号」,而它的理由是「『白方走棋』在一个没有白方的棋种里是错的」。象棋和五子棋都恰好两个座位,于是该拦的从缝里过去了。**判据要贴着理由写,不要贴着当时手边那个数字写。**

#### Scenario: 象棋房说红 / 黑
- **WHEN** 象棋房在屏上
- **THEN** 座位 0 MUST 显示为红方,座位 1 MUST 显示为黑方
- **AND** MUST NOT 出现「黑方 / 白方」

#### Scenario: 五子棋房仍说黑 / 白
- **WHEN** 五子棋房在屏上
- **THEN** 座位 0 MUST 显示为黑方,座位 1 MUST 显示为白方
- **AND** 这一条与上一条 MUST 同时存在 —— 少了它,一个「永远说红 / 黑」的实现同样通过

#### Scenario: 分派依据是棋种,不是座位数
- **WHEN** 检查读法的实现
- **THEN** 分派 MUST 以 `gameKey` 为依据
- **AND** MUST NOT 以座位数为依据 —— 象棋与五子棋都是两个座位,按座位数分派**恰好分不开**

---

### Requirement: 象棋盘 SHALL 按描述符的行列渲染,装饰 MUST NOT 与五子棋共用一个画笔

象棋盘 SHALL 是 10 行 9 列,取自 `GameDescriptorDto` 的 `Rows` / `Cols`,MUST NOT 有客户端硬编码。

#### Scenario: 尺寸来自描述符
- **WHEN** 打开一个 `gameKey` 为 `xiangqi` 的房间
- **THEN** 棋盘 MUST 是 10 行 9 列
- **AND** 客户端 MUST NOT 有任何把 `xiangqi` 映射到 `10, 9` 的表

#### Scenario: 装饰不共用
- **WHEN** 画象棋盘
- **THEN** MUST 有河界与九宫斜线,MUST NOT 有星位
- **AND** 两个棋种的装饰 MUST NOT 由同一个画笔里的 `if (gameKey == …)` 分派 ——
  共用的是几何(格距、内缩、交叉点 ↔ 像素),不是装饰

#### Scenario: 满盘在 375 px 下不溢出
- **WHEN** 在 375 px 宽下渲染**开局满盘 32 子**
- **THEN** MUST NOT 有任何溢出
- **AND** 判据 MUST 是满盘而不是空盘:**空盘通过每一条布局断言**,
  而这个仓库四个溢出缺陷里有三个在空数据上看不见

---

### Requirement: 象棋走子 SHALL 走 `MovePiece`,交互 SHALL 是选中 → 落点

客户端 SHALL 调用 `MovePiece(roomId, fromRow, fromCol, row, col)`,MUST NOT 给 `MakeMove` 加参数。

**理由是 SignalR 的绑定层:** 它不套用 C# 可选参数缺省值,客户端多送或少送参数都会在绑定层被拒 —— 早于任何 filter,且低于配置的日志级别,所以**两头都看不见**。给活着的 hub 方法加参数是破坏性变更;加方法不是。

#### Scenario: 点自己的子是重新选中,不是走子
- **WHEN** 已选中一个自己的子,又点了另一个自己的子
- **THEN** MUST 改为选中后者,MUST NOT 发出任何走子
- **AND** 这条**也是测试的约束**:「往自己子上走」因此测不出服务端拒绝,
  因为什么都没发出去

#### Scenario: 一步非法的走子由服务端拒绝,客户端不预判
- **WHEN** 选中一个子,落点是该子走不到的**空格**
- **THEN** 客户端 MUST 把它发出去
- **AND** 服务端 MUST 拒绝,界面 MUST 显示错误
- **AND** 落点 MUST 是空格或敌子 —— 这是唯一能确认客户端没有偷偷自己判合法性的路

### Requirement: 客户端订阅的 hub 方法名 SHALL 从服务端源码派生

客户端 `connection.on('X')` 里的每一个 `X` MUST 属于服务端 `SendAsync("X"` 的全集,而那个全集 MUST 从服务端源码里抽,MUST NOT 手写在客户端旁边。

**这条机制已经落地(`test/hub_contract_test.dart`),而当时没有任何要求守着它。** 一个有机制没要求的东西,是下一个整理代码的人第一个删掉的。

实测的数字:服务端发 **10** 个方法名,web 端订阅的 10 个逐字对得上(那是同时写出来的,不是有机制),手机端订阅 **2** 个、其中 **1** 个不存在。

#### Scenario: 订阅一个不存在的名字会红
- **WHEN** 客户端订阅一个服务端不发的方法名
- **THEN** 走查 MUST 红,并 MUST 指名那个字符串
- **AND** 合法名字的全集 MUST 从 `backend/src/Gewu.Api/Hubs/SignalRRoomNotifier.cs` 里抽,
  `shared_sync_test` 读 `frontend-web/` 早有先例

#### Scenario: 两边都不能是空的
- **WHEN** 跑那条走查
- **THEN** 服务端那份名字集 MUST 非空(至少 8 个),客户端那份 MUST 非空
- **AND** 少了这一条,一个源文件改名就会让两边都变空,而「每个名字都合法」对空集
  平凡成立 —— 正是这条要求要防的那种形状

### Requirement: 离开房间 SHALL 告诉服务端,而走哪条路由由服务端的规则决定

离开一个房间 SHALL 调用 `POST /api/rooms/{id}/leave`,**除非**调用者是一个**等待中房间的房主** —— 那种情况 SHALL 调 **`DELETE /api/rooms/{id}`**。

**解散的路由是实测出来的,而第一版猜错了。** 它写的是 `POST /api/rooms/{id}/dissolve`,那条路由不存在;而客户端旁边那条单测断言的正是这个错路径**并且通过了**,因为假 adapter 接受任何 URL。**一个断言「客户端发出了它被写成要发的那个 URL」的测试,对「服务端有没有那个 URL」一无所知。** 只有真服务端说了 404。`test/room_route_contract_test.dart` 现在从 controller 自己的特性里抠出合法的「动词 + 模板」集合。

**这个分支不是客户端的偏好,是服务端的规则:** 等待中房间的房主走 `/leave` 会被明确拒绝(`HostCannotLeaveWaitingRoom`),而 `/dissolve` 只对等待中的房间开放。把它写成一条客户端的「选择」会让下一个人以为可以两条都试。

离开 SHALL 同时:退出 hub 分组(`LeaveRoom`),并摘掉那个把 hub 推送搬进 `live` 的监听器。

**在这一笔之前手机端一次都没有调过它们。** `lib/` 里搜 `leave` / `dissolve` 只有注释,hub 的 `LeaveRoom` 是 0 次 —— 按下返回箭头只 pop 了一个路由,服务端那边人还坐在座位上。

#### Scenario: 判据是那个分支,而不是座位 —— 座位那条是错的
- **WHEN** 离开之后去问服务端
- **THEN** **等待中房间的房主**那一支:房间 MUST 不存在了(`GET` 返回 404)
- **AND** **其他任何人**那一支:房间 MUST 仍然存在
- **AND** 「离开之后那个座位空了」**MUST NOT** 作为判据 —— **实测:两种状态下座位都不腾空**,
  所以普通的 `/leave` 没有任何服务端可观测的变化。这条判据的第一版就是这么写错的,
  而它听起来完全合理
- **AND** 「调用返回 200」也 MUST NOT 算作判据 —— 那与「服务端收到了」是同一类的半个证明

#### Scenario: 分支搞反了服务端会拒,而那正是它承重的地方
- **WHEN** 分支选错
- **THEN** 服务端 MUST 拒绝,实测三种:等待中房间的房主走 `/leave` 是 **409**
  (「dissolve it instead」)、非房主解散是 **403**、解散进行中的房间是 **409**
- **AND** 两个方向 MUST 同时被测:少了后半条,一个「永远解散」的实现在自己的房间里通过,
  而在别人的房间里把人困住

#### Scenario: 两座位的房间没有「两个人的等待中」
- **WHEN** 第二个人加入一个两座位的房间
- **THEN** 状态**立刻**变成 `Playing`
- **AND** 所以「等待中的房间」指的是**还空着座位**的那种。这一条写下来是因为不写它
  就会去构造一个不存在的状态,而那种测试失败时看起来像功能坏了

---

### Requirement: 房间被解散时,客户端 SHALL 把人送出去

收到 `RoomDissolved` 时客户端 SHALL 导航回该棋种的大厅。

**解散之后不会再有 `RoomState`** —— 房间被物理删除了。所以忽略这条推送不是「少一个提示」,是把人留在一个已经不存在的房间里:盘面还在,点哪儿都是错误。

#### Scenario: 收到解散就离开这一屏
- **WHEN** 客户端在房间里且收到 `RoomDissolved`
- **THEN** MUST 导航回该棋种的大厅
- **AND** MUST NOT 停在盘面上等下一条 `RoomState` —— 不会有下一条了

#### Scenario: 这条订阅仍然在契约走查的范围内
- **WHEN** 跑 `hub_contract_test`
- **THEN** 订阅数从 2 变成 3,而走查 MUST 仍然绿(它断言的是「订阅的 ⊆ 服务端发的」)
- **AND** 那条走查是**一边倒**的:它对「服务端发的里哪些*该*订阅」不作断言,而那一半
  没法机械推导 —— 十个里七个不订阅是对的。**本笔只补那一个真缺口,不追平 10/10**

---

### Requirement: 离开一局进行中的对局 SHALL 先问,等待中的 MUST NOT 问

进行中的对局 SHALL 在离开前用一个对话框确认,文案取自 `game.leave-confirm.*`;等待中的房间 MUST NOT 问。

**理由在语义里:** 离开不会结束这一局,座位仍然是你的 —— 但回合计时继续走,超时有后果。一个静默的出口在这种语义下是错的。

判据 SHALL 与 web 端**同一条**,MUST NOT 另写一条:两条规则会分叉,而分叉的表现是某条路径悄悄不问了,那是看不出来的。

对话框 SHALL 用框架提供的那条路(`showDialog`),而不是手搭一个覆盖层 —— 焦点陷阱与返回键处理不该重新实现。

#### Scenario: 进行中要问
- **WHEN** 在一局进行中的对局里按离开
- **THEN** MUST 出现确认对话框,选「留下」MUST NOT 发出任何离开调用

#### Scenario: 等待中不问
- **WHEN** 在一个等待中的房间里按离开
- **THEN** MUST 直接离开,MUST NOT 出现对话框
- **AND** 这一条与上一条 MUST 同时存在:少了它,一个「永远问」的实现同样通过

---

### Requirement: 那个把推送搬进 `live` 的监听器 SHALL 注册一次、摘掉一次

`RoomRepository.open()` SHALL 保证同一个监听器不会被重复注册,离开时 SHALL 摘掉它。

**它此前每开一个房间就注册一次,而没有任何地方摘。** 进过五个房间就注册五次,每条推送把解析跑五遍。今天无害(它是幂等的),**而无害恰好是这种东西活下来的方式**。

#### Scenario: 进出多个房间之后,盘面是当前这个房间的
- **WHEN** 进房间 A、离开、进房间 B,然后 A 里有人走了一步
- **THEN** B 的盘面 MUST 不变
- **AND** 这条 Scenario 在 `fix-mobile-hub-inbound` 之前**不可能失败**,因为那时一条推送
  都到不了。**「不调 LeaveRoom」在入向是死的那段时间里是无害的,修好入向让它变成了活的**
  —— 一个结论可以在支撑它的前提变假之后仍然看起来没问题

### Requirement: 人机对局入口 SHALL 按 `supportsAi` 出现,MUST NOT 按一份清单

哪些棋种能开人机房,SHALL 读 `GameDescriptorDto.supportsAi`,MUST NOT 在客户端写一份「哪些棋种有 AI」的名单。

**那个字段存在的理由就是这件事:** 它投影自 `IGameAiRegistry.For(gameKey) is not null`,与 `POST /api/rooms/ai` 的校验读**同一份**注册表,所以客户端看到的与服务端会接受的不可能不一致。它自己的文档写着:一份手写副本的症状是**一个永远 400 的按钮**。

#### Scenario: 入口跟着描述符走,两个方向都要
- **WHEN** 某棋种 `supportsAi` 为 true
- **THEN** 它的大厅 MUST 有人机对局入口
- **AND** `supportsAi` 为 false 的棋种 MUST 没有 —— 少了后半条,一个「永远显示」的实现
  同样通过前半条,而它在那些棋种上就是那个永远 400 的按钮

---

### Requirement: 不支持人人对战的棋种 MUST NOT 显示建房入口

`supportsHumanVsHuman` 为 false 的棋种,大厅 MUST NOT 显示建房入口,MUST 显示「目前只有人机对战」。

**这是一个实测出来的、已经存在的隐患:** 大厅现在**无条件**显示建房 FAB,而

```
POST /api/rooms  {"gameKey":"tictactoe"}
→ 400  "'tictactoe' has no human-vs-human mode on this platform."
```

今天够不着(一字棋在目录里是禁用的),**而它一有 renderer 就够得着了** —— 所以这一条 MUST 与一字棋的 renderer **同一笔落地**。

#### Scenario: 一字棋的大厅没有建房按钮
- **WHEN** 打开一字棋的大厅
- **THEN** MUST NOT 有建房入口,MUST 显示 `lobby.game-lobby.unavailable.ai-only-title`
- **AND** MUST 有人机对局入口

#### Scenario: 五子棋的大厅两个都有
- **WHEN** 打开五子棋的大厅
- **THEN** MUST 同时有建房入口与人机对局入口
- **AND** 这一条与上一条 MUST 同时存在:少了它,一个「永远隐藏建房」的实现同样通过

---

### Requirement: 一字棋 SHALL 复用五子棋的棋盘,而注册表 MUST 允许一个 renderer 服务多个棋种

一字棋 SHALL 用与五子棋**同一个** renderer,按描述符给的 3×3 渲染。

服务端把它注册成 `NInARowRules("tictactoe", 3, 3, 3)`,一行胜负判定都没自己写;平台自己的说法是「一字棋是缩小的五子棋,同一套读法」。星位**已经是从尺寸推出来的**,3×3 推出空集。

**注册表因此第一次不是一对一。** 「启用条数 == 注册表键数」那条不变量按**键**算,所以仍然成立 —— 但「一个 renderer 只服务一个棋种」此前是巧合,而巧合不该被下一个人当成保证。

#### Scenario: 两个键指向同一个 renderer
- **WHEN** 检查棋盘注册表
- **THEN** 一字棋与五子棋 MUST 映射到同一个 renderer 实例
- **AND** 目录里启用的条数 MUST 仍然等于注册表的**键**数

#### Scenario: 3×3 上没有星位
- **WHEN** 以 3×3 渲染
- **THEN** MUST 没有任何装饰落在盘面之外,也 MUST 没有星位
- **AND** 判据 MUST 是渲染出来的像素或推导出来的行号,MUST NOT 是「代码看起来对」

---

### Requirement: 人机房 SHALL 走 `POST /api/rooms/ai`,难度与执边显式给出

请求体为 `{name, difficulty, gameKey, humanSide}`;`difficulty` 是 `Easy` / `Medium` / `Hard`,`humanSide` 是 `Black` / `White`。三者 MUST 由调用方给出。

**路由 MUST 先对着 controller 的特性核过,再写代码。** 上一笔在解散那条路由上猜错了(`POST .../dissolve` 不存在),而旁边的单测断言了那个错的并且通过 —— 假 adapter 接受任何 URL。`test/room_route_contract_test.dart` 是那次补上的机制,这一笔 MUST 把新路由也纳入它。

难度写错时服务端返回 **400**,但那是一个**绑定层**错误(`"The body field is required"` 加 `$.difficulty` 的转换失败),不是领域错误 —— 客户端 MUST NOT 试图从它里面取字段级消息。

#### Scenario: 建出来的房间当即可下
- **WHEN** 建一个人机房
- **THEN** 服务端 MUST 返回 201,状态 MUST 是 `Playing`,两个座位 MUST 都坐上人(一个是 `AI_<难度>`)
- **AND** 判据 MUST 是服务端返回的房间,MUST NOT 是「调用没抛异常」

---

### Requirement: 人执白时,AI 的第一步 SHALL 自己出现在屏幕上

选「执白」时,棋盘开局是空的,**AI 的第一步经 hub 推送到达**。客户端 MUST 不需要任何交互就把它画出来。

**这是实测的,不是推的:** 创建响应里 `moves=0`,八秒后那个房间有 1 步 `(0,0) seat=0`、`currentSeat=1`。**AI 是异步走的。**

**所以 `fix-mobile-hub-inbound` 是这一条的前置条件** —— 在入向修好之前,选执白会看到一块永远空的棋盘,而那看起来像 AI 坏了。

#### Scenario: 一下都不碰屏幕
- **WHEN** 建一个执白的人机房,然后什么都不做
- **THEN** AI 的那一子 MUST 自己出现,回合 MUST 变成我
- **AND** 判据 MUST 是屏幕上那一子,MUST NOT 是服务端有那一步 —— 后者只证明服务端动了,
  不证明客户端听得到,而这个区别上一轮刚付过一次学费

#### Scenario: 执黑时轮到我
- **WHEN** 建一个执黑的人机房
- **THEN** 回合 MUST 是我,盘面 MUST 是空的
- **AND** 这一条与上一条 MUST 同时存在:少了它,一个「总是等 AI」的实现在执黑时会永远等

### Requirement: 一局结束时,手机端 SHALL 说出结果

对局结束时客户端 SHALL 显示赢 / 输 / 和,以及结束原因,并给出「返回大厅」与「关掉再看棋盘」两条出路。

**在真机上量到的缺陷是「界面停在那」:** 棋盘还在,点哪儿都没反应(服务端在拒),没有任何一句话说结果。而数据到了**两次**都被扔了 —— 服务端每一份快照都带 `Result` / `WinnerUserId` / `EndReason`,客户端只解析 `moves` 和 `currentSeat`;`GameEnded` 推送订阅了,却推进一个没人消费的流。

赢还是输 MUST 按 **`WinnerUserId` 与自己的用户 id** 判定,MUST NOT 按用户名 —— 用户名是显示名,这个平台已经为「把显示名当身份」付过两次账。

#### Scenario: 三个结果都说得出来
- **WHEN** 一局以我获胜 / 我落败 / 和局结束
- **THEN** 分别 MUST 显示 `game.ended.title-win` / `title-lose` / `title-draw`
- **AND** 三个方向 MUST 同时被测:只测「赢」的话,一个「永远说你赢了」的实现同样通过

#### Scenario: 未结束时什么都不显示
- **WHEN** 对局仍在进行(`Result` 为 `Ongoing`,或者根本没有 `result` 字段)
- **THEN** MUST NOT 显示任何结果
- **AND** 这一条与上一条 MUST 同时存在:少了它,一个「一进房间就报结果」的实现同样通过

#### Scenario: 结束原因跟着结果一起说
- **WHEN** 结束原因是 `Decided` / `Resigned` / `TurnTimeout`
- **THEN** MUST 显示对应的 `game.ended.reason-*`
- **AND** 每一个键 MUST 在两个 locale 里都有文案 —— 一个渲成原始键的结果框比没有更糟

---

### Requirement: 对局结果 SHALL 只有一个来源:房间快照

客户端 SHALL 从 `RoomState` 携带的 `Result` / `WinnerUserId` / `EndReason` 得出结果,MUST NOT 另外依赖 `GameEnded` 推送。

**理由是从服务端源码量出来的顺序:** `MakeMoveCommandHandler` 与 `ResignCommandHandler` 都是 `SaveChangesAsync` → `RoomStateChangedAsync` → `GameEndedAsync`,而 `GameEndedDto` 是从**已经写好的** `room.Game` 上取的。所以结束时那份 `RoomState` 一定带着结果,`GameEnded` 对这件事是冗余的。

**两个来源描述同一件事正是这个仓库反复付账的形状**,所以这一笔**删掉** `GameEnded` 订阅和那条没人消费的 `_errors` 流 —— 最好的机制是能被删掉的那种。

#### Scenario: 只靠快照就够
- **WHEN** 一局真的下到结束
- **THEN** 结果 MUST 出现在**屏幕**上
- **AND** 判据 MUST 是屏幕而不是服务端:问服务端「有没有结果」只证明服务端结束了,
  而这个区别在 `fix-mobile-hub-inbound` 里刚付过一次学费

#### Scenario: 那条没人消费的流没了
- **WHEN** 检查 hub 服务
- **THEN** MUST NOT 再有 `GameEnded` 订阅,也 MUST NOT 再有一条没有消费者的错误流
- **AND** `hub_contract_test` 的订阅数从 3 回到 2,而走查 MUST 仍然绿
  (它断言的是「订阅的 ⊆ 服务端发的」,不是「订阅得越多越好」)

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

### Requirement: 认输入口 SHALL 只在能成功时出现,而「能不能」的三个判据里有一个是座位数

手机端的对局页 SHALL 在满足**全部三个**条件时显示认输入口,否则 MUST NOT 显示:

1. 当前用户坐在这局的某个座位上(不是围观者、不是路人);
2. 房间状态是进行中;
3. **这个房间的座位数恰好是 2**,而该数字 SHALL 读自**房间自身**的 `seatCount`(服务端已随
   `RoomStateDto` 下发),MUST NOT 由客户端按棋种猜、也 MUST NOT 绕道再查一次棋种目录 ——
   被认输的是**这个房间**,而房间自己就说了它有几个座位。

第三条不是保守。平台的 `Room.Resign` 需要恰好两个座位才能指出赢家,三座位棋种上 API 答 409 ——
web 端曾因为客户端假设了座位数而在真实点击上返回 **500**。手机端目前两个棋种都是两座位,所以
这条判据**今天恒真**;它存在是为了第三个棋种落地那天不必重新发现。

认输 SHALL 走 `POST /api/rooms/{id}/resign`。

#### Scenario: 玩家在进行中的两座位对局里看得到认输
- **WHEN** 当前用户坐在一局进行中的五子棋房间里
- **THEN** 对局页显示认输入口

#### Scenario: 围观者看不到
- **WHEN** 当前用户不在任何座位上
- **THEN** 对局页 MUST NOT 显示认输入口

#### Scenario: 等待中的房间看不到
- **WHEN** 房间还在等待对手
- **THEN** 对局页 MUST NOT 显示认输入口

#### Scenario: 座位数不是 2 就看不到
- **WHEN** 房间的 `seatCount` 是 3
- **THEN** 对局页 MUST NOT 显示认输入口(平台无法在三座位下指出赢家)

---

### Requirement: 认输 SHALL 先确认,且 MUST NOT 自己宣布结果

认输不可逆,所以 SHALL 先弹确认;取消 MUST NOT 发出任何请求。

确认之后,客户端 MUST NOT 自行渲染「你输了」——结果 SHALL 走既有的那一条路:房间快照的
`result` / `winnerUserId` / `endReason`,以及 `GameEnded` 推送。**两条宣布结果的路会分叉,而
分叉的表现是其中一条说错了赢家。**

文案 SHALL 复用 `game.actions.resign-confirm-title` / `-body` / `-ok` / `-cancel`,MUST NOT 新增键。

#### Scenario: 取消不发请求
- **WHEN** 玩家点认输,然后在确认框里点取消
- **THEN** MUST NOT 调用 `POST /api/rooms/{id}/resign`,且对局仍在进行中

#### Scenario: 确认才认输
- **WHEN** 玩家点认输并确认
- **THEN** 调用 `POST /api/rooms/{id}/resign`

#### Scenario: 结果由既有那条路显示
- **WHEN** 认输成功,服务端随后推来 `GameEnded`(或快照带上 `result`)
- **THEN** 屏幕上的结果来自那一份数据,而不是客户端在认输成功时自己写下的

---

### Requirement: 催促入口 SHALL 在不可用时说明原因,而冷却 MUST NOT 由客户端判定

催促入口 SHALL 在「当前用户是玩家 且 对局进行中」时显示。**可点**的条件再加一条:当前不是
自己的回合。

不可点时 SHALL 显示原因文案,MUST NOT 只是把按钮变灰:

- 轮到自己 → `game.urge.button-disabled-own-turn`
- 刚催过(收到 429 之后) → `game.urge.button-disabled-cooldown`

客户端 MUST NOT 自己实现 30 秒冷却计时。它 MAY 在收到 429 之后临时禁用按钮,但「服务端会不会
接受这次催促」这个结论 SHALL 由服务端给出。**一份并行的冷却计时器是第二处规则,而两处规则会
分叉,分叉的表现是按钮说「可以」而服务端说「不行」。**

催促 SHALL 走 hub 方法 `Urge(roomId)`。

#### Scenario: 轮到对手时可以催
- **WHEN** 对局进行中且当前回合是对手
- **THEN** 催促入口可点

#### Scenario: 轮到自己时不可点,并说明原因
- **WHEN** 对局进行中且当前回合是自己
- **THEN** 催促入口不可点,且屏幕上出现 `game.urge.button-disabled-own-turn` 的文案

#### Scenario: 冷却由服务端告知
- **WHEN** 服务端以 429 拒绝一次催促
- **THEN** 屏幕上出现 `game.errors.urge-cooldown`,MUST NOT 落到通用错误文案

---

### Requirement: `UrgeReceived` SHALL 出现在屏幕上,且 MUST NOT 需要刷新

客户端 SHALL 订阅 hub 方法 `UrgeReceived`,收到时在对局页上给出可见反馈(`game.urge.toast`)。

这是**推送**,不是快照的一部分 —— 服务端不会把「你被催了」写进 `RoomStateDto`,所以任何靠
重新拉取房间来发现它的实现都会永远发现不了。

被催的那一方 SHALL 收到;催的那一方 MUST NOT 收到自己那一条。

#### Scenario: 被催的人看得见
- **WHEN** 对手催促当前用户,服务端推来 `UrgeReceived`
- **THEN** 对局页上出现催促提示

#### Scenario: 催的人不会被自己催
- **WHEN** 当前用户催促对手
- **THEN** 当前用户的屏幕上 MUST NOT 出现催促提示

### Requirement: 聊天历史 SHALL 来自房间快照,而推送 SHALL 追加而不是替换

客户端 SHALL 从 `RoomStateDto.chatMessages` 读取进入房间时已有的消息,MUST NOT 为此调用
第二个接口。

服务端推送的 `ChatMessage` 每次只带**一条**消息。客户端 SHALL 把它**追加**到已有列表之后,
MUST NOT 用它替换整个列表 —— 后者的表现是「一发消息,前面的全没了」。

`ChatChannel` SHALL 按**名字**解析(`Room` / `Spectator`),不认识的取值 MUST NOT 塌成一个
默认频道 —— 一个没人认识的频道应该是可见的,不是被悄悄当成房间频道广播出去。

#### Scenario: 进房间就看得到之前的话
- **WHEN** 房间快照里有 3 条消息
- **THEN** 打开房间时这 3 条都在列表里

#### Scenario: 推送追加
- **WHEN** 列表里已有 3 条,服务端推来第 4 条
- **THEN** 列表变成 4 条,前 3 条不变

#### Scenario: 不认识的频道不当成房间频道
- **WHEN** 一条消息的 `channel` 是服务端将来新增的取值
- **THEN** 它 MUST NOT 被当作 `Room` 频道

---

### Requirement: 发送 SHALL 走 `SendChat`,频道以字符串给出,合法性 MUST NOT 由客户端判定

发送 SHALL 调用 hub 方法 `SendChat(roomId, content, channel)`,三个参数一个不多一个不少
—— SignalR 两个方向都不套用 C# 可选参数默认值,多一个少一个都在绑定层被拒,而那层的拒绝
低于日志级别,两端都看不见。

`channel` SHALL 以**字符串**给出(`'Room'`),与本客户端解析其他枚举的方式一致。

内容规则(trim 后 1–500 字符)由服务端判定。客户端 MAY 限制输入长度作为输入体验,但
MUST NOT 据此断定一条消息「能不能发」;被拒时 SHALL 显示服务端错误码对应的文案。

#### Scenario: 发送用字符串频道
- **WHEN** 玩家在房间频道发一条消息
- **THEN** 调用 `SendChat`,第三个参数是字符串 `'Room'`

#### Scenario: 空白内容不发
- **WHEN** 输入框里只有空白
- **THEN** MUST NOT 调用 `SendChat`(这不是判定合法性,是没有内容可发)

#### Scenario: 服务端拒绝时说服务端的理由
- **WHEN** 服务端以 `InvalidChatMessage` 拒绝
- **THEN** 屏幕上出现 `game.errors.invalid-chat`,MUST NOT 落到通用错误文案

---

### Requirement: 手机端的聊天频道页签 SHALL 只对到得了那个频道的人出现

聊天面板 SHALL 只把一个频道的入口显示给到得了那个频道的人。

围观频道**只有围观者收得到、也只有围观者发得出**。

因此聊天面板 SHALL 只对**围观者**显示频道页签(房间 / 围观);对坐在座位上的玩家 SHALL
只显示房间频道,且 MUST NOT 显示围观页签。

判据是**「谁到得了这个频道」**,不是「这个客户端支不支持围观」。前者在围观落地之后对玩家
仍然成立,后者不成立 —— 一个写成后者的条件会在围观落地当天把一个永远空的页签放到玩家
面前,而一个永远空的页签看起来像坏了。

#### Scenario: 玩家看不到围观页签
- **WHEN** 坐在座位上的玩家打开聊天面板
- **THEN** 界面上 MUST NOT 出现围观频道的入口

#### Scenario: 围观者两个频道都看得到
- **WHEN** 围观者打开聊天面板
- **THEN** 房间与围观两个频道都可选

### Requirement: 围观入场 SHALL 是三步,而 `JoinRoom` 是其中一步

以围观者身份进入房间,客户端 SHALL 依次执行:

1. `POST /api/rooms/{id}/spectate`;
2. hub `JoinRoom(roomId)`;
3. hub `JoinSpectatorGroup(roomId)`。

**第二步不可省。** 房间频道的推送发给**房间组**,而进房间组的方法是 `JoinRoom`;
`JoinSpectatorGroup` 只加围观子群。少了第二步的表现是「围观者收不到房间里的消息」,而那
读起来和一个服务端缺陷一模一样 —— 这个平台的探针第一版就是这么错的。

第三步对非围观者是服务端侧的静默无操作,所以客户端 MUST NOT 为它加一个「我是不是围观者」
的前置判断:那是一个会过期的判断,而服务端已经查过聚合了。

#### Scenario: 三步都发生,顺序正确
- **WHEN** 用户围观一个进行中的房间
- **THEN** 客户端先 `POST /api/rooms/{id}/spectate`,再 `JoinRoom`,再 `JoinSpectatorGroup`

#### Scenario: 围观者收得到房间频道
- **WHEN** 围观期间桌上有人说话
- **THEN** 围观者的屏幕上出现那条消息

---

### Requirement: 围观者离开 SHALL 走 `DELETE /api/rooms/{id}/spectate`

围观者退出房间 SHALL 调用 `DELETE /api/rooms/{id}/spectate`,MUST NOT 调用
`POST /api/rooms/{id}/leave`(那是玩家的路由)。

**哪条路由由服务端的规则决定**,与「主持人退等待中的房间要走 `DELETE /api/rooms/{id}`」
是同一类。客户端 MUST NOT 按「哪条更顺手」选。

#### Scenario: 围观者退出
- **WHEN** 围观者离开房间
- **THEN** 客户端调用 `DELETE /api/rooms/{id}/spectate`

#### Scenario: 玩家退出仍走玩家的路由
- **WHEN** 坐在座位上的玩家离开一个进行中的房间
- **THEN** 客户端调用 `POST /api/rooms/{id}/leave`,MUST NOT 调用围观的那条

---

### Requirement: 围观者的棋盘 SHALL 是只读的,而 MUST NOT 靠界面隐藏来实现

围观者点棋盘 MUST NOT 向服务端发出任何走子。这条 SHALL 在 ViewModel 上成立,而不是靠
View 不画棋盘或不接收点击 —— 一个只在界面层拦住的规则,会在下一个进入这块棋盘的路径上
失效。

认输与催促的入口对围观者 MUST NOT 出现(它们的条件已经要求「坐在座位上」)。

#### Scenario: 围观者点棋盘什么都不发
- **WHEN** 围观者在棋盘上点一个空点
- **THEN** MUST NOT 调用 `MakeMove` 或 `MovePiece`

#### Scenario: 玩家点棋盘照常
- **WHEN** 轮到自己的玩家点一个空点
- **THEN** 照常发出走子(否则上一条是因为整条路断了才成立的)

---

### Requirement: 大厅 SHALL 给坐不下的房间一个围观入口,而判据是空位不是状态

大厅列表 SHALL 按「这个房间还坐得下吗」决定点击的去向:

- 还有空位的房间 → 入座(`POST /join`);
- 没有空位或已经开打的房间 → 围观。

**判据是「这个房间还坐得下吗」,不是房间状态的字面值** —— 一个满员但仍在 `Waiting` 的房间
坐不下,而客户端按状态判断会给出一个必然被服务端拒绝的入座按钮。

**而「还坐得下吗」的座位总数 SHALL 取自棋种描述符,MUST NOT 取自房间摘要。** 这是量出来的:
`GET /api/rooms` 返回的 `RoomSummaryDto` **不含 `seatCount`**,且 `seats` **只列已坐下的
座位** —— 于是「已坐 < 总数」在摘要上退化成 `1 < 1`,每个房间(包括空房间)都会被判成坐不下。
大厅是按棋种打开的,所以描述符就在手边。

**一份用完整房间 JSON 造的夹具证明不了这件事**:它带着 `seatCount`,于是无论实现读哪一个都
绿。这个缺陷是集成测试抓到的,而单测夹具此后 SHALL 用**摘要的形状**。

#### Scenario: 进行中的房间给的是围观
- **WHEN** 大厅里有一个进行中的房间
- **THEN** 点它进入围观,而不是尝试入座

#### Scenario: 有空位的房间给的是入座
- **WHEN** 大厅里有一个还有空位的房间
- **THEN** 点它尝试入座

### Requirement: 声音事件集 SHALL 从 web 的 `SOUND_EVENTS` 派生,MUST NOT 手写

手机端的声音事件名 SHALL 与 `frontend-web/src/app/core/sound/sound.tokens.ts` 的
`SOUND_EVENTS` 完全一致,且该一致性 SHALL 由一条读**那份源码**的测试守住。

**手写一份清单会落后于它,而症状是一个事件永远不响** —— 这个仓已经为「手写清单冒充注册表」
付过九次账。这与 `hub_contract_test` 从服务端源码派生 hub 方法名是同一招。

手机端**只会触发其中一个子集**(它只有落子类与走子类棋种),这不是缺陷:事件集是平台级的,
一个棋种播它需要的那些。

#### Scenario: 事件集与 web 一致
- **WHEN** 走查读取 web 的 `sound.tokens.ts`
- **THEN** 手机端的事件名集合与之相等

#### Scenario: web 新增一个事件会让走查红
- **WHEN** web 的 `SOUND_EVENTS` 多了一个名字而手机端没有
- **THEN** 走查失败

---

### Requirement: 音效 SHALL 在设备上合成,MUST NOT 打包音频文件

音效 SHALL 由纯 Dart 代码合成为 PCM 样本,MUST NOT 以音频资产文件的形式随包分发。

理由有两条,第二条是判据上的:

1. web 端**没有音频文件可同步** —— 它的包是 WebAudio 现场合成的,所以「从 web 拉过来」这条
   路在这里不存在;
2. **合成出来的音频是一串数字,因此是可断言的。** 一个打包好的音频文件只能断言它存在;而
   一段生成的 PCM 可以断言它的长度、峰值幅度与**主频**。

因此每个事件的输出 SHALL 可被测试直接检查,而不必播放。

#### Scenario: 落子音是一段短促的可测样本
- **WHEN** 请求 `move-place` 的样本
- **THEN** 样本时长在 100 ms 以内,峰值幅度非零且不削顶,主频落在设计频率的容差内

#### Scenario: 每个事件都有声音
- **WHEN** 遍历事件集里的每一个事件
- **THEN** 每一个都产出非空、非全零的样本

---

### Requirement: 静音 SHALL 是一个开关,而关掉时 MUST NOT 走到播放层

设置页 SHALL 提供声音开关,复用 `header.sound.label` / `header.sound.on` /
`header.sound.off`,MUST NOT 新增翻译键。该选择 SHALL 持久化,与主题、深浅同一个存储。

关闭时,客户端 MUST NOT 调用播放层 —— 而不是「播一个音量为零的声音」。**后者在一台静音的
设备上和前者看起来一样,却仍然会申请音频焦点、打断别人的音乐。**

#### Scenario: 关掉之后不播
- **WHEN** 声音开关是关,发生一次落子
- **THEN** 播放层 MUST NOT 被调用

#### Scenario: 打开之后播
- **WHEN** 声音开关是开,发生一次落子
- **THEN** 播放层被调用一次(否则上一条是因为整条路都断了才成立的)

#### Scenario: 重启后记得
- **WHEN** 关掉声音并重启应用
- **THEN** 声音仍然是关

---

### Requirement: 声音 MUST NOT 参与任何判断,失败 MUST NOT 影响对局

播放音效 SHALL 是即发即忘的:它的返回值 MUST NOT 被等待,它抛出的异常 MUST NOT 传播到调用点。

**一局棋不能因为音频设备忙而下不下去。** 播放层不可用(无设备、被占用、平台不支持)时,
客户端 SHALL 静默地继续。

#### Scenario: 播放失败不影响落子
- **WHEN** 播放层抛出异常
- **THEN** 落子照常完成,屏幕上 MUST NOT 出现错误

### Requirement: 棋盘的位置与大小 MUST NOT 随回合、错误或任何每手都变的状态改变

对局页的棋盘 SHALL 在一局棋进行中保持同一个矩形。回合易手、走子被拒、催促按钮可用性变化
等**每一手都会发生**的状态改变,MUST NOT 改变棋盘的位置或尺寸。

这不是审美要求。棋盘下方任何高度会变的行,都会让居中的棋盘跟着动 —— 而**每落一子动一次**
就是玩家看到的「闪动」。

因此:说明性文字若只在某些回合出现,SHALL **预留其空间**(而不是删掉它 —— 禁用的入口仍然
必须说明原因);错误提示 SHALL 叠加显示,MUST NOT 作为占布局的一行插在棋盘上下。

判据是**矩形**:一条测试 SHALL 在回合易手前后比较棋盘的 `Rect`,并要求二者相等。

#### Scenario: 回合易手,棋盘不动
- **WHEN** 一手棋落下,回合从当前用户转到对手
- **THEN** 棋盘的矩形与落子前完全相同

#### Scenario: 走子被拒,棋盘不动
- **WHEN** 服务端拒绝一步棋,错误文案出现在屏幕上
- **THEN** 棋盘的矩形不变,且那句文案**仍然可见**

#### Scenario: 禁用时仍然说明原因
- **WHEN** 轮到当前用户,催促入口因此不可点
- **THEN** 屏幕上仍然显示不可点的原因

