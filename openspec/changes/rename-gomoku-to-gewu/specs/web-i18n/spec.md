# web-i18n Specification Delta

## MODIFIED Requirements

### Requirement: `LanguageService` 契约

`LanguageService` SHALL 定义为 abstract class 作为 DI token,由 `DefaultLanguageService` 实现并通过 `providers` 注册。

API 契约:

- `static readonly supported: readonly SupportedLocale[]` — 首发为 `['zh-CN', 'en']`;类型 `SupportedLocale = typeof supported[number]`。
- `readonly current: Signal<SupportedLocale>` — 当前 locale 的 signal。
- `use(locale: SupportedLocale): void` — 切换语言;MUST 调用 `translocoService.setActiveLang(locale)`,持久化到 `localStorage['gewu:lang']`,并更新 `current` signal。

行为约束:

- 未知 locale 传入 `use`:编译期被 `SupportedLocale` 类型拒绝;运行期若发生(反序列化等)MUST 忽略并保持当前值,同时在开发期输出 warning。

#### Scenario: use 切换并持久化
- **WHEN** `languageService.use('zh-CN')`
- **THEN** `translocoService.getActiveLang() === 'zh-CN'`、`localStorage.getItem('gewu:lang') === 'zh-CN'`、`current() === 'zh-CN'`

#### Scenario: DI 走抽象类 token
- **WHEN** 测试里用 `{ provide: LanguageService, useValue: stub }` 替换实现
- **THEN** 组件通过 `inject(LanguageService)` 拿到 stub

### Requirement: 初始语言解析顺序

`DefaultLanguageService` 在 app 启动时 SHALL 按如下优先级解析初值:

1. `localStorage['gewu:lang']`(若值在 `supported` 中);
2. `navigator.language` 规范化后匹配 `supported`:
   - 精确匹配优先(`'zh-CN'` → `'zh-CN'`);
   - 仅主标签匹配次之(`'zh'` → `'zh-CN'`,`'zh-HK'` → `'zh-CN'`,`'en-US'` → `'en'`);
3. 回退为 `'en'`。

解析结果 MUST 在 Transloco 初次渲染之前生效,即用户不会看到"先闪一下英文再切回中文"的 FOUC。

#### Scenario: localStorage 优先
- **WHEN** `localStorage['gewu:lang'] === 'en'` 且 `navigator.language === 'zh-CN'`
- **THEN** 启动后 `current() === 'en'`

#### Scenario: navigator 主标签匹配
- **WHEN** `localStorage['gewu:lang']` 不存在、`navigator.language === 'zh-HK'`
- **THEN** 启动后 `current() === 'zh-CN'`

#### Scenario: 回退 en
- **WHEN** `localStorage['gewu:lang']` 不存在、`navigator.language === 'ja-JP'`
- **THEN** 启动后 `current() === 'en'`

#### Scenario: 首屏无语言 FOUC
- **WHEN** 在 `localStorage['gewu:lang'] === 'zh-CN'` 的页面首次加载
- **THEN** 首次可观察到的 home 渲染已经是中文,不存在英文→中文的可见切换
