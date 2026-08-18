# puzzle-core Specification

## Purpose
TBD - created by archiving change add-puzzle-core. Update Purpose after archive.
## Requirements
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

星级 SHALL 由 `IPuzzleRules.Score(PuzzleScoreInput input)` 计算,取值 1–3。

`PuzzleScoreInput` SHALL 含 `HintsUsed`、`Mistakes`、`Duration`、`LayoutJson`、`SolutionJson`、`SubmissionJson`。

前三项 MUST 全部是服务端事实:提示由服务端发放并计数、错误由服务端在 `check` 中判定并计数、用时取服务端时钟。系统 MUST NOT 采信客户端上报的关于自身表现的任何数值。

星级**公式**按游戏而异(华容道计步数、成语纵横计错误与提示),但"入参必须服务端可观测"这条属于平台,MUST NOT 由单个游戏放宽。

**提交进入计分不是这条约束的例外,理由是它的性质不同。** 「我只错了 0 次」是一句无法验证的自述 —— 服务端没有独立途径确认它,这正是本条存在的原因。「这是我的 81 步」不一样:`Validate` MUST 已经从关卡初始布局重放过每一步,任何一步不合法或走完不通关都整份作废;`Score` 看见提交时,它的每一步都已被服务端确认。

> 一个客户端给的数字不可信;一个**服务端必须重建之后才肯接受**的数字,是服务端观测到的事实。

因此实现 MAY 依据提交计算步数一类的量,但 MUST NOT 读取提交里任何**未经重放确认**的字段(例如客户端自己写进去的 `moveCount` / `elapsedMs`)——那会把这条约束绕回原样。

`Score` 之所以还要看关卡的两半,是因为「多少步算好」是关卡属性(经典局面有已知最少步数),不是常数。

#### Scenario: 客户端上报的错误数被忽略
- **WHEN** 提交请求体里带 `mistakes: 0`,而服务端在 `check` 中已记录 3 次错误
- **THEN** 计分使用 3,不使用 0

#### Scenario: 提示计数由服务端维护
- **WHEN** 客户端调用 `hint` 两次
- **THEN** 该尝试的 `HintsUsed` 为 2,且计分以此为输入

#### Scenario: 计分能看到被验证过的提交
- **WHEN** 某游戏的 `Score` 依据提交里的着法条数计算星级
- **THEN** 那份提交 MUST 已被 `Validate` 判定为通关;未通关的提交 MUST NOT 进入计分

#### Scenario: 提交里的自述数值不算数
- **WHEN** 提交体内含一个客户端写的 `moveCount`,与它实际列出的着法条数不符
- **THEN** 计分 MUST 使用重放确认过的着法条数,MUST NOT 使用该字段

### Requirement: `check` 校验部分答案并在服务端计错

`POST /api/puzzle-attempts/{id}/check` SHALL 接收一份**部分**答案,由 `IPuzzleRules.CheckPartial(solutionJson, layoutJson, partialJson)` 判定,返回是否正确。

判定为错误时,系统 SHALL 递增该尝试的 `Mistakes`。判定正确时 MUST NOT 递增。

该端点存在的理由是"答案不下发":原型在一条成语填满的瞬间就地判定,客户端没有答案就做不到 —— 把这一步放到服务端既保住了这个手感,又让错误计数从"客户端自述"变成"服务端观测"。

**因此它对每个游戏都是可选的。** 客户端能自己判定的游戏 MAY 完全不调用它:华容道的滑动合法性由公开的盘面与公开的规则决定,客户端自己就能判,为每一步发一个请求不会让服务端多知道任何东西(它最后无论如何都要重放整条路径)。规则实现 MUST 仍然提供 `CheckPartial`,但平台 MUST NOT 假定它被调用过 —— 尤其是计分 MUST NOT 依赖 `Mistakes` 非零。

**判定正确时**,`CheckPartial` MAY 在结果中附带一份游戏自定义的 `PayloadJson`,由端点原样转发给客户端;判定错误时 MUST NOT 附带。

该字段是给"答对之后要说点什么"用的:成语纵横要在一条成语填满的瞬间显示它的释义,而释义在数据库里、词典没有 HTTP 面,客户端凭自己拼不出来。它对答案封闭规则**没有**削弱 —— 载荷描述的是玩家刚刚已经解开的那部分,不透露网格未解部分的任何信息。

三个关卡类游戏都会需要这个能力(华容道要说"这一步把曹操挪出来了"、猜成语要给出处),所以它属于平台契约,而不是某个游戏的旁路。

