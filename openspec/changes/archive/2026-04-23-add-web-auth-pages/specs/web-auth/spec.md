## ADDED Requirements

### Requirement: `AuthService` contract — Signal-backed state, abstract DI token

前端 SHALL 在 `src/app/core/auth/auth.service.ts` 定义 abstract class `AuthService` 作为 DI token,由 `DefaultAuthService` 实现并通过 `{ provide: AuthService, useClass: DefaultAuthService }` 注册。API 契约:

- `readonly accessToken: Signal<string | null>`
- `readonly user: Signal<UserDto | null>`
- `readonly accessTokenExpiresAt: Signal<Date | null>`
- `readonly isAuthenticated: Signal<boolean>` — `computed(() => accessToken() !== null && user() !== null)`
- `login(email: string, password: string): Observable<void>`
- `register(email: string, username: string, password: string): Observable<void>`
- `logout(): Observable<void>`
- `changePassword(currentPassword: string, newPassword: string): Observable<void>`
- `refresh(): Observable<void>`
- `bootstrap(): Promise<void>` — 启动期恢复会话

所有 state-changing 方法在成功 HTTP 响应后 MUST 在**同一 microtask** 内同时更新 `accessToken`、`user`、`accessTokenExpiresAt` 三个 signal,不得出现"accessToken 已设但 user 仍为 null"的中间态。组件 MUST 通过 `inject(AuthService)` 消费,MUST NOT 直接 `inject(DefaultAuthService)`。

#### Scenario: 初始状态
- **WHEN** app 启动且 `localStorage['gomoku:refresh']` 不存在
- **THEN** `accessToken() === null`、`user() === null`、`isAuthenticated() === false`

#### Scenario: 抽象类 DI 可替换
- **WHEN** 测试里 `TestBed.configureTestingModule({ providers: [{ provide: AuthService, useValue: stub }] })`
- **THEN** 组件通过 `inject(AuthService)` 拿到 stub,无需修改组件代码

#### Scenario: 登录成功原子更新
- **WHEN** `login` 收到 200 + `AuthResponse`
- **THEN** `accessToken`、`user`、`accessTokenExpiresAt` 三个 signal MUST 在同一 microtask 内同时更新;监听 `isAuthenticated` 的 `effect` MUST 观察到恰好 1 次 false → true 跃迁

#### Scenario: 返回值不泄漏 HTTP 细节
- **WHEN** `login` / `register` / `refresh` / `changePassword` / `logout` 完成
- **THEN** 返回值 MUST 是 `Observable<void>`;service 本身 IS state,调用方读取 `user()` / `accessToken()` 而不是 Observable 的 payload

---

### Requirement: Token 存储策略 —— access 仅内存,refresh 在 `localStorage['gomoku:refresh']`

`DefaultAuthService` SHALL 按如下存储 access token 和 refresh token:

- **Access token**: 仅存在 `accessToken` signal 中,MUST NOT 写入 `localStorage` / `sessionStorage` / `IndexedDB` / Cookie / 任何持久化存储。
- **Refresh token**: 在 `login` / `register` / `refresh` 成功后写入 `localStorage['gomoku:refresh']`;在 `logout` 成功或 `refresh` 最终失败后 MUST `localStorage.removeItem('gomoku:refresh')`。

service / interceptor / page 任何地方 MUST NOT `console.log` / `console.debug` 打印 refresh token 或 access token 的原文。

#### Scenario: access token 不落盘
- **WHEN** 登录成功后搜索所有 Web Storage API
- **THEN** `localStorage` 与 `sessionStorage` 的任何 value 都不等于当前的 access token 字符串

#### Scenario: refresh token 持久化
- **WHEN** `login` 成功
- **THEN** `localStorage.getItem('gomoku:refresh')` MUST 返回后端返回的 refresh token 字符串

#### Scenario: logout 清除 refresh token
- **WHEN** `logout` 完成(无论网络调用成功与否,见下文 logout 容错)
- **THEN** `localStorage.getItem('gomoku:refresh') === null`

#### Scenario: 日志不泄漏 token
- **WHEN** 审阅 `src/app/core/auth/` 下所有 `console.*` 调用与 `Serilog`-style 日志
- **THEN** 均 MUST NOT 直接或间接打印 token 原文

