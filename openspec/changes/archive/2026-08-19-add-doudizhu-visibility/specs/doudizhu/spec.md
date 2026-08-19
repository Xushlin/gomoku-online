# doudizhu 的规格变化

## ADDED Requirements

### Requirement: 每个座位看得到自己的牌,看不到别人的

`DoudizhuRules` SHALL 实现 `IPerSeatViewRules`,返回一份 camelCase JSON,含且仅含这个看客有权知道的东西:

| 字段 | 谁看得到 | 理由 |
| --- | --- | --- |
| `myHand` | **只有那个座位** | 这就是要藏的东西 |
| `handCounts` | 所有人 | 每人剩几张是牌桌上**看得见**的;藏它只会让"对家只剩两张了"画不出来 |
| `phase` / `landlord` / `baseScore` / `bidsMade` | 所有人 | 叫分与地主是公开过程 |
| `kitty` | 所有人,**但定下地主之后才给** | 地主当众收底牌是规则;而叫分阶段它决定谁值得抢地主,早给一步就是给了不该有的信息 |
| `tableSeat` / `tableCards` | 所有人 | 桌上打出来的牌本来就摊着 |
| `winner` | 所有人 | — |

不占座位的人(围观者 / 尚未入座)`myHand` MUST 是空串。座位号越界时同样 —— 一个坏座位号 MUST NOT 变成"看别人的牌"。

地主的 `myHand` 在定下地主之后 MUST 是 **20** 张(17 + 底牌 3)。

**`Game.Setup` 仍然 MUST NOT 出现在任何 DTO 上。** 那条反射断言不动:三家的牌是从 `Setup` 重建出来的,而送出去的是**裁剪过的投影**,不是 `Setup` 本身。

#### Scenario: 三家各看到自己的 17 张
- **WHEN** 一局刚发完牌,三个座位各取自己的视图
- **THEN** 每份 `myHand` 是 17 张,且**三份两两无交集**

#### Scenario: 没有一个座位看得到别人的任何一张
- **WHEN** 对每个座位,把它的视图与另两家的手牌**逐张**比对
- **THEN** 一张都 MUST NOT 命中

#### Scenario: 围观者看不到任何手牌
- **WHEN** 围观者取视图
- **THEN** `myHand` 是空串,而 `handCounts` / `phase` / `tableCards` 照常给

#### Scenario: 底牌在叫分阶段是隐藏的
- **WHEN** 还没定下地主
- **THEN** `kitty` MUST 是 `null`

#### Scenario: 底牌在定下地主之后是公开的
- **WHEN** 地主已定
- **THEN** 三家(以及围观者)的 `kitty` 都 MUST 是那 3 张,而地主的 `myHand` MUST 是 20 张
