# authentication Specification Delta

## MODIFIED Requirements

### Requirement: Access Token 为 HS256 JWT,包含 `sub` 和 `preferred_username`,15 分钟过期

系统 SHALL 用 `System.IdentityModel.Tokens.Jwt` 签发 Access Token,算法 `HS256`,claims 至少包含:
- `sub` = `UserId` 的 `Guid` 字符串形式
- `preferred_username` = `Username` 的原始字符串
- `jti` = 每次签发唯一的 `Guid`
- `iat` / `exp` 标准字段

`iss`(Issuer)MUST = `"gewu"`,`aud`(Audience)MUST = `"gewu-clients"`。过期时间 MUST = 签发时刻 + 15 分钟。签名密钥长度 MUST ≥ 32 字节。

Api 层 JWT Bearer 中间件 MUST 同时校验 `Issuer`、`Audience`、`Lifetime`、`SigningKey`;`ClockSkew` MUST = 30 秒。

#### Scenario: Token 可被自身 Bearer 中间件接受
- **WHEN** 对一个新注册用户签发 Access Token,随后以该 token 请求 `GET /api/users/me`
- **THEN** 请求 MUST 成功(HTTP 200)

#### Scenario: 篡改 token 被拒
- **WHEN** 以修改过 payload / signature 的 token 请求任何受保护端点
- **THEN** MUST 返回 HTTP 401

#### Scenario: 过期 token 被拒
- **WHEN** token 的 `exp` 小于当前时间减 30 秒
- **THEN** MUST 返回 HTTP 401

#### Scenario: claims 完整性
- **WHEN** 解析已签发的 Access Token
- **THEN** MUST 能读到 `sub`、`preferred_username`、`jti`、`iat`、`exp` 五个 claim
