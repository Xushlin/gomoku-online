# web-theming Specification Delta

## MODIFIED Requirements

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
