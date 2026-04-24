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
- `activate(name: string): void` — 切换主题;MUST 设置 `document.documentElement.dataset.theme = name`,并持久化到 `localStorage['gomoku:theme']`,并更新 `themeName` signal。
- `setDark(isDark: boolean): void` — 切换明暗;MUST 在 `document.documentElement.classList` 上 toggle `'dark'` 类,并持久化到 `localStorage['gomoku:dark']`(`'1'` / `'0'`),并更新 `isDark` signal。

`themeName` 与 `isDark` 是**正交**的两个 signal —— 切换其一不影响另一个。

#### Scenario: activate 切换 data-theme 并持久化
- **WHEN** `themeService.activate('system')`
- **THEN** `document.documentElement.dataset.theme === 'system'`、`localStorage.getItem('gomoku:theme') === 'system'`、`themeName() === 'system'`,且 `isDark()` 不变

#### Scenario: setDark 切换 dark class 并持久化
- **WHEN** `themeService.setDark(true)`
- **THEN** `document.documentElement.classList.contains('dark') === true`、`localStorage.getItem('gomoku:dark') === '1'`、`isDark() === true`,且 `themeName()` 不变

#### Scenario: DI 走抽象类 token
- **WHEN** 在测试里 `TestBed.configureTestingModule({ providers: [{ provide: ThemeService, useValue: stub }] })`
- **THEN** 组件通过 `inject(ThemeService)` 拿到 stub,无需修改组件代码

---

### Requirement: 初始主题与明暗解析顺序

`DefaultThemeService` 在 app 启动时 SHALL 按如下优先级解析初值:

**主题名**:
1. `localStorage['gomoku:theme']`(若值在 `register` 过的主题列表中)
2. 回退为 `'material'`

**明暗**:
1. `localStorage['gomoku:dark']`(`'1'` 即暗,`'0'` 即明)
2. `window.matchMedia('(prefers-color-scheme: dark)').matches` → `true` 即暗
3. 回退为 `false`(明色)

用户一旦手动切换过,持久化值 MUST 始终优先于系统偏好。

#### Scenario: localStorage 存在时优先
- **WHEN** `localStorage['gomoku:dark'] === '0'` 但 OS 处于 dark preferred
- **THEN** 启动后 `isDark() === false`

#### Scenario: 无持久化时跟随系统
- **WHEN** `localStorage['gomoku:dark']` 不存在且 OS `prefers-color-scheme: dark`
- **THEN** 启动后 `isDark() === true`

#### Scenario: 无效主题名回退
- **WHEN** `localStorage['gomoku:theme'] === 'nonexistent'`
- **THEN** 启动后 `themeName() === 'material'`(回退),且 `localStorage` 被覆盖为 `'material'`

---

### Requirement: 首发两套主题 —— `material` 与 `system`

本 change SHALL 交付并注册两套主题,每套都包含明暗两份 token 集合:

- `material`:Angular Material 默认风格 —— 较大圆角、明显阴影、Material 调色(primary 落在蓝紫区)。
- `system`:Apple / Fluent-ish 简洁风 —— 更小圆角(≤ 8px)、更轻阴影、更平。

两套主题 MUST 在明暗两种模式下都通过对比度校验(WCAG AA 标准,正文 text 对 bg 对比度 ≥ 4.5:1)。

#### Scenario: 两套主题都注册
- **WHEN** 启动后读取 `themeService` 的内部注册表(通过一个公开的 `availableThemes()` 访问器或等价方式)
- **THEN** 返回包含 `'material'` 与 `'system'` 两项

#### Scenario: 所有 4 种组合都工作
- **WHEN** 依次切换到 (material, light) / (material, dark) / (system, light) / (system, dark)
- **THEN** 每一种组合下 header 与 home 都正确渲染,无不可见文本(text 与 bg 对比度通过 WCAG AA)

---

### Requirement: 扩展点 —— 加主题是单文件改动

新增一个主题 MUST 只需要:

1. 在 `src/app/core/theme/themes/<name>.(light|dark).ts` 新增两份 token 对象;
2. 在 `src/styles/tokens.css` 追加两段 `[data-theme="<name>"]` 与 `[data-theme="<name>"].dark` 规则;
3. 在 `DefaultThemeService` 启动注册序列中新增一行 `this.register('<name>', ...)`。

MUST NOT 需要:修改任何组件源码、修改任何现有主题的 token、修改 Tailwind config(因为 utility 已经绑定到 CSS 变量)。

本 spec 在 `add-web-scaffold` 实施期间通过"走一遍 `system` 主题的加入流程"自验证。

#### Scenario: 扩展仪式
- **WHEN** 假想新增一个 `playful` 主题
- **THEN** 从 diff 角度:纯新增一个 ts 文件 + 一段 css 规则 + 一行注册调用,`grep -r` 不显示任何既有组件或既有主题文件被修改

