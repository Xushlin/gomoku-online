# web-klotski Specification

## Purpose
TBD - created by archiving change add-web-klotski. Update Purpose after archive.
## Requirements
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

### Requirement: 华容道棋盘由棋盘皮肤层绘制,且三个皮肤 MUST 画出不同的盘面

`.kt-board` / `.kt-piece` / `.kt-exit` / `.kt-target` SHALL 只消费 CSS 变量,其中盘面与棋子的颜色、材质、圆角、阴影 MUST 来自 `board-skins.css`(`--board-*` 与本变更新增的 `--kt-*`),而 MUST NOT 来自 shell 的 `--color-surface` / `--color-text`。组件与模板 MUST NOT 出现任何 skin 名或 skin 条件分支 —— 与 `web-board-skins` 对其它棋盘的要求同一条。

**这条要求的判据是"三个皮肤画出来不一样",而不是"代码里写了 `var(--board-…)`"。** 理由是量出来的:改这条之前,`data-board-skin` 在 `wood` / `classic` / `midnight` 之间切,`.kt-board` 与 `.kt-piece` 的计算背景**三个皮肤逐字节相同** —— 因为它消费的是 shell 变量。一个只检查"引用了皮肤变量"的断言,在那个版本上**同样是绿的**(它确实引用了 `--radius-card`)。

#### Scenario: 换皮肤,盘面跟着变
- **WHEN** `data-board-skin` 依次设为每一个已注册皮肤,读 `.kt-board` 与 `.kt-piece` 的计算背景
- **THEN** 任意两个皮肤给出的读数 MUST NOT 相同;清单 MUST 从 `BoardSkinService` 的注册表推导,而不是手写

#### Scenario: 组件对皮肤零感知
- **WHEN** 检索 `klotski-board.ts` 与 `klotski-board.html`
- **THEN** MUST NOT 出现任何皮肤名

### Requirement: 棋子按几何形状分成四类角色,而角色 MUST 由尺寸推出

棋子 SHALL 按 `width × height` 分为四类,各有自己的面:`2×2` 且 `target` 为**主帅**;`1×2` 为**竖将**;`2×1` 为**横将**;`1×1` 为**兵**。分类 MUST 是从 `KlotskiPiece` 的 `width` / `height` / `target` 推导的纯函数,MUST NOT 新增模型字段,MUST NOT 依赖 `name` 或 `id`,也 MUST NOT 维护一份棋子名单。

理由:改这条之前六个棋子里**五个是同一个颜色**,唯一的区分是那两个汉字;而 `name` 是关卡数据里的自由文本,任何按名字分类的写法都会在下一份 `layoutJson` 上悄悄退化成"全都是默认那一类"。尺寸是规则本身,推不歪。

其它形状(未来关卡若出现 `1×3`、`3×2` 等)MUST 落到一个明确的兜底类,而 MUST NOT 让 `undefined` 流进模板。

#### Scenario: 四类都出现,且各不相同
- **WHEN** 渲染一个同时含 `2×2`、`1×2`、`2×1`、`1×1` 的关卡
- **THEN** 四类棋子的面 MUST 两两不同,且断言 MUST 在**四类都出现**时才通过 —— 一个只有两类的盘面 MUST NOT 让这条恒真

#### Scenario: 面用 `background` 简写接住,颜色与渐变都能画
- **WHEN** 某个皮肤把某类棋子的面给成一个**颜色**而非渐变(跟随主题的皮肤就是这样)
- **THEN** 它 MUST 真的画出来。把面赋给 `background-image` MUST 失败:颜色不是合法的 `background-image` 值,它计算成 `none`,那个皮肤下棋子完全没有底色,而 CSS 不报错、jsdom 也量不到

#### Scenario: 分类不看名字
- **WHEN** 把某个棋子的 `name` 改成任意其它字符串,几何尺寸不变
- **THEN** 它的角色分类 MUST 不变

### Requirement: 棋子移动 SHALL 是可见的滑动,而 `prefers-reduced-motion` 下不滑

棋子的位置 SHALL 由 `transform: translate()` 表达,而不是由 `grid-area` 的行列线表达。于是位置变化落在一个**可动画的属性**上,`transition` 才真的生效。

平移的步长 MUST 用**容器查询单位**表达,MUST NOT 用百分比:`transform: translate()` 里的百分比按**元素自己**的尺寸解析,于是一个 2×2 的块和一个 1×1 的块会走出不同的步长。牌桌的扇形公式在同一条上栽过四次。

