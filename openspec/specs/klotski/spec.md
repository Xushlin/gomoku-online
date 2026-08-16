# klotski Specification

## Purpose
TBD - created by archiving change add-klotski. Update Purpose after archive.
## Requirements
### Requirement: 华容道的盘面与棋子

`KlotskiRules` SHALL 服务游戏键 `klotski`,盘面为 5 行 × 4 列。

关卡布局(`LayoutJson`)SHALL 描述:盘面尺寸、棋子列表(每枚含 `id`、`kind`、左上角 `row`/`col`、`height`/`width`),以及**出口**(曹操左上角必须到达的格)。

棋子 MUST NOT 重叠、MUST NOT 越界。布局若违反,`Validate` MUST 判定为不通关,MUST NOT 抛出未处理异常 —— 一份坏关卡是数据问题,不该表现为 500。

#### Scenario: 经典布局有十枚子
- **WHEN** 载入「横刀立马」
- **THEN** 盘面 5×4,共 10 枚子:1 枚 2×2、1 枚 1×2、4 枚 2×1、4 枚 1×1,余 2 格空

#### Scenario: 重叠的布局不通关而不是崩溃
- **WHEN** 用一份两枚子重叠的布局校验任何提交
- **THEN** 返回不通关,MUST NOT 抛出

### Requirement: 一次移动是一格,提交是一串移动

一次移动 SHALL 是「某枚子朝上下左右之一滑动**一格**」,记作 `{ id, dr, dc }`,其中 `dr`/`dc` 恰有一个非零且绝对值为 1。

提交(`SubmissionJson`)SHALL 是一串这样的移动。

选一格一步而不是「连滑算一步」,是因为它无歧义:客户端的一次拖拽可能跨两格,服务端不该猜玩家想算几步。重放、计数、计分因此用同一个定义。

一步合法当且仅当:该 `id` 存在、目标位置全部在盘内、且被占据的每一格要么原本为空、要么原本属于这枚子自己。

#### Scenario: 合法的一格滑动
- **WHEN** 一枚 1×1 卒旁边是空格,朝那个方向滑一格
- **THEN** 该步合法,盘面更新

#### Scenario: 滑进别的子里不合法
- **WHEN** 目标格被另一枚子占据
- **THEN** 该步不合法,整份提交判定为不通关

#### Scenario: 一步跨两格不合法
- **WHEN** 某步的 `dr`/`dc` 绝对值为 2
- **THEN** 该步不合法

#### Scenario: 不存在的棋子不合法
- **WHEN** 某步的 `id` 不在布局里
- **THEN** 该步不合法

### Requirement: 权威来自重放,而不是隐藏

`Validate` SHALL 从关卡的 `LayoutJson` 出发,逐步重放提交里的每一次移动,并且仅当**每一步都合法且曹操最终落在出口**时判定为通关。

**华容道没有秘密,这一条因此是它唯一的权威来源。** 成语纵横的服务端权威建立在不下发答案上;华容道什么都不藏 —— 棋子、盘面、出口、滑动规则全部公开且全部在客户端,一个判不了滑动的客户端连动画都做不出来。它的权威建立在**重新执行**上:服务端重放玩家声称走过的每一步,任何一步不合法或走完不到位就整份作废。

`SolutionJson` SHALL 因此只携带计分参数 `{ "minMoves": N }`。系统 MUST NOT 在其中存放一份「标准解」—— 没有要藏的东西,编造一份只会让读代码的人以为那里有秘密。

#### Scenario: 一串合法且到位的移动通关
- **WHEN** 提交一串从初始布局出发全部合法、且末态曹操在出口的移动
- **THEN** 判定通关

#### Scenario: 只声称到位不算数
- **WHEN** 提交一串移动,其中某一步不合法,但如果跳过它末态确实到位
- **THEN** 判定不通关 —— 服务端不接受它重放不出来的东西

#### Scenario: 到不了出口不通关
- **WHEN** 提交一串全部合法但曹操未到出口的移动
- **THEN** 判定不通关

#### Scenario: 空提交不通关
- **WHEN** 提交零步
- **THEN** 判定不通关(除非关卡的初始布局本身就已到位)

#### Scenario: 畸形提交不通关而不是崩溃
- **WHEN** 提交不是合法 JSON
- **THEN** 判定不通关,MUST NOT 抛出

### Requirement: `minMoves` 由搜索算出,不引用任何外部数字

关卡产物里的 `minMoves` SHALL 由 A\* 搜索求得(启发函数为曹操到出口的曼哈顿距离,可采纳,故结果为最优步数),MUST NOT 抄录任何出版物或记忆中的数字。

理由与 `add-xiangqi-ai` 拒绝声称「不可战胜」同一条:**一个验不了的断言比没有断言更糟**。经典局面的公开步数还随数法而异(见「一次移动是一格」),抄进来会既不可复现又可能不自洽。

MUST 有一条测试对关卡重新跑一遍搜索,断言结果与产物中的 `minMoves` 一致。

#### Scenario: 产物中的最优步数可复现
- **WHEN** 对每个已提交的关卡重新运行求解器
- **THEN** 得到的最优步数与产物中的 `minMoves` 完全相等

#### Scenario: 求得的解真的能通关
- **WHEN** 把求解器给出的最优解当作提交交给 `Validate`
- **THEN** 判定通关,且其长度等于 `minMoves`

