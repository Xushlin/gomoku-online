## Why

`api-ops` 承诺 `GOMOKU_CORS__ALLOWEDORIGINS__0` 能在 Production 覆盖 CORS 白名单。**它从来不工作。** `Program.cs` 从未调 `AddEnvironmentVariables("GOMOKU_")`,而 `WebApplication.CreateBuilder` 默认加的是**无前缀**那一个。

实测(Production,同一个进程,同时给两个变量,第三个作对照):

| 环境变量 | preflight |
| --- | --- |
| `GOMOKU_Cors__AllowedOrigins__0=https://prefixed.example.com` | **被拒**,无 `Access-Control-Allow-Origin` |
| `Cors__AllowedOrigins__1=https://unprefixed.example.com` | **放行** |
| `https://evil.example.com` | 被拒 |

这是纯 spec 漂移:代码一直是对的(照 .NET 默认约定),spec 一直在说另一件事。按仓库规矩走 tiny `fix-spec-*-drift`。

**为什么它比一般的漂移值得一个归档记录**:一个被文档化、却被运行时**静默忽略**的配置项,比没有文档更糟。照文档配的人会以为自己改了白名单,而请求照旧被拒,现场没有任何线索指向"前缀"。这个仓库的主题是「声明了但没有机制维持」——这一条是它在**运维面**的版本。

## What Changes

把那条 scenario 里的变量名改成无前缀的 `Cors__AllowedOrigins__0`,并**新增一条 scenario 明确断言带前缀的不生效**。

后者是故意的:那曾是本要求承诺过的行为,把它反向钉住,比删掉它更能防止有人"好心"再把前缀加回来。

### 不加前缀支持,三条理由

1. 无前缀是 .NET 的默认约定,运维照默认写就对。
2. 前缀的价值是避免与同机其他应用的变量冲突 —— 容器化单应用部署里接近于零。
3. **若加上前缀支持,两种写法都会生效**,而文档只写一种。那是把"文档说的不工作"换成"两种都对但没人知道有两种"。

## Impact

- 受影响 spec:`api-ops` 一条 requirement(MODIFIED)。
- **零代码改动。** 代码本来就是对的。
- 顺带在同一条要求里记下另一个已验证的事实:连接串键是 `ConnectionStrings:Default`,覆盖它的变量是 `ConnectionStrings__Default`。
