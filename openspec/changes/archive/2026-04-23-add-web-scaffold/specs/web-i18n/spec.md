## ADDED Requirements

### Requirement: Transloco 作为 i18n 引擎,首发 `zh-CN` + `en`

前端 SHALL 使用 **Transloco**(`@jsverse/transloco`)提供运行时可切换的 i18n。

- 翻译文件位于 `src/assets/i18n/<locale>.json`,locale 采用 BCP-47 标签(`zh-CN`、`en`)。
- 翻译 key 采用**点分扁平**路径(例:`header.language.label`、`home.hello`);JSON 内部允许嵌套对象,但最终在代码中以 `'a.b.c'` 引用。
- 默认 / 回退语言 = `'en'`。
- `TranslocoConfig.availableLangs` 与 `LanguageService.supported` MUST 保持同源(二者由同一常量数组导出,不允许分叉维护)。

缺失 key 的处理:

- 开发期(`ng serve`)MUST 输出 console 警告;
- 生产构建 MUST 回落为显示 key 本身(而非空字符串),使缺失可见。

#### Scenario: `zh-CN` 与 `en` 都被加载
- **WHEN** app 启动后调用 `translocoService.getTranslation('zh-CN')` 与 `translocoService.getTranslation('en')`
- **THEN** 两者返回非空对象,至少包含 `home.hello`、`header.language.label`、`header.theme.label`、`header.theme.dark-toggle` 等 shell 必需 key

#### Scenario: availableLangs 与 supported 同源
- **WHEN** 对比 `TranslocoConfig.availableLangs` 与 `LanguageService.supported`
- **THEN** 二者引用同一常量数组(或其一 `= [...other]` 派生),且集合完全相等

---

### Requirement: 模板与 attribute 中的用户可见字符串必须走翻译

`src/app/` 下所有模板(`.html`)与组件模板字符串 MUST NOT 出现:

- 字面的汉字(Unicode block CJK Unified Ideographs);
- 大于 2 字符的连续英文字母串作为显示文本(aria-label、alt、title、按钮文字、段落文字等)。

所有此类字符串 MUST 通过 `{{ 'key.path' | translate }}`、`[attr]="'key.path' | translate"`、结构指令 `*transloco` 或组件类中 `translocoService.translate(...)` 中的一种方式获取。

例外:

- 路由路径字面量(`path: 'home'` 这种 URL 片段)不属于用户可见文本,豁免;
- 商标 / 专有名词(例 `Gomoku`、`SignalR`)允许作为字面量出现;
- HTML tag name、CSS class name、debug 用的 test-id 不属于用户可见文本。

本规则在审查 `add-web-*` 后续 change 时对 reviewer 有约束力。

#### Scenario: scaffold 模板零硬编码
- **WHEN** 在 `frontend-web/src/app/**/*.html` 与内联模板中检索 CJK 字符
- **THEN** 0 个匹配

#### Scenario: 按钮文字走 translate 管道
- **WHEN** 检查 `Header` 模板中 language switcher、theme switcher、dark toggle 的显示文字
- **THEN** 每处都形如 `{{ 'header.xxx' | translate }}` 或等价 Transloco 用法

---

### Requirement: `LanguageService` 契约

`LanguageService` SHALL 定义为 abstract class 作为 DI token,由 `DefaultLanguageService` 实现并通过 `providers` 注册。

API 契约:

- `static readonly supported: readonly SupportedLocale[]` — 首发为 `['zh-CN', 'en']`;类型 `SupportedLocale = typeof supported[number]`。
- `readonly current: Signal<SupportedLocale>` — 当前 locale 的 signal。
- `use(locale: SupportedLocale): void` — 切换语言;MUST 调用 `translocoService.setActiveLang(locale)`,持久化到 `localStorage['gomoku:lang']`,并更新 `current` signal。

行为约束:

- 未知 locale 传入 `use`:编译期被 `SupportedLocale` 类型拒绝;运行期若发生(反序列化等)MUST 忽略并保持当前值,同时在开发期输出 warning。

#### Scenario: use 切换并持久化
- **WHEN** `languageService.use('zh-CN')`
- **THEN** `translocoService.getActiveLang() === 'zh-CN'`、`localStorage.getItem('gomoku:lang') === 'zh-CN'`、`current() === 'zh-CN'`

#### Scenario: DI 走抽象类 token
- **WHEN** 测试里用 `{ provide: LanguageService, useValue: stub }` 替换实现
- **THEN** 组件通过 `inject(LanguageService)` 拿到 stub

---

### Requirement: 初始语言解析顺序

`DefaultLanguageService` 在 app 启动时 SHALL 按如下优先级解析初值:

1. `localStorage['gomoku:lang']`(若值在 `supported` 中);
2. `navigator.language` 规范化后匹配 `supported`:
   - 精确匹配优先(`'zh-CN'` → `'zh-CN'`);
   - 仅主标签匹配次之(`'zh'` → `'zh-CN'`,`'zh-HK'` → `'zh-CN'`,`'en-US'` → `'en'`);
3. 回退为 `'en'`。

解析结果 MUST 在 Transloco 初次渲染之前生效,即用户不会看到"先闪一下英文再切回中文"的 FOUC。

#### Scenario: localStorage 优先
- **WHEN** `localStorage['gomoku:lang'] === 'en'` 且 `navigator.language === 'zh-CN'`
- **THEN** 启动后 `current() === 'en'`

#### Scenario: navigator 主标签匹配
- **WHEN** `localStorage['gomoku:lang']` 不存在、`navigator.language === 'zh-HK'`
- **THEN** 启动后 `current() === 'zh-CN'`

#### Scenario: 回退 en
- **WHEN** `localStorage['gomoku:lang']` 不存在、`navigator.language === 'ja-JP'`
- **THEN** 启动后 `current() === 'en'`

#### Scenario: 首屏无语言 FOUC
- **WHEN** 在 `localStorage['gomoku:lang'] === 'zh-CN'` 的页面首次加载
- **THEN** 首次可观察到的 home 渲染已经是中文,不存在英文→中文的可见切换

---

### Requirement: 扩展点 —— 加 locale 是单文件改动

新增一个 locale MUST 只需要:

1. 在 `src/assets/i18n/<locale>.json` 新增翻译文件;
2. 在 `LanguageService.supported` 常量数组末尾追加一条 locale 标签。

MUST NOT 需要:修改任何组件源码、修改 Transloco 主配置(除了 `availableLangs` 与 `supported` 的同源绑定)、修改任何翻译文件以外的现有文件。

#### Scenario: 扩展仪式
- **WHEN** 假想新增 `ru` locale
- **THEN** 从 diff 角度:新增一个 `ru.json` + `supported` 数组末尾一行,`grep -r` 不显示任何组件或既有 locale 文件被修改

---

### Requirement: 日期 / 数字走 locale-aware API

涉及日期与数字显示的代码 MUST 使用 Angular 的 `formatDate` / `formatNumber` / `DatePipe` / `DecimalPipe` 并传入当前 `current()` 作为 locale 参数;MUST NOT 直接 `new Date().toLocaleString()` 而不提供 locale。

本 scaffold 不渲染日期 / 数字,但本 spec 为后续 change(如 `add-web-lobby` 的排行榜分数、`add-web-game-board` 的倒计时)建立契约。

#### Scenario: 有日期/数字时走 locale 参数
- **WHEN** 任意后续 change 新增日期或数字显示
- **THEN** MUST 通过 Angular 管道或带 locale 参数的 `formatXxx` 调用,且 locale 参数来自 `LanguageService.current()`
