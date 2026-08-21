# web-theming Specification

## Purpose
TBD - created by archiving change add-web-scaffold. Update Purpose after archive.
## Requirements
### Requirement: 主题以 CSS 变量为载体,组件不得使用色值字面量

前端 SHALL 通过 CSS 变量(custom properties)表达所有颜色、圆角、阴影 token。token 集合位于 `src/styles/tokens.css`,挂在 `:root` 及 `[data-theme="<name>"]` / `[data-theme="<name>"].dark` 选择器上。

组件样式(模板 `class`、组件样式表、`@apply`、CSS 文件)MUST NOT 出现:

- 任何十六进制色值字面量(`#xxx`、`#xxxxxx`、`#xxxxxxxx`);
- `rgb(...)`、`rgba(...)`、`hsl(...)`、`hsla(...)` 的字面量颜色;
- Tailwind 内置颜色 utility(`bg-gray-*`、`text-white`、`bg-black`、`text-blue-500` 等);
- 命名颜色关键字(`white`、`black`、`red`、……)。

唯一合法来源:

- `var(--color-*)`、`var(--radius-*)`、`var(--shadow-*)` 等由 tokens.css 定义的变量;
- Tailwind 自定义映射(见下一条 Requirement)把 `bg-bg` / `text-text` / `shadow-elevated` 等映射到上述 CSS 变量。

例外:tokens.css 本身是 token 的定义处,允许写具体色值。

#### Scenario: tokens.css 是唯一色值来源
- **WHEN** 在 `frontend-web/src/` 下(不含 `src/styles/tokens.css`、`node_modules`、生成物)检索硬编码色值(正则 `#[0-9a-fA-F]{3,8}\b`、`\brgb\(`、`\bhsl\(`)
- **THEN** 0 个匹配

#### Scenario: 组件通过变量着色
- **WHEN** 审阅 `Header` / `Home` / `Shell` 的模板与样式
- **THEN** 所有视觉属性(background、color、border、shadow、radius)都源自 `var(--...)` 或 Tailwind 的 token 映射 utility,不存在 `bg-gray-900` / `text-white` 这类硬编码

---

### Requirement: Tailwind 被配置为 token 消费者

`tailwind.config.js` SHALL 扩展 `theme.extend.colors` / `borderRadius` / `boxShadow`,使得关键 utility 类映射到 CSS 变量:

- `colors.bg → var(--color-bg)`
- `colors.surface → var(--color-surface)`
- `colors.primary → var(--color-primary)`
- `colors.text → var(--color-text)`
- `colors.muted → var(--color-text-muted)`
- `colors.border → var(--color-border)`
- `colors.danger / success / warning → var(--color-danger/success/warning)`
- `borderRadius.card → var(--radius-card)`
- `boxShadow.elevated → var(--shadow-elevated)`

`darkMode` SHALL 设置为 `'class'`(不用 media 策略)。

#### Scenario: darkMode 走 class 策略
- **WHEN** 读取 `frontend-web/tailwind.config.*`
- **THEN** 配置中 `darkMode === 'class'`

#### Scenario: 关键 token utility 可用
- **WHEN** 在模板里写 `class="bg-bg text-text rounded-card shadow-elevated"`
- **THEN** Tailwind 编译后产出的 CSS 使用 `var(--color-bg)` 等变量而非固定色值

---

### Requirement: `ThemeService` 提供注册表 + 双信号 API

`ThemeService` SHALL 定义为 abstract class 作为 DI token,由 `DefaultThemeService` 实现并通过 `providers: [{ provide: ThemeService, useClass: DefaultThemeService }]` 注册。

API 契约:

- `readonly themeName: Signal<string>` — 当前主题名(如 `'material'`、`'system'`)。
- `readonly isDark: Signal<boolean>` — 当前是否暗色。
- `register(name: string, tokens: ThemeTokens): void` — 注册一个主题的 token 形状(主要用于主题切换 UI 列举 + 开发期完整性校验);不写 CSS。
- `activate(name: string): void` — 切换主题;MUST 设置 `document.documentElement.dataset.theme = name`,并持久化到 `localStorage['gewu:theme']`,并更新 `themeName` signal。
- `setDark(isDark: boolean): void` — 切换明暗;MUST 在 `document.documentElement.classList` 上 toggle `'dark'` 类,并持久化到 `localStorage['gewu:dark']`(`'1'` / `'0'`),并更新 `isDark` signal。

`themeName` 与 `isDark` 是**正交**的两个 signal —— 切换其一不影响另一个。

