## RENAMED Requirements

两条 requirement 连**标题**一起改了,所以先 RENAMED 再 MODIFIED —— archive 的应用顺序是
RENAMED → REMOVED → MODIFIED → ADDED,只写 MODIFIED 会因为在现行 spec 里找不到新标题而失败。

- FROM: ### Requirement: `IGomokuAi` 是纯函数式 AI 决策接口
- TO: ### Requirement: `IBoardGameAi` 是纯函数式 AI 决策接口

- FROM: ### Requirement: `GomokuAiFactory` 按难度返回 `IGomokuAi` 实例
- TO: ### Requirement: AI 工厂按棋种注册,再按难度构造实例

## MODIFIED Requirements

### Requirement: `IBoardGameAi` 是纯函数式 AI 决策接口

系统 SHALL 在 `Gewu.Domain/Ai/IBoardGameAi.cs` 定义(由 `IGomokuAi` 更名而来 —— 它从来就没用到任何五子棋专属的东西,名字是唯一把它绑在一个棋种上的地方):

```
Position SelectMove(Board board, Stone myStone);
```

实现 MUST 满足:
- 返回的 `Position` 落在 `board` 的空格(`Stone.Empty`)上;
- 不修改 `board`(调用方可认为传入实例在返回后与调用前等价);
- 不读时钟 / 磁盘 / 网络 / 静态可变状态;
- 对相同 `board` 快照 + 相同 `myStone` 与相同随机源,输出 MUST 可复现。

`myStone` 传入 `Stone.Empty` 时实现 MUST 抛 `ArgumentOutOfRangeException`。若 `board` 已经没有任何空格,实现 MUST 抛 `InvalidOperationException`(调用方在棋盘满之前已经见过 `GameResult.Draw` 并应停止调用)。

"棋盘已满"MUST 按 `board.Rows * board.Cols` 判定,MUST NOT 硬编码 225 —— 一字棋的满盘是 9 格。

#### Scenario: 合法输出
- **WHEN** 对一个包含若干空格的 `Board` 和 `Stone.Black` 调 `SelectMove`
- **THEN** 返回的 `Position` 对应格子为 `Stone.Empty`;`board` 在返回前后内容完全一致

#### Scenario: 拒绝 Empty 棋色
- **WHEN** 传入 `myStone == Stone.Empty`
- **THEN** 抛 `ArgumentOutOfRangeException`

#### Scenario: 满棋盘
- **WHEN** `board` 已经全部格子被占(五子棋 225 格 / 一字棋 9 格),调 `SelectMove`
- **THEN** 抛 `InvalidOperationException`

### Requirement: AI 工厂按棋种注册,再按难度构造实例

系统 SHALL 用与 `IGameRulesRegistry` / `IPuzzleRulesRegistry` 相同的形状暴露 AI:

```
public interface IGameAiFactory
{
    string GameKey { get; }
    IBoardGameAi Create(BotDifficulty difficulty, Random random);
}

public interface IGameAiRegistry
{
    IGameAiFactory? For(string gameKey);   // 未注册返回 null
}
```

`GomokuAiFactory` 由静态类改为 `IGameAiFactory` 实现(`GameKey => "gomoku"`),分支不变:

- `Easy` → 新 `EasyAi(random)`
- `Medium` → 新 `MediumAi(random)`
- `Hard` → 新 `HardAi(random)`(默认 `searchDepth=2`)
- 其它 → `ArgumentOutOfRangeException`

工厂本身不持有状态;每次 `Create` 返回一个新实例。注册表住在 `Infrastructure`,与规则注册表一致 —— `Domain` MUST NOT 因此获得任何外部依赖。

`ExecuteBotMoveCommandHandler` MUST 改为经 `IGameAiRegistry.For(room.GameKey)` 取工厂;该键解析不出工厂时 MUST 与规则解析失败同样处理(返回 404,不抛未处理异常)。

