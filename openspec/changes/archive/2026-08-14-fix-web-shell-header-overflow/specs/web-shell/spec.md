## MODIFIED Requirements

### Requirement: 响应式基线 —— mobile-first 375px

Shell 及 home 页 SHALL 在浏览器视口宽 375px 时保持可用(所有交互可达、无水平滚动、文本不被截断),并通过 Tailwind 默认断点(`sm`, `md`, `lg`, `xl`, `2xl`)向 1440px+ 扩展。MUST NOT 为单一设备像素宽度撰写专用媒体查询。

「无水平滚动」是**全宽度区间**的约束,不只是 375px 这一个点:shell(header + `<main>`)在 375px 到 1440px+ 之间的任意视口宽度下 MUST 满足 `document.documentElement.scrollWidth <= document.documentElement.clientWidth`。header 是一条不换行的 flex 行,因此它的固有宽度 MUST 随断点收缩 —— 具体规则见「Header 外观控件在窄视口折叠进单一 Settings 菜单」。

header MUST NOT 通过 `flex-wrap` 换行来规避溢出:header 是 `sticky top-0`,换行会在 375px 下把它撑到三行(~150px,约手机视口的 18%),而棋盘 / 关卡路由最稀缺的正是纵向空间。

#### Scenario: 375px 下 shell 无横向滚动
- **WHEN** 在 375 × 667 视口加载 `/`
- **THEN** `document.documentElement.scrollWidth <= document.documentElement.clientWidth`,且 header 的 language switcher、theme switcher、dark toggle 三个控件都可达 —— 该宽度下经由 Settings 菜单(一次点击展开)可达即满足本条

#### Scenario: 断点边界无横向滚动
- **WHEN** 依次在 375 / 640 / 768 / 1024 / 1280 / 1440 宽度下加载 shell 的任一路由
- **THEN** 每个宽度都满足 `document.documentElement.scrollWidth <= document.documentElement.clientWidth`;`header.scrollWidth <= header.clientWidth`

#### Scenario: header 不靠换行避免溢出
- **WHEN** 检查 `src/app/shell/header/header.html` 根 `<header>` 元素的类
- **THEN** 不存在 `flex-wrap`;溢出通过折叠控件而非换行解决

#### Scenario: 断点策略
- **WHEN** 检查 Tailwind 配置与全局样式
- **THEN** 所有断点 SHALL 使用 Tailwind 预设值(640/768/1024/1280/1536);不存在针对 `320px` / `414px` / `iPhone 12` 等具体设备尺寸的硬编码 `@media`

---

### Requirement: 可访问性与动效尊重基线

全局样式 SHALL 为所有交互元素提供 `focus-visible` 环(颜色取自主题变量),并在 `@media (prefers-reduced-motion: reduce)` 下禁用或大幅削弱过渡/动画。每个交互元素 MUST 可通过键盘访问(Tab/Shift+Tab/Enter/Space 语义正确)。

窄视口下被折叠进 Settings 菜单的控件同样受本条约束:它们 MUST 经由 CDK menu 的 roving tabindex / 方向键 / `ESC` 语义完整可达,MUST NOT 因为折叠而退出键盘可达范围。被 CSS 隐藏的那一份副本(`display: none`)MUST NOT 出现在 tab 序或无障碍树中。

#### Scenario: focus-visible 可见
- **WHEN** 用键盘 Tab 到 header 的任一控件
- **THEN** 该控件显示可见的 focus 环(不是浏览器默认的被覆盖的 outline)

#### Scenario: 尊重 reduced motion
- **WHEN** 用户系统开启 `prefers-reduced-motion: reduce`
- **THEN** 全局 CSS SHALL 将过渡时长限制到 ≤ 0.01s 或直接 `none`(通过 `@media (prefers-reduced-motion: reduce)` 规则)

#### Scenario: 宽视口键盘可达
- **WHEN** 视口 ≥ 1024px,shell 渲染完成后只用键盘操作
- **THEN** 可依次 focus 到 language switcher、theme switcher、dark toggle 且每个都能用 Enter / Space 激活

#### Scenario: 窄视口键盘可达
- **WHEN** 视口 < 1024px,焦点落在 Settings 触发器上并按 Enter / Space
- **THEN** 菜单打开且焦点进入菜单;方向键可在六个外观控件间移动;language / theme / board / sound-pack 行按 Enter 展开子菜单;sound / dark 行按 Enter 翻转状态;`ESC` 关闭并把焦点还给触发器

#### Scenario: 隐藏副本不进 tab 序
- **WHEN** 视口 375px,连续按 Tab 遍历 header
- **THEN** 每个外观控件最多被 focus 一次 —— `lg:` 内联行的那份副本因 `display: none` 不参与 tab 序

## ADDED Requirements

### Requirement: Header 外观控件在窄视口折叠进单一 Settings 菜单

`src/app/shell/header/header.html` SHALL 把六个**外观**控件 —— 语言、主题、棋盘皮肤、音效皮肤、音效开关、深色开关 —— 在 `lg`(1024px)以下折叠进**一个** CDK menu 触发器,文案走 `header.settings.label`。导航与身份类元素(品牌链接、`/games` 链接、登录 / 登出)MUST 留在 header 行内,不进 Settings 菜单。

