# extend-theme-tokens

把棋盘那一层已经存在的**游戏化视觉词汇**扩到棋盘外的整个外壳,并把组件从「拼视觉值」改成「说出角色名」。

**这个变更屏幕上一个像素都不该动。** 它是使能改动;真正的皮肤是紧接着的 `add-qq-game-theme`。

## Why

用户的原话是「现在的 UI 太粗糙了不适合游戏的 UI」。量过之后,「粗糙」有精确形态 —— 在 `/home`(整个游戏大厅,66 个元素)上:

| 视觉词汇 | 现状 |
| --- | --- |
| 圆角 | **1 种**(12px,全站同一个) |
| 阴影 | **1 种**(同一个软阴影) |
| 背景色 | **2 种**(`#ffffff` / `#fafafa`) |
| 渐变 / 纹理 / 图像 | **0** |

一共四个视觉值。**这不是做糙了,是词汇量的问题** —— 一套后台管理页的调色板被放进了游戏大厅。九个棋种在大厅里是**一个汉字**贴在九张一模一样的白卡片上。

而真正的发现是这个反差:

| | token 数 | 支持渐变 / 纹理 |
| --- | --- | --- |
| 棋盘 / 牌桌(`board-skins.css`) | **26** —— `--board-bg-image`、`--felt-edge`、`--card-face-edge`、`--stone-black-shadow` | **支持** |
| 棋盘外的整个外壳(`theme.tokens.ts`) | **11** —— 9 色 + 1 圆角 + 1 阴影 | **不支持** |

**做 QQ 游戏观感不需要发明新机制。** 机制已经存在、已经有三套皮肤跑在上面、`check-styles.mjs` 已经在给它做 token 对齐校验 —— 它只是**停在棋盘边缘**。这个变更把那条边界往外推。

## What(以及为什么是两个变更)

拆成两步,而理由不是行数:

1. **`extend-theme-tokens`(本变更)** —— 扩契约、给现有三套主题补中性值、引入角色 utility、把组件的 class 串换成角色名。**验收标准是「屏幕上零变化」。**
2. **`add-qq-game-theme`(下一个)** —— 一个 token 文件 + 一段 `tokens.css` + 一行 `register` + 改一个默认值常量。

**合成一个变更,那条「零变化」的验收标准就不可能成立** —— 一个像素动了,你分不清是重构挪的还是新皮肤画的。分开之后第一步可以逐条比对计算样式,第二步则**成为架构承诺的证明**:活规格写着「加一套主题是单文件改动,MUST NOT 修改任何组件源码」,而第二步的 `git diff --name-only` 里**不出现任何组件文件**,那就不再是一句声明。

### 一处必须讲明白的张力

本变更**要改组件源码**,而上面那条规格说 MUST NOT。两者不冲突,因为那条约束管的是**「加一套主题」**这个动作,而本变更做的是**扩词汇**——一次性的,并且做完之后那条约束原封不动、并且立刻被第二步检验。提案把这件事写在这里,而不是等 review 时被问。

### 契约怎么扩

按 `board-skins.css` 已经证明可用的那些种类,而不是凭想象:

- **面(surface)**:`--surface-image`(渐变 / 纹理)、`--surface-edge`(顶部高光那道边)、`--surface-edge-width`。这三个对应棋盘那边的 `--board-bg-image` / `--felt-edge`。
- **控件(control)**:`--control-image`、`--control-edge`、`--shadow-raised`(按钮的凸起)、`--shadow-inset`(输入框 / 凹槽)。
- **尺度**:`--radius-control`、`--radius-pill`(现在只有 `--radius-card` 一个)。
- **强调**:`--accent`、`--accent-image`(金边 / 朱漆那一路装饰)。

**每一个都进必需契约,四套主题都必须声明,明暗两份都要。** 这是这个仓库自己判过的形状:少一个 token 的皮肤**编译不过**,而「可选的装饰层」意味着一套主题可以半实现,画出来缺一块却没有任何东西变红。代价诚实写下来:**加一个 token 是三套现有主题一起付的账** —— 它们各拿到中性值(`none` / `transparent` / `0`),而中性值的定义就是「画出来和今天一模一样」。

### 组件改什么

组件今天写 `class="bg-surface rounded-card shadow-elevated"` —— 那是**把视觉值拼出来**。渐变没法从 `--color-*` 走(颜色不能是渐变),所以角色化是必需的而非审美:

```
@utility panel { background-color: var(--color-surface); background-image: var(--surface-image);
                 border-top: var(--surface-edge-width) solid var(--surface-edge);
                 border-radius: var(--radius-card); box-shadow: var(--shadow-elevated); }
```

组件改成 `class="panel"`。角色名一共不超过六个(`panel` / `panel-raised` / `control` / `control-primary` / `well` / `rail`),而它们的意义是**语义**:一个主题想让面板带纹理,不需要知道哪个组件在哪里。

## 不做的事

- **不动布局、不动 DOM 结构、不动任何组件的模板结构。** 用户选的是「只换视觉层」。大厅九宫格换成有画的游戏牌、房间列表换成带座位小人的桌子列表 —— 那是后面单独的事,本变更不碰。
- **不加位图。** 纯 CSS + 内联 SVG。位图不占那 480 kB 的 bundle 预算(那是 JS 的),但它**不跟着主题和暗色变**,得每套各出一份 —— 与 `draw-card-suits` 当初从 PNG 换成 SVG path 的判断同源。
- **不删任何现有主题。** `material` / `system` / `ink` 全留。

## 顺手修两处漂移

都是找到的,不是计划里的:

1. **活规格 `web-theming` 的需求标题写着「首发两套主题 —— `material` 与 `system`」,而它自己的 Scenario 写着「三套主题都注册」。** `ink` 上线时改了 Scenario 没改标题。走 `RENAMED` + `MODIFIED`,而新标题**不写数量** —— 一个数量写进标题,就是下一次漂移的位置。
2. **`CLAUDE.md` 第 207 行还写着「Two themes ship」。** 与刚修掉的 board skin 那句同族:散文里的手写枚举。同样改成指目录。

**这一处值得记下来:** 它是在 `docs/restructure-claude-md` **之后**找到的 —— 那个重构把眼前那份的成本从 53k 降到 6.9k token,但它不会、也不可能自己把每句陈述查一遍。我是因为这个任务去翻主题系统才撞见的。**减少无条件载入的量,和保证载入的内容为真,是两件事。**
