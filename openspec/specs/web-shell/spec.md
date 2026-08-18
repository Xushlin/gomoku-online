# web-shell Specification

## Purpose
TBD - created by archiving change add-web-scaffold. Update Purpose after archive.
## Requirements
### Requirement: `frontend-web/` is an Angular 21 standalone workspace

系统 SHALL 在仓库根的 `frontend-web/` 目录下维护一个 Angular 21 工作区,使用 standalone components,不创建 NgModule(除非框架/库强制要求),启用 TypeScript `strict` 模式,文件名一律 kebab-case。

工作区 MUST 提供 `package.json` 脚本:`start`、`build`、`test`、`test:ci`、`lint`。根组件挂载点 `<app-root>`(或等价)承载一个 `<router-outlet>`;不存在顶层 NgModule 的启动入口。

#### Scenario: 全新克隆后能起服务
- **WHEN** 在干净 checkout 下执行 `cd frontend-web && npm ci && npm start`
- **THEN** 开发服务器监听 `http://localhost:4200/` 并返回一个渲染出 shell 的 HTML 响应(包含 header 与 `<router-outlet>`)

#### Scenario: 严格 TypeScript
- **WHEN** `frontend-web/tsconfig.json`(或其 extend 链)被读取
- **THEN** `compilerOptions.strict === true`

#### Scenario: 构建通过
- **WHEN** 执行 `npm run build`
- **THEN** 构建输出到 `dist/` 且退出码为 0,无 TypeScript 错误、无 Angular 模板编译错误

#### Scenario: Lint 通过
- **WHEN** 执行 `npm run lint`
- **THEN** 退出码为 0,无 error 级别问题

#### Scenario: 未创建额外 NgModule
- **WHEN** 递归扫描 `frontend-web/src/`
- **THEN** 文件中 MUST NOT 出现用户编写的 `@NgModule({ ... })` 装饰器(Angular Material 等第三方库内部使用的 NgModule 不计)

---

### Requirement: 根路由契约 —— shell 以外的路由必须懒加载

`app.routes.ts` SHALL 只在 eager 加载列表中包含:(a) shell 布局、(b) 一个占位的 `home` 路由、(c) 必要的 fallback / redirect。所有其它路由 —— 包括此次 scaffold 之后由后续 change 新增的任何业务路由 —— MUST 通过 `loadComponent` 或 `loadChildren` 懒加载。

单个懒加载 chunk 目标 < 200 KB(gzip 后);超出时后续 change 必须拆分,不得在本规范中放宽此阈值。

**初始包同理,而且方向是单向的。** `angular.json` 里配置的 initial 预算 MUST NOT 被放宽来消除告警 —— 超预算时要减小 **eager 依赖图**,而不是抬高阈值。一个被抬高的预算把一个活着的信号变成沉默,而它下一次再响,就是包已经大到没人记得原来多大了。

**「路由是懒加载的」并不等于「它用到的东西是懒加载的」。** 一个在 `app.config.ts` 的 provider 列表里被点名的服务,连同它的 import 图,都在 eager 包里 —— 无论使用它的路由多么懒。同理,被 eager 组件(shell、header、`/home` 的卡片)import 的第三方模块也是 eager 的,即使同一个模块在别处只被懒加载页面用到。判断一个依赖到底在哪一侧,唯一的办法是**量**:`ng build --stats-json` 之后看它落在哪个 chunk 里。

#### Scenario: home 路由在根包中
- **WHEN** 访问 `/`
- **THEN** 初始渲染无需再发起额外 JS chunk 请求即可显示 home 占位页

#### Scenario: 新路由走懒加载
- **WHEN** 任意 `add-web-*` 后续 change 向 `app.routes.ts` 新增业务路由
- **THEN** 该路由 MUST 使用 `loadComponent: () => import(...)` 或 `loadChildren: () => import(...)`,不得直接 `component: XxxComponent`

#### Scenario: 超预算不靠放宽预算解决
- **WHEN** 某次 change 让 initial 包超出 `angular.json` 里配置的预算
- **THEN** 该 change MUST 减小 eager 依赖图,MUST NOT 提高 `maximumWarning` / `maximumError`

