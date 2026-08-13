## ADDED Requirements

### Requirement: 答案永不离开服务端

`PuzzleLevel` SHALL 同时持有 `LayoutJson`(下发给客户端)与 `SolutionJson`(**永不**下发)。

- 关卡读取 DTO MUST NOT 包含任何能承载 `SolutionJson` 的字段 —— 泄漏必须表现为"有人新增了一个属性",而不是"有人忘了删一个属性"。
- 校验、提示、计分 MUST 全部在服务端对 `SolutionJson` 执行。
- 仓储 MAY 把实体交给 handler,但 API 层 MUST NOT 拿到实体本身。

#### Scenario: 关卡 DTO 序列化后不含答案
- **WHEN** 某关卡的 `SolutionJson` 含可识别标记串,把该关卡的读取 DTO 序列化为 JSON
- **THEN** 输出中 MUST NOT 出现该标记串

#### Scenario: 关卡列表与详情都不下发答案
- **WHEN** 客户端取关卡列表或单个关卡
- **THEN** 响应体中 MUST NOT 出现 `solution` 字样的字段

### Requirement: 一次尝试是权威单位,且不可复用

`PuzzleAttempt` SHALL 持有 `UserId`、`PuzzleLevelId`、`StartedAt`、`HintsUsed`、`Mistakes`、`FinishedAt`、`Stars` 与并发令牌 `RowVersion`。

`hint` / `check` / `submit` MUST 经领域方法修改尝试,且这些方法 MUST 拒绝一个已结束(`FinishedAt` 非空)的尝试 —— 客户端因此无法在提交后继续要提示、无法重复提交刷分。

用时 SHALL 由服务端时钟计算(`FinishedAt - StartedAt`,两端均取自 `IDateTimeProvider`),MUST NOT 采用客户端上报的任何时间。

尝试只能由其所有者操作:`UserId` 不匹配 MUST 返回 404(而非 403)—— 不向调用方泄漏"该 id 存在"。

#### Scenario: 已提交的尝试不能再要提示
- **WHEN** 对一个已 `submit` 的尝试调用 `hint`
- **THEN** 请求被拒绝,`HintsUsed` 不变

#### Scenario: 不能重复提交
- **WHEN** 对同一个尝试第二次调用 `submit`
- **THEN** 请求被拒绝,`Stars` 与 `FinishedAt` 保持首次提交的值

#### Scenario: 他人的尝试不可见
- **WHEN** 用户 B 以用户 A 的尝试 id 调用 `check` / `hint` / `submit`
- **THEN** 返回 404

#### Scenario: 用时取服务端时钟
- **WHEN** 客户端在提交请求体里带上任何时间字段
- **THEN** 该字段 MUST 被忽略;记录的用时等于服务端 `FinishedAt - StartedAt`

### Requirement: 计分只用服务端可观测信号

星级 SHALL 由 `IPuzzleRules.Score(hintsUsed, mistakes, duration)` 计算,取值 1–3。

三个入参 MUST 全部是服务端事实:提示由服务端发放并计数、错误由服务端在 `check` 中判定并计数、用时取服务端时钟。系统 MUST NOT 采信客户端上报的关于自身表现的任何数值。

星级**公式**按游戏而异(华容道计步数、成语纵横计错误与提示),但"入参必须服务端可观测"这条属于平台,MUST NOT 由单个游戏放宽。

#### Scenario: 客户端上报的错误数被忽略
- **WHEN** 提交请求体里带 `mistakes: 0`,而服务端在 `check` 中已记录 3 次错误
- **THEN** 计分使用 3,不使用 0

#### Scenario: 提示计数由服务端维护
- **WHEN** 客户端调用 `hint` 两次
- **THEN** 该尝试的 `HintsUsed` 为 2,且计分以此为输入

### Requirement: `check` 校验部分答案并在服务端计错

`POST /api/puzzle-attempts/{id}/check` SHALL 接收一份**部分**答案,由 `IPuzzleRules.CheckPartial` 对 `SolutionJson` 判定,返回是否正确。

判定为错误时,系统 SHALL 递增该尝试的 `Mistakes`。判定正确时 MUST NOT 递增。

该端点存在的理由是"答案不下发":原型在一条成语填满的瞬间就地判定,客户端没有答案就做不到 —— 把这一步放到服务端既保住了这个手感,又让错误计数从"客户端自述"变成"服务端观测"。

#### Scenario: 错误答案计一次错
- **WHEN** 提交一份与答案不符的部分答案
- **THEN** 响应指示不正确,`Mistakes` 加 1

#### Scenario: 正确答案不计错
- **WHEN** 提交一份与答案相符的部分答案
- **THEN** 响应指示正确,`Mistakes` 不变