---

### Requirement: 启动期 session 恢复 —— `bootstrap()` 在首次渲染前完成

`DefaultAuthService.bootstrap()` SHALL:

1. 若 `localStorage['gomoku:refresh']` 为空 → 立即 resolve,state 保持 unauthenticated;
2. 若存在 → 调 `POST /api/auth/refresh { refreshToken }`:
   - 成功:填入 `accessToken` / `user` / `accessTokenExpiresAt`;写入新的 refresh token;resolve;
   - 失败(任何 4xx / 5xx / 网络错误):`localStorage.removeItem('gomoku:refresh')`,state 保持 unauthenticated,resolve(**MUST NOT** reject —— 启动不应被 refresh 失败阻塞);
3. 5 秒内未完成 → 放弃等待,视同失败(避免网络慢时页面卡死)。

`app.config.ts` 的 `provideAppInitializer` SHALL 把 `bootstrap()` 与 i18n preload 并行 `await`,保证首屏渲染前 auth state 已就绪。

#### Scenario: 有效 refresh token → 启动直接登录
- **WHEN** `localStorage['gomoku:refresh']` 为一枚后端尚未吊销的 token;app 启动
- **THEN** 首屏渲染时 `isAuthenticated() === true`,header 显示用户名;不存在"先 anonymous 再切 authenticated"的可见跳变

#### Scenario: 失效 refresh token → 启动仍完成
- **WHEN** `localStorage['gomoku:refresh']` 为一枚过期或已吊销的 token
- **THEN** `POST /api/auth/refresh` 收到 401;`bootstrap()` 清理 localStorage 后 resolve;app 正常启动,`isAuthenticated() === false`

#### Scenario: 网络故障 5 秒超时
- **WHEN** `POST /api/auth/refresh` 长时间挂起不返回
- **THEN** 最多 5 秒后 `bootstrap()` MUST resolve,state 保持启动前的值(unauthenticated),localStorage 的 refresh token MUST 保留(不确定是否有效,下次网络好时再尝试)

---

### Requirement: `authInterceptor` 附加 Bearer + 401 静默刷新重试

前端 SHALL 在 `src/app/core/auth/auth.interceptor.ts` 定义 functional `HttpInterceptorFn` `authInterceptor`,通过 `withInterceptors([authInterceptor])` 注册到 `provideHttpClient`。行为契约:

**附加 Bearer**:

- 请求 URL 以 `/api/auth/login`、`/api/auth/register`、`/api/auth/refresh` 之一开头的,MUST NOT 附加 `Authorization` 头(它们要么不需要 token,要么 token 本身就是 body/nothing)。
- `/api/auth/logout` 与 `/api/auth/change-password`:需要 `Authorization` 头,所以走正常附加路径。
- 其它请求:若 `accessToken() !== null`,MUST 在 outgoing 请求上设 `Authorization: Bearer <accessToken()>` 头;若 `null`,不附加头直接放行。

**401 处理**:

- 如果响应是 HTTP 401 且请求 URL 属于"附加 Bearer"集合(即**非** `/api/auth/login|register|refresh`):
  1. 若**不存在**正在进行的 refresh:启动一个 `refresh()`,并把其返回的 Observable 存入模块级 `refreshInFlight$` Subject;
  2. 若**已存在**正在进行的 refresh:等待它;
  3. refresh 成功:用**新**的 access token 重发原请求一次;
  4. 重试结果原样返回给调用者(即使重试再 401,也 MUST NOT 再触发第二次 refresh —— 每个原始请求最多重试一次)。
- refresh 失败(refresh 本身 4xx / 网络错误):
  - 清理 AuthService 状态(等价于 logout 本地路径);
  - `Router.navigateByUrl('/login?returnUrl=<current>')`;
  - 原 401 错误 MUST 透传给调用者。
- 如果响应是 HTTP 401 且请求本身是 `/api/auth/login|register|refresh`:原样透传,不尝试 refresh。

**并发去重不变式**:N 个并发的被拦截请求同时 401 触发的 refresh,对后端的 `POST /api/auth/refresh` 调用次数 MUST = 1。

