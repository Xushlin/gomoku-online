# Tasks — generalize-match-seats

## 1. 先量,再决定改哪里

- [x] 1.1 `Stone` 的分布:内核(`Rooms/` + `ValueObjects/`)48 次 / 5 文件,棋种规则 `Games/` 119 次 / 10 文件。
- [x] 1.2 两人假设归到底是 `Game.cs` 的一行 `stone == Stone.Black ? Stone.White : Stone.Black`。
- [x] 1.3 **`PlayedMove.Side` 全仓库只有一处读**(`NInARowRules:105`),`Apply` 只有一个调用点
      (`Room.PlayMove:334`)。象棋根本不读出手方 —— `from → to` 移动的是格子上那颗子,side 是隐含的。
      改面比提案估的还小。

## 2. 座位号进内核

- [x] 2.1 `IGameRules.SeatCount`,三个实现类(覆盖五个 key)全部返回 2。
- [x] 2.2 `Game.CurrentTurn` 改座位号;轮转 `(seat + 1) % seatCount`。
- [x] 2.3 `Move.Stone` → `Move.Seat`。
- [x] 2.4 `Room.PlayMove` 用新的 `Room.SeatOf(userId)`。
- [x] 2.5 `IGameRules.Apply(history, intent, seat)`;`BoardSeats` 做棋盘家族内部的座位↔棋色换算。
- [x] 2.6 `Room.SeatOf` 合并了落子、催促两处各写一遍的 if/else —— 座位变多之后,漏一个座位的
      表现是"某个座位的人被当成不是玩家",而漏的概率随座位数涨。

## 3. 不变量

- [x] 3.1 `IsRated ⇒ SeatCount == 2` 进 `NInARowRules` 构造器,与既有那条并列。
- [x] 3.2 两条遍历 `BuiltInGameRules.All` 的测试各钉一条。

## 4. 源码级断言

- [x] 4.1 `Gewu.Domain/Rooms/` 下不出现 `Stone`。
- [x] 4.2 **第一版这条断言红在我自己写的注释上** —— `Game` 与 `Move` 上都留着一句
      "此前这里是 Stone"的说明,而那正是要留的东西。断言的是「内核不**用** Stone」,
      不是「这个词不许出现」,所以它现在按行剥掉注释再搜。字符串字面量仍会被抓到,那合意。

## 5. 三座位的轮转

- [x] 5.1 走真实 `Room` + 一个三座位探针规则:`CurrentTurn` 走 `0 → 1 → 2`,而不是翻回 0。
- [x] 5.2 测试里写明它证明的是**取模算术**,不是"这个接缝对牌类够用"。
- [x] 5.3 顺带钉住:2 号座位现在**没人坐**,因为房间只有两个座位字段。这就是下一个变更存在的理由,
      写成断言而不是 TODO。

## 6. 迁移

- [x] 6.1 `RenameMoveStoneToSeat`。
- [x] 6.2 **EF 只生成了一句改名,而少掉的那半是静默的。** 两处存量数值要位移:
      `Moves.Stone`(Black=1/White=2 → 0/1),以及 `Games.CurrentTurn` —— 后者**连列都没变**
      (本来就是 int),所以生成器对它一个字都没写。
      不做位移的后果不是报错,是**错位一位**:进行中的对局轮次反转、历史出手方反转,
      在棋盘上是整局颜色翻过来,在结算上是赢家错人。
- [x] 6.3 `Down` 手写,顺序与 `Up` 相反(先还原数值再改回列名),并有回滚测试。
- [x] 6.4 三条迁移测试停在 `AddScoreRuns` 上取数据 —— 位移只在两侧之间可观测。

## 7. 变异验证

| 改坏什么 | 结果 |
| --- | --- |
| 轮转退回布尔翻转 | RED |
| 内核里塞回一个 `Stone` 引用 | RED |
| 迁移漏掉 `Moves` 的值位移 | RED |
| 迁移漏掉 `Games.CurrentTurn` 的值位移(生成器本来也没写) | RED |
| 一个三座位却计分的棋种 | RED |

## 8. 我自己的两处错,都值得记下来

### 8.1 「现有测试一条不改」是**说过头了**

提案里的验收标准写的是"现有测试一条不改地全绿,需要改断言就说明越界了"。真实情况:

- **没有一条关于行为的断言改过。**
- 改过的是:实现了接口的**测试替身**(`SpyRules` 要加 `SeatCount`、改 `Apply` 签名),
  以及**传出手方那个参数的调用点**(表示法从 `Stone.Black` 变成座位号)。

这与 `add-xiangqi-ai` 那次「zero changes to existing AI tests」的更正是同一类:签名换了,
实现它的替身就得跟着换,那不是行为变化,但也不是"一条不改"。标准本身该这么写才对。

### 8.2 `const int 0` 会隐式变成 `Stone.Empty`,而编译器一声不吭

第一版 `BoardSeats.FirstSeat` 是 `const int = 0`。**C# 里常量表达式 `0` 可以隐式转换成任意枚举** ——
所以自动改写把 `Stone.Black` 换成 `BoardSeats.FirstSeat` 之后,凡是那个位置**要的是 `Stone`** 的地方,
都静默编译成了 `Stone.Empty`。`SecondSeat = 1` 没这个问题:只有 0 有这个特权。

代价是具体的:**10 处被悄悄改坏,其中只有 2 处在运行时炸了**(一处棋盘断言 `GetStone(...)`、
一处 `SelectMove(history, Red)`),另外 8 处是潜伏的。

修法是机制而不是搜索:把三个座位常量改成 `static readonly int`。它不再是常量表达式,那条隐式转换
就不适用 —— **同一批错误当场变成 10 条编译错误**,一次全部看见。代价是 `BoardSeats.ToStone` 的
switch 表达式要改成 if/else(switch 的模式要求常量),那行注释也写在那里了。

## 9. 顺带修的

- [x] 9.1 三个迁移测试的原生 SQL 会在**两个不同的迁移点**上跑同一个 seed(有的停在中间站、
      有的跑到最新),而出手方那一列前后**列名和取值都不同**。抽出 `MoveSideColumn.DetectAsync`
      让 seed 自己探测,而不是让每个调用方记住自己站在哪儿 —— 后者是"加用例时忘了传参、
      于是它悄悄验了别的东西"的形状。
- [x] 9.2 `IdiomChainRulesTests.An_empty_side_is_refused` **改写而不是删掉**。它守的是
      `Stone.Empty` 这个**可表达的非值**,而座位号没有这种取值:越界就只是越界。对一个
      **根本不读出手方**的棋种,越界的座位号没有东西可以弄坏 —— 所以它现在钉的是那条设计决定:
      成语接龙的判定只取决于历史里的最后一个字。加范围检查会是内核已保证过的事实的第二份实现。

## 10. 结果

- 后端 **1069** 条测试全绿(改动前 1060,新增 9:6 条座位内核 + 3 条迁移)。
- 前端、线上格式、数据库结构:**零改动**(座位仍是两列,DTO 仍是 `'Black' | 'White'`)。