#### Scenario: activate 切换 data-theme 并持久化
- **WHEN** `themeService.activate('system')`
- **THEN** `document.documentElement.dataset.theme === 'system'`、`localStorage.getItem('gewu:theme') === 'system'`、`themeName() === 'system'`,且 `isDark()` 不变

#### Scenario: setDark 切换 dark class 并持久化
- **WHEN** `themeService.setDark(true)`
- **THEN** `document.documentElement.classList.contains('dark') === true`、`localStorage.getItem('gewu:dark') === '1'`、`isDark() === true`,且 `themeName()` 不变

#### Scenario: DI 走抽象类 token
- **WHEN** 在测试里 `TestBed.configureTestingModule({ providers: [{ provide: ThemeService, useValue: stub }] })`
- **THEN** 组件通过 `inject(ThemeService)` 拿到 stub,无需修改组件代码

### Requirement: 初始主题与明暗解析顺序

`DefaultThemeService` 在 app 启动时 SHALL 按如下优先级解析初值:

**主题名**:
1. `localStorage['gewu:theme']`(若值在 `register` 过的主题列表中)
2. 回退为 `'material'`

**明暗**:
1. `localStorage['gewu:dark']`(`'1'` 即暗,`'0'` 即明)
2. `window.matchMedia('(prefers-color-scheme: dark)').matches` → `true` 即暗
3. 回退为 `false`(明色)

用户一旦手动切换过,持久化值 MUST 始终优先于系统偏好。

#### Scenario: localStorage 存在时优先
- **WHEN** `localStorage['gewu:dark'] === '0'` 但 OS 处于 dark preferred
- **THEN** 启动后 `isDark() === false`

#### Scenario: 无持久化时跟随系统
- **WHEN** `localStorage['gewu:dark']` 不存在且 OS `prefers-color-scheme: dark`
- **THEN** 启动后 `isDark() === true`

#### Scenario: 无效主题名回退
- **WHEN** `localStorage['gewu:theme'] === 'nonexistent'`
- **THEN** 启动后 `themeName() === 'material'`(回退),且 `localStorage` 被覆盖为 `'material'`

### Requirement: 内置主题集合与它们各自的取舍

平台 SHALL 交付并注册以下主题,每套都包含明暗两份 token 集合:

- `material`:Angular Material 默认风格 —— 较大圆角、明显阴影、Material 调色(primary 落在蓝紫区)。
- `system`:Apple / Fluent-ish 简洁风 —— 更小圆角(≤ 8px)、更轻阴影、更平。
- `ink`:活字印刷风 —— 墨蓝/宣纸底、朱砂作强调色、竹青作成功色,圆角小、阴影重(字块的"厚度")。

每一套的加入 SHALL 走既有扩展点(见下一条要求),即一个 token 文件 + 一段 `tokens.css` 规则 + 一行 `register` 调用,MUST NOT 修改任何组件或既有主题。`ink` 是走通这条路的那一次。

**每套主题都 MUST 同时定义明暗两份。** `ink` 的原型(成语纵横)只有暗色墨蓝一种,而主题层的契约是成对的;它的浅色一套取宣纸为底、墨为字,朱砂保持强调色 —— 朱砂落纸本来就是这套视觉里更古老的那一半。**「原型只有一种模式」不是少交一份的理由,是设计另一份的任务。**

全部主题 MUST 在明暗两种模式下都通过对比度校验(WCAG AA 标准,正文 text 对 bg 对比度 ≥ 4.5:1)。

**主题数量 MUST NOT 出现在需求标题里。** 上一版把「两套」写进了标题,而 `ink` 上线后标题和它自己的 Scenario 就互相矛盾了 —— 这是本仓库判过五次的「手写清单冒充注册表」在**散文**里的同一个形状,而散文没有编译器。

走查 SHALL 从 `themeService.availableThemes()` 推导,而**断言的形式是包含,不是数量**。这一条与本仓库「优先『恰好 N』而不是『至少 N』」那条规则不冲突,是它的细化:**「恰好 N」在 N 变化意味着某个不变量可能被破坏时值钱;当 N 变化本身就是写在规格里的正常扩展路径时,它只是一条「请来更新我」的测试。** 加一套主题是本 spec 明文承诺的单文件改动,所以数量变红不携带信息;而「每套注册的主题都完整」会因为**对的理由**变红,那一条 MUST 有(见下面的 token 对齐要求)。

#### Scenario: 注册表就是主题清单
- **WHEN** 启动后读取 `themeService.availableThemes()`
- **THEN** 它含 `'material'`、`'system'` 与 `'ink'`;断言用包含,MUST NOT 断言长度