而定义步长的那个变量 MUST NOT 写在容器元素自己身上 —— 一个元素用不了自己的容器查询单位(它会解析到更外层的容器),同理一条查询自己的 `@container` 永远不匹配,是死代码。所以棋盘与坐标系是**两层**元素。

**这条 MUST 有一个能变红的判据,而"transition 列表里有 transform"不是。** 改这条之前 `.kt-piece` 的计算值就是 `transition-property: box-shadow, transform` —— 已经含 `transform` 了,而棋子照样瞬移,因为位置根本不由 `transform` 表达。

判据是**位置由格坐标经 `transform` 落位**,分两条,因为没有哪一条单独够:

1. 棋子 MUST 携带自己的格坐标(`--kt-r` / `--kt-c`),移动一个棋子 MUST 只改变**那一个**棋子的坐标 —— 一个把所有坐标都重写的实现同样能让"变了"成立;
2. 棋子 MUST NOT 声明 `grid-area`。

**MUST NOT 把"计算出来的 `transform` 变了"当判据** —— 那读不到:`transform` 是渲染阶段的量,而本仓库的浏览器面板在不合成时**布局值会更新、`transform` 不会**。量到过:JS 改了容器宽度之后,棋子宽度跟着新格距变了,位置却停在旧格距上,读起来**和真的溢出一模一样**。要判位置,量元素的 `getBoundingClientRect()` 与格坐标是否吻合,并且在**重新渲染之后**量。

`global.css` 里那句「The CSS transition is on `grid-area`'s resolved position, which browsers animate as a layout change」MUST 删掉:grid 的行列线不可动画,这句话描述的是一件浏览器不做的事。

`@media (prefers-reduced-motion: reduce)` 下过渡时长 MUST 为 0 —— 平台基线已有这条,这里只是不许绕开它。

#### Scenario: 移动只改那一个棋子的格坐标
- **WHEN** 选中一个棋子并滑到一个合法落点
- **THEN** 恰好一个棋子的 `--kt-r` / `--kt-c` 改变,其余不变;且没有任何棋子声明 `grid-area`

#### Scenario: 减少动效时不滑
- **WHEN** `prefers-reduced-motion: reduce`
- **THEN** 棋子的 `transition-duration` MUST 为 `0s`

### Requirement: 棋盘在窄屏可用,在桌面不再是一张小卡片

棋盘 MUST 在 375px 宽度下完整可见、无横向溢出;`sm` 以上 SHALL 跟随容器长大,MUST NOT 固定在 360px。长宽比 MUST 始终等于 `cols / rows`,格子 MUST 保持正方形。

#### Scenario: 375px 无横向溢出
- **WHEN** 布局视口 375px **且页面是在该宽度下渲染的**,渲染格数最多的那一关
- **THEN** 页面 `scrollWidth` MUST NOT 大于 `clientWidth`,且每个棋子的右边缘 MUST 落在盘面之内。**改完宽度不重新渲染就量 MUST NOT 算数** —— 量到过一次假溢出

#### Scenario: 桌面上变大
- **WHEN** 视口 ≥ 1024px
- **THEN** 棋盘渲染宽度 MUST 大于 360px

### Requirement: 新盘面的前景/填充配对 MUST 在四主题 × 明暗 × 每个皮肤下都达标

棋子上的字、出口标记与合法落点标记,MUST 在每一个「主题 × 明暗 × 皮肤」组合下对其所在填充达到 4.5:1。四类棋子的面各不相同,所以这是四组配对而不是一组。

**同色画同色是无声的** —— 本仓库栽过:`currentColor` 的字画在 `currentColor` 的填充上,不抛错、不失败、什么也看不见。

检查 MUST 能解开跟随主题的皮肤所用的 `color-mix()`,否则那些配对会被**静默跳过** —— 而跳过与通过打印的是同一行字。

#### Scenario: 对比度由检查保证
- **WHEN** 任一皮肤块把某类棋子的字与面调成对比度低于 4.5:1
- **THEN** `npm run lint` MUST 失败,并 MUST 报出是哪个皮肤、哪一类棋子、哪个主题与明暗

#### Scenario: 解不出来的颜色算「没量到」,不算「通过」
- **WHEN** 任一 (皮肤, 主题, 明暗, 角色) 组合的颜色解析不出最终值
- **THEN** 检查 MUST 失败并报出被跳过的组合数。**MUST NOT 只对读数总数设下限** —— 量到过:打断 `color-mix()` 解析后读数从 224 掉到 200,而按 96 设的下限照样通过,跟随主题那个皮肤的 24 条读数就那样消失了

