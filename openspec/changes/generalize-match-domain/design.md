# generalize-match-domain — design notes

## D1. 规则拿到的是**走子历史**，不是棋盘

`Apply(IReadOnlyList<PlayedMove> history, MoveIntent intent, Stone side)`。

备选是让 `Room` 持有一个抽象的 `IBoardState` 传进去。不做：那样 `Room` 又知道「有一个盘面对象」了，
只是把耦合换了个名字，而且盘面要么冗余存盘（第二份真源），要么每次重放（那就是现在这样）。

传历史的代价是每步 O(n) 重放。实测量级：五子棋一局 < 100 步，象棋 < 200 步，重放是亚毫秒的。
**今天的 `Game.ReplayBoard` 已经在这么做**，本变更没有让它变慢，只是把重放的实现搬进了规则。
真慢了再在规则内部加缓存 —— 那是规则的私事，接口不用动。

## D2. `From` 可空，而不是给两类棋种两个方法

`MoveIntent(Position? From, Position To)`。落子类棋种 `From == null`。

备选一：`IGameRules` 上开 `PlaceMove` 与 `MovePiece` 两个方法。不做 —— 调用方就得知道该调哪个，
于是「这是哪类棋种」这个判断散到每个调用点，而那正是注册表要消灭的东西。

备选二：给落子类也编一个 `From`（比如等于 `To`）。不做：那是**用一个合法值表示「没有值」**，
读代码的人看到 `from == to` 得猜这是原地不动还是落子。`null` 说的是实话。

规则自己校验形状：`NInARowRules` 收到非 null 的 `From` 抛 `InvalidMoveException`
（客户端发了一个这个棋种不存在的走法），象棋收到 null 的 `From` 同样抛。
**这条校验属于规则，不属于聚合根** —— 聚合根不知道哪些棋种走子。

## D3. `INInARowRules` 从 `IGameRules` 分出去

`IGameRules` 留：`GameKey` / `Rows` / `Cols` / `SupportsHumanVsHuman` / `IsRated` / `Apply`。
`INInARowRules` 加：`WinLength` / `CreateBoard()`。

理由是 `IGameRules` 上那条门槛注释自己写的：能力声明超过三个就该拆。这里是同一个问题的另一面 ——
`WinLength` 对象棋无意义，`CreateBoard()` 返回的 `Board` 象棋根本不用。硬留在基接口上，
象棋就得实现两个骗人的成员，而「骗人的实现」是下一个人删不掉的东西（他不知道有没有人在调）。

`GET /api/games` 的 DTO **不受影响** —— 它本来就没有 `WinLength`（见 `add-web-per-game-rating`）。
AI 层吃的是 `Board`，所以 n-in-a-row 的 AI 工厂接 `INInARowRules`；象棋 AI 自带它的表示。

## D4. `Connected5` → `Decided`

一字棋从上线第一天起就在给三连记录「Connected5」。象棋会给将死记录同一个词。

**它不是陈旧，是错的** —— 错在它描述的是五子棋的胜利条件，而这个字段回答的问题是
「这局怎么结束的」，答案只有三类：规则判出了结果 / 有人认输 / 时间到。`Decided` 覆盖前者，
包括平局（一字棋满盘和棋也是规则判出来的）。

底层值保持 `= 0`，数据库存 int，**不需要数据迁移**。变的只有 JSON 线上的字符串，
而 web 与后端同批发布、本仓库没有生产数据。

per-game 那次学到的教训在这里同样适用：**判断会过期，而注释里的待办不是机制。**
`Connected5` 在路线图上挂了三个变更没人动，正因为它只是一条注释。

## D5. `Move` 加两个可空列，不加 JSON 列

`FromRow` / `FromCol`，可空 int。

- 可空 → 迁移是纯增量，既有 9 条 gomoku 记录不用回填，`Down` 只丢列。
- 仍然是列 → 「查所有吃子走法」这类查询将来还能写 SQL；JSON 列在 SQLite 上要 `json_extract`。
- 强类型 → replay 不需要反序列化，写错了是编译错误而不是运行时的 `JsonException`。

代价：出现真正不规则的走子（升变、王车易位）时要再加列或那时才上 JSON。
接受 —— 象棋不需要，而**为一个假想的第三个棋种设计，会把一个今天能验证的决定换成一个今天验证不了的决定**。