#### Scenario: eager 与懒加载的判断只认实测
- **WHEN** 需要断言某个依赖不在初始包里
- **THEN** 依据 MUST 是构建产物(`ng build --stats-json` 的 chunk 归属),MUST NOT 是「用它的路由是懒加载的」这一推理

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

### Requirement: HTTP 调用只在 `services/api/` 发生

规范级约束(后续 change 继承):组件(template + class)SHALL NOT 直接 `inject(HttpClient)` 或调用 `fetch`。所有后端 HTTP 通讯 MUST 封装在 `src/app/**/api/**/*.ts` 或等价的 `services/api/` 层,组件通过 service 消费。

本 scaffold change 不添加任何 API service(尚无后端消费点),但此规则自本 spec 起生效,审查 `add-web-*` 后续 change 时以此为准。

#### Scenario: scaffold 不直接使用 HttpClient
- **WHEN** 扫描本 change 交付的所有 `frontend-web/src/` 下的组件 `.ts` 文件
- **THEN** 不存在 `inject(HttpClient)` 或 `constructor(private http: HttpClient)` 引用

---

### Requirement: Container vs. Presentational 分层

组件 SHALL 按职责分成两类:

- **Container**:拿数据(通过 service 注入)、编排、分发事件 —— 持有状态与副作用。
- **Presentational**:纯粹通过 `@Input()` 接收数据、通过 `@Output()` 发事件 —— 不注入 service(除了 `ThemeService` / `LanguageService` 这类横切服务),不读路由参数,不触发 HTTP。

一个组件 MUST NOT 同时承担两种职责;超出 200 LOC 的组件 SHALL 拆分或将状态抽到 service。

`Shell`(container,承载 outlet)与 `Header`(container,注入 `ThemeService` + `LanguageService`)示范该分层。

页面级 container 通过 `providers: [...]` 提供自己的数据 service,使其生命周期与页面绑定 —— `Lobby` 提供 `HomeDataService`,`GameLobby` 提供 `LOBBY_GAME_KEY` 与 `LobbyDataService`。

#### Scenario: Shell 是纯 container
- **WHEN** 打开 `src/app/shell/shell.ts`
- **THEN** 它只承载 `<router-outlet>` 与 `Header`,不发起任何 HTTP

#### Scenario: 页面 service 随页面销毁
- **WHEN** 用户离开一个页面级 container
- **THEN** 它 `providers` 提供的数据 service MUST 一同销毁并停掉自己的定时器

#### Scenario: 组件 LOC 上限
- **WHEN** 统计任意单一组件 `.ts` 文件行数
- **THEN** ≤ 200(不含注释/空行可放宽,但模板大小不作为豁免理由 —— 模板过长同样需拆)

### Requirement: Header 多一个"音效"开关 toggle

`src/app/shell/header/header.{ts,html}` SHALL 在现有 dark-mode toggle 旁边新增第三个状态切换按钮,样式跟 dark toggle 完全一致(`<button role="switch" [attr.aria-checked]>`,移动端隐藏标签部分):

- 标签 `header.sound.label`(en: "Sound" / zh-CN: "音效")
- 状态文本 `header.sound.on` / `.off`(en: "On" / "Off",zh-CN: "开" / "关")
- 点击调 `sound.setMuted(!sound.muted())`
- `aria-checked` 反映当前 **non-muted** 状态(true = 有声音)

注入 `SoundService` 抽象类(已在 `app.config.ts` 注册),不直接 inject 实现。

#### Scenario: 默认状态为开
- **WHEN** 全新用户首次打开 `/home`
- **THEN** 音效 toggle 显示 "On";`aria-checked === "true"`

#### Scenario: 切换后 SoundService 状态翻转
- **WHEN** 用户点 toggle
- **THEN** `sound.muted()` 翻转;按钮文本 / `aria-checked` 同步更新;`localStorage.gewu:sound-muted` 写入新值

#### Scenario: 刷新后状态保留
- **WHEN** 用户切到 muted 后刷新页面
- **THEN** toggle 显示 "Off";`sound.muted() === true`

### Requirement: i18n —— `header.sound.*` 双语对齐

`public/i18n/en.json` 与 `public/i18n/zh-CN.json` SHALL 同步新增以下键:

- `header.sound.label`
- `header.sound.on`
- `header.sound.off`

