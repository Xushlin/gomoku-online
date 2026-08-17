# idiom-dictionary Specification Delta

## ADDED Requirements

### Requirement: 同步的 `IIdiomLexicon` 供落子路径使用

`Gewu.Domain` SHALL 定义 `IIdiomLexicon`,只有一个**同步**成员 `bool Contains(string word)`;Infrastructure SHALL 以一次性载入全部成语原文的方式实现它,查询 MUST 是 O(1) 且落子路径上 MUST NOT 有 I/O。

它与既有的 `IIdiomRepository` **并存**,两者都不删:

- `IIdiomRepository` 是异步的、返回完整的 `Idiom` 行,供生成器与将来的猜成语使用。
- `IIdiomLexicon` 只回答"这个词是不是成语",供 `IGameRules.Apply` 使用。

分成两个口,是因为 `Apply` 是**同步的、在 Domain 里、由聚合方法内部调用**。为一个棋种把它改成异步,会让五子棋和象棋为一个它们没有的需求买单,还会把一次数据库往返塞进 `Room.PlayMove`。

`add-idiom-dictionary` 当初就是为这个游戏建的 `FindByWordAsync`,它的文档注释写着「成语接龙用它判断"这是不是一条真成语"」。**那个端口选对了消费者,选错了调用路径。** 一个为尚不存在的消费者建的端口是一次预测,而这次预测对了一半 —— 值得写下来,而不是悄悄绕过去。

`Contains` MUST NOT 按层级过滤:玩家答一条冷僻但合法的成语,拒掉是 bug。

#### Scenario: 认得词典里的成语
- **WHEN** 以一条库中存在的成语调 `Contains`
- **THEN** 返回 `true`

#### Scenario: 不认得别的词
- **WHEN** 以一个不在库中的四字词调 `Contains`
- **THEN** 返回 `false`

#### Scenario: 冷僻层也算数
- **WHEN** 以一条 `Obscure` 层的成语调 `Contains`
- **THEN** 返回 `true` —— 校验的是"在不在词典里",不是"常不常见"

#### Scenario: 落子路径上没有 I/O
- **WHEN** 一局对局中连续调用 `Contains` 多次
- **THEN** MUST NOT 产生任何数据库查询 —— 数据在首次使用前已全部载入内存

#### Scenario: 两个端口都还在
- **WHEN** 审阅 `IIdiomRepository`
- **THEN** 它的四个方法 MUST 仍然存在 —— 本变更只是不从落子路径上调它
