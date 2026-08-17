# idiom-chain Specification Delta

## MODIFIED Requirements

### Requirement: 成语接龙的一步是一个成语,合法性有三条

`IdiomChainRules` SHALL 实现 `IGameRules`,`GameKey` 为 `idiom-chain`,并在 `Apply` 中校验三条,任一不满足 MUST 抛 `InvalidMoveException`:

1. **词典里有** —— 该成语能在 `IIdiomLexicon` 中查到。查询 MUST NOT 按层级过滤:玩家答一条冷僻但合法的成语,拒掉是 bug。违反时的码 MUST 是 `idiom-not-found`。
2. **接得上** —— 该成语的**首字**等于上一个成语的**末字**。历史为空时(开局第一步)本条不适用,任何词典里的成语都合法。违反时的码 MUST 是 `idiom-does-not-link`。
3. **没说过** —— 本局历史里没有出现过同一个成语。违反时的码 MUST 是 `idiom-already-used`。

载荷 MUST 是文本类:收到位置类的一步 MUST 抛 `InvalidMoveException`(通过 `RequireText()`),码为缺省的 `invalid-move` —— 那不是三条规则之一,而是送错了形状。

三条各有自己的码,而不是共用 `invalid-move`。理由不是整洁:成语接龙的界面**故意不在客户端判合法性**(见 `web-idiom-chain`),所以服务端的拒绝是玩家了解规则的**唯一**途径,而三条规则要三种不同的纠正。

规则实例 MUST 无状态。判定所需的一切都从 `Apply` 收到的历史里读出来:上一个成语是历史最后一项的 `Text`,已用集合是历史全部 `Text`。

#### Scenario: 开局任意成语
- **WHEN** 历史为空,提交一条词典里的成语
- **THEN** 接受,返回 `Ongoing`

#### Scenario: 接得上
- **WHEN** 上一步是「一心一意」,提交「意气风发」
- **THEN** 接受

#### Scenario: 接不上被拒
- **WHEN** 上一步是「一心一意」,提交「风和日丽」
- **THEN** 抛 `InvalidMoveException`

#### Scenario: 不在词典里被拒
- **WHEN** 提交一个四字词但词典里没有
- **THEN** 抛 `InvalidMoveException`

#### Scenario: 重复被拒
- **WHEN** 「一心一意」在本局早些时候出现过,再次提交
- **THEN** 抛 `InvalidMoveException`,即便它接得上

#### Scenario: 位置类载荷被拒
- **WHEN** 提交一步带坐标的走法
- **THEN** 抛 `InvalidMoveException`,错误信息说明本棋种不在盘面上进行

#### Scenario: 冷僻成语照样接受
- **WHEN** 提交一条 `Obscure` 层但确实在词典里的成语
- **THEN** 接受 —— 校验用的是"在不在词典里",不是"常不常见"

#### Scenario: 三条各带自己的码
- **WHEN** 分别触发「不在词典」「接不上」「说过了」
- **THEN** 三次的 `Code` 分别是 `idiom-not-found` / `idiom-does-not-link` / `idiom-already-used`,两两不同

#### Scenario: 送错载荷形状不占用三个码之一
- **WHEN** 给成语接龙提交一步带坐标的走法
- **THEN** 码是 `invalid-move`