#### Scenario: 普通请求附加 Bearer
- **WHEN** `isAuthenticated() === true` 且 `accessToken() === 'abc123'`;发起 `GET /api/rooms`
- **THEN** 实际网络请求头包含 `Authorization: Bearer abc123`

#### Scenario: `/api/auth/login` 不附加 Bearer
- **WHEN** 发起 `POST /api/auth/login`(即使此时恰好有陈旧的 access token 在 state 里)
- **THEN** 实际网络请求 MUST NOT 包含 `Authorization` 头

#### Scenario: 401 → refresh → retry 成功
- **WHEN** `GET /api/rooms` 回 401;refresh 调用回 200 + 新 AuthResponse
- **THEN** 原 `GET /api/rooms` 被重试一次,且重试时带**新的** access token;调用方看到重试的响应(200),从不知道发生了 401 + refresh

#### Scenario: 并发 401 触发单次 refresh
- **WHEN** 3 个不相关的请求同时发出,全部被后端回 401
- **THEN** 对 `POST /api/auth/refresh` 的调用次数 MUST = 1;3 个原请求各被重试 1 次(共 3 次)

#### Scenario: refresh 失败 → 清状态 + 跳转 login
- **WHEN** 401 触发的 refresh 自身回 401
- **THEN** `accessToken()` 被清为 `null`、`user()` 清为 `null`、`localStorage['gomoku:refresh']` 被删除、`Router.navigateByUrl` 被调用,参数形如 `/login?returnUrl=<origURL>`

#### Scenario: 重试仍 401,不再嵌套 refresh
- **WHEN** 重试后的请求再次回 401
- **THEN** MUST NOT 再启动第二次 refresh;错误透传给调用者

#### Scenario: `/api/auth/refresh` 回 401 不递归
- **WHEN** `refresh()` 自身的 HTTP 调用回 401
- **THEN** interceptor MUST NOT 尝试对这个 refresh 请求再次 refresh;按"refresh 失败"路径处理

---

### Requirement: 路由守卫 —— `authGuard` + `guestGuard`,functional `CanMatchFn`

前端 SHALL 在 `src/app/core/auth/auth.guards.ts` 定义两个 `CanMatchFn`:

- `authGuard`: 若 `isAuthenticated() === true` 返回 `true`;否则返回 `Router.createUrlTree(['/login'], { queryParams: { returnUrl: '<matched path>' } })`。
- `guestGuard`: 若 `isAuthenticated() === false` 返回 `true`;否则返回 `Router.createUrlTree(['/home'])`。

`app.routes.ts` MUST 在 `/login`、`/register` 路由上挂 `guestGuard`,在 `/account/password` 路由上挂 `authGuard`。MUST 使用 `CanMatchFn`(不是 `CanActivateFn`)—— 这样匿名用户永远不会下载 `/account/password` 的 lazy chunk。

#### Scenario: 匿名访问受保护路由
- **WHEN** `isAuthenticated() === false`;导航到 `/account/password`
- **THEN** 路由器最终落在 `/login?returnUrl=/account/password`;`/account/password` 的 chunk MUST NOT 被下载

#### Scenario: 已登录访问 guest 路由
- **WHEN** `isAuthenticated() === true`;导航到 `/login`
- **THEN** 路由器最终落在 `/home`;`/login` 的 chunk MUST NOT 被下载

#### Scenario: 认证状态变化时守卫重算
- **WHEN** 用户在 `/home`,调用 `logout()`,自动或手动触发路由评估
- **THEN** 若后续导航到 `/account/password`,guard 返回 UrlTree 跳 `/login`

---

### Requirement: 三个懒加载 auth 路由

`app.routes.ts` SHALL 包含三个新路由,全部使用 `loadComponent`:

- `{ path: 'login', loadComponent: () => import('./pages/auth/login/login').then(m => m.Login), canMatch: [guestGuard] }`
- `{ path: 'register', loadComponent: () => import('./pages/auth/register/register').then(m => m.Register), canMatch: [guestGuard] }`
- `{ path: 'account/password', loadComponent: () => import('./pages/auth/change-password/change-password').then(m => m.ChangePassword), canMatch: [authGuard] }`

