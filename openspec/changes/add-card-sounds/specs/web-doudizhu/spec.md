# web-doudizhu 的规格变化

## ADDED Requirements

### Requirement: 出牌与过牌是两个声音,发牌只在牌到手时响一次

斗地主的**出牌** SHALL 放 `card-play`,而**叫分与不要** SHALL 留在 `move-place` 上。

**这正是分成两个事件的理由,不是副产品**:不看屏幕也听得出别人是出了牌还是过了牌。
`move-place` 的语义是「有人走了一手」,而它对叫分与不要是准确的 —— 所以它们不需要各自的声音。

**发牌** SHALL 在手牌**第一次到手**时放一次 `card-deal`:从「没有手牌」到「有手牌」的那一次跳变。

- 哨兵 MUST 被**第一份真快照**吃掉,而不是被 effect 的第一次运行吃掉 —— 后者会让打开一局
  进行中的牌局也响一声。
- 抢到地主后底牌进手(17 → 20)MUST NOT 响:那不是发牌。
- 刷新页面时发牌**动画**会重播(牌的 DOM 节点是新建的),而声音 MUST NOT ——
  **重播一个动画是装饰,重播一个声音是在报告一件没有发生的事。**

判据 SHALL 由 `games/doudizhu/trick.ts` 的 `moveKind(move)` 给出,房间页 MUST NOT 再抄一份
`play:` / `pass` / `bid:` 的前缀编码。

#### Scenario: 出牌与过牌听得出区别
- **WHEN** 一手 `play:` 到达,以及一手 `pass` 或 `bid:` 到达
- **THEN** 前者 MUST 放 `card-play`,后两者 MUST 放 `move-place`

#### Scenario: 发牌响一次
- **WHEN** 页面打开时还没发牌,随后第三个人入座、牌发下来
- **THEN** MUST 放且只放一次 `card-deal`

#### Scenario: 打开一局进行中的牌局是静的
- **WHEN** 页面打开时手上已经有牌
- **THEN** MUST NOT 放 `card-deal`
