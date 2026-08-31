# desktop-shell Specification

## Purpose
TBD - created by archiving change add-desktop-shell. Update Purpose after archive.
## Requirements
### Requirement: 页面 SHALL 由自定义 `app://` 协议装载,而不是 `file://`

桌面壳 SHALL 用 Electron 的 `protocol.handle()` 注册一个 `app://` 协议来提供 Angular 产物,MUST NOT 用 `file://`,也 MUST NOT 为此把 Web 端改成哈希路由。

**理由是 `index.html` 里那一行 `<base href="/" />`。** 在 `file://` 下 `/` 解析到**文件系统根**,于是每一个 chunk 都 404 —— 症状是白屏加一串找不到的文件,看起来像构建坏了。

改哈希路由能绕开,但路由模式是 Web 端共享的:改它就改了浏览器里的 URL 形状,而 `add-api-base-url` 刚立下「Web 端行为一个字节不变」。**不在下一个变更里给自己破例。**

`app://` 还给一个**真正的 origin**,而那不只是整洁:`localStorage`(refresh token 就在里面)因此有稳定的分区,CSP 写得出,路径路由原样工作。

#### Scenario: 深链直接打开
- **WHEN** 渲染进程请求一个不存在于磁盘上的路径(如 `/g/idiom-guess/levels/0`)
- **THEN** MUST 返回 `index.html`,由 Angular 路由接管;MUST NOT 返回 404

#### Scenario: base href 不动
- **WHEN** 本变更合并
- **THEN** `index.html` 里的 `<base href="/" />` MUST 一字未改

### Requirement: 协议处理器 SHALL 拒绝路径穿越,而判据是解析结果落在产物目录内

「URL → 磁盘路径」SHALL 是一个不依赖 Electron 的**纯函数**,并且 MUST 拒绝任何解析结果落在产物目录之外的请求。

**这是本变更唯一真正的安全面** —— 它是这里唯一能被恶意输入触碰的东西。抽成纯函数,是因为它同时也是最容易钉住的东西:不启动 Electron 就能测。

#### Scenario: 穿越被拒
- **WHEN** 请求 `app://x/../../../../Windows/System32/config/SAM` 一类的路径
- **THEN** MUST 不返回产物目录之外的任何文件
- **AND** 判据 MUST 是「解析出的路径在产物目录内」,MUST NOT 是「函数没有抛异常」——
  后者对一个原样拼接的实现同样成立

#### Scenario: 正常资源照常返回
- **WHEN** 请求一个真实存在的 chunk
- **THEN** MUST 返回它 —— 这一条与上一条同时存在,否则一个「拒绝一切」的实现也能通过

### Requirement: 渲染进程 SHALL 拿不到 Node,preload 只暴露一个只读字符串

窗口 SHALL 以 `contextIsolation: true`、`nodeIntegration: false`、`sandbox: true` 创建;preload 通过 `contextBridge` 暴露的 MUST 只是一个只读字符串(服务器地址),MUST NOT 是任何函数。

外部导航 MUST 被拦住(`setWindowOpenHandler` 与 `will-navigate`)——**一个游戏厅没有任何理由在自己的窗口里打开外站**,而一个能被导航走的壳等于把用户的 token 交给了那一页。

#### Scenario: 渲染进程没有 Node
- **WHEN** 页面里读 `window.require` / `process`
- **THEN** MUST 是 undefined

#### Scenario: 外部链接不在壳里打开
- **WHEN** 页面尝试导航到一个非 `app://` 的地址
- **THEN** 壳 MUST 阻止它

### Requirement: 服务器地址 SHALL 由宿主提供,而缺省仍然保持 Web 端同源

`API_BASE_URL` 的默认 factory SHALL 读一个约定的宿主全局;**该全局不存在时 MUST 仍然返回空字符串**。

这样同一份 Angular 产物在浏览器里(读不到全局)仍然同源、逐字节不变,在桌面壳里(preload 设了它)指向配置的服务器 —— **不需要第二份构建**,而这个机制对将来的手机壳与「静态站点 + 独立 API 域名」的自托管部署同样适用。

#### Scenario: 宿主给了地址
- **WHEN** 宿主全局存在
- **THEN** 请求 MUST 打到那个地址

#### Scenario: 没有宿主时一切照旧
- **WHEN** 宿主全局不存在(浏览器)
- **THEN** `API_BASE_URL` MUST 是空字符串
- **AND** 这一条与上一条 MUST 同时存在 —— 少了它,一个「永远返回默认」的实现也能通过上一条

#### Scenario: 断言的是 token,不是那个函数
- **WHEN** 宿主全局在场,注入 `API_BASE_URL`
- **THEN** 它的值 MUST 等于宿主给的地址
- **AND** 只断言 `hostApiBaseUrl()` **不够**:把 token 的 factory 改回 `() => ''` 时
  那些断言一条都不红(实测)。而那正是桌面壳死掉的方式 —— 每个请求退回同源,
  变成 `app://gewu/api/…`,被壳自己的 SPA 回落当成路由,**返回 index.html 和一个 200**。
  没有报错、没有 404,只是所有数据都不见了

### Requirement: 部署 SHALL 把桌面壳的来源加进 CORS 白名单

服务端的 `Cors:AllowedOrigins` MUST 包含 `app://gewu`,否则桌面壳一个请求都发不出去。

**这一条是把窗口真的开起来才发现的。** 单测全绿、Web 端跨源验证也全绿,而壳里第一个请求就是 `Failed to fetch` —— 渲染进程的来源是 `app://gewu`,而白名单里只有 `http://localhost:4299`。

它属于**部署**而不是代码:`AddCors` + `WithOrigins` 早就在,少的只是一个配置项。但它不写下来就没人知道,而症状(所有数据都没有、界面却正常)与「服务器挂了」一模一样。

#### Scenario: 白名单里没有 app:// 时壳是空的
- **WHEN** `Cors:AllowedOrigins` 不含 `app://gewu`
- **THEN** 壳里的每个 API 请求 MUST 失败,而界面 MUST NOT 显示任何服务器错误 ——
  这正是它难被归因的原因

