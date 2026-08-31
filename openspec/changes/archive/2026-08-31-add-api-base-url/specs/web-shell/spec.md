## ADDED Requirements

### Requirement: 客户端的 API 地址 SHALL 可由宿主覆盖,而默认必须保持同源

前端 SHALL 通过一个 DI token 取得 API 前缀,默认值 MUST 是空字符串,使拼出的地址与今天**逐字节相同**(`/api/…`、`/hubs/match`)。

**这条存在的理由是量出来的:** 前端今天没有任何绝对 URL,REST 与实时连接都是同源相对路径,而 `src/environments/` 不存在。浏览器里这没问题;而桌面壳把页面装在本地,那时**没有「同源」这回事**,每一个相对路径都会打到一个不存在的地方 —— 症状是登录 404,看起来像后端挂了。

覆盖点 MUST 只有一处(一个 provider),供 Electron、将来的手机端与测试使用。

服务端 MUST NOT 因此改动:`AddCors` + `WithOrigins` 早已存在,新增客户端只是往配置里加一个来源。

#### Scenario: 默认仍是同源
- **WHEN** 不提供任何覆盖(即 Web 端)
- **THEN** 请求地址 MUST 与本变更之前逐字节相同

#### Scenario: 宿主覆盖之后 REST 与实时连接都跟着走
- **WHEN** 宿主把前缀设为 `https://example.test`
- **THEN** REST 请求 MUST 打到 `https://example.test/api/…`
- **AND** 实时连接 MUST 打到 `https://example.test/hubs/match` ——
  **两者都要断言**:只钉 REST 的话,实时那一半没有任何东西守着,而它恰恰是
  桌面壳里最容易被忘掉的一条

### Requirement: 「Web 端行为不变」SHALL 由生产 provider 链上的断言守住,而不是由既有测试

「请求地址一个字节不变」这条不变量 MUST 有一条跑在**生产 provider 列表**(`appConfig.providers`)上的断言;既有的那批精确 URL 断言 MUST NOT 被当作它的覆盖。

**这条要求是被变异测试改写的,原文是错的。** 提案里写着「既有的 `expectOne('/api/rooms')` 就是这条不变量的可执行形式」,并要求把默认值改成非空时**大面积**变红。实测:**只红了 2 条,而且两条都是本变更新写的**,既有 1042 条**全绿**。

原因是量出来的:19 个 spec 提供了 `HttpClient`,其中**只有 2 个注册了任何拦截器**。其余的用一个光秃秃的 `provideHttpClient()` 建自己的注入器 —— **那条拦截器根本不在它们的管线里**,所以它们不可能对它的任何改动有反应。

**一个「显然覆盖到了」的断言集合,可能整批都不在被测代码的路径上。** 而它绿着,看起来正是通过的样子。

因此判据改为:一条使用 `appConfig.providers` 的测试,断言 `/api/…` 与 `/i18n/…` 两者的地址都**逐字精确**不变。`appConfig` 是拦截器真正被装上的唯一地方。

#### Scenario: 生产链上的地址不变
- **WHEN** 用 `appConfig.providers` 建注入器并发出请求
- **THEN** `/api/rooms` 与 `/i18n/zh-CN.json` MUST 都以**精确**原样到达(`expectOne`,不是 `toContain`)

#### Scenario: 改默认值必须红
- **WHEN** 把默认前缀改成一个非空值
- **THEN** 上面那条 MUST 红
- **AND** 既有 1042 条**不会**红,而那**不是**覆盖不足的证据 —— 它们本来就不在这条路径上;
  把它们算作覆盖才是错误

### Requirement: 既有测试的断言 SHALL 一条不改

本变更 MUST NOT 修改任何既有测试的断言。新增测试可以,改既有断言不行。

**改动面是平台上最宽的** —— 每一个 HTTP 调用加唯一的实时连接。一个静默的行为变化会表现成「某个页面偶尔加载不出来」,那种缺陷要几周才归因得回来。既有断言全部原样通过,说明没有任何既有路径被动到;而**它证明的就是这一件事,不多**(见上一条)。

#### Scenario: 既有 spec 文件的断言未被改动
- **WHEN** 对比本变更与基线
- **THEN** 既有 spec 文件里 MUST 没有任何断言被修改
- **AND** 全部 MUST 通过