三段式布局,断点全部取 Tailwind 预设:

| 视口 | header 行内容 |
| --- | --- |
| `< lg` | 品牌 · Games · **Settings ▾** · 登出(用户名隐藏) |
| `lg` … `< xl` | 品牌 · Games · 六个内联控件(只显示当前值) · 用户名 · 登出 |
| `≥ xl` | 同上,控件额外显示 `标签:` 前缀 |

控件顺序在两种排布下 MUST 一致:语言 → 主题 → 棋盘 → 音效皮肤 → 音效开关 → 深色。

六个控件的定义 MUST 只存在一份:`Header` SHALL 暴露两个列表 —— `pickers`(语言 / 主题 / 棋盘 / 音效皮肤)与 `toggles`(音效 / 深色),两种排布都以 `@for` 遍历同一份列表渲染。新增一个外观控件因此是加一个数组条目,而非改两处模板。

`PickerControl` 每项 SHALL 携带:`prefix`(该控件全部文案的 i18n 命名空间 —— 标签 `<prefix>.label`,当前值与每个选项 `<prefix>.<option>`)、`options`(直接取自所属服务的注册表)、`value`、`hasVolume`、`apply`。四个选项列表 MUST 共用**一份** `<ng-template>`,由触发器经 `cdkMenuTriggerData` 传入自身的 `PickerControl`。

CDK 约束,MUST 遵守:`CdkMenu` 以 `@ContentChildren(CdkMenuItem, { descendants: true })` 收集菜单项,而 content query 既不进入子组件的 **view**,也不进入 `ngTemplateOutlet` 实例化的embedded view(后者的 DI 与查询都解析到模板的**声明**位置)。因此菜单项 MUST 与其 `cdkMenu` 声明在同一个模板中 —— MUST NOT 把菜单行抽成 `<app-header-picker>` 之类的子组件,也 MUST NOT 用 `ngTemplateOutlet` 把它们放进菜单:前者静默破坏 roving tabindex / 方向键 / type-ahead,后者直接抛 `NG0201: No provider found for InjectionToken cdk-menu-stack`。

两种排布下同一个控件的渲染差异:内联为带边框按钮(picker 用 `[cdkMenuTriggerFor]`,toggle 用 `role="switch"`);Settings 菜单内 picker 为 `cdkMenuItem` + `cdkMenuTriggerFor`(CDK 子菜单),toggle 为 `cdkMenuItemCheckbox`(`role="menuitemcheckbox"` + `aria-checked`,而非 menu 语境下非法的 `role="switch"`)。

本折叠 MUST NOT 改变任何服务 API、i18n 键(除新增 `header.settings.label`)、或控件自身的行为契约 —— 音效皮肤菜单的音量滑杆在两种排布下都 MUST NOT 标记 `cdkMenuItem`,拖动仍不关闭菜单。

#### Scenario: 375px 下只暴露导航与身份控件
- **WHEN** 视口 375px,shell 渲染完成
- **THEN** header 行内可见的交互元素为品牌链接、`/games` 链接、Settings 触发器、登出(或未登录时的登录 CTA);六个外观控件都 MUST NOT 直接可见

#### Scenario: Settings 菜单列全六个外观控件
- **WHEN** 视口 375px,点击 Settings 触发器
- **THEN** 菜单按序列出语言、主题、棋盘、音效皮肤、音效开关、深色六行;前四行展开子菜单后的选项集合与 `lg` 以上内联控件的选项集合完全一致

#### Scenario: `lg` 以上恢复内联
- **WHEN** 视口 ≥ 1024px
- **THEN** 六个外观控件内联显示在 header 行中;Settings 触发器 MUST NOT 可见

#### Scenario: 标签在 `xl` 显示
- **WHEN** 视口从 1024px 增大到 1280px
- **THEN** 每个内联控件前出现 `标签:` 前缀;1024–1279px 之间只显示当前值

#### Scenario: 新增皮肤仍不改模板
- **WHEN** 按 `web-board-skins` 的开放/封闭规则注册一个新棋盘皮肤
- **THEN** 它同时出现在 `lg` 以上的内联棋盘菜单和 Settings 子菜单中,`header.html` 无 diff

#### Scenario: 音量滑杆行为不变
- **WHEN** 在任一排布下打开音效皮肤菜单并拖动音量滑杆
- **THEN** 菜单保持打开;释放时 `sound.setVolume` 被调一次;未静音时播放一次 `move-place` 试听

---

### Requirement: i18n —— `header.settings.*` 双语对齐

`public/i18n/en.json` 与 `public/i18n/zh-CN.json` SHALL 同步新增以下键:

- `header.settings.label`(en: "Settings" / zh-CN: 「设置」)

flatten 后两份 JSON 的 key 集合 MUST 完全相等(零漂移)。

#### Scenario: parity
- **WHEN** 比对 `en.json` 与 `zh-CN.json` flatten key 集合
- **THEN** 差集为空

#### Scenario: 触发器不硬编码文案
- **WHEN** 检索 `src/app/shell/header/header.html` 中的 Settings 触发器
- **THEN** 其可见文本与 `aria-label` 均形如 `{{ 'header.settings.label' | transloco }}`,不存在 `Settings` / 「设置」字面量
