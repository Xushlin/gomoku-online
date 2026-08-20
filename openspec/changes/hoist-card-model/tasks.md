# tasks — hoist-card-model

- [x] 1. `Card` / `CardRank` / `CardSuit` → `Games/Cards/Card.cs`,命名空间 `Gewu.Domain.Games.Cards`。
- [x] 2. `Card.SuitedDeck`:52 张,`FullDeck.Where(!IsJoker)` —— **子集而不是另一份构造**。
- [x] 3. `CardShuffle.Shuffle(IList<T>, int seed)`:Fisher–Yates + xorshift32 + 零状态替代常数,
      注释(含那条「后果曾被写错」的记录)一起搬过去。`DoudizhuDeal` 改用它,删掉私有 `NextState`。
- [x] 4. `TetrisPieceSequence` **不动**,理由写在 `CardShuffle` 的头注里。
- [x] 5. `CardRank` 的注释改掉「数值就是大小顺序」——那只对斗地主成立;新增一条断言钉住
      「数值是编码顺序」这件事。
- [x] 6. 六个斗地主源文件 + 十个测试文件补 `using`;`CardTests` 随源码搬到 `Games/Cards/`。
- [x] 7. `dotnet test Gewu.slnx` **1312** 绿(此前 1304,新增 8:`CardShuffleTests` 六条 +
      `SuitedDeck` 与「数值是编码顺序」各一条)。
- [x] 8. **变异**:去掉零状态替代常数 → **2 红**(新的 `Seed_zero_is_substituted_so_the_entropy_is_not_lost`
      与斗地主既有的同名断言)。
- [x] 9. **行为零改动的证据**:斗地主既有的 `The_encoded_deal_is_pinned` 把一个种子发出的整副牌
      写死成一个字符串,搬家之后它仍然绿。没有那条测试,这次重构只能是「看起来等价」。
