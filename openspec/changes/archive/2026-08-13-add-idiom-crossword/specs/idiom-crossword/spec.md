## ADDED Requirements

### Requirement: 非交叉格不得与其它格正交相邻

摆放一条与已有成语垂直交叉的成语时,除共用的**交叉格**以外,新占用的每一格 MUST NOT 有任何已被占用的正交邻格(上下左右)。

这是本能力最核心的正确性属性。违反它会生成"两条成语平行只隔一格"的网格 —— 并排的字连读起来是无意义的串,玩家无法把它和真正的约束区分开。谜题仍然"可解",但已经坏了。

该规则 MUST 在摆放时校验,并 MUST 由一道独立的审计遍历对每个产出关卡重新验证一次 —— 这条性质最容易被一个看起来没问题的实现悄悄破坏。

#### Scenario: 平行相邻的摆放被拒绝
- **WHEN** 一条候选成语的摆放会让某个非交叉格与另一条成语的格子正交相邻
- **THEN** 该摆放 MUST 被拒绝,生成器改试其它候选

#### Scenario: 交叉格本身允许相邻
- **WHEN** 新成语在交叉格与已有成语共用一格
- **THEN** 该格的相邻关系不受本规则限制 —— 交叉正是它存在的理由

#### Scenario: 审计覆盖每个产出关卡
- **WHEN** 生成器完成一批关卡
- **THEN** 审计遍历对每个关卡的每个非交叉格重新检查相邻性,任一关卡不通过 MUST NOT 被写入产物

### Requirement: 生成器由显式种子决定,产物可复现

生成器 SHALL 接收一个显式随机种子,并 MUST 把全部随机选择都建立在该种子上。给定相同的种子与相同的词典,两次运行 MUST 产出完全相同的关卡集。

生成器 MUST NOT 使用 `Random.Shared`、系统时钟或任何环境随机源。

产物文件头部 MUST 记录:所用种子、词典所依据的上游 commit、生成时间、以及难度档位配置。

这条让关卡集成为**产物**而不是**事件**:坏关卡可追溯可复现可修,重新生成的差异干净,评审者能自己重跑一遍确认提交的文件就是工具产出的那个。

#### Scenario: 同种子两次运行结果一致
- **WHEN** 以同一种子、同一词典运行生成器两次
- **THEN** 两次产出逐字节相同

#### Scenario: 不同种子产出不同关卡
- **WHEN** 以两个不同种子运行
- **THEN** 产出的关卡集不同

#### Scenario: 产物记录来源
- **WHEN** 读取产物文件头部
- **THEN** 其中包含种子、词典上游 commit 与难度配置

### Requirement: 关卡布局含格位、字盘与预填格,不含答案

`LayoutJson` SHALL 包含:网格范围、哪些格子存在、哪些格子已预填(**含其字**)、字盘字符列表、以及各词槽的 `(行, 列, 方向, 长度)`。

`SolutionJson` SHALL 包含:每一格的正确字,以及每条成语的词、释义。

字盘**不构成泄漏**:它给出所需字符的多重集合外加干扰字,揭示的是"有哪些字"而非"哪个字放哪格" —— 后者才是谜题本身。预填格刻意公开自己的字,与原型一致,它们是让关卡有下手处的立足点。

#### Scenario: 布局不含任何答案字段
- **WHEN** 序列化某关卡的 `LayoutJson`
- **THEN** 其中 MUST NOT 出现完整成语词、释义,或非预填格的字

#### Scenario: 字盘足以填满所有空格
- **WHEN** 审计某关卡
- **THEN** 字盘的字符多重集合 MUST 覆盖全部非预填格所需的字

#### Scenario: 词槽声明可被客户端用于判定何时该 check
- **WHEN** 客户端读取布局
- **THEN** 它能从词槽的位置、方向、长度得知每条成语占哪些格,从而在填满时发起 `check`

### Requirement: `IdiomCrosswordRules` 实现四个操作

`Gewu.Domain` SHALL 提供注册在 `idiom-crossword` 下的 `IPuzzleRules` 实现:

- `Validate` —— 全部格子与答案一致才算通关。
- `CheckPartial` —— 校验**一个词槽**;正确时 MUST 附带该成语与其释义作为载荷(见 `puzzle-core` 的 `check` 要求)。
- `Hint` —— 按阅读顺序揭示第 N 个非预填格,N 由 `alreadyRevealedCount` 决定。
- `Score` —— `cost = mistakes + hintsUsed`;`cost == 0` → 3 星,`cost <= 2` → 2 星,否则 1 星。

计分公式与原型一致。三个入参都由服务端产生,本实现 MUST NOT 引入任何其它信号。

#### Scenario: 全对才通关
- **WHEN** 提交的网格有任意一格与答案不符
- **THEN** `Validate` 判定未通关

#### Scenario: 答对一条成语时返回释义
- **WHEN** 某词槽被填满且与答案一致
- **THEN** `CheckPartial` 判定正确,载荷中含该成语的词与释义

#### Scenario: 答错一条成语时不返回释义
- **WHEN** 某词槽被填满但与答案不符
- **THEN** `CheckPartial` 判定错误,MUST NOT 返回载荷

#### Scenario: 提示按阅读顺序推进
- **WHEN** 连续请求三次提示
- **THEN** 依次揭示阅读顺序上第 1、2、3 个非预填格

#### Scenario: 计分与原型一致
- **WHEN** `(hintsUsed, mistakes)` 分别为 `(0,0)`、`(1,1)`、`(0,3)`
- **THEN** 星级分别为 3、2、1

### Requirement: 关卡数据随仓库提交并在启动时幂等载入

生成产物 SHALL 提交进仓库。启动时,若 `PuzzleLevels` 中不存在 `GameKey = 'idiom-crossword'` 的行,系统 SHALL 载入该产物;否则 MUST 为无操作。

幂等性 MUST 以 `(GameKey, LevelIndex)` 为键 —— 即 `add-puzzle-core` 已声明的唯一约束。

与词典相同的取舍:数据库由 migration **加**已提交数据文件共同复现,而非仅由 migration 复现。

#### Scenario: 二次启动不重复插入
- **WHEN** 连续两次启动应用
- **THEN** `idiom-crossword` 的关卡行数在第二次启动后不变

#### Scenario: 空库被填充
- **WHEN** 对一个刚应用完 migration 的空库启动
- **THEN** `idiom-crossword` 的关卡行数等于产物中的关卡数

### Requirement: 注册后既有 puzzle 路由对本游戏不再 404

本变更 MUST NOT 新增任何 HTTP 端点。它唯一的对外效果是:`add-puzzle-core` 已有的那批路由在 `gameKey = 'idiom-crossword'` 时不再返回 404。

#### Scenario: 关卡列表可取
- **WHEN** 已登录用户请求 `idiom-crossword` 的关卡列表
- **THEN** 返回关卡数组而非 404

#### Scenario: 未注册的游戏仍然 404
- **WHEN** 请求任意其它未注册 `gameKey`
- **THEN** 仍返回 404
