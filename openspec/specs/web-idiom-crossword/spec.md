# web-idiom-crossword Specification

## Purpose
TBD - created by archiving change add-web-idiom-crossword. Update Purpose after archive.
## Requirements
### Requirement: `/g/idiom-crossword` 是受保护的懒加载关卡列表

`app.routes.ts` SHALL 新增 `/g/:gameKey` 路由分支,成语纵横的两条路由为:

- `/g/idiom-crossword` —— 关卡列表。
- `/g/idiom-crossword/levels/:index` —— 对局页。

两者 MUST 带 `canMatch: [authGuard]` 并通过 `loadComponent` / `loadChildren` 懒加载。本变更 MUST NOT 改动任何既有路由 —— 五子棋仍在 `/home`。

关卡类游戏走纯 REST,这两条路由 MUST NOT 建立任何 SignalR 连接。

#### Scenario: 懒加载
- **WHEN** 已登录用户从 `/games` 点进成语纵横
- **THEN** 游戏 chunk 此刻才被请求,MUST NOT 在应用启动时下载

#### Scenario: 未登录被拦
- **WHEN** 未登录用户直接访问 `/g/idiom-crossword`
- **THEN** 路由落在 `/login?returnUrl=/g/idiom-crossword`

#### Scenario: 不建立 hub 连接
- **WHEN** 玩家进入对局页并完成一整关
- **THEN** MUST NOT 出现任何 SignalR 握手

### Requirement: 关卡列表展示星级、最好用时与锁定状态

列表 SHALL 为每一关渲染一张卡片,含:关卡序号、难度、已获星级(未通关显示空星)、最好用时。

- `unlocked === true` 的卡片 SHALL 是进入对局页的链接。
- `unlocked === false` 的卡片 MUST NOT 渲染为 `<a>` 或可聚焦的 `<button>`,SHALL 带 `aria-disabled="true"`,并以文案(而非仅颜色)表达锁定状态。

锁定状态 MUST 取自服务端返回的 `unlocked` 字段,MUST NOT 由客户端依据星级自行推算 —— 解锁规则属于服务端。

#### Scenario: 首次进入只有第一关可点
- **WHEN** 新玩家打开关卡列表
- **THEN** 第 0 关是链接,其余关卡均为带 `aria-disabled="true"` 的非交互元素

#### Scenario: 通关后下一关解锁
- **WHEN** 玩家通关第 0 关后返回列表
- **THEN** 第 1 关变为链接,第 0 关显示所获星级与用时

### Requirement: 客户端不持有答案,也不自行计分

对局页 SHALL 只从 `GET /api/games/idiom-crossword/levels/{index}` 取布局与字盘。

`mistakes`、`hintsUsed`、星级 MUST 全部读自服务端响应,客户端 MUST NOT 在本地累计这三者中的任何一个。

客户端**可以**维护"哪些格填了什么"作为展示状态 —— 那不是成绩。

#### Scenario: 错误数来自响应
- **WHEN** 一次 `check` 判错
- **THEN** 界面显示的错误数取自响应里的 `mistakes`,而非本地自增

#### Scenario: 星级来自提交响应
- **WHEN** 提交成功
- **THEN** 结果弹层显示的星级取自响应的 `stars`,客户端 MUST NOT 用自己的公式预测

#### Scenario: 布局响应中不含答案
- **WHEN** 检查取关卡的网络响应
- **THEN** 其中 MUST NOT 出现完整成语或释义

### Requirement: 一个词槽填满即发起 `check`

当某词槽的全部格子都被填入时,客户端 SHALL 立即对该词槽发起一次 `check`,携带词槽下标与玩家填出的字串。

未填满任何词槽的落子 MUST NOT 触发请求。

一次落子同时填满两个交叉词槽时,SHALL 各发一次 `check`,且两次请求 MUST NOT 相互串行等待。

#### Scenario: 填满才请求
- **WHEN** 玩家在一个 4 格词槽里放下第 3 个字
- **THEN** MUST NOT 发起 `check`

#### Scenario: 填满即请求
- **WHEN** 玩家放下该词槽的第 4 个字
- **THEN** 立即对该词槽发起一次 `check`

#### Scenario: 同时填满两条
- **WHEN** 一次落子同时补全了横竖两个词槽
- **THEN** 发起两次 `check`,分别携带各自的词槽下标

### Requirement: 答对锁定并显示释义纸条,答错抖动并退回字块

`check` 判定正确时,该词槽的格子 SHALL 转为锁定态(不可再改),并显示一张含成语与释义的纸条。释义 MUST 取自响应载荷 —— 客户端没有别的来源。

`check` 判定错误时,该词槽中**非锁定、非预填**的格子 SHALL 播放抖动反馈,其字块退回字盘。抖动 MUST 遵循 `prefers-reduced-motion`。

界面 MUST NOT 提示"哪一个字错了" —— 服务端不返回该信息,客户端猜测既不可靠也超出了本游戏想给的提示强度。

#### Scenario: 答对显示释义
- **WHEN** 某词槽 `check` 判定正确
- **THEN** 纸条显示该成语与其释义,内容来自响应载荷

#### Scenario: 答错退回字块
- **WHEN** 某词槽 `check` 判定错误
- **THEN** 该词槽中非锁定非预填的字块回到字盘,格子恢复为空

