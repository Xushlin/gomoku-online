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

#### Scenario: home 路由在根包中
- **WHEN** 访问 `/`
- **THEN** 初始渲染无需再发起额外 JS chunk 请求即可显示 home 占位页

#### Scenario: 新路由走懒加载
- **WHEN** 任意 `add-web-*` 后续 change 向 `app.routes.ts` 新增业务路由
- **THEN** 该路由 MUST 使用 `loadComponent: () => import(...)` 或 `loadChildren: () => import(...)`,不得直接 `component: XxxComponent`

---

### Requirement: 响应式基线 —— mobile-first 375px

Shell 及 home 页 SHALL 在浏览器视口宽 375px 时保持可用(所有交互可达、无水平滚动、文本不被截断),并通过 Tailwind 默认断点(`sm`, `md`, `lg`, `xl`, `2xl`)向 1440px+ 扩展。MUST NOT 为单一设备像素宽度撰写专用媒体查询。

#### Scenario: 375px 下 shell 无横向滚动
- **WHEN** 在 375 × 667 视口加载 `/`
- **THEN** `document.documentElement.scrollWidth <= document.documentElement.clientWidth`,且 header 的 language switcher、theme switcher、dark toggle 三个控件都可点击可达

#### Scenario: 断点策略
- **WHEN** 检查 Tailwind 配置与全局样式
- **THEN** 所有断点 SHALL 使用 Tailwind 预设值(640/768/1024/1280/1536);不存在针对 `320px` / `414px` / `iPhone 12` 等具体设备尺寸的硬编码 `@media`

---

### Requirement: 可访问性与动效尊重基线

全局样式 SHALL 为所有交互元素提供 `focus-visible` 环(颜色取自主题变量),并在 `@media (prefers-reduced-motion: reduce)` 下禁用或大幅削弱过渡/动画。每个交互元素 MUST 可通过键盘访问(Tab/Shift+Tab/Enter/Space 语义正确)。

#### Scenario: focus-visible 可见
- **WHEN** 用键盘 Tab 到 header 的任一控件
- **THEN** 该控件显示可见的 focus 环(不是浏览器默认的被覆盖的 outline)

#### Scenario: 尊重 reduced motion
- **WHEN** 用户系统开启 `prefers-reduced-motion: reduce`
- **THEN** 全局 CSS SHALL 将过渡时长限制到 ≤ 0.01s 或直接 `none`(通过 `@media (prefers-reduced-motion: reduce)` 规则)

#### Scenario: 键盘可达
- **WHEN** 在 shell 渲染完成后只用键盘操作
- **THEN** 可依次 focus 到 language switcher、theme switcher、dark toggle 且每个都能用 Enter / Space 激活

---

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

本 scaffold 只有 `Home`(presentational 占位)、`Shell`(container,承载 outlet)、`Header`(container,注入 `ThemeService` + `LanguageService`)三个组件,本身示范该分层。

#### Scenario: Home 是 presentational
- **WHEN** 打开 `src/app/pages/home/home.ts`
- **THEN** 不存在 `inject(HttpClient)`;若有 `inject`,仅限 `ThemeService` / `LanguageService` 这两类跨横切服务

#### Scenario: 组件 LOC 上限
- **WHEN** 统计本 change 引入的任意单一组件 `.ts` 文件行数
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
- **THEN** `sound.muted()` 翻转;按钮文本 / `aria-checked` 同步更新;`localStorage.gomoku:sound-muted` 写入新值

#### Scenario: 刷新后状态保留
- **WHEN** 用户切到 muted 后刷新页面
- **THEN** toggle 显示 "Off";`sound.muted() === true`

---

### Requirement: i18n —— `header.sound.*` 双语对齐

`public/i18n/en.json` 与 `public/i18n/zh-CN.json` SHALL 同步新增以下键:

- `header.sound.label`
- `header.sound.on`
- `header.sound.off`

flatten 后两份 JSON 的 key 集合 MUST 完全相等(零漂移)。

#### Scenario: parity
- **WHEN** 比对 `en.json` 与 `zh-CN.json` flatten key 集合
- **THEN** 差集为空

