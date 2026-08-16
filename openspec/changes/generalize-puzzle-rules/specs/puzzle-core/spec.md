## MODIFIED Requirements

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