#### Scenario: 错误答案计一次错
- **WHEN** 提交一份与答案不符的部分答案
- **THEN** 响应指示不正确,`Mistakes` 加 1

#### Scenario: 正确答案不计错
- **WHEN** 提交一份与答案相符的部分答案
- **THEN** 响应指示正确,`Mistakes` 不变

#### Scenario: 答对时可附带游戏自定义载荷
- **WHEN** 某游戏的 `CheckPartial` 在判定正确时返回了 `PayloadJson`
- **THEN** 端点响应中原样包含该载荷

#### Scenario: 答错时不附带载荷
- **WHEN** 判定为错误
- **THEN** 响应中的载荷字段为空 —— 未解开的部分 MUST NOT 借错误路径泄漏任何信息

#### Scenario: 不提供载荷的游戏照常工作
- **WHEN** 某游戏的 `CheckPartial` 不返回载荷
- **THEN** 响应中的载荷字段为空,其余行为不变

#### Scenario: 从不调用 check 的游戏也能拿到星级
- **WHEN** 一次尝试从头到尾没有调用过 `check`,`Mistakes` 为 0
- **THEN** 提交 MUST 正常计分 —— 星级公式 MUST NOT 要求 `Mistakes` 被填充过

### Requirement: `hint` 由服务端揭示并计费

`POST /api/puzzle-attempts/{id}/hint` SHALL 由 `IPuzzleRules.Hint` 依据 `SolutionJson`、`LayoutJson` 与**客户端上报的盘面状态**决定要揭示的片段,返回该片段,并递增 `HintsUsed`。

请求体 MAY 携带一份对平台**不透明**的 `stateJson` —— 与 `check` / `submit` 的载荷同一性质,平台不理解其内容,由各游戏的规则自行解析。缺省或无法解析时,规则 SHALL 退化到一个合理的默认揭示,MUST NOT 返回错误。

上报的盘面状态 MUST NOT 参与计分。`HintsUsed` 仍由服务端在每次调用时递增,是唯一算数的那个数字;客户端上报的只是"我这边哪些格有字、光标在哪",它决定的是**揭哪一格**,而不是**花了几次**。

采信这份上报不构成计分漏洞:客户端报告的是自己可见的盘面,不是答案;答案始终只在服务端,响应也始终只有一格。客户端确实可以借此**指定**要揭哪一格 —— 那是特性而非漏洞,原型本来就让玩家点着某格要提示,而且每次照样扣一颗星。

响应 MUST 只包含被揭示的那一个片段,MUST NOT 包含答案的其余部分。

#### Scenario: 提示只揭示一个片段
- **WHEN** 对一个 4 字成语关卡调用 `hint` 一次
- **THEN** 响应只含一个位置及其字,`HintsUsed` 为 1

#### Scenario: 上报状态影响揭哪一格,不影响计数
- **WHEN** 两次调用 `hint`,分别携带不同的 `stateJson`
- **THEN** 两次可能揭示不同的格子,但 `HintsUsed` 依次为 1、2 —— 计数只由调用次数决定

#### Scenario: 缺省请求体仍可用
- **WHEN** 调用 `hint` 且不带请求体
- **THEN** 仍返回一个被揭示的片段并递增 `HintsUsed`,MUST NOT 返回 4xx

#### Scenario: 畸形状态不致报错
- **WHEN** `stateJson` 不是合法 JSON
- **THEN** 规则退化到默认揭示,请求正常完成

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

`Gewu.Domain` SHALL 定义 `IPuzzleRules` 与 `IPuzzleRulesRegistry`。注册表按 `GameKey` 解析实现,未注册的键 SHALL 返回 `null`,handler MUST 将其映射为 404。

`IPuzzleRules` 的方法 SHALL 一律收到**关卡的两半**加上玩家这次给的载荷:

- `Validate(solutionJson, layoutJson, submissionJson)`
- `CheckPartial(solutionJson, layoutJson, partialJson)`
- `Hint(solutionJson, layoutJson, stateJson?)`
- `Score(PuzzleScoreInput)` —— 见计分那条

平台对这三份 JSON 的内容一律不理解,只保证 `solutionJson` 不出服务端。

**布局必须一起传,是因为不是每种答案都自描述。** 成语纵横的答案是**位置性**的:每一格该填什么都在 `SolutionJson` 里,判定不需要参照起点。华容道的答案是一条**路径**,而路径只能对着它的起点验 —— 起点就是 `LayoutJson`。`Hint` 一开始就同时收两半;`Validate` 与 `CheckPartial` 当初只收答案,不是一个决定,是当时唯一那个实现的形状透了出来。

