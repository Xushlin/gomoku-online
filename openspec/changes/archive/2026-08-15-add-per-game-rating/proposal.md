## Why

平台有三个游戏，但只有**一个**评分池。它挂在 `User.Rating` 上，实质上就是五子棋排行榜 —— 只是名字里没写。

`add-tictactoe` 为了不污染它，加了 `IGameRules.IsRated` 这个临时开关，并在三处（字段的文档注释、`game-rules-registry` 的 spec、`UnratedGameEloTests`）以及 `CLAUDE.md` 的 roadmap 里写明：**本变更负责删掉它**。

现在有两个对战棋种了，所以 per-game 评分终于有第二个真实消费者可以验证 —— 这正是当初把它排在 `add-tictactoe` 之后的理由：一个只有一个 gameKey 的 `UserGameStats` 表，和一个只有一条记录的注册表一样没被验证过。

## `IsRated` 的事已经在 `add-game-capabilities` 里解决了

那个变更把它从「手工维护的判断」换成了受不变量约束的声明(`IsRated ⇒ SupportsHumanVsHuman`,
构造器 + 遍历注册表的测试双重强制)。**本变更因此不碰 `IsRated`** —— 一字棋不计分是它没有人类
对手池的后果,而不是"共享评分池"的临时补丁,所以池子拆开之后那个开关照样成立。

那一刀也是为了让本变更能被审:删 `User` 上五个战绩列会强制所有读者在同一个 commit 里改,
本变更没法再小,所以能力模型先走。

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

### 三个 DTO 的**形状一个字节不变**，只多一个棋种选择器

这是本变更刻意收窄的地方。`LeaderboardEntryDto` / `UserPublicProfileDto` / 搜索结果全部保持
现有字段，只是数据来源从 `User` 换成 `UserGameStats`：

- `GET /api/leaderboard?gameKey=`（缺省 `gomoku`）
- `GET /api/users/{id}?gameKey=`（缺省 `gomoku`）
- `GET /api/users?search=` —— **不加参数**，钉在 `gomoku`

好处是已发布的 Web 客户端**零改动**，看到的数字与现在完全一致；而"资料页同时展示所有棋种的
战绩"、"排行榜加棋种切换"变成纯前端工作，留给 `add-web-per-game-rating`。

代价是资料页此刻只能看到一个棋种 —— 记为缺口，不是遗漏。

搜索钉在 `gomoku` 的理由不是省事:找人卡片是**五子棋大厅**的一个组件
(`pages/lobby/cards/find-player`),给它加棋种参数等于开始泛化大厅,而那是 roadmap 下一步。

## Scope

**后端 only。** 前端跟在 `add-web-per-game-rating`：排行榜的棋种切换、资料页的分棋种战绩。同 `add-idiom-crossword` / `add-web-idiom-crossword` 的拆法。

后端改完之后，缺省参数让已发布的 Web 客户端继续显示五子棋的榜与战绩 —— 它看到的数字与现在完全一致，只是来源换了张表。

## Impact

- **Affected specs:** `elo-rating`（RENAMED ×1 + MODIFIED ×5）、`user-management`（MODIFIED ×5）。`game-rules-registry` 不动 —— 能力声明已由 `add-game-capabilities` 处理。
- **Affected code:** `Gewu.Domain/Users`（新 `UserGameStats`）、`Gewu.Application/Features/Users/{GetLeaderboard,GetUserProfile,SearchUsers}`、`Features/Rooms/Common/GameEloApplier`、`Common/Mapping/UserMapping`、`Common/DTOs/{UserDto,UserPublicProfileDto,LeaderboardEntryDto}`、`Gewu.Infrastructure/Persistence/{Configurations,Repositories,Migrations}`、`Gewu.Api/Controllers/LeaderboardController`。
- **一条 migration，含数据搬迁。** 本地 SQLite 无生产数据，但迁移仍要写对 —— 它是唯一会被别人在自己的库上跑的东西。
- **不做**：前端、`add-per-game-rating` 之外的排行榜（谜题的星数+用时榜、俄罗斯方块的分数榜仍各自独立，这是刻意的）、大厅泛化、搜索的棋种参数。
