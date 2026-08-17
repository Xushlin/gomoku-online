# Tasks — fix-spec-api-ops-env-prefix

## 1. 实测

- [x] 1.1 Production 实例,同时设 `GOMOKU_Cors__AllowedOrigins__0` 与 `Cors__AllowedOrigins__1`,
      第三个 origin 作对照组。
- [x] 1.2 三个 origin 各发一次 `OPTIONS /api/rooms` preflight,看 `Access-Control-Allow-Origin`。

```
  https://prefixed.example.com       -> 被拒(无 ACAO 头)
  https://unprefixed.example.com     -> 放行
  https://evil.example.com           -> 被拒(无 ACAO 头)
```

对照组要紧:没有它,"带前缀被拒"可能只是说明我的 preflight 请求本身有问题。

## 2. Spec

- [x] 2.1 变量名改成无前缀。
- [x] 2.2 **新增**一条 scenario 断言带前缀的**不**生效 —— 反向钉住,比删掉更能防止有人把它加回来。
- [x] 2.3 写下不加前缀支持的三条理由。
- [x] 2.4 顺带记下 `ConnectionStrings__Default`(已验证)这条同类事实。

## 3. 验证

- [x] 3.1 零代码改动 —— `git diff` 只含 openspec 与 CLAUDE.md。
- [x] 3.2 `openspec validate --strict` 通过。

## 4. 这一条为什么值得归档而不是随手改掉

一个被文档化、却被运行时**静默忽略**的配置项,比没有文档更糟:照文档配的人会以为白名单改了,
而请求照旧被拒,现场没有任何线索指向"前缀"两个字。

这个仓库反复处理「声明了但没有机制维持」——`SupportsHumanVsHuman` 无人强制、
围观频道的可见性无人强制、`IsRated` 是个会过期的判断。**这一条是同一个主题在运维面的版本**,
而它的发现方式也一样:不是读代码,是起一个进程、设两个变量、发三个请求。
