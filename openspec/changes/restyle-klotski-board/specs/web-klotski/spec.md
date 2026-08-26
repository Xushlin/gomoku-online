## ADDED Requirements

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
- **THEN** 四类棋子的计算背景 MUST 两两不同,且断言 MUST 在**四类都出现**时才通过 —— 一个只有两类的盘面 MUST NOT 让这条恒真

#### Scenario: 分类不看名字
- **WHEN** 把某个棋子的 `name` 改成任意其它字符串,几何尺寸不变
- **THEN** 它的角色分类 MUST 不变

### Requirement: 棋子移动 SHALL 是可见的滑动,而 `prefers-reduced-motion` 下不滑

棋子的位置 SHALL 由 `transform: translate()` 表达,而不是由 `grid-area` 的行列线表达;格子尺寸仍由 grid 计算。于是位置变化落在一个**可动画的属性**上,`transition` 才真的生效。

**这条 MUST 有一个能变红的判据,而"transition 列表里有 transform"不是。** 改这条之前 `.kt-piece` 的计算值就是 `transition-property: box-shadow, transform` —— 已经含 `transform` 了,而棋子照样瞬移,因为位置根本不由 `transform` 表达。判据 MUST 是"位置来自 `transform`":即棋子的 `transform` 在移动前后不同,且它的 grid 起止线不变。

`global.css` 里那句「The CSS transition is on `grid-area`'s resolved position, which browsers animate as a layout change」MUST 删掉:grid 的行列线不可动画,这句话描述的是一件浏览器不做的事。

`@media (prefers-reduced-motion: reduce)` 下过渡时长 MUST 为 0 —— 平台基线已有这条,这里只是不许绕开它。

#### Scenario: 移动改变的是 transform
- **WHEN** 选中一个棋子并滑到一个合法落点
- **THEN** 该棋子的 `transform` MUST 与移动前不同,而它的 `grid-row-start` / `grid-column-start` MUST 不变

#### Scenario: 减少动效时不滑
- **WHEN** `prefers-reduced-motion: reduce`
- **THEN** 棋子的 `transition-duration` MUST 为 `0s`

### Requirement: 棋盘在窄屏可用,在桌面不再是一张小卡片

棋盘 MUST 在 375px 宽度下完整可见、无横向溢出;`sm` 以上 SHALL 跟随容器长大,MUST NOT 固定在 360px。长宽比 MUST 始终等于 `cols / rows`,格子 MUST 保持正方形。

#### Scenario: 375px 无横向溢出
- **WHEN** 视口 375px,渲染格数最多的那一关
- **THEN** 页面 `scrollWidth` MUST NOT 大于 `clientWidth`

#### Scenario: 桌面上变大
- **WHEN** 视口 ≥ 1024px
- **THEN** 棋盘渲染宽度 MUST 大于 360px

### Requirement: 新盘面的前景/填充配对 MUST 在四主题 × 明暗 × 每个皮肤下都达标

棋子上的字、出口标记与合法落点标记,MUST 在每一个「主题 × 明暗 × 皮肤」组合下对其所在填充达到 4.5:1。四类棋子的面各不相同,所以这是四组配对而不是一组。

**同色画同色是无声的** —— 本仓库栽过:`currentColor` 的字画在 `currentColor` 的填充上,不抛错、不失败、什么也看不见。

#### Scenario: 对比度由检查保证
- **WHEN** 任一皮肤块把某类棋子的字与面调成对比度低于 4.5:1
- **THEN** `npm run lint` MUST 失败,并 MUST 报出是哪个皮肤、哪一类棋子
