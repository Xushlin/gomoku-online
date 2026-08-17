# room-and-gameplay Specification Delta

## RENAMED Requirements

标题里的「可空的起点」已经不够 —— 现在整个坐标部分都可空。archive 的应用顺序是
RENAMED → REMOVED → MODIFIED → ADDED,所以下面 MODIFIED 用的是新标题。

- FROM: ### Requirement: `Move` 子实体记录可空的起点
- TO: ### Requirement: 一步棋要么是位置,要么是文本,不能既是又不是

## MODIFIED Requirements

### Requirement: `Move` 子实体记录每一步的上下文

`Move` MUST 包含:`Id: Guid`、`GameId: Guid`、`Ply: int (1-based)`、`Stone: Stone`、`PlayedAt: DateTime`(UTC),外加**恰好一种载荷**(见下一条 Requirement)。数据库持久化:`(GameId, Ply)` 唯一。

#### Scenario: Ply 从 1 起严格递增
- **WHEN** 在同一局依次走 3 步
- **THEN** 三个 `Move` 的 `Ply` 分别为 1、2、3

---

### Requirement: 一步棋要么是位置,要么是文本,不能既是又不是

`Move`、`MoveIntent`、`PlayedMove` SHALL 各携带两种互斥载荷之一:

- **位置类** —— `Row` / `Col`(终点,非空)加可选的 `FromRow` / `FromCol`(起点)。落子类棋种(五子棋 / 一字棋)没有起点;走子类(中国象棋)有。`FromRow` 与 `FromCol` MUST 同为 `null` 或同为非 `null` —— 半个坐标不是坐标。
- **文本类** —— `Text`(非空非空白),四个坐标列全为 `null`。成语接龙的一步是一个成语,它没有格子。

**恰好一种 MUST 被填充。** 两种都填、两种都不填,MUST 在构造时抛异常,MUST NOT 只写在文档里。这个不变量 MUST 由一条枚举非法组合的测试守着,而不是靠"只能从工厂函数构造"—— 工厂是约定,构造器检查是机制。

坐标列因此 MUST 可空。**MUST NOT 用 `Row = 0, Col = 0` 表示"这一步没有格子"** —— 那与本 spec 已经禁止的「用一个合法值表示没有起点」是同一件事,只是换了一个字段:读代码的人看到 `(0,0)` 得猜这是左上角还是不适用。

仍然 MUST NOT 改用 JSON 载荷列。理由未被本变更削弱:一个成语是**一个标量**,一列就装得下,而列仍然可查询、EF 原生映射、replay 仍是强类型的。JSON 会为一个还没有人提出的扩展性付钱。

#### Scenario: 落子类的起点为空
- **WHEN** 记录一步五子棋
- **THEN** `FromRow == null && FromCol == null`,`Row` / `Col` 非空,`Text == null`

#### Scenario: 走子类的起点非空
- **WHEN** 记录一步中国象棋
- **THEN** 四个坐标列都非 `null`,`Text == null`

#### Scenario: 文本类没有坐标
- **WHEN** 记录一步成语接龙
- **THEN** `Text` 非空,`FromRow` / `FromCol` / `Row` / `Col` 四列全为 `null`

#### Scenario: 两种载荷都给会被拒
- **WHEN** 构造一个同时带 `Text` 与 `Row`/`Col` 的 `MoveIntent` 或 `Move`
- **THEN** 构造 MUST 失败并抛异常

#### Scenario: 一种载荷都不给会被拒
- **WHEN** 构造一个既无 `Text` 也无 `Row`/`Col` 的 `MoveIntent` 或 `Move`
- **THEN** 构造 MUST 失败并抛异常

#### Scenario: 空白文本不算文本
- **WHEN** 以 `Text` 为 `""` 或 `"   "` 构造
- **THEN** 构造 MUST 失败 —— 一个空字符串不是一步棋

#### Scenario: 不变量由测试枚举,不由工厂保证
- **WHEN** 审阅这条不变量的测试
- **THEN** 它 MUST 直接构造非法组合,MUST NOT 只调用 `Place` / `Slide` / `Say` 三个工厂

#### Scenario: 迁移是加宽,不是回填
- **WHEN** 在含既有 `Moves` 行的库上跑迁移
- **THEN** 每行的 `Ply` / `Row` / `Col` / `Stone` 一字不变;新增的 `Text` 列为 `NULL`;`Row` / `Col` 由非空改为可空

#### Scenario: `Down` 遇到文本类记录必须失败
- **WHEN** 在已经存在文本类 `Move` 的库上回滚本迁移
- **THEN** 迁移 MUST 报错中止,MUST NOT 把那些行的 `Row` / `Col` 填 0 或把它们静默丢弃 —— 收窄一列而底下有装不进去的数据时,唯一诚实的动作是拒绝