#### Scenario: 所有 6 种组合都工作
- **WHEN** 依次切换到 (material|system|ink) × (light|dark)
- **THEN** 每一种组合下 header 与 home 都正确渲染,无不可见文本(text 与 bg 对比度通过 WCAG AA)

#### Scenario: 每套主题的明暗两份都完整
- **WHEN** 遍历注册表,逐套读取它的 token 集合
- **THEN** `light` 与 `dark` 的键集合完全相同,且都覆盖**当时契约里的全部组** —— 组的清单在下面那条「游戏化视觉词汇」要求里,而这里 MUST NOT 重列一遍(重列就是第二份会落后的清单)

#### Scenario: 主题切换器显示新主题
- **WHEN** 打开 header 的主题菜单
- **THEN** 出现 `ink` 条目,文案取自 `header.theme.ink` 翻译键而非裸 key

---

### Requirement: 扩展点 —— 加主题是单文件改动

新增一个主题 MUST 只需要:

1. 在 `src/app/core/theme/themes/<name>.(light|dark).ts` 新增两份 token 对象;
2. 在 `src/styles/tokens.css` 追加两段 `[data-theme="<name>"]` 与 `[data-theme="<name>"].dark` 规则;
3. 在 `DefaultThemeService` 启动注册序列中新增一行 `this.register('<name>', ...)`。

MUST NOT 需要:修改任何组件源码、修改任何现有主题的 token、修改 Tailwind config(因为 utility 已经绑定到 CSS 变量)。

**这条约束管的是「加一套主题」这个动作,不管「扩 token 词汇」。** 两者必须分开,否则它会禁止自己的前提:token 契约只有 9 色 + 1 圆角 + 1 阴影时,任何需要渐变、斜角或纹理的主题都**表达不出来**,而颜色不能是渐变。扩词汇是一次性的、要动组件的(组件得从「拼视觉值」改成「说出角色名」),而扩完之后本条约束原封不动 —— 并且下一个主题就是它的检验。

**每一次扩词汇 SHALL 以「现有主题画出来一模一样」为验收标准,而例外必须逐条列出并给出量到的证据。** 判据是 6 种(主题 × 明暗)组合下关键面的**计算样式逐条比对**,而不是目测。没有这条基线,「只是加了个可选层」和「悄悄改了三套主题的长相」在屏幕上分辨不出来。

中性值 SHALL 定义在一处(`NEUTRAL_DECORATION`),而 **MUST NOT 靠名字去猜** —— 头两个「显然」的猜法都是错的,而两个都会以「零变化」的名义把东西改坏:

- `--surface-edge` 的中性值是 `var(--color-border)`,**不是** `transparent`:角色 utility 用它设**上边框**,所以中性必须是「和另外三条边同色」;写 `transparent` 会让每一块面板的**上边消失**。
- `--shadow-raised` / `--shadow-inset` 的中性值是 `0 0 #0000`,**不是** `none`:`panel` 把它拼进 `box-shadow: var(--shadow-elevated), var(--shadow-raised)`,而 `none` **不是合法的阴影列表成员** —— 整条声明失效,连原有的阴影一起没了。

#### Scenario: 扩词汇不改现有主题的长相
- **WHEN** 一次扩 token 契约的改动落地后,依次切到 (material|system|ink) × (light|dark)
- **THEN** 关键面的 `background-color` / `background-image` / 四边宽与色 / `border-radius` / `color` / `padding` 与扩之前**逐条相同**;唯一允许的差异是本 spec 另一条要求点名的 `box-shadow`

#### Scenario: 那条基线自己是可以变红的
- **WHEN** 故意把某一套主题的某个新 token 的中性值写成非中性
- **THEN** 校验变红,并点名**哪套主题、哪个 token、期望值与实际值** —— 一条测不出错的基线证明不了零变化

#### Scenario: 比对时的归一化只许去掉真正的空操作
- **WHEN** 比对 `box-shadow` 字符串
- **THEN** 只有**恰好** `rgba(0, 0, 0, 0) 0px 0px 0px 0px` 的成员可以被忽略(中性 token 的产物,画出来什么也没有);任何其它差异 MUST 计入

#### Scenario: 扩展仪式
- **WHEN** 假想新增一个 `playful` 主题
- **THEN** 从 diff 角度:纯新增一个 ts 文件 + 一段 css 规则 + 一行注册调用,`grep -r` 不显示任何既有组件或既有主题文件被修改

### Requirement: token 契约带一层游戏化视觉词汇,而它对每套主题都是必需的

`ThemeTokenSet` SHALL 在现有 `colors` / `radii` / `shadows` 之外再带三组 token,而每一组的种类**取自 `board-skins.css` 已经证明可用的那些**,不是凭想象拟的:

