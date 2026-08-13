## ADDED Requirements

### Requirement: 精选数据产物随仓库提交,且不参与构建

系统 SHALL 把上游 [chinese-xinhua](https://github.com/pwxcoo/chinese-xinhua)(MIT)的 `idiom.json` 经离线转换后,产出 `backend/data/idioms.curated.json` 并**提交进仓库**。

- 构建、测试、CI MUST NOT 访问网络获取成语数据。
- 该文件头部 MUST 记录上游仓库地址与所依据的上游 commit SHA,使任意一行数据的来源可追溯。
- 仓库根 MUST 提供 `NOTICE` 文件声明上游 MIT 许可与署名。
- 导入器(`backend/tools/IdiomImporter/`)MUST NOT 被 `Gewu.Api` 引用,它是开发者偶尔手动运行的工具。

#### Scenario: 干净检出无需联网即可得到词典
- **WHEN** 在无网络环境下检出仓库并首次启动 API
- **THEN** 词典数据被载入数据库,启动过程 MUST NOT 发起任何外部 HTTP 请求

#### Scenario: 产物可追溯
- **WHEN** 读取 `backend/data/idioms.curated.json` 头部
- **THEN** 其中包含上游仓库地址与 commit SHA

### Requirement: `Idioms` 与 `IdiomChars` 表结构

Infrastructure 层 SHALL 新增两张表:

- `Idioms`:`Id`、`Word`(唯一)、`Pinyin`、`Explanation`、`Derivation`、`Example`、`CharCount`、`MinCharFrequency`、`Tier`、`TierOverride`(可空)。`MinCharFrequency` 落库是为了让"这条为什么是 3 层"可以直接从数据回答,而不必重跑导入器。
- `IdiomChars`:`IdiomId`(外键,级联删除)、`Position`(0 起)、`Char`。

索引:`IdiomChars` MUST 同时建立 `(Char, Position)` 与 `(Position, Char)` 两个索引 —— 纵横生成按"某字出现在第 i 位"检索,接龙按"首位为某字"检索,两种访问模式偏好相反的列序。

本变更 SHALL 只包含一个 migration,且该 migration MUST 只建表建索引,MUST NOT 包含 `InsertData` / `HasData` 形式的成语数据。

#### Scenario: 词条与字符行一致
- **WHEN** 一条 4 字成语被写入 `Idioms`
- **THEN** `IdiomChars` 中存在 4 行,`Position` 分别为 0/1/2/3,`Char` 与 `Word` 逐字对应

#### Scenario: 级联删除
- **WHEN** 删除一行 `Idioms`
- **THEN** 其全部 `IdiomChars` 行一并被删除

#### Scenario: 词条唯一
- **WHEN** 尝试写入一条 `Word` 已存在的成语
- **THEN** 数据库以唯一约束拒绝

### Requirement: 难度分层由可得信号计算,`TierOverride` 永不被导入器覆盖

`Tier` SHALL 取值 1(适合出题)/ 2(可用)/ 3(生僻),由**纯函数**依据以下三个信号计算:

1. **字数恰为 4**。
2. **`example` / `derivation` 是否真实存在。** 上游用字符串 `"无"` 表示缺失,而非空字符串 —— 30,895 条中 `example` 为 `"无"` 的有 19,208 条、`derivation` 有 6,850 条。因此判定 MUST 同时排除空串与 `"无"`;仅判空会让该信号恒真。
3. **`MinCharFrequency`** —— 成语中最生僻那个字的语料文档频率(该字出现在多少条成语里),由导入器在全语料(30,895 条、4,886 个不同汉字)上统计后传入。

阈值:

- **Tier 1** = 4 字 **且** 有 example **且** 有 derivation **且** `MinCharFrequency >= 80`
- **Tier 2** = 4 字 **且**(有 example **或** 有 derivation)**且** `MinCharFrequency >= 20`(且不满足 Tier 1)
- **Tier 3** = 其余全部

上游**不含任何词频数据**,`word.json` 的 16,142 字近乎涵盖全部汉字、无法用作常用字筛选,所以以上第 3 条用语料自身的字频作代理。`Tier` 因此是**难度假设而非事实** —— 规范不主张其准确性,只主张:它可调、可解释(`MinCharFrequency` 随行落库)、且可被人工覆盖。

实测该阈值下 Tier 1 为 1,171 条,人工抽样仍有约两成偏生僻。这是已知且被接受的 —— 真正的收敛手段是 `TierOverride` 加实际试玩,不是更复杂的启发式。

`TierOverride` MUST 由人工维护,导入器 MUST NOT 写入该列(包括重新导入时)。全部消费方 MUST 以 `COALESCE(TierOverride, Tier)` 作为生效层级,使人工校订跨多次重新导入永久留存。

导入器 MUST 在运行结束时打印各层级条数分布与每层的随机样例,供人工据此选定阈值。

#### Scenario: 重新导入不覆盖人工校订
- **WHEN** 某条成语的 `TierOverride` 被人工设为 1,随后重新运行导入器
- **THEN** 该条的 `TierOverride` 仍为 1

#### Scenario: 生效层级优先取人工值
- **WHEN** 某条成语 `Tier = 3` 且 `TierOverride = 1`
- **THEN** 以 `maxTier = 1` 查询时该条 MUST 被返回

#### Scenario: 分层为纯函数
- **WHEN** 以相同的上游条目与相同的 `MinCharFrequency` 两次计算 `Tier`
- **THEN** 两次结果相同(不读时钟、不用随机)

#### Scenario: `"无"` 被当作缺失而非内容
- **WHEN** 某条 4 字成语的 `example` 为 `"无"`、`derivation` 有正文、`MinCharFrequency` 为 200
- **THEN** 该条 MUST NOT 被判为 Tier 1(缺 example),而是 Tier 2

### Requirement: 种子载入幂等,以 `Word` 为键

应用启动时,若 `Idioms` 表为空,系统 SHALL 从 `idioms.curated.json` 批量载入;若非空则 MUST 为无操作。

幂等性 MUST 以 `Word` 为键判定,而非行标识 —— 数据来自可重新生成的文件,行 Id 不稳定。

#### Scenario: 二次启动不重复写入
- **WHEN** 连续两次启动应用
- **THEN** 第二次启动 MUST NOT 新增任何 `Idioms` 或 `IdiomChars` 行,总行数不变

#### Scenario: 空库被填充
- **WHEN** 对一个刚应用完 migration 的空库启动应用
- **THEN** `Idioms` 行数等于产物文件中的条目数

### Requirement: `IIdiomRepository` 只暴露三个游戏所需的四个读操作

`Gewu.Application` SHALL 定义 `IIdiomRepository`,由 `Gewu.Infrastructure` 实现。方法:

- `FindByWordAsync(word)` —— 接龙校验"是否真成语"。
- `FindContainingCharAsync(char, position, maxTier)` —— 纵横生成检索交叉字。
- `FindStartingWithCharAsync(char, maxTier)` —— 接龙候选检索。
- `GetRandomAsync(maxTier, count)` —— 猜成语选题。

接口 MUST NOT 暴露 `IQueryable`、表达式树或通用查询对象 —— 每个方法都是一次走索引的查询,新增需求以新增方法的方式显式加入。

本变更 MUST NOT 新增任何 HTTP 端点、DTO 或 Hub 方法 —— 词典在本变更中对客户端完全不可达。

#### Scenario: 按字与位置检索走索引
- **WHEN** 以 `('山', 2, maxTier: 2)` 调用 `FindContainingCharAsync`
- **THEN** 返回全部生效层级 ≤ 2 且第 3 个字为「山」的成语

#### Scenario: 层级过滤生效
- **WHEN** 以 `maxTier: 1` 调用任一检索方法
- **THEN** 返回结果中 MUST NOT 出现生效层级为 2 或 3 的成语

#### Scenario: 未知词条
- **WHEN** 以一个不存在的字符串调用 `FindByWordAsync`
- **THEN** 返回 `null`

#### Scenario: 词典不可达
- **WHEN** 检索本变更新增的全部 controller 与 hub 方法
- **THEN** 0 匹配 —— 没有任何端点读取 `Idioms`