flatten 后两份 JSON 的 key 集合 MUST 完全相等(零漂移)。

#### Scenario: parity
- **WHEN** 比对 `en.json` 与 `zh-CN.json` flatten key 集合
- **THEN** 差集为空

### Requirement: Header 多一个"音效皮肤"下拉切换器

`src/app/shell/header/header.{ts,html}` SHALL 在现有 sound on/off toggle **之前**(语言 → 主题 → 棋盘 → **音效皮肤** → 音效开关 → 深色 → 用户)新增一个 CDK menu trigger,样式跟 `theme` / `board-skin` 触发器完全一致(`<button>` + `[cdkMenuTriggerFor]`)。

- 标签 `header.sound-pack.label`(en: "Sound pack" / zh-CN: "音效皮肤")
- 当前激活 pack 名通过 `sound.packName()` signal 提供,文本走 `header.sound-pack.{packName}` 翻译键
- 下拉列表通过 `sound.availablePacks()` 渲染,每项点击调 `sound.activate(name)` —— 并立即 `sound.play('move-place')` 作为预览(被 `muted()` 短路时跳过)

菜单项 MUST 完全由 `sound.availablePacks()` 派生。**本 requirement 与其 Scenario MUST NOT 点名具体 pack,也 MUST NOT 写下项数** —— 这条限制不是风格:上一版这里写着「列出 `wood` 和 `chiptune` 两项」,而第三个 pack 落地那天它就错了,只有在有人恰好去数的时候才会被发现。

#### Scenario: 下拉列出全部已注册 pack
- **WHEN** 用户点击 sound-pack trigger
- **THEN** menu 里的 menuitem **逐项等于** `sound.availablePacks()`(数量与顺序都相同);断言 MUST 从该清单派生,MUST NOT 写死数量

#### Scenario: 选择切换 + 持久化
- **WHEN** 用户点某个非当前 pack
- **THEN** `sound.activate(name)` 被调一次;`sound.packName() === name`;`localStorage.gewu:sound-pack === name`

#### Scenario: 选择后预览
- **WHEN** `muted() === false`,用户点某个 pack
- **THEN** 紧随 `activate` 后调 `sound.play('move-place')` 一次

#### Scenario: muted 时不预览
- **WHEN** `muted() === true`,用户点某个 pack
- **THEN** `sound.activate(name)` 被调;`sound.play` MUST NOT 被调

---

### Requirement: i18n —— `header.sound-pack.*` 双语对齐

`public/i18n/en.json` 与 `public/i18n/zh-CN.json` SHALL 同时含有:

- `header.sound-pack.label`
- `header.sound-pack.<name>` —— `BUILT_IN_PACKS`(`src/app/core/sound/packs/index.ts`)里**每一个** key 一条

键清单 MUST 从 `BUILT_IN_PACKS` 派生,MUST NOT 在 spec 或测试里逐个列出。**上一版列的是 `label` / `wood` / `chiptune`,漏掉 `minimal`;而 `minimal` 的键之所以存在,是因为 `i18n-parity.spec.ts` 里另一份手写清单点名要了它。** 两份手写清单守着同一个事实,于是第四个 pack 的键不会有任何东西要求 —— 派生之后,加一个 pack 而忘记翻译会当场变红。

flatten 后两份 JSON 的 key 集合 MUST 完全相等(零漂移)。

#### Scenario: 每个已注册 pack 都有双语翻译
- **WHEN** 遍历 `Object.keys(BUILT_IN_PACKS)`
- **THEN** 两份 locale 都能解析出非空的 `header.sound-pack.<name>`

#### Scenario: parity
- **WHEN** 比对 `en.json` 与 `zh-CN.json` flatten key 集合
- **THEN** 差集为空

### Requirement: 音效菜单包含音量滑杆行

`src/app/shell/header/header.{ts,html}` SHALL 在现有音效皮肤 CDK menu(`[cdkMenuTriggerFor]` 打开的 pack 选项列表)底部追加一个音量滑杆行:

