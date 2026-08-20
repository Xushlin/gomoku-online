# hoist-card-model

## Why

挖坑用**同一副牌、同一份编码**,而 `Card` 住在 `Games/Doudizhu/` 下 —— 它当初住那里是对的:
那时只有一个牌类棋种。现在有两个。

让挖坑 `using Gewu.Domain.Games.Doudizhu` 会让**一个棋种的命名空间成为另一个棋种的承重结构**,
而下一个读代码的人有理由问「删掉斗地主会不会弄坏挖坑」。

同时,挖坑要洗同一副牌 —— 而那会是 Fisher–Yates 加 xorshift32 的**第三份**副本
(第二份在 `TetrisPieceSequence`)。

## What Changes

- `Card` / `CardRank` / `CardSuit` 搬到 `Games/Cards/`,命名空间 `Gewu.Domain.Games.Cards`。
- 新增 `Card.SuitedDeck`(52 张,不含大小王)——`FullDeck` 的子集,不是另一份构造。
- 洗牌提成 `CardShuffle.Shuffle(IList<T>, int seed)`,`DoudizhuDeal` 改用它。
- **行为零改动**,由斗地主既有的 `The_encoded_deal_is_pinned` 保证。

## 三个决定

**一、`TetrisPieceSequence` 刻意不动。** 它那一份副本的存在理由是**客户端必须用 TypeScript
实现同一个算法**,而那份 TS 已经与它逐项对齐过(三个整袋、21 个方块)。让它去依赖一个叫
`CardShuffle` 的东西,是把「方块序列」说成「洗牌」。**共享要按「是不是同一件事」分,
而不是按「代码长得像不像」分。**

**二、`CardRank` 的数值不能改,而它的注释必须改。** 数值是编码下标的来源,也就是持久化格式;
但那段注释写的是「**数值就是大小顺序**,所以比大小是整数比较」—— 而那只对当时唯一存在的那个
棋种成立。挖坑是 `3 > 2 > A > K > … > 4`,3 最大而不是最小。**一个只被一个实现验证过的说法,
在第二个实现出现时才显出它是个巧合。** 这次同时补了一条断言钉住这件事。

**三、行为零改动是被钉住的,不是被相信的。** `The_encoded_deal_is_pinned` 把一个种子发出的
整副牌写死成一个字符串 —— 洗牌搬家之后它仍然绿,那就是「一个字节都没变」的可执行形式。
若没有那条测试,这次重构会是「看起来等价」。

## Impact

- Affected specs: `doudizhu`(「牌与它的一字符编码」那条)
- Affected code: 新增 `Games/Cards/{Card,CardShuffle}.cs`;`Games/Doudizhu/*` 六个文件补 using、
  `DoudizhuDeal` 改用共享洗牌;测试 `CardTests` 随源码搬到 `Games/Cards/`,新增 `CardShuffleTests`
- 后端行为:**零改动**;前端:**零改动**