### Requirement: 提示是从玩家当前局面搜出来的

`Hint` SHALL 解析客户端上报的 `stateJson`(当前棋子位置),对该局面运行同一套搜索,返回**最短解上的下一步**。

MUST NOT 返回一条预存路径上的下一步:玩家离开那条路径三步之后,预存的建议既不最优、甚至可能不合法。

`stateJson` 缺失、畸形、或描述了一个不合法/不可达的局面时,`Hint` SHALL 退化到「从关卡初始布局出发的最短解的第一步」,MUST NOT 返回错误。

#### Scenario: 从当前局面给下一步
- **WHEN** 玩家走了若干步后请求提示
- **THEN** 返回的一步在当前局面下合法,且走完之后到出口的最短距离**恰好减一**

#### Scenario: 畸形上报退化到默认
- **WHEN** `stateJson` 不是合法 JSON
- **THEN** 返回从初始布局出发的第一步,请求正常完成

#### Scenario: 不带请求体也能用
- **WHEN** 调用 `hint` 且不带 `stateJson`
- **THEN** 仍返回一步并递增 `HintsUsed`

### Requirement: 计分按步数,且不依赖 `Mistakes`

星级 SHALL 由提交的步数与 `minMoves` 的比值,以及提示次数决定:

- **3 星**:步数 ≤ `minMoves` 且未用提示
- **2 星**:步数 ≤ `minMoves` × 1.4 且提示 ≤ 2
- **1 星**:其余

`Mistakes` MUST NOT 参与。它对本游戏结构性地恒为 0 —— 那个计数器只有客户端调用 `check` 才增长,而华容道的客户端没有理由调。把一个永远为 0 的量写进公式等于写一段永不执行的代码。

用时同样不参与:想清楚每一步的玩家不该因为想得慢而掉星,与成语纵横同一取舍。用时仍被记录,用作最好成绩的次级排序。

#### Scenario: 最优解且无提示得三星
- **WHEN** 以恰好 `minMoves` 步通关且未用提示
- **THEN** 3 星

#### Scenario: 用过提示拿不到三星
- **WHEN** 以 `minMoves` 步通关但用过 1 次提示
- **THEN** 2 星

#### Scenario: 步数远超最优得一星
- **WHEN** 以 `minMoves` × 2 步通关
- **THEN** 1 星

#### Scenario: 星级与用时无关
- **WHEN** 同样的步数与提示数,用时相差一小时
- **THEN** 星级相同

#### Scenario: `Mistakes` 不影响星级
- **WHEN** 同样的步数与提示数,`Mistakes` 分别为 0 与 5
- **THEN** 星级相同

### Requirement: `CheckPartial` 存在但客户端不会调它

`KlotskiRules.CheckPartial` SHALL 把 `partialJson` 当作一段从初始布局出发的移动**前缀**,判定它是否全部合法;判定为真时 MAY 附带 `{"caoCaoOut": bool}`。

它存在是因为接口要求它,并且被调用时必须行为正确 —— 恒返回 `false` 会污染服务端的错误计数。它**不**存在是因为预期会被调用:滑动合法性由公开的盘面与公开的规则决定,客户端自己判得了,为每一步发一个请求既慢又什么都换不来。

#### Scenario: 合法前缀判定为真
- **WHEN** 提交一段全部合法的移动前缀
- **THEN** 判定为真,`Mistakes` 不变

#### Scenario: 不合法前缀判定为假
- **WHEN** 前缀中某一步不合法
- **THEN** 判定为假

### Requirement: 关卡随产物到来,不进 migration

华容道的关卡 SHALL 以提交进仓库的产物 `backend/data/levels/klotski.json` 形式提供,由一个**幂等**的 seeder 在启动时灌入 `PuzzleLevels`;该游戏已有关卡时 seeder MUST 无操作。

本变更 MUST NOT 新增任何 migration,MUST NOT 新增任何端点或 DTO。

产物中的布局是**手写**的 —— 华容道的经典局面是文化物,不是随机产物;生成器只负责对每个布局求 `minMoves`。

#### Scenario: 幂等
- **WHEN** 连续两次运行 seeder
- **THEN** 关卡只被插入一次

#### Scenario: 缺产物不致启动失败
- **WHEN** 产物文件不存在
- **THEN** 记一条警告,不插入关卡,应用正常启动

### Requirement: 新增本游戏未触碰谜题内核

本变更 SHALL NOT 修改 `Gewu.Domain/Puzzles/`、`Gewu.Application/Features/Puzzles/` 或谜题端点下的任何文件。

这是 `generalize-puzzle-rules` 交下来的验收条件,判据是**可执行**的而不是断言的:检查本变更的 `git diff --name-only`。此前覆盖「新增游戏不改既有文件」的测试注册的是一个照着成语纵横形状写的 fake,而一个 fake 不可能推翻写它时依据的假设 —— 所以这一次由真实的第二个游戏来检验。

#### Scenario: diff 里没有内核文件
- **WHEN** 检查本变更的 `git diff --name-only`
- **THEN** 其中 MUST NOT 出现 `Gewu.Domain/Puzzles/`、`Gewu.Application/Features/Puzzles/` 下的任何路径

