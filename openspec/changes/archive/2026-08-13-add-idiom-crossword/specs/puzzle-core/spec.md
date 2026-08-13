## MODIFIED Requirements

### Requirement: `check` 校验部分答案并在服务端计错

`POST /api/puzzle-attempts/{id}/check` SHALL 接收一份**部分**答案,由 `IPuzzleRules.CheckPartial` 对 `SolutionJson` 判定,返回是否正确。

判定为错误时,系统 SHALL 递增该尝试的 `Mistakes`。判定正确时 MUST NOT 递增。

该端点存在的理由是"答案不下发":原型在一条成语填满的瞬间就地判定,客户端没有答案就做不到 —— 把这一步放到服务端既保住了这个手感,又让错误计数从"客户端自述"变成"服务端观测"。

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
