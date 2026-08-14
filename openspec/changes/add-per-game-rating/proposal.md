## Why

平台有三个游戏，但只有**一个**评分池。它挂在 `User.Rating` 上，实质上就是五子棋排行榜 —— 只是名字里没写。

`add-tictactoe` 为了不污染它，加了 `IGameRules.IsRated` 这个临时开关，并在三处（字段的文档注释、`game-rules-registry` 的 spec、`UnratedGameEloTests`）以及 `CLAUDE.md` 的 roadmap 里写明：**本变更负责删掉它**。

现在有两个对战棋种了，所以 per-game 评分终于有第二个真实消费者可以验证 —— 这正是当初把它排在 `add-tictactoe` 之后的理由：一个只有一个 gameKey 的 `UserGameStats` 表，和一个只有一条记录的注册表一样没被验证过。

## 我承诺过要删掉 `IsRated`。检查之后，那个承诺和那个字段本身都是错的

拆开来看，`IsRated` 当初有两个理由：

1. **污染**：一字棋结果会推动平台唯一的排行榜。
2. **无意义**：一字棋是已解游戏，完美对弈必和，Hard 档不可战胜。

本变更彻底解决 ①。② 没有解决，而且现在更尖锐：**一字棋没有人人对战**，唯一的对手是机器人，而机器人对局**是计分的**（`add-ai-opponent` D7 的反套利约束）。所以一字棋阶梯排出来的不是棋力，而是**谁刷 Easy 档刷得多** —— Hard 必和分不动，Easy 稳赢分单调涨。那不是噪声，是一个可见且可刷的错误信号。

**但真正的问题是 `IsRated` 这个字段的形状。** 它是一个手工维护的布尔，语义是「要不要给这个棋种算分」—— 一个判断，不是一个事实。判断会过期：一字棋将来有了人人对战，得有人记得回来翻它；而没人记得的时候，代码里的判断和现实就分岔了，且没有任何东西会报错。

所以本变更把它换成一个**结构性事实**加一条不变量：

```
IGameRules.SupportsHumanVsHuman : bool     ← 声明（结构性事实）
IGameRules.IsRated              : bool     ← 判断，但被不变量约束
不变量（测试强制）：IsRated ⇒ SupportsHumanVsHuman
```

于是：

- **今天的一字棋**：`SupportsHumanVsHuman = false`，所以 `IsRated` **只能**是 false。这不再是一个我替你做的判断，是被不变量逼出来的。
- **一字棋将来有了人人对战**：翻 `SupportsHumanVsHuman`，评分就从「禁止」变成「允许」。开不开是一个独立的、有理由可讲的产品决定 —— 而不是一件需要有人记得的事。
- **`SupportsAi` 我不加。** `IGameAiRegistry.For(key)` 已经知道答案了；再加个字段就是第二份真源，而这个仓库已经为「两份真源迟早不一致」付过两次学费。

## 关于象棋、孔明棋、华容道：预留什么，不预留什么

你要的是「别设计一个只对今天成立的模型」。但这个仓库刚刚用 `add-tictactoe` 证明了相反方向的风险：**对着单一游戏设计多游戏抽象，会造出装得下唯一实例、却在真加第二个时全线漏水的抽象**。所以我不会写今天没有消费者的机制。

我做的是把「加这些游戏各要碰什么」写成合同，并保证它们是**加东西**而不是改东西：

| 将来的游戏 | 要碰什么 | 会不会改到既有代码 |
| --- | --- | --- |
| **一字棋人人对战** | 翻 `SupportsHumanVsHuman`；大厅泛化（roadmap 下一步） | 会 —— 大厅那一步本来就要动 `/home` |
| **中国象棋人人 + 人机** | 一个 `IGameRules` 实现（不是 `NInARowRules`，它的判胜是另一套）+ 一个 `IGameAiFactory` + 各一处注册；`SupportsHumanVsHuman = true`、`IsRated = true` | 不会。座位与 from→to 载荷是它自己的账（roadmap 第 3 步） |
| **孔明棋、华容道** | 一个 `IPuzzleRules` 实现 + 一处注册 + 关卡数据 | **完全不会** —— 它们是单人关卡，走 `IPuzzleRules` 那条线，跟 `IGameRules` / 评分 / `UserGameStats` 一个字都不沾。它们的阶梯是谜题阶梯（星数 + 用时），成语纵横已经验证过 |

