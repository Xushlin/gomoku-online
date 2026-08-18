# Tasks — rename-gomoku-to-gewu

## 1. 前端:localStorage 键

- [x] 1.1 八个常量 `gomoku:*` → `gewu:*`(auth / lang / sound×3 / board-skin / theme / dark)。
- [x] 1.2 **不做读旧写新的 shim** —— 理由见提案:会被登出的人数是零,而且
      JWT issuer/audience 同时改会让 shim 保住一个已经失效的 token。
- [x] 1.3 各 spec 文件里的键名跟上。

## 2. SignalR

- [x] 2.1 `GomokuHub` → `MatchHub`(文件改名 + 类名 + 全部引用)。
- [x] 2.2 `/hubs/gomoku` → `/hubs/match`(`Program.cs` + `game-hub.service.ts`)。
- [x] 2.3 `AiSmoke` 跟上 —— 它在 CI 里,改漏了会**当场变红**。

## 3. 其余平台级名字

- [x] 3.1 `logs/gomoku-.log` → `logs/gewu-.log`。
- [x] 3.2 JWT `Issuer: gomoku-online` → `gewu`,`Audience` → `gewu-clients`。
- [x] 3.3 `CorsOptions.cs` 注释里的 `GOMOKU_CORS__...` —— #57 的遗漏,顺带修正。

## 4. **不改**的(最容易出错的地方)

- [x] 4.1 `gameKey: "gomoku"` / `GameKeys.Gomoku` / `/g/gomoku/*` —— 五子棋就是叫 gomoku。
- [x] 4.2 `GomokuRules` / `GomokuAiFactory` / `gomokuManifest` / `games.gomoku.*` —— 同上。
- [x] 4.3 一条测试断言五子棋的键仍然是 `"gomoku"`,防止将来有人"顺手"把它也改了。

## 5. 验证

- [x] 5.1 `dotnet build` 0 warning;`dotnet test` 全绿;`npm run lint` + `npm run test:ci` 全绿。
- [x] 5.2 全仓搜索:平台级 `gomoku` 归零(排除 bin/obj/logs 与游戏键的合法用法)。
- [x] 5.3 真实 HTTP 实测:

```
JWT iss = gewu | aud = gewu-clients
POST /hubs/match/negotiate   -> 200
POST /hubs/gomoku/negotiate  -> 404      ← 旧路径确实没了
日志文件                      -> gewu-20260818.log
```

  旧路径回 404 这一条要紧:它证明的不是"新路径通了",而是"**旧路径真的移走了**" ——
  两个都通会是最糟的结果,因为那意味着改名只加了一个别名,而没人会发现旧的还在。
- [x] 5.4 AiSmoke 在 CI 里通过。

## 6. 实测

- 平台级 `gomoku` 在源码里归零(排除 bin/obj/logs 与游戏键的合法用法)。
- `dotnet build` 0 warning;`dotnet test` **999** 全绿(247 + 84 + 668)。
- `npm run lint` 通过;`npm run test:ci` **505** 全绿。
- 13 份 live spec 同步,31 条 MODIFIED + 3 条 RENAMED,`openspec validate --strict` 通过。

## 7. 第一版机械替换误伤了五子棋

`gomoku:` 这个裸模式命中了三类**不该动**的东西:

```
-  gomoku: { rows: 15, cols: 15 },          ← TS 对象字面量的键,这会改掉五子棋的盘面尺寸
- * shares its entire rules engine with gomoku: the backend registers...   ← 英文散文里的冒号
-      gomoku: { title: 'Gomoku', ... },    ← i18n 测试夹具
```

误伤在提交前被 `git diff` 拦下,但**下一次未必**。所以:

1. 替换模式改成**只匹配引号内**的 `'gomoku:<key>'`,八个键逐一列出,不用通配。
2. 新增 `GameKeyNamingTests`:五子棋的键仍然是 `"gomoku"`,且没有任何已登记棋种的键含 `gewu`。

**键是契约**:它进房间记录、进 API 路径、进前端注册表、进已落库的每一行 `Room`。
改它不是改名,是数据迁移。

## 8. 三处**故意不改**

- **migration 里的 bot 邮箱** `easy@bot.gomoku.local` 等三处。改已合并的 migration 是本仓库的硬规矩,
  而它是内部标识、不对外可见 —— 为它做一次数据迁移换来的收益是零。
- **五子棋自己的一切**:`gameKey`、`GomokuRules`、`GomokuAiFactory`、`gomokuManifest`、
  `games.gomoku.*`、`/g/gomoku/*`。
- **GitHub 仓库名** `gomoku-online` —— 不在代码里,改它是仓库设置的事。

## 9. 顺带修的两处,都是这次搜索路过发现的

- `Program.cs` 与 `CorsOptions.cs` 的注释里还写着 `GOMOKU_CORS__ALLOWEDORIGINS__0` ——
  那个写法上一个变更(#57)刚实测证伪。spec 改了,注释没跟上。
- `CreateRoomCommand` 的文档注释说「HTTP 层对缺省的兼容处理只存在于 controller」——
  **那是假的**,`require-room-game-key` 已经删掉了那个默认(`grep -c GameKeys.Gomoku` 在
  controller 里是 **0**)。一条描述已被删除机制的注释,比没有注释更误导。