#### Scenario: Easy 分支
- **WHEN** `For("gomoku").Create(BotDifficulty.Easy, new Random(1))`
- **THEN** 返回 `IBoardGameAi` 实例,运行时类型是 `EasyAi`

#### Scenario: Medium 分支
- **WHEN** `For("gomoku").Create(BotDifficulty.Medium, new Random(1))`
- **THEN** 返回 `IBoardGameAi` 实例,运行时类型是 `MediumAi`

#### Scenario: Hard 分支
- **WHEN** `For("gomoku").Create(BotDifficulty.Hard, new Random(1))`
- **THEN** 返回 `IBoardGameAi` 实例,运行时类型是 `HardAi`

#### Scenario: 未定义枚举值
- **WHEN** `For("gomoku").Create((BotDifficulty)99, new Random())`
- **THEN** 抛 `ArgumentOutOfRangeException`

#### Scenario: 未注册的棋种
- **WHEN** `For("xiangqi")`
- **THEN** 返回 `null`

### Requirement: `CreateAiRoomCommand` 一步创建房间并让机器人加入

Application 层 SHALL 新增:

```
public sealed record CreateAiRoomCommand(
    UserId HostUserId, string Name, BotDifficulty Difficulty, Stone HumanSide, string GameKey)
    : IRequest<RoomStateDto>;
```

Handler 流程 MUST 按顺序:

1. `FindByIdAsync(HostUserId)` 加载 Host;未找到抛 `UserNotFoundException`。
2. **断言 `host.IsBot == false`**;若为 true 抛 `ValidationException`("AI cannot host an AI room.")。
3. 按 `BotAccountIds.For(Difficulty)` 定位 bot UserId;`FindByIdAsync` 加载;未找到抛 `UserNotFoundException`(提示检查 migration seed)。
4. `Room.Create(new RoomId(Guid.NewGuid()), Name, HostUserId, _clock.UtcNow, GameKey)`。
5. `room.JoinAsPlayer(botUserId, _clock.UtcNow)` —— 状态从 Waiting 进 Playing,`Game` 实例化。
6. **如果 `HumanSide == Stone.White`,调 `room.SwapPlayers(_clock.UtcNow)`**。结果:`BlackPlayerId == botUserId`,`WhitePlayerId == HostUserId`,host 仍是真人;`Game.CurrentTurn` 仍是 Black,即立刻轮到 bot 走第 1 步。如果 `HumanSide == Stone.Black`,跳过这步(默认行为)。
7. `IRoomRepository.AddAsync(room, ct)`。
8. `IUnitOfWork.SaveChangesAsync(ct)` —— **一次**事务内提交房间、Game 与潜在的 swap。
9. 拉 username 字典(Host + Bot),用 `room.ToState(usernames)` 组装 `RoomStateDto` 返回。

Validator(FluentValidation)独立校验 `Name` 规则(和现有 `CreateRoomCommandValidator` 一致:3–50 非空白),并校验 `GameKey` 能在 `IGameRulesRegistry` 中解析。`Difficulty` 与 `HumanSide` 由 enum 类型系统保证。`HumanSide` MUST 仅接受 `Stone.Black` 或 `Stone.White`(`Stone.Empty` 抛 ValidationException)。

机器人账号 MUST 跨棋种共用 —— 一个 bot 账号是**身份**而不是策略,它在某局里跑哪套算法由 `(GameKey, Difficulty)` 经 AI 注册表解析。新增一个棋种 MUST NOT 往 users 表里插新行。

#### Scenario: 成功创建 AI 房间(默认 humanSide=Black)
- **WHEN** 真人 Alice 发 `CreateAiRoomCommand(alice, "quick practice", Medium, Stone.Black, "gomoku")`
- **THEN** 返回 `RoomStateDto`,`Status == Playing`,`BlackPlayerId == alice`,`WhitePlayerId == BotAccountIds.Medium`,`Game.CurrentTurn == Black`,`Game.Moves` 空

