## ADDED Requirements

### Requirement: `/g/klotski` 与 `/g/klotski/levels/:index` 是华容道的两个页面

`app.routes.ts` SHALL 新增两条路由,均带 `canMatch: [authGuard]` 且通过 `loadComponent` 懒加载:关卡列表 `g/klotski`,闯关页 `g/klotski/levels/:index`。

与成语纵横同一形状 —— 两个游戏走同一套谜题端点,页面结构没有理由分叉。

#### Scenario: 懒加载且受保护
- **WHEN** 检视两条路由
- **THEN** 都使用 `loadComponent`,都带 `authGuard`

#### Scenario: 未登录被拦
- **WHEN** 未登录用户访问 `/g/klotski`
- **THEN** 重定向到 `/login?returnUrl=/g/klotski`

### Requirement: 客户端自己判定滑动,并高亮合法落点

华容道的闯关页 SHALL 在本地判定一次滑动是否合法(目标格在盘内、且未被别的子占据),并把选中子的合法落点标出来。

**这与象棋棋盘的做法相反,而两者都对。** 象棋的棋盘刻意不懂规则:把象棋规则移植成 TypeScript 会造出第二份真源,它与服务端悄悄分叉时没有任何机制会发现。华容道不存在这个问题 —— 客户端本来就必须知道棋子、盘面和「滑块移进相邻空格」这一条规则,否则它连动画都做不出来。**要新造的第二份真源不存在,那一条规则在两边是同一条。**

因此本页 MUST NOT 为每一步调用 `check`:服务端不会因此多知道任何东西,它最后无论如何都要重放整条路径。

#### Scenario: 合法落点被标出
- **WHEN** 选中一枚旁边有空格的子
- **THEN** 那些落点被标记为可点

#### Scenario: 挡住的方向不可点
- **WHEN** 选中一枚四面被占的子
- **THEN** 没有落点被标记,点它周围不产生移动

#### Scenario: 每一步不发请求
- **WHEN** 玩家滑动若干次
- **THEN** MUST NOT 发出任何 `check` 请求

### Requirement: 交互是两步,并且键盘可达

闯关页 SHALL 采用两步交互:选中一枚子 → 点一个合法落点 → 滑动一格并计一步。

- 再点同一枚子取消选中;点另一枚子改选。
- 选中后方向键沿该方向滑一格(不合法则无事发生)。
- `Escape` 取消选中。
- 每枚子 MUST 有可翻译的 `aria-label`(名称 + 位置 + 尺寸),选中态用 `aria-pressed`。

#### Scenario: 两步滑动
- **WHEN** 选中一枚子并点它下方的空格
- **THEN** 该子下移一格,步数加一

#### Scenario: 方向键
- **WHEN** 选中一枚子并按方向键
- **THEN** 若该方向合法则滑动一格,否则步数不变

#### Scenario: 取消选中
- **WHEN** 按 `Escape`
- **THEN** 选中态清空,步数不变

### Requirement: 通关时一次性提交整条路径

盘面达成「目标子左上角落在出口」时,页面 SHALL 自动把**整条移动序列**提交给 `POST /api/puzzle-attempts/{id}/submit`,并按响应展示星级、步数与是否新纪录。

MUST NOT 由客户端自行计算星级 —— 星级是服务端对一条它重放过的路径给出的判断。

提交失败时 MUST 有真实 UI:可翻译的错误提示 + 重试入口,MUST NOT 静默失败。

#### Scenario: 到位即提交
- **WHEN** 最后一步把目标子送到出口
- **THEN** 发出一次 `submit`,请求体含全部移动

#### Scenario: 星级来自服务端
- **WHEN** 服务端返回 `stars: 2`
- **THEN** 页面显示 2 星,MUST NOT 自行推算

#### Scenario: 提交失败可重试
- **WHEN** `submit` 返回错误
- **THEN** 显示可翻译错误并提供重试,盘面保持已解开的状态

### Requirement: 提示由服务端给,并且上报当前盘面

「提示」按钮 SHALL 调用 `POST /api/puzzle-attempts/{id}/hint`,请求体携带**当前**每枚子的位置,并把服务端返回的那一步直接走掉。

上报当前盘面是必需的:服务端从玩家所在的局面搜最短路径,而不是从一条预存路径上取下一步。

页面 MUST 说明提示会影响星级 —— 玩家有权在点之前知道。

#### Scenario: 提示走掉一步
- **WHEN** 点「提示」
- **THEN** 请求体含当前所有棋子位置;返回的那一步被走掉,步数加一

#### Scenario: 代价写在按钮旁
- **WHEN** 检视闯关页
- **THEN** 存在一段可翻译文案说明提示影响评级

### Requirement: 目标步数在通关之前不显示

页面 MUST NOT 在解开之前展示该关的最少步数。

它是计分的分母,写在服务端的答案里;在解题过程中把它顶在屏幕上会把一道谜题变成一个倒计时。通关之后展示自己的步数是合适的 —— 那时它是成绩而不是压力。

#### Scenario: 解题时不显示目标
- **WHEN** 闯关中
- **THEN** 页面显示当前步数,MUST NOT 显示该关的最少步数

### Requirement: 关卡列表显示解锁与最好成绩

`/g/klotski` SHALL 列出全部关卡,显示难度、解锁状态、最好星级与最好用时,未解锁的关卡不可进入。

数据全部来自 `GET /api/games/klotski/levels` 与 `GET /api/games/klotski/progress`,MUST NOT 在客户端另算解锁规则。

#### Scenario: 未解锁不可进入
- **WHEN** 某关 `unlocked === false`
- **THEN** 它的入口不可点,并对辅助技术表明原因

#### Scenario: 成绩来自服务端
- **WHEN** 某关有最好成绩
- **THEN** 显示服务端返回的星级与用时

### Requirement: 华容道 manifest 从「即将上线」翻到「可玩」

`src/app/games/klotski/manifest.ts` SHALL 把 `status` 改为 `'available'` 并加上 `launchRoute: '/g/klotski'`。

它是 `category: 'puzzle'`,所以 MUST NOT 有排行榜入口 —— 谜题的成绩是星与用时,不是 ELO,这条已由 platform-catalog 的既有约束覆盖。

#### Scenario: 目录页可点进
- **WHEN** 打开 `/games`
- **THEN** 华容道卡片可交互,指向 `/g/klotski`

#### Scenario: 没有排行榜入口
- **WHEN** 检视华容道卡片
- **THEN** MUST NOT 存在排行榜链接

### Requirement: i18n —— `klotski.*` 键在两份 locale 中齐备

`public/i18n/zh-CN.json` 与 `public/i18n/en.json` SHALL 各增加 `klotski.*` 子树,覆盖:两页的标题与说明、难度分档、解锁/未解锁、步数、提示按钮与其代价说明、结果(星级 / 步数 / 新纪录)、错误与重试、棋子的无障碍文案。

两份文件的键集合 MUST 完全一致。模板 MUST NOT 硬编码任何中英文展示字符串。

棋子上印的**人物名**(曹操、关羽…)来自关卡布局里的 `name` 字段,是**内容**而非界面文案,因此不进 locale 文件 —— 与成语纵横的成语同一处理。华容道因此是 `contentLocales: ['zh-CN']`。

#### Scenario: 键集合一致
- **WHEN** 比较两份 locale 中 `klotski.*` 的键集合
- **THEN** 两者相等

#### Scenario: 模板无硬编码
- **WHEN** 检视两个页面的模板
- **THEN** 所有界面文案经 `| transloco`
