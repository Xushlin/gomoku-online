## Why

斗地主有**三个座位**,而 `GameResult` 是 `Ongoing / BlackWin / WhiteWin / Draw` —— **2 号座位赢了没有值可以表示。**

`generalize-match-seats` 与 `add-room-seats` 已经把回合、走子、座位都改成了座位号,内核不再知道"黑白"。胜负是那条路上剩下的最后一处两人形状,而且它藏在一个**名字里没有"二"的枚举**里。

## 真正的发现:那两个值是**两处镜像**,不只是形状不对

在动手改之前先去数了它们到底被谁用、怎么用。src 里 18 处引用,**每一处都是同一个式子**:

```csharp
stone == Stone.Black ? GameResult.BlackWin : GameResult.WhiteWin
```

也就是"刚走这一步的那一方赢了"。逐处核对之后:

| 位置 | 那个颜色是从哪来的 |
| --- | --- |
| `Board.PlaceStone` | **`move.Stone` —— 它自己的入参** |
| `HardAi.IsWinForStone(result, stone)` | 刚试走的那颗子 |
| `MediumAi`(自赢 / 堵五两层) | 刚试走的那颗子 |
| `TicTacToeBoard.IsWinFor(result, stone)` | 刚试走的那颗子 |
| `XiangqiRules` | `side`,即走子方 |
| `Room.PlayMove` | 映回 `BlackPlayerId` / `WhitePlayerId`,再写进 `WinnerUserId` |

所以这不是"一个枚举少了第三个值",是**同一个事实存了两份**,两处都是:

- `Board.PlaceStone(move)` 被告知了 `move.Stone`,然后**回答了同一件事** —— 落子类棋种里,落子的人不可能因为落子而输,所以返回值里的颜色恒等于入参里的颜色。
- `Game` 同时有 `Result ∈ {BlackWin, WhiteWin}` 和 `WinnerUserId`。两个字段说同一句"谁赢了",而 `add-per-game-rating` 已经为这种形状付过账:**镜像是第二份真源,漂移的那天没有东西会报。**

镜像被消掉之后,三座位那个洞顺手就补上了 —— 这个顺序值得记下来:**先问"这个值是从哪来的",而不是先问"怎么加第三个值"。**

## What Changes

- `GameResult` → `{ Ongoing = 0, Decided = 1, Draw = 3 }`。**枚举名不变**,`Draw` 的底层值也不变;`BlackWin` / `WhiteWin` 合并成 `Decided`。
- `MoveApplication` → `(GameResult Result, int? WinnerSeat)`。**`WinnerSeat` 非 `null` 当且仅当 `Result == Decided`**,由构造器强制,不由注释保证。
- `Room.PlayMove` 用 `PlayerAt(WinnerSeat)` 得到 `WinnerUserId`,不再 `switch` 颜色。
- `Board.PlaceStone` / 两个 AI / `TicTacToeBoard` 全部**变短**:`result == myWin` 变成 `result == GameResult.Decided`。
- `GameEloApplier` 按 `WinnerUserId` 判胜负,而不是按颜色。
- 迁移:`UPDATE Games SET Result = 1 WHERE Result = 2`(`WhiteWin` → `Decided`),`WinnerUserId` 本来就是对的,不用动。

### 不引入第二个"盘面家族的胜负枚举"

一个看起来更省的方案是:`Board.PlaceStone` 继续返回带颜色的枚举(叫 `BoardOutcome`),只把内核那一层换掉 —— 理由现成:`Stone` 就是这么下沉成棋盘家族内部词汇的,`BoardSeats` 是那道边界。

**但那条类比在这里不成立。** `Stone` 下沉是因为棋盘上那颗东西**确实**有颜色,座位号说不出它;而 `PlaceStone` 返回值里的颜色**没有一点信息**是入参里没有的。为一份冗余数据造一个类型,再写一层换算把它换掉,是把一个镜像升级成一个有名字的镜像。

判据仍然是这个仓库自己的那条:**一份副本能不能留,不看它多小,看它错了会不会有人发现。** `Stone` 错了满盘皆错;`PlaceStone` 返回 `WhiteWin` 而入参是黑子,今天没有任何测试会红。

### 线上格式跟着变,不加兼容层

DTO 直接暴露 `GameResult`,所以 `'BlackWin' | 'WhiteWin'` 会变成 `'Decided'`。**不做读旧写新。**

已发布客户端数量是**零**,与 `require-room-game-key` 删掉那个「为已发布客户端保留」的默认值、`rename-gomoku-to-gewu` 拒绝加 localStorage 兼容层,是同一个理由。而且客户端那边**本来就得改**:

```ts
if (result === 'BlackWin' && mySide === 'black') return 'game.ended.title-win';
```

这一行是拿两份镜像重建"我赢了没有" —— 而 `winnerUserId` 一直都在 DTO 里。三个座位一到,`mySide` 这种写法本身就没了着落。加一层兼容映射等于**为一个必须改的地方付两次钱**。

`SeatWire` 保持原样。它换的是一个**值**(0 ↔ Black),而胜负枚举换的是一个**客户端还要拿它去跟第二份镜像比对的概念** —— 不是同一件事,它写下的那条触发条件(`SeatCount != 2` 的棋种落地)也还没到。

## Impact

- Domain:`GameResult`、`MoveApplication`、`Board`、`HardAi`、`MediumAi`、`TicTacToeBoard`、`XiangqiRules`、`NInARowRules`、`IdiomChainRules`、`Room`。
- Application:`GameEloApplier`,四个 DTO 的取值集合。
- Infrastructure:一个迁移(`Result` 值重映射)。
- Web:`GameResult` 类型、`game-ended-dialog`、`room-page` 的胜负判定与音效判定,以及三处战绩显示。
- **行为零变化**:两座位棋种的每一条路径逐步等价。这是纯重构,唯一可观察的差异是线上那个字符串。