#### Scenario: 真人选 White 反转座位
- **WHEN** 真人 Alice 发 `CreateAiRoomCommand(alice, "defense practice", Medium, Stone.White, "gomoku")`
- **THEN** 返回 `RoomStateDto`,`Status == Playing`,`BlackPlayerId == BotAccountIds.Medium`,`WhitePlayerId == alice`,`HostUserId == alice`,`Game.CurrentTurn == Black`(轮到 bot 先走),`Game.Moves` 空;后续 AI worker 轮询会触发 bot 的第 1 步

#### Scenario: 机器人不存在(migration 未应用)
- **WHEN** 库里不存在 `BotAccountIds.Easy` 对应 User,调 `CreateAiRoomCommand(alice, "x", Easy, Stone.Black, "gomoku")`
- **THEN** 抛 `UserNotFoundException`

#### Scenario: AI-vs-AI 被拒
- **WHEN** 某调用方传入 `HostUserId = BotAccountIds.Easy`(即以机器人身份 Host)
- **THEN** 抛 `ValidationException`

#### Scenario: HumanSide=Empty 被拒
- **WHEN** 调 `CreateAiRoomCommand(alice, "x", Easy, Stone.Empty, "gomoku")`
- **THEN** 抛 `ValidationException`

#### Scenario: 一字棋 AI 房间共用同一批 bot 账号
- **WHEN** 调 `CreateAiRoomCommand(alice, "ttt", Hard, Stone.Black, "tictactoe")`
- **THEN** `WhitePlayerId == BotAccountIds.Hard`(与五子棋同一个账号),`GameKey == "tictactoe"`,users 表 MUST NOT 因本变更新增任何行

## ADDED Requirements

### Requirement: 一字棋的 AI 由 `TicTacToeAiFactory` 提供,Easy 复用既有实现

系统 SHALL 提供 `TicTacToeAiFactory : IGameAiFactory`(`GameKey => "tictactoe"`),分支:

- `Easy` → 新 `EasyAi(random)` —— **复用五子棋的实现,不新写类**。`EasyAi` 只按 `board.Rows` / `board.Cols` 遍历空格并均匀随机选点,不含任何棋种假设。
- `Medium` → 新 `TicTacToeMediumAi(random)`
- `Hard` → 新 `TicTacToeHardAi()`
- 其它 → `ArgumentOutOfRangeException`

`MediumAi` 与 `HardAi` MUST NOT 被复用。原因不止是它们内含 `BoardCenter = 7` 与 `length >= 5` 两个常数(那两个尚可参数化),而是 `HardAi` 的候选生成限于"已有子 2 格邻域"、评估函数按活三 / 活四打分 —— 那是五子棋的**战术**而非**参数**。在 3×3 上邻域限制退化为无操作,棋形词汇也不成立。把它们泛化成一套只服务两个棋种、其中一个根本不需要评估函数的打分语言,是比多写两个小类更差的交易。

#### Scenario: 各难度的运行时类型
- **WHEN** 以 `Easy` / `Medium` / `Hard` 调 `TicTacToeAiFactory.Create`
- **THEN** 分别返回 `EasyAi` / `TicTacToeMediumAi` / `TicTacToeHardAi`

#### Scenario: Easy 在 3×3 上给出合法着法
- **WHEN** 对一块含空格的 3×3 `Board` 调 `EasyAi.SelectMove`
- **THEN** 返回的 `Position` 在界内且对应格子为 `Stone.Empty`

#### Scenario: 不为一字棋改动五子棋 AI
- **WHEN** 比对本变更的 diff
- **THEN** `MediumAi.cs` 与 `HardAi.cs` MUST NOT 出现行为改动(仅允许因 `IGomokuAi` → `IBoardGameAi` 更名产生的机械修改)

### Requirement: `TicTacToeMediumAi` 按"自赢 → 堵对手 → 中心 → 角 → 随机"选点