三个路由都 MUST NOT 使用 `component: ...`(违反懒加载规则)。

#### Scenario: 初始 bundle 不包含页面代码
- **WHEN** 生产构建完成
- **THEN** 初始(eager)chunk MUST NOT 包含 `Login` / `Register` / `ChangePassword` 三个类的代码 —— 每个应各自产出一个懒 chunk

#### Scenario: 路由路径匹配
- **WHEN** 导航到 `/login` / `/register` / `/account/password`(且守卫放行)
- **THEN** 对应组件被加载并挂载在 `<router-outlet />`

---

### Requirement: Login / Register / Change-Password 页面契约

三个页面 SHALL 使用 Angular Reactive Forms(`@angular/forms` 的 `FormBuilder`),每个字段渲染为 label + input + field-level error slot,提交按钮在 in-flight 期间禁用。UI 文本 MUST 全部走 Transloco;**零** hardcoded 中英文显示字符串。

**Login**:
- Fields: `email` (required + email), `password` (required)
- Submit: `auth.login(email, password)`;成功后 `router.navigateByUrl(returnUrl || '/home')`
- 顶部错误区展示 401 → `auth.login.errors.invalid-credentials` 等已知类型的翻译文案
- 有一个"没有账号?注册"的链接指向 `/register`

**Register**:
- Fields: `email` (required + email), `username` (required + minlength 符合 `user-management`), `password` (required + `passwordPolicyValidator()`)
- Submit: `auth.register(email, username, password)`;成功后直接进入已登录态,`router.navigateByUrl('/home')`
- 409 → email/username-taken 映射到对应字段
- 400 的 `ProblemDetails.errors` 通过 `mapProblemDetailsToForm` 映射到字段错误

**Change-Password**:
- Fields: `currentPassword` (required), `newPassword` (required + `passwordPolicyValidator()`), `confirmPassword` (required)
- Form-level validator `matchFieldsValidator('newPassword', 'confirmPassword')` 在 confirm 上挂 `{ mismatch: true }` 当两者不相等
- Submit: `auth.changePassword(currentPassword, newPassword)`;成功后无论结果 MUST 清理本地 state 并 `router.navigateByUrl('/login?flash=password-changed')`
- 401 → `auth.change-password.errors.wrong-current` 映射到 `currentPassword` 字段
- `/login` 页面见到 `flash=password-changed` 查询参数时,MUST 显示一条"密码已修改,请用新密码登录"的提示

所有页面 MUST 为 standalone + `ChangeDetectionStrategy.OnPush`,类文件 body ≤ 150 LOC(含导入,不含模板)。

#### Scenario: Login 成功
- **WHEN** 输入合法凭据,点击 submit
- **THEN** `AuthService.login` 被调一次;成功后 `isAuthenticated() === true`;路由跳到 `returnUrl` 或 `/home`

#### Scenario: Login 凭据错误
- **WHEN** 后端回 401
- **THEN** 顶部显示 `'auth.login.errors.invalid-credentials' | transloco` 翻译后的文案;表单不清空;提交按钮重新可用

#### Scenario: Register 邮箱已存在
- **WHEN** 后端回 409 `EmailAlreadyExistsException`
- **THEN** `email` 字段下显示 `'auth.register.errors.email-taken'` 翻译文案;其它字段值不丢

#### Scenario: Register 密码策略本地校验
- **WHEN** 用户输入密码 `"abcdefgh"`(无数字)并失焦
- **THEN** `password` 字段显示 `'auth.errors.password-missing-digit'` 翻译文案,**不发起**网络请求

#### Scenario: Change-Password 成功 → 强制重登
- **WHEN** 调 `changePassword` 收到 204
- **THEN** AuthService 本地 state 清空;`localStorage['gomoku:refresh']` 删除;路由跳到 `/login?flash=password-changed`;`/login` 页面顶部显示成功提示

#### Scenario: 提交期间按钮禁用
- **WHEN** 表单 in-flight(Observable 未完成)
- **THEN** submit button 的 `disabled` 属性 MUST 为 true;再次点击 MUST NOT 触发第二次网络请求

---

