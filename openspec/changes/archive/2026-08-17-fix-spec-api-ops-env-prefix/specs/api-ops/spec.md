# api-ops Specification Delta

## MODIFIED Requirements

### Requirement: CORS 策略从配置读取允许的 origin 列表

Api 层 SHALL 注册名为 `FrontendPolicy` 的 CORS 策略,允许的 origin 来自 `appsettings.json` 的 `"Cors:AllowedOrigins"` 数组(空数组视为"不允许任何跨域")。策略 MUST:

- `WithOrigins(allowedOrigins)` —— 不使用 `AllowAnyOrigin`(与 `AllowCredentials` 不兼容);
- `AllowAnyMethod()` —— 放行 GET / POST / PUT / DELETE / PATCH / OPTIONS;
- `AllowAnyHeader()` —— 放行 `Authorization` / `Content-Type` / `X-Correlation-Id` 等业务所需头;
- `AllowCredentials()` —— SignalR WebSocket 握手必需,同时让未来若切到 HttpOnly cookie 方案时零改代码;
- `WithExposedHeaders("X-Correlation-Id")` —— 前端 `fetch` 默认只能读 CORS-safelisted 响应头,需要显式 expose `X-Correlation-Id`(由 `observability` 能力设置)以便前端日志上报时携带。

HTTP 管道中 `app.UseCors(CorsOptions.PolicyName)` MUST 排在 `UseAuthentication` **之前** —— 预检 OPTIONS 请求不带 Authorization,必须先过 CORS。

`CorsOptions` MUST 定义 `public const string PolicyName = "FrontendPolicy"` 常量;`Program.cs` 与任何将来的策略引用都用该常量,禁止字面量重复。

#### Scenario: Preflight 放行白名单 origin
- **WHEN** 客户端发 `OPTIONS /api/rooms` 请求,`Origin: http://localhost:4200` 且该 origin 在 `Cors:AllowedOrigins`
- **THEN** 响应 204 或 200;响应头含 `Access-Control-Allow-Origin: http://localhost:4200`、`Access-Control-Allow-Credentials: true`、`Access-Control-Allow-Methods: GET, POST, PUT, DELETE, PATCH`、`Access-Control-Expose-Headers: X-Correlation-Id`

#### Scenario: Preflight 拒绝非白名单 origin
- **WHEN** `Origin: http://evil.example.com`(不在白名单)
- **THEN** 响应**不**含 `Access-Control-Allow-Origin` 头 —— 浏览器根据此判断 block

环境变量覆盖用 .NET 的**默认无前缀约定**:`Cors__AllowedOrigins__0`。MUST NOT 使用
`GOMOKU_` 前缀 —— `Program.cs` 从未调 `AddEnvironmentVariables("GOMOKU_")`,而
`WebApplication.CreateBuilder` 默认加的是无前缀那一个。

**本要求此前断言的正是带前缀的那个,而它从来不工作。** 实测(Production,同时给两个变量):

| 环境变量 | preflight 结果 |
| --- | --- |
| `GOMOKU_Cors__AllowedOrigins__0=https://prefixed.example.com` | **被拒**,无 `Access-Control-Allow-Origin` 头 |
| `Cors__AllowedOrigins__1=https://unprefixed.example.com` | **放行** |
| `https://evil.example.com`(对照组) | 被拒 |

改 spec 而不是加前缀支持,理由有三条:无前缀是 .NET 的默认约定,运维照默认写就对;
前缀的价值是避免与同机其他应用的变量冲突,而容器化单应用部署里那个价值接近于零;
最要紧的是**若加上前缀支持,两种写法都会生效**,而文档只写一种 —— 那是把一个"文档说的不工作"
换成一个"两种都对但没人知道有两种"。**让 spec 说真话是成本最低、风险最低的那条路。**

同理值得记下的一条:连接串的键是 `ConnectionStrings:Default`,**不是** `:DefaultConnection`,
所以覆盖它的环境变量是 `ConnectionStrings__Default`(已验证可用)。

#### Scenario: Production 通过环境变量覆盖
- **WHEN** 环境变量 `Cors__AllowedOrigins__0 = https://gomoku.example.com`
- **THEN** 运行时 CORS 白名单含该 origin(.NET 配置的数组覆盖语法)

#### Scenario: 带 `GOMOKU_` 前缀的变量不生效
- **WHEN** 只设 `GOMOKU_Cors__AllowedOrigins__0 = https://prefixed.example.com`
- **THEN** 该 origin **不在**白名单里 —— preflight 响应不含 `Access-Control-Allow-Origin`。这一条是**故意**写下来的:它此前是本要求承诺过的行为,而承诺从未成立,而一个"文档化了却被静默忽略"的配置项比没有文档更糟

#### Scenario: CORS 与 SignalR 兼容
- **WHEN** 前端从白名单 origin 发 WebSocket 握手到 `/hubs/gomoku?access_token=...`
- **THEN** 握手成功;CORS 中间件不拦 WebSocket upgrade 请求