| 组 | token | 对应棋盘那边已有的 |
| --- | --- | --- |
| `surfaces` | `image`、`edge`、`edgeWidth` | `--board-bg-image`、`--felt-edge` |
| `controls` | `image`、`edge`、`edgeWidth` | `--card-face`、`--card-face-edge` |
| `accents` | `color`、`image` | `--board-star`、`--last-move-ring` |
| `grounds` | `image` | `--felt-bg` |

`radii` SHALL 增 `control`;`shadows` SHALL 增 `raised` 与 `inset`;`colors` SHALL 增 `onPrimary`。

**`pill` 不加。** 提案里列了它,而走查里没有任何一处需要 —— 一个没有调用点的 token 是每套主题都要付账的死条目。加它的触发条件是**第一个真的需要全圆角的控件**。

**`onPrimary` 必须加,而它的中性值是字面 `#ffffff`、明暗两份都是。** 主操作按钮的前景色今天写死在模板里(`text-white`),不 token 化的话下一个主题画不了按钮文字而又不碰组件。**保留这个中性值等于保留一个既有缺陷**:material 暗色的 `--color-primary` 是淡蓝,白字在它上面约 1.9:1。那是修复前就有的,而本变更的验收标准是「什么都不改变长相」,所以修它属于**另一个允许改外观的变更**。触发条件:任何一次动主操作按钮配色的变更。

**新 token MUST 进必需契约,而不是做成一个可选装饰层。** 每套主题、明暗两份,都必须声明每一个。理由与皮肤 / sound pack 那两处逐字相同:一个可选层允许一套主题**半实现**,画出来缺一块而没有任何东西变红;而这个仓库的做法是让缺一个 token 的实现**过不去**。代价照实写:**加一个 token 是所有现有主题一起付的账**,它们各拿中性值。

`--surface-image` 与 `--control-image` 的值 SHALL 是 CSS 渐变或内联 SVG data URI,MUST NOT 引用外部位图 —— 位图不跟着主题与暗色变,得每套各出一份,而这与 `draw-card-suits` 从 PNG 换成 SVG path 的判断同源。

#### Scenario: 每套主题都声明每一个 token
- **WHEN** 遍历 `themeService.availableThemes()`,逐套取 `light` 与 `dark`
- **THEN** 两份的键集合完全相同,且都覆盖契约里的全部组 —— 清单从 `ThemeTokenSet` 推导,这里 MUST NOT 重列(重列就是第二份会落后的清单)

#### Scenario: 缺一个 token 会被点名
- **WHEN** 某套主题的 `dark` 缺一个 `surfaces.image`
- **THEN** 校验失败,且信息点名**哪套主题、哪个模式、哪个键** —— 只说「校验失败」的话,四套主题里定位它要靠试

#### Scenario: 占位值退化成今天的样子
- **WHEN** 一套主题在 `tokens.css` 里漏了某个新 token 的值
- **THEN** 它从 `@theme` 拿到的占位值是**中性的**,于是画出来是今天的样子 —— 而不是空白或透明

### Requirement: 组件说出角色名,而不是拼出视觉值

组件 SHALL 通过**角色 utility** 着色,MUST NOT 在模板里拼 `bg-surface rounded-card shadow-elevated` 这类视觉值组合。

角色是**量出来的,不是拟的**:走查全部 `class` 属性、按 token utility 的共现分组,落在这七个上 —— `panel` / `panel-flat` / `well` / `cell` / `control-primary` / `bar` / `ground`。

提案原先拟了六个,而**两个方向都错了**:`rail` 与 `control` 在代码里**一次都没出现**,而占比最大的那一组(有边框、无填充,64 处)根本没被想到。「六个名字里有两个说不出区别就是同一个」这条规则在提案自己身上响了 —— 所以清单 SHALL 来自共现统计,MUST NOT 来自设计时的想象。

`bar`(顶栏)与 `ground`(页面的底)是走查**剩下那 29 处「不属于任何角色」**时才发现要加的:header 与 shell 根恰好都不属于,而它们是全站最显眼的两块。**一个漏掉的角色不会让本变更变红,它会让下一个变更做不到「不改组件」** —— 所以「剩下的每一处都有理由」这条走查 MUST 逐条读过,而不是只看总数。

每一个角色 SHALL 在定义处的注释里说明**什么时候用它、以及它和最接近的那个的区别**;两个说不出区别的角色就是同一个。

