## Why

平台叫**格物 / Gewu**,而它身上还挂着六处 `gomoku`:localStorage 键、SignalR hub 的类名与路径、日志文件名、JWT 的 issuer 与 audience。这些是**平台级**名字,不是五子棋这个游戏的名字。

它们各自都小,分三次做的总成本高于一次做完 —— 每一次都要改 spec、跑全套、开 PR。

## 一个我自己的前提,重新检查之后是错的

我在把这件事交给用户决定时写的是:localStorage 那条**「会让所有人登出(需要读旧写新的 shim)」**。

那句话有两个问题。

**第一,会被登出的人数是零。** CLAUDE.md 明确记着:没有部署、没有生产数据、本地 gitignore 的 SQLite。唯一会重新登录一次的是开发者自己的浏览器。这与 `require-room-game-key` 撞见的形状一样 —— 那次的兼容 shim 是为「已发布的客户端」写的,而那是**零个**。

**第二,更要命:shim 与本变更的另一半互相抵消。** JWT 的 `Issuer` / `Audience` 也叫 `gomoku-online`,也要改。改了之后**所有既有 token 立刻失效**。一个读旧写新的 localStorage shim 会忠实地保住那个 refresh token —— 保住一个已经不能用的东西,然后照样登出。

所以 **不做 shim**。代价是一次可预期的、有明确原因的重新登录;收益是不往代码里留一段"临时"的死逻辑 —— 而"临时 shim"是这一类东西里最容易变成永久的。

## What Changes

**平台级名字,全部改掉:**

| 现在 | 改成 |
| --- | --- |
| `gomoku:*` localStorage 键(8 个) | `gewu:*` |
| `GomokuHub` | `MatchHub` |
| `/hubs/gomoku` | `/hubs/match` |
| `logs/gomoku-.log` | `logs/gewu-.log` |
| JWT `Issuer: gomoku-online` | `gewu` |
| JWT `Audience: gomoku-online-clients` | `gewu-clients` |

**游戏本身的名字,一个都不动** —— 这个区分是本变更最容易出错的地方:

- `gameKey: "gomoku"`、`GameKeys.Gomoku`、`/g/gomoku/lobby`
- `GomokuRules`、`GomokuAiFactory`、`gomokuManifest`、`games.gomoku.*`

五子棋**就是**叫 gomoku。把它一起改掉会破坏一款正在运行的游戏,而且改的是一个本来就对的名字。

**顺带修一处 #57 的遗漏**:`CorsOptions.cs` 的文档注释里仍写着 `GOMOKU_CORS__ALLOWEDORIGINS__0` —— 那个写法上一个变更刚实测证伪(前缀被静默忽略)。spec 改了,代码注释没跟上。这正是"改一处忘一处",而它就在本变更的搜索路径上。

## Impact

- 受影响 spec:`room-and-gameplay`、`observability`、`api-ops`、`web-auth`、`web-shell`、`web-theming`、`web-sound`、`web-i18n`(逐一以实际命中为准)。
- 受影响代码:8 个 localStorage 常量、`GomokuHub.cs`(改名)、`Program.cs`、`SignalRRoomNotifier.cs`、`game-hub.service.ts`、`appsettings.json`、`CorsOptions.cs` 注释。
- **所有既有会话失效**,这是有意的,理由见上。
- `AiSmoke` 走 hub 路径,必须跟着改 —— 它现在在 CI 里,所以改漏了会**当场变红**而不是悄悄坏掉。那正是上一个变更买到的东西。