- 行内为一个原生 `<input type="range" min="0" max="100" step="1">`,值绑定 `sound.volume()`,`(change)` 时调 `sound.setVolume(...)`。
- 该行 MUST NOT 标记 `cdkMenuItem` —— 拖动滑杆不得关闭菜单;滑杆本身在 tab 序内,方向键可调(原生行为)。
- 滑杆释放(`change` 事件,非 `input`)且未静音时 SHALL 播放一次 `move-place` 试听,与现有切 pack 试听模式一致。
- 滑杆样式走 token:`accent-color: var(--color-primary)`;MUST NOT 出现色值字面量。
- 行首标签用 `header.sound.volume` 翻译键;`<input>` MUST 带 `[attr.aria-label]`(同键)。

#### Scenario: 拖动滑杆菜单不关闭
- **WHEN** 用户打开音效皮肤菜单,拖动音量滑杆
- **THEN** 菜单保持打开;`sound.setVolume` 在释放时被调用一次

#### Scenario: 滑杆释放播放试听
- **WHEN** 未静音状态下用户把滑杆从 100 拖到 40 并释放
- **THEN** `sound.setVolume(40)` 后播放一次 `'move-place'`;静音状态下不播放

#### Scenario: 键盘可达
- **WHEN** 菜单打开,焦点 tab 到滑杆,按 ←/→
- **THEN** 音量逐步变化;`focus-visible` 样式可见

---

### Requirement: i18n —— `header.sound.volume` 与新变体标签双语对齐

`public/i18n/en.json` 与 `public/i18n/zh-CN.json` SHALL 同步新增:

- `header.sound.volume`(en: "Volume" / zh-CN: "音量")
- `header.sound-pack.minimal`(en: "Minimal" / zh-CN: "极简")
- `header.board-skin.midnight`(en: "Midnight" / zh-CN: "午夜")

两个 locale 文件的 key 集合 MUST 保持一致(parity 测试通过)。

#### Scenario: 双语 key 对齐
- **WHEN** 跑现有 i18n parity 测试
- **THEN** `en.json` 与 `zh-CN.json` key 集合一致,新增 3 个 key 均有非空翻译

#### Scenario: 菜单显示新条目标签
- **WHEN** 打开棋盘皮肤菜单 / 音效皮肤菜单
- **THEN** `midnight` / `minimal` 条目分别显示翻译后的标签,而非裸 key

---

### Requirement: Header 品牌名走 i18n 键 `header.brand`

`src/app/shell/header/header.html` 的品牌链接 MUST NOT 硬编码任何展示文本。该链接 SHALL 保持 `routerLink="/home"`,文本走 `{{ 'header.brand' | transloco }}`。

平台更名为 格物 / Gewu 后,品牌名成为**随语言变化**的展示字符串(`zh-CN` 为「格物」、`en` 为 "Gewu"),因此不能再像更名前那样以字面量 `Gomoku` 留在模板里 —— 那既违反"模板禁止硬编码展示字符串"的项目硬规则,也无法随语言切换。

键名归入既有的 `header.*` 命名空间(与 `header.language.*` / `header.sound.*` / `header.sound-pack.*` 同级),不新开顶层命名空间。

#### Scenario: zh-CN 下显示中文品牌名
- **WHEN** 活动语言为 `zh-CN`,shell 渲染完成
- **THEN** header 品牌链接的文本为「格物」

#### Scenario: en 下显示英文品牌名
- **WHEN** 活动语言为 `en`,shell 渲染完成
- **THEN** header 品牌链接的文本为 "Gewu"

#### Scenario: 品牌链接仍然回到首页
- **WHEN** 点击 header 品牌链接
- **THEN** 路由跳转到 `/home`

#### Scenario: 模板中不存在硬编码品牌字面量
- **WHEN** 检索 `src/app/shell/header/header.html`
- **THEN** 文件中 MUST NOT 出现 `Gomoku` / `Gewu` / 「格物」 作为展示文本字面量

### Requirement: i18n —— `header.brand` 双语对齐

`public/i18n/en.json` 与 `public/i18n/zh-CN.json` SHALL 同步新增以下键:

- `header.brand`(en: "Gewu" / zh-CN: 「格物」)

flatten 后两份 JSON 的 key 集合 MUST 完全相等(零漂移)。

#### Scenario: parity
- **WHEN** 比对 `en.json` 与 `zh-CN.json` flatten key 集合
- **THEN** 差集为空

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