**这一步是必需的而非审美。** 渐变没法从 `--color-*` 走(CSS 里颜色不能是渐变),所以「让主题给面板加纹理」这件事必须有一个组件不需要知道的落点。角色 utility 就是那个落点:一个主题改 `--surface-image`,所有 `panel` 一起变,而没有任何组件被修改 —— 这正是上面那条扩展点约束要求的。

角色 utility 的定义 MUST 只引用 `var(--…)`,MUST NOT 出现任何色值字面量;这一条 SHALL 由 `check-styles.mjs` 钉住(与它已经在钉的「stylesheet 不得硬编码花色路径」同一处)。

换完之后残留的 `bg-surface` / `rounded-card` / `shadow-elevated` **MUST 逐个有理由**(例如确实只要背景色、不要边和影)。**要求 MUST NOT 是「清零」** —— 清零会逼出一个只为了消灭 grep 结果而存在的假角色,而那比留一个说得通的裸 utility 差。

#### Scenario: 主题换纹理而组件不动
- **WHEN** 只改一套主题的 `--surface-image`
- **THEN** 所有用 `panel` 的面一起变,而 `git diff --name-only` 里没有任何组件文件

#### Scenario: 角色定义里没有色值字面量
- **WHEN** `check-styles.mjs` 扫过角色 utility 的定义
- **THEN** 每个声明的值都是 `var(--…)` 或几何量;出现 `#rrggbb` / `rgb(` 即失败

### Requirement: 主题 token 的对齐校验从注册表推导

`check-styles.mjs` SHALL 断言**每个已注册主题在明暗两份里都声明了每一个 token**,而清单 SHALL 从生产源推导 —— `tokens.css` 的 `[data-theme=…]` 选择器与 `theme.tokens.ts` 的类型,MUST NOT 手写成一份主题名清单。

它 SHALL 跑在 `npm run lint` 下而不是 vitest,与已有的 board skin 对齐校验同一处;那条今天打印「3 skins x 26 variables」,新增这条打印同形状的一行。

**这一条是「每套主题都完整」从散文变成机制的地方。** 上面那条要求写着必需契约,而没有这条校验,它就只是一句话:`ThemeService.validateTokens` 只在**运行时** warn,而一个 warn 在 CI 里不会让任何东西变红。

#### Scenario: 清单不是手写的
- **WHEN** 新增一套主题而**不**改 `check-styles.mjs`
- **THEN** 新主题自动进入校验范围;它缺 token 就失败

#### Scenario: 校验自己会红
- **WHEN** 删掉某套主题某个模式下的一个 token 值
- **THEN** `npm run lint` 失败并点名该主题、该模式、该 token

### Requirement: `shadow-elevated` 这个 class 不许再用,因为它忽略主题

模板 MUST NOT 使用 `shadow-elevated` 这个 Tailwind class;阴影 SHALL 只经由角色 utility 落地,而角色 utility 直接写 `var(--shadow-elevated)`。

**理由是量出来的,不是洁癖。** Tailwind v4 的 `shadow-*` utility 不发 `box-shadow: var(--shadow-elevated)`,它走 `@property` 注册的 `--tw-shadow`,并在**构建期把 `@theme` 的占位值内联进去**。于是运行时的 `[data-theme]` 覆盖永远到不了它。

量到的后果:改这一层之前,6 种(主题 × 明暗)组合声明了 **6 个不同的** `--shadow-elevated`,而**全部画出同一个** `rgba(0,0,0,0.12) 0 4px 12px` —— material 浅色那份占位值。三套主题各自写的阴影**从主题系统上线起就没生效过**,包括 `ink` 那条「阴影重 —— 活字是有厚度的」。

因此 **`extend-theme-tokens` 的「零视觉变化」有且只有一个例外**,并且它是一次修复:6 种组合里的 **5 种** 阴影会变(第 6 种是 material 浅色,它本来就等于占位值)。450 个被比对的属性里,差异恰好是这 5 处 `box-shadow`,其余逐条相同。

`check-styles.mjs` SHALL 钉住这一条,并 SHALL 点名文件 —— 一处退回用这个 class 的地方会**静默地**回到那个死值,而「阴影看起来差不多」是没人会去查的那种差别。

#### Scenario: 模板里出现这个 class 就失败
- **WHEN** 任何模板的 `class` 属性里含 `shadow-elevated`
- **THEN** `npm run lint` 失败并点名该文件,提示改用角色 utility

#### Scenario: 阴影 token 真的活了
- **WHEN** 依次切到 6 种(主题 × 明暗)组合,读取某个 `panel` 的 `box-shadow`
- **THEN** 它等于该组合声明的 `--shadow-elevated`(允许附带一个全透明零尺寸成员);6 种组合 MUST NOT 得到同一个值 —— 那正是修复前的症状