#### Scenario: 锁定格不可修改
- **WHEN** 玩家点击一个已锁定的格子
- **THEN** 不发生任何变化,字块 MUST NOT 被取回

#### Scenario: 尊重减少动效
- **WHEN** 系统设置为 `prefers-reduced-motion: reduce`
- **THEN** 抖动动画不播放,错误仍以非动画方式表达

### Requirement: 提示由服务端揭示并计费

提示按钮 SHALL 调用 `POST /api/puzzle-attempts/{id}/hint`,把响应中揭示的那一格填入并锁定,并以响应中的 `hintsUsed` 更新界面。

请求 SHALL 携带客户端当前的盘面状态:已填入字符的格子集合与当前选中格。这让服务端能揭示玩家**真正想解的那一格**,而不是阅读顺序上碰巧排到的某格。

客户端 MUST NOT 自行决定揭示哪一格 —— 它只上报状态,揭哪一格仍由服务端依答案决定,客户端手里从来没有答案。

若被揭示的格子此前放着玩家填错的字块,客户端 SHALL 先把该字块退回字盘,再写入正确的字并锁定。

#### Scenario: 提示填入被揭示的格
- **WHEN** 玩家点击提示
- **THEN** 响应中 `(row, col)` 指定的格子被填入 `char` 并锁定,提示计数取自响应

#### Scenario: 请求携带盘面状态
- **WHEN** 玩家在选中某格后点击提示
- **THEN** 请求体中含已填格集合与该选中格

#### Scenario: 覆盖填错的格时字块回到字盘
- **WHEN** 被揭示的格子此前放着一个错的字块
- **THEN** 该字块回到字盘可再用,格中显示正确的字并锁定

#### Scenario: 通关后不可再要提示
- **WHEN** 关卡已提交通关
- **THEN** 提示按钮不可用

### Requirement: 网格几何由 computed signal 决定

格子尺寸 SHALL 由一个 `computed()` 从容器宽度、列数与间距算出,容器宽度由 `ResizeObserver` 驱动的 signal 提供。

MUST NOT 使用 `window.resize` 监听器,MUST NOT 在组件里命令式地写 `--cell` 之类的样式变量。

375px 宽度下页面 MUST NOT 产生横向滚动;网格过宽时 SHALL 在其自身的滚动容器内横向滚动。

#### Scenario: 容器变化即重算
- **WHEN** 容器宽度改变(窗口缩放或布局变化)
- **THEN** 格子尺寸随之更新,无需手动重绘

#### Scenario: 375px 无横向滚动
- **WHEN** 在 375px 宽度下打开最大的关卡
- **THEN** `document.documentElement.scrollWidth <= clientWidth`,网格在自身容器内滚动

### Requirement: 结果弹层用 CDK,展示星级与全部成语释义

网格全部填满时,客户端 SHALL 发起 `submit`;判定通关后弹出结果层,显示星级、用时,以及本关全部成语及其释义。

弹层 MUST 使用 Angular CDK(`@angular/cdk/dialog` 或 `overlay`),MUST NOT 手搓 `<div>` + 条件渲染 —— 焦点陷阱、ESC、backdrop 与 ARIA 都是必需的。

弹层 SHALL 提供"再玩一次"与"下一关"两个操作;当前是最后一关时,后者改为返回关卡列表。

#### Scenario: 通关弹出结果
- **WHEN** 提交判定通关
- **THEN** CDK 弹层出现,显示服务端返回的星级与用时

#### Scenario: ESC 可关闭
- **WHEN** 结果弹层打开时按 ESC
- **THEN** 弹层关闭,焦点回到触发元素

#### Scenario: 最后一关的下一步
- **WHEN** 在最后一关通关
- **THEN** 次要操作为"返回关卡列表"而非"下一关"

### Requirement: 载荷双层解析只包一次

`payloadJson` 是"JSON 字符串套在 JSON 响应里"(平台不理解各游戏的内容,只能原样透传)。`PuzzleApiService` SHALL 在服务层完成解析并返回带类型的对象,组件 MUST NOT 见到原始字符串。

畸形载荷 MUST 解析为 `null` 而不是抛错 —— 一张纸条坏掉不该带垮一整关。

#### Scenario: 服务层返回结构化载荷
- **WHEN** `check` 判定正确并附带载荷
- **THEN** 服务返回已解析的对象,组件直接读取其中的成语与释义

#### Scenario: 畸形载荷不致崩溃
- **WHEN** 载荷不是合法 JSON
- **THEN** 解析结果为 `null`,该词槽仍正常锁定,不抛异常

### Requirement: i18n —— 游戏文案双语对齐,成语内容保持中文

`public/i18n/en.json` 与 `public/i18n/zh-CN.json` SHALL 同步新增 `idiom-crossword.*` 键组,覆盖:关卡列表、对局页控件、提示、结果弹层、错误提示。

模板 MUST NOT 硬编码任何展示字符串。

成语与释义是**数据不是界面文案**,MUST 保持中文原样 —— 这正是该游戏 manifest 声明 `contentLocales: ['zh-CN']` 的原因。

#### Scenario: parity
- **WHEN** 比对两份 JSON flatten 后的 key 集合
- **THEN** 差集为空

#### Scenario: en 界面下成语仍是中文
- **WHEN** 活动语言为 `en` 时通关一条成语
- **THEN** 纸条上的成语与释义仍为中文,周围的界面文案为英文

