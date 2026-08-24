# platform-catalog 的规格变化

## MODIFIED Requirements

### Requirement: `GameManifest` 是游戏的唯一声明形状

一份 `GameManifest` SHALL 声明:

- `key: string` —— 全局唯一的 kebab-case 标识。
- `category: 'match' | 'puzzle' | 'score'`。
- `status: 'available' | 'planned'`。
- `titleKey: string` / `descriptionKey: string` —— Transloco 键,MUST 形如 `games.<key>.title` / `games.<key>.description`。
- `emblem: readonly EmblemShape[]` —— 纹章的形状表,见下条要求。**MUST 非空,连 `planned` 的棋种也是** —— 空表画出来是一张看不见的牌,而看不见的牌不会让任何走查变红。
- `contentLocales: readonly string[]` —— 该游戏**内容**(而非 UI)可用的 locale 列表。
- `launchRoute?: string` —— 仅当 `status === 'available'` 时有意义的入口路由。

不变量:`status === 'available'` 的清单 MUST 提供非空 `launchRoute`;`status === 'planned'` 的清单 MUST NOT 依赖 `launchRoute` 被读取。

**清单 MUST NOT 携带盘面尺寸。** 它此前有一个 `board` 字段,是服务端权威数据的一份刻意副本,当时被接受的理由是「错了会被看见」——格数肉眼可辨,且服务端会挡住越界落子。

**`icon: string` 已被 `emblem` 取代,而 MUST NOT 两者并存。** 那个字段是一个字符(`'⬤'` 一类),而九个棋种呈现为九个字符贴在九张一模一样的牌上,正是「UI 太粗糙、不像游戏」被量出来时的样子:整个大厅只有四个视觉值。留着 `icon` 会让同一件事有两种表示,而**两份表示里必有一份会烂**。

#### Scenario: 每个注册棋种都有非空纹章
- **WHEN** 遍历 `GAME_REGISTRY`
- **THEN** 每份清单的 `emblem` 非空;断言从注册表推导,MUST NOT 手写棋种名单

### Requirement: 目录页为每份清单渲染一张卡片

目录页 SHALL 从 `GameCatalogService.all()` 渲染卡片,每张卡片包含:**纹章**、`titleKey` 翻译、`descriptionKey` 翻译、`category` 徽标(`catalog.category-{match,puzzle,score}`)。

- `status === 'available'` 的卡片 SHALL 是导航到 `launchRoute` 的链接。
- `status === 'planned'` 的卡片 SHALL 显示 `catalog.coming-soon` 文案。
- 当活动 locale **不在** `contentLocales` 内时,卡片 SHALL 额外显示 `catalog.chinese-only` 徽标。

模板 MUST NOT 硬编码任何游戏名、描述或状态文案 —— 全部走 Transloco。纹章 SHALL 经由渲染组件绘制,模板 MUST NOT 内联任何 SVG。

#### Scenario: 卡片画的是纹章,不是一个字符
- **WHEN** 渲染目录页的任一张卡片
- **THEN** 图标位置是 `<app-game-emblem>`;模板里 MUST NOT 出现内联 `<svg>`,也 MUST NOT 出现 `manifest.icon`

#### Scenario: 两种状态的卡片都画纹章
- **WHEN** 分别渲染 `available` 与 `planned` 的卡片
- **THEN** 两者都画出纹章 —— `planned` 的清单也 MUST 有非空形状表,否则那张卡片是空的而没有任何断言会红

## ADDED Requirements

### Requirement: 纹章是形状表,而渲染器独占作图系统

每个棋种的纹章 SHALL 声明为 **24×24 网格上的一组图元**(线 / 圆 / 方 / 字形,外加一个 `path` 逃生口),而 **网格、描边宽度、线端与线接形状 SHALL 由渲染组件独占,任何清单 MUST NOT 指定它们**。

**这条独占不是为了省字节,它是「十个纹章读起来像一套」的机制。** 十段手写 `path` 各自都有权选自己的粗细与视觉尺寸,那会得到十张画。字节上的便宜是顺带的,但值得记下量到的数:十份形状表共 **1.27 kB**,平均 142 B,而 `card-art.ts` 一个花色剪影平均 575 B。

颜色 SHALL 一律来自 `currentColor`,由牌面给出身份色;形状表里 MUST NOT 出现任何色值字面量。

新增图元种类 SHALL 由**编译器**保证被处理 —— 映射函数的 `default` 分支参数类型是 `never`,漏一种就编译不过并点名。一个静默落空的 `switch` 会让纹章少画一笔,而**一张 30 px 的牌上少一笔没人会发现**。

#### Scenario: 只有盒子缩放,网格不缩放
- **WHEN** 以两个不同的 `size` 渲染同一份形状表
- **THEN** `width` / `height` 跟着变,`viewBox` **不变**

#### Scenario: 形状表里没有颜色
- **WHEN** 序列化全部注册棋种的形状表
- **THEN** 其中不含 `#rrggbb` 也不含 `rgb(`

#### Scenario: 纹章是装饰性的
- **WHEN** 渲染任一纹章
- **THEN** `<svg>` 带 `aria-hidden="true"` 与 `focusable="false"` —— 棋种名就在旁边,读两遍是噪音

### Requirement: 字形图元按**墨迹**定尺寸,而容器边界要算上描边

用 `<text>` 的图元(象棋的「帥」、斗地主的「王」)SHALL 按**墨迹的实测尺寸**定字号,而 MUST NOT 按字号或 `getBBox()` 推断。

这条要求是一次真实缺陷的产物,而三次测量给了三个答案,其中前两次都在量错的东西:

1. **按字宽估算** —— CJK 字形的宽恰好等于字号,于是「10 宽的卡片放 9 号字」看起来放得下。上线后一眼就能看出撑破了。
2. **`getBBox()`** —— 它返回**行盒**,而 CJK 行盒高约 `1.45 × 字号`,含一大截字形用不到的上伸部。据它判定会得到「上沿溢出」这个**假失败**。
3. **把 SVG 画进 canvas 采样墨迹像素** —— 这个才回答问题。

**而真正反复取错的是容器边界:** 一个**描边**的容器,其内沿是 `半径 − 线宽/2`。象棋内圈半径 7、线宽 1.6,所以墨迹可用半径是 **6.2**;「帥」在 9.5 号时墨迹半对角 **6.79**(压线),在 7.5 号时 **5.36**(通过)。

字形 MUST NOT 画在**填充**图元之上 —— 两者同为 `currentColor`,叠在一起是隐形的,而隐形不报错、不变红。

**能自动化的守卫弱于这条要求本身,而这一点 SHALL 被写明:** 「字形合不合容器」只有在真浏览器里量墨迹才能回答,而本仓库的测试跑在 jsdom 上(无布局、无 `getBBox`、画不了 SVG 文字)。所以自动化守的是**字号上界**与**不叠在填充上**这两件事,它们会在原样回归时变红;「容器改小了」需要一次浏览器里的重新测量。

#### Scenario: 字号超过实测上界
- **WHEN** 任一字形图元的字号大于实测上界
- **THEN** 测试失败,并点名棋种与该字号

#### Scenario: 字形叠在填充图元上
- **WHEN** 某个棋种既有字形图元、又有 `f: 1` 的图元
- **THEN** 测试失败并点名 —— 同色叠同色看不见