### Requirement: `hint` 由服务端揭示并计费

`POST /api/puzzle-attempts/{id}/hint` SHALL 由 `IPuzzleRules.Hint` 依据 `SolutionJson`、`LayoutJson` 与已揭示集合决定下一个要揭示的片段,返回该片段,并递增 `HintsUsed`。

响应 MUST 只包含被揭示的那一个片段,MUST NOT 包含答案的其余部分。

#### Scenario: 提示只揭示一个片段
- **WHEN** 对一个 4 字成语关卡调用 `hint` 一次
- **THEN** 响应只含一个位置及其字,`HintsUsed` 为 1

### Requirement: 每关只记录最好成绩,且只升不降

`PuzzleLevelProgress` SHALL 为每个 `(UserId, PuzzleLevelId)` 保留一行,含 `BestStars`、`BestDurationMs`、`AttemptCount`。

成功提交时,`BestStars` / `BestDurationMs` MUST 仅在成绩**更好**时更新:星级更高,或星级相同而用时更短。重玩因此 MUST NOT 降低已有评级。

`AttemptCount` MUST 在每次完成时递增 —— 它是统计量,不是成绩。

#### Scenario: 更差的重玩不覆盖
- **WHEN** 用户以 3 星 60 秒通关,随后以 1 星 200 秒再通关一次
- **THEN** `BestStars` 仍为 3、`BestDurationMs` 仍为 60000;`AttemptCount` 为 2

#### Scenario: 同星更快则更新
- **WHEN** 用户以 2 星 90 秒通关,随后以 2 星 50 秒再通关
- **THEN** `BestDurationMs` 更新为 50000

### Requirement: 进度为派生量,不落库

系统 MUST NOT 为"已解锁关卡下标"或"总星数"设置存储列。

- 已解锁下标 SHALL 由 `MAX(已完成关卡的 LevelIndex) + 1` 查询得出。
- 总星数 SHALL 由 `SUM(BestStars)` 查询得出。

理由:反范式计数器会与产生它的行不一致(一次失败事务、一次手工修数、两条写路径里的一个 bug),而在"每人每关最多一行"的量级上,两个走索引的聚合查询没有可观成本。

#### Scenario: 完成第 3 关后解锁第 4 关
- **WHEN** 用户已完成 `LevelIndex` 为 0、1、2 的关卡
- **THEN** 查询返回的已解锁下标为 3

#### Scenario: 总星数等于各关最好成绩之和
- **WHEN** 用户在三关分别取得 3、2、3 星
- **THEN** 查询返回的总星数为 8

### Requirement: `IPuzzleRules` 注册表按 `GameKey` 解析,未知键 404

`Gewu.Domain` SHALL 定义 `IPuzzleRules`(含 `GameKey`、`Validate`、`CheckPartial`、`Hint`、`Score`)与 `IPuzzleRulesRegistry`。注册表按 `GameKey` 解析实现,未注册的键 SHALL 返回 `null`,handler MUST 将其映射为 404。

新增一个单人关卡游戏 MUST 只需要:一个 `IPuzzleRules` 实现 + 一处 DI 注册。MUST NOT 需要修改本能力的任何既有文件。

本变更 MUST NOT 注册任何游戏 —— 因此它新增的全部路由在 成语纵横 落地前一律 404,这正是"这个游戏在本平台不存在"的诚实答复。

#### Scenario: 未注册的游戏键 404
- **WHEN** 以任意未注册的 `gameKey` 请求关卡或发起尝试
- **THEN** 返回 404

#### Scenario: 新增游戏不改既有文件
- **WHEN** 新增一个 `IPuzzleRules` 实现并注册
- **THEN** 本能力既有的领域类、handler、controller MUST NOT 被修改

### Requirement: 关卡唯一性与迁移形态

`PuzzleLevels` SHALL 以 `(GameKey, LevelIndex)` 唯一。本变更 SHALL 只包含一个 migration,且 MUST 只建表建索引 —— 关卡数据随拥有它的游戏到来,MUST NOT 出现在 migration 里。

本变更 MUST NOT 新增任何 SignalR 方法或事件:单人关卡走纯 REST,关卡路由 MUST NOT 建立 hub 连接。

#### Scenario: 同游戏同下标不可重复
- **WHEN** 插入一条 `(GameKey, LevelIndex)` 已存在的关卡
- **THEN** 数据库以唯一约束拒绝

#### Scenario: 没有实时面
- **WHEN** 检索本变更新增的代码
- **THEN** 不存在新增的 Hub 方法或 `IRoomNotifier` 调用