孔明棋值得单独说一句：它是**单人**游戏，没有对手，所以连「评分」这个概念都不适用 —— 它属于 `category: 'puzzle'`，和华容道同一条路。目录注册表里现在还没有它，加它是一个 manifest 条目加两个 i18n 键。

## What Changes

### `IGameRules` 增加 `SupportsHumanVsHuman`，`IsRated` 被不变量约束

见上。`SupportsAi` 不加 —— 由 `IGameAiRegistry` 解析。

五子棋：两者皆 true。一字棋：`SupportsHumanVsHuman = false`，因此 `IsRated` 强制为 false。

### `UserGameStats` 成为战绩的唯一真源

```
UserGameStats(UserId, GameKey, Rating, GamesPlayed, Wins, Losses, Draws, RowVersion)
主键 (UserId, GameKey)
```

`User` 上的 `Rating` / `GamesPlayed` / `Wins` / `Losses` / `Draws` **删除**，不保留镜像字段。理由是保留就等于有两份真源，而两份真源迟早会不一致 —— 与建房校验不许内联白名单是同一条道理。

`User.RecordGameResult` 随之移到 `UserGameStats`。它是 elo-rating 对该聚合的唯一写入口，不变量 `Wins + Losses + Draws == GamesPlayed` 原样保留，只是现在**每个棋种各自成立**。

### 迁移把既有战绩归给五子棋

一条 migration：建表，然后把每个 `User` 的现有五个字段搬成一行 `GameKey = 'gomoku'` 的 `UserGameStats`，再删列。

搬迁 MUST 在同一个 migration 里完成，且 MUST 显式写 SQL 而不是依赖 EF 生成 —— 先删列再搬数据就把数据搬没了。（`AddRoomGameKey` 那次 EF 生成了 `defaultValue: ""`，会让每个既有房间的 `GameKey` 变成空串、房间全部不可玩；那次是手工改的。同一类风险。）

### 排行榜变成分棋种的

- `GetLeaderboardQuery(GameKey, Page, PageSize)`，`GET /api/leaderboard?gameKey=`，缺省 `gomoku`。
- 未登记或不计分的棋种返回**空列表 + 200**，与房间列表同一处理 —— 集合端点上「这个棋种没有榜」与「榜是空的」对调用方无区别。
- 从没下过某棋种的人不出现在该棋种的榜上（没有 `UserGameStats` 行 = 不在榜上），而不是以 1200 分占位。

### 公开资料与搜索

- `UserPublicProfileDto` 的战绩变成**按棋种的一组**，而不是一份。
- `SearchUsers` 返回的 `Rating` 需要一个棋种上下文；本变更让它取 `gomoku`，并把「搜索按哪个棋种排序」记为缺口 —— 找人卡片是五子棋大厅的一部分，泛化它属于大厅泛化那一步。

## Scope

**后端 only。** 前端跟在 `add-web-per-game-rating`：排行榜的棋种切换、资料页的分棋种战绩。同 `add-idiom-crossword` / `add-web-idiom-crossword` 的拆法。

后端改完之后，缺省参数让已发布的 Web 客户端继续显示五子棋的榜与战绩 —— 它看到的数字与现在完全一致，只是来源换了张表。

## Impact

- **Affected specs:** `elo-rating`（大改：MODIFIED ×4）、`user-management`（MODIFIED ×2）、`room-and-gameplay`（MODIFIED ×1，`GameEloApplier`）、`game-rules-registry`（MODIFIED ×1，`IsRated` 的理由与拆除条件）、`api-ops`（MODIFIED ×1，排行榜端点）。
- **Affected code:** `Gewu.Domain/Users`（新 `UserGameStats`）、`Gewu.Application/Features/Users/{GetLeaderboard,GetUserProfile,SearchUsers}`、`Features/Rooms/Common/GameEloApplier`、`Common/Mapping/UserMapping`、`Common/DTOs/{UserDto,UserPublicProfileDto,LeaderboardEntryDto}`、`Gewu.Infrastructure/Persistence/{Configurations,Repositories,Migrations}`、`Gewu.Api/Controllers/LeaderboardController`。
- **一条 migration，含数据搬迁。** 本地 SQLite 无生产数据，但迁移仍要写对 —— 它是唯一会被别人在自己的库上跑的东西。
- **不做**：前端、`add-per-game-rating` 之外的排行榜（谜题的星数+用时榜、俄罗斯方块的分数榜仍各自独立，这是刻意的）、大厅泛化、搜索的棋种参数。