### Requirement: `passwordPolicyValidator` 与后端 `RegisterCommandValidator` 同规则

`src/app/pages/auth/shared/password-policy.validator.ts` SHALL 导出 `passwordPolicyValidator(): ValidatorFn`,对入参:

- 长度 < 8 → 返回 `{ minlength: { requiredLength: 8, actualLength: n } }`
- 长度 ≥ 8 但不含 `/[A-Za-z]/` → 返回 `{ missingLetter: true }`
- 长度 ≥ 8 但不含 `/\d/` → 返回 `{ missingDigit: true }`
- 合法 → 返回 `null`

error key 可以共存(同一次调用多于一个问题时可同时返回)。Validator 对 `null` / `''` 输入返回 `null`(`Validators.required` 负责必填性)。

`Register` 页面与 `ChangePassword` 页面的密码字段都 MUST 绑定此 validator,不得在组件内部重新实现规则。

#### Scenario: 合法密码
- **WHEN** validator 接收 `"Password1"`、`"abc12345"`、`"Zzz99999"`
- **THEN** 返回 `null`

#### Scenario: 长度不足
- **WHEN** 输入 `"abc1"`
- **THEN** error object 包含 `minlength`

#### Scenario: 缺字母
- **WHEN** 输入 `"12345678"`
- **THEN** error object 包含 `missingLetter`(且不包含 `minlength`)

#### Scenario: 缺数字
- **WHEN** 输入 `"abcdefgh"`
- **THEN** error object 包含 `missingDigit`

#### Scenario: 空输入放行
- **WHEN** 输入 `""` 或 `null`
- **THEN** 返回 `null`(让 `Validators.required` 处理必填)

---

### Requirement: `mapProblemDetailsToForm` —— 服务端字段错误落到正确 FormControl

`src/app/pages/auth/shared/problem-details.mapper.ts` SHALL 导出:

```ts
mapProblemDetailsToForm(form: FormGroup, problem: ProblemDetails): boolean
```

行为:

- 遍历 `problem.errors` 的每个 `(fieldName, messages[])`;
- 将 `fieldName` 的首字母小写化(后端可能大写如 `"Email"`);
- 若 `form.get(<normalized>)` 存在,调 `form.get(...)!.setErrors({ ...existingErrors, server: messages[0] })`;并 `markAsTouched()`;
- 若至少匹配到 1 个字段,返回 `true`;否则返回 `false`(调用方可退回到顶部 banner 显示 `problem.detail`)。

模板消费端 MUST 按优先级渲染错误,`server` 优先级**最低**(被内建 validator 的 key 压过,避免服务器刚回的 stale 消息覆盖用户正在编辑的新输入带来的 required 错误)。

字段文本 `{{ ctrl.errors!['server'] }}` MUST 通过 Angular 模板插值(自动 HTML 转义)渲染,MUST NOT 通过 `innerHTML` / `bypassSecurityTrust*` 之类 API。

#### Scenario: Email 字段错误被映射
- **WHEN** 后端回 `{ errors: { "Email": ["That email is already taken"] } }`
- **THEN** `form.get('email')?.errors?.['server'] === "That email is already taken"`,`touched === true`;函数返回 `true`

#### Scenario: 未匹配字段 → 返回 false
- **WHEN** 后端回 `{ errors: { "UnknownField": ["..."] } }`
- **THEN** 函数返回 `false`;调用方可把 `problem.detail` 显示在 form-level banner

#### Scenario: 服务端消息不走 innerHTML
- **WHEN** 后端消息包含 `<script>alert(1)</script>` 这样的字符串
- **THEN** 渲染结果 MUST 是字面文本,**不**执行脚本(依赖 Angular 默认插值转义)

---

### Requirement: i18n —— `auth.*` 与 `header.auth.*` 翻译 key 同步扩充

`public/i18n/en.json` 与 `public/i18n/zh-CN.json` SHALL 同步加入所有 auth 相关 key,包括但不限于:

- `auth.login.{title, submit, submit-loading, email-label, email-placeholder, password-label, password-placeholder, no-account-cta, flash.password-changed}`
- `auth.login.errors.{invalid-credentials, account-inactive, network}`
- `auth.register.{title, submit, submit-loading, email-label, username-label, password-label, already-have-account-cta}`
- `auth.register.errors.{email-taken, username-taken}`
- `auth.change-password.{title, submit, submit-loading, current-label, new-label, confirm-label, success-toast}`
- `auth.change-password.errors.{wrong-current, mismatch}`
- `auth.errors.{generic, network, required, email-invalid, password-min-length, password-missing-letter, password-missing-digit}`
- `header.auth.{login-cta, logout, account}`

两份 JSON 的 key 集合 MUST 完全相等(一边加了一边没加等于漏翻)。键风格沿用 scaffold 的扁平点分路径 + 值内 kebab-case。

#### Scenario: 两个 locale key 集合一致
- **WHEN** 用一个小脚本对比 `en.json` 与 `zh-CN.json` 递归打平后的 key 集合
- **THEN** 差集 MUST 为空

#### Scenario: 页面无硬编码字符串
- **WHEN** 在 `src/app/pages/auth/**/*.html` 与内联模板中检索 CJK 字符或 ≥ 3 字母的英文显示字符串
- **THEN** 0 匹配(除了类名 / ID / test-id 等非展示文本)

---

### Requirement: Header auth 插槽 —— 匿名/已登录两种状态

`Header` 组件 SHALL 在现有三个控件(language/theme/dark)之外再提供一个 auth 区域:

- **匿名**(`isAuthenticated() === false`):显示一个链接文本为 `{{ 'header.auth.login-cta' | transloco }}`,`routerLink` 指向 `/login`。
- **已登录**(`isAuthenticated() === true`):显示 `{ user().username, Logout button }` —— 用户名用 Angular 插值(自动转义),logout 按钮点击调 `auth.logout().subscribe()` 随后 `router.navigateByUrl('/home')`。

本需求不修改既有的三控件要求(`web-shell` header spec 里的行为不变)。

#### Scenario: 匿名看到 login 链接
- **WHEN** 未登录时渲染 header
- **THEN** DOM 中存在一个 `<a>` 或 `<button>`,其可见文本是 `'header.auth.login-cta'` 翻译后的字面量

#### Scenario: 已登录看到用户名 + logout
- **WHEN** `user() = { username: 'Alice', ... }`
- **THEN** header 中渲染出 `Alice` 和一个 logout 控件(文本翻译自 `'header.auth.logout'`)

#### Scenario: Logout 清状态并回首页
- **WHEN** 点击 header 的 logout
- **THEN** `POST /api/auth/logout` 被发出(带当前 refresh token);不论网络结果,service 本地 state MUST 被清,localStorage 的 refresh token MUST 被删,路由跳 `/home`

#### Scenario: 用户名字段防 XSS
- **WHEN** 某 `user().username` 恰好是 `<img onerror=alert(1)>` 这样的串
- **THEN** DOM 中 MUST 渲染为字面文本,不触发脚本

---

### Requirement: Logout 对网络错误容错

`DefaultAuthService.logout()` SHALL:

1. 读取当前 `localStorage['gomoku:refresh']`;
2. 发 `POST /api/auth/logout { refreshToken }`(后端接 204,幂等);
3. **无论** HTTP 调用成功与否,都 MUST 执行:
   - 清空 `accessToken` / `user` / `accessTokenExpiresAt` 三个 signal;
   - `localStorage.removeItem('gomoku:refresh')`。

即:客户端 logout 从不失败。网络错误只影响"是否通知了服务端",不影响"本地状态是否清理"。

#### Scenario: 网络正常
- **WHEN** `logout` 的 HTTP 请求收到 204
- **THEN** 本地 state 被清;Observable complete

#### Scenario: 网络故障
- **WHEN** `logout` 的 HTTP 请求网络错误 / 超时 / 5xx
- **THEN** 本地 state 同样被清;Observable complete(**不** error)—— 调用方不需要做额外错误处理

#### Scenario: 幂等重复 logout
- **WHEN** 已 logout 后再次调 `logout()`(此时 `refreshToken` 可能为 null)
- **THEN** MUST NOT 崩;若 `refreshToken === null` 可跳过 HTTP 调用直接完成本地清理