新增一个单人关卡游戏 MUST 只需要:一个 `IPuzzleRules` 实现 + 一处 DI 注册。MUST NOT 需要修改本能力的任何既有文件。

**这条此前由一个假实现验证,而假实现证明不了它。** 覆盖它的测试注册的是一个照着成语纵横形状写的 fake —— 一个 fake 不可能推翻写它时所依据的假设。判据因此改为可执行的形式:下一个游戏落地时,`git diff --name-only` 里 MUST NOT 出现本能力的任何文件。

#### Scenario: 未注册的游戏键 404
- **WHEN** 以任意未注册的 `gameKey` 请求关卡或发起尝试
- **THEN** 返回 404

#### Scenario: 新增游戏不改既有文件
- **WHEN** 新增一个 `IPuzzleRules` 实现并注册
- **THEN** 本能力既有的领域类、handler、controller MUST NOT 被修改

#### Scenario: 路径类答案能被表达
- **WHEN** 某游戏的答案是一段从关卡初始布局出发的操作序列
- **THEN** 它的 `Validate` MUST 能拿到 `layoutJson` 并据此重放,无需把布局复制进 `solutionJson`

### Requirement: 关卡唯一性与迁移形态

`PuzzleLevels` SHALL 以 `(GameKey, LevelIndex)` 唯一。本变更 SHALL 只包含一个 migration,且 MUST 只建表建索引 —— 关卡数据随拥有它的游戏到来,MUST NOT 出现在 migration 里。

本变更 MUST NOT 新增任何 SignalR 方法或事件:单人关卡走纯 REST,关卡路由 MUST NOT 建立 hub 连接。

#### Scenario: 同游戏同下标不可重复
- **WHEN** 插入一条 `(GameKey, LevelIndex)` 已存在的关卡
- **THEN** 数据库以唯一约束拒绝

#### Scenario: 没有实时面
- **WHEN** 检索本变更新增的代码
- **THEN** 不存在新增的 Hub 方法或 `IRoomNotifier` 调用

### Requirement: 关卡产物入库时去掉多余空白,而语义一字不变

`LayoutJson` 与 `SolutionJson` 入库时 SHALL 是**紧凑**的 JSON:结构部分 MUST NOT 含多余空白。
入库文本与产物中对应的值 MUST 语义相同(`JsonNode.DeepEquals` 为真)—— 这是重排版,MUST NOT 是转换。

产物文件本身**保持缩进**提交。那份是给人审阅的,数据库里那份是给机器下发的,两者的正确格式
本来就不同。从前的缺陷正是把这两件事当成了一件:seeder 用 `JsonElement.GetRawText()`
取值,而它返回**源文本原样的切片**,于是产物的缩进被逐字复制进列里,再在每次加载关卡时发出去。
实测一个真实开发库:存下来的字节 **58% 是空白**,最重的一关从 6,389 B 降到 2,321 B。

非 ASCII 字符 MUST NOT 被转义。`Utf8JsonWriter` 的默认编码器会把每个非 ASCII 字符写成
`\uXXXX` —— 六字节换一个字,**比省下的空白更大**,而且入库文本在数据库浏览器里不可读。
必须显式选一个不转义非 ASCII 的编码器。

这两条 MUST 各有断言,而且**体积那条不是装饰**:把编码器换回默认值之后,「语义相同」与
「无多余空白」都仍然成立,只有体积会变差 —— 只测语义的话,那是一次全绿的退步。

#### Scenario: 入库文本与产物语义相同
- **WHEN** 用一份**缩进过**的产物灌库,取出该关的 `LayoutJson` / `SolutionJson`
- **THEN** 两者与产物中对应的值 `JsonNode.DeepEquals` 为真

#### Scenario: 结构部分没有多余空白
- **WHEN** 把入库文本里的字符串字面量挖掉,只看结构部分
- **THEN** MUST NOT 含换行或连续空格

#### Scenario: 中文以字符存在
- **WHEN** 产物里含中文(成语、棋子名)
- **THEN** 入库文本里它们仍是字符,MUST NOT 出现转义码点

#### Scenario: 紧凑一定比转义小
- **WHEN** 比较入库文本与「把同一个值按转义非 ASCII 的方式序列化」的结果
- **THEN** 入库文本 MUST 更短

