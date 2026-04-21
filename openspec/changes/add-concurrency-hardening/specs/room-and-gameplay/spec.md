## MODIFIED Requirements

### Requirement: 相关领域异常与其 HTTP 映射

系统 SHALL 把 `DbUpdateConcurrencyException`(来自 EF)映射为 HTTP 409 + `ProblemDetails`(`type: "https://gomoku-online/errors/concurrent-move"`)。本次修订 MUST 把该映射的覆盖面从原先"仅 Room/Game 并发"扩展到"Room/Game **与** User 聚合并发冲突";两种情况下 EF 抛出同一异常类型,Api 中间件 MUST NOT 为二者引入不同的 `ProblemDetails.type`。

- 既有(`add-rooms-and-gameplay` 引入):Room / Game 并发冲突(由 `Game.RowVersion` 保护)。
- **新增**(`add-concurrency-hardening`):User 聚合 `RecordGameResult` 写入冲突(由 `User.RowVersion` 保护)。

本次 MUST NOT 新增其它异常与 HTTP 映射条目(所有其它既有条目 `RoomNotFoundException` / `RoomNotWaitingException` / ... / `TurnNotTimedOutException` 保持不变)。

| 异常 | HTTP |
|---|---|
| `DbUpdateConcurrencyException`(来自 EF,覆盖 Game 并发 **与** User 并发) | 409 + `type: "concurrent-move"` |

#### Scenario: 并发落子冲突(覆盖既有)
- **WHEN** 两个玩家几乎同时调 `MakeMove`,EF 在 `SaveChangesAsync` 抛 `DbUpdateConcurrencyException`(Game.RowVersion 冲突)
- **THEN** HTTP 409,`ProblemDetails.type == "https://gomoku-online/errors/concurrent-move"`

#### Scenario: 并发战绩更新冲突(本次新增)
- **WHEN** 两个对局结束事务并发更新同一 User 的战绩(Alice 同时是两盘的黑方,两盘都触发 ResignCommand / TurnTimeoutCommand 几乎同刻完成)
- **THEN** 一者成功(第一次 RecordGameResult 的结果持久);另一者 EF 抛 `DbUpdateConcurrencyException`;Api 返回 HTTP 409,客户端重拉 `GET /api/users/me` + 相关 `GET /api/rooms/{id}` 再决定重试
