## ADDED Requirements

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