`TicTacToeMediumAi` SHALL 按固定优先级选点:

1. 存在一步使自己立即三连的空格 → 走它。
2. 否则存在一步使对手下一手立即三连的空格 → 堵它。
3. 否则中心 `(1,1)` 若空 → 走它。
4. 否则四个角中任一空格 → 走它(并列时用注入的 `Random` 打破)。
5. 否则任一空格(同样随机打破)。

它 MUST 可被击败 —— 这是难度阶梯里唯一一个"会犯错但不犯低级错"的档位,`TicTacToeHardAi` 不可战胜,`EasyAi` 毫无抵抗。三档之间的区别 MUST 是可观察的。

#### Scenario: 优先取胜而不是堵
- **WHEN** 己方与对手同时各有一个"下一手三连"的点
- **THEN** 走自己取胜的那点

#### Scenario: 无胜可取时堵
- **WHEN** 己方没有立即取胜点,而对手有一个
- **THEN** 走对手那个点

#### Scenario: 空盘走中心
- **WHEN** 盘面全空
- **THEN** 返回 `(1,1)`

### Requirement: `TicTacToeHardAi` 穷举整棵博弈树,永不落败

`TicTacToeHardAi` SHALL 用无深度限制的 minimax 穷举整棵博弈树,MUST NOT 使用评估函数、深度截断或棋形启发 —— 3×3 的可达局面只有 5,478 个,完整搜索是瞬时的,完美走法是**完备性**的推论而不是调参的产物。

由此得到一条比任何启发式 AI 都强的可测性质,本实现 MUST 通过:**从任意合法局面出发、执任意一方,它拿到的结果 MUST 正好等于该局面的博弈论值**。

措辞刻意不是"永远不会输" —— 那句话对任意局面是**假的**。反例:

```
X O X     X 同时握有 (0,0)(1,1) 与 (0,2)(1,1) 两条各差一子的线 —— 双威胁。
O X .     轮到 O 走,堵哪边都输。
. . .
```

这是个合法局面,但在 Hard 接手**之前**就已经输定了。完美走法保证不了从死局翻盘,它保证的是永远拿到这个局面本来能拿到的最好结果。要求"相等"而不是"不差于":拿到比理论值更好的结果意味着求值有错,同样必须失败。

验证 MUST 用一个**独立写成的**求值器(如 negamax ±1),MUST NOT 复制被测实现来做对比 —— 那等于用同一个错误验证它自己。

已知后果:玩家从开局打不赢 Hard 档。这是一字棋这个已解游戏的事实,不是缺陷 —— Easy 与 Medium 仍可战胜,难度选择器已经存在。

#### Scenario: 从开局出发穷举对局永不落败
- **WHEN** 让 `TicTacToeHardAi` 执 X 与执 O,各自对抗一个穷举所有合法应手的对手,从空盘跑遍整棵树
- **THEN** 结果集合 MUST 只含"胜"与"和",MUST NOT 出现一次"负" —— Hard 自己不会走进死局,所以从开局出发这条更强的说法成立

#### Scenario: 每个合法局面都拿到博弈论值
- **WHEN** 枚举全部可达的非终局局面,对每一个让 `TicTacToeHardAi` 执该走方、对手穷举所有应手
- **THEN** Hard 的最坏结果 MUST 等于由独立求值器算出的该局面理论值

#### Scenario: 双方完美对弈必和
- **WHEN** 两个 `TicTacToeHardAi` 实例对弈
- **THEN** 结果 MUST 是和棋

#### Scenario: 有胜必取
- **WHEN** 存在一步立即三连
- **THEN** 走它,MUST NOT 走一步只是"不输"的着法

#### Scenario: 确定性
- **WHEN** 对同一局面重复调用 `SelectMove`
- **THEN** 每次返回同一坐标 —— 并列最优里随机挑也不会输,但确定性让上述性质可以穷举验证而不是抽样验证
