# Tasks — enforce-ai-availability

## 1. 校验器(防线)

- [x] 1.1 `GameKeyValidation.MustHaveAnAi(rules, ai)` —— 与 `MustSupportHumanVsHuman` 同形:
      判据取自注册表,键解析不出规则时静默通过。
- [x] 1.2 `CreateAiRoomCommandValidator` 注入 `IGameAiRegistry`,只在 AI 房路径上挂这条。
- [x] 1.3 真人房路径**不动** —— 成语接龙开放人人对战。

## 2. 描述符

- [x] 2.1 `GameDescriptorDto` 加 `bool SupportsAi`。
- [x] 2.2 `GetGameDescriptorsQueryHandler` 注入 `IGameAiRegistry`,投影 `For(key) is not null`。
- [x] 2.3 前端 `GameDescriptor` 模型加 `supportsAi`;`StubGameCapabilities` 跟上。

## 3. 大厅

- [x] 3.1 `game-lobby.html` 按 `showAiCard()` 渲染 `<app-ai-game-card />`,与 `showLeaderboard()` 同形。
- [x] 3.2 `capabilities.loaded()` 为 false 时仍走骨架 —— 不在"还不知道"的时候先下结论。

## 4. 测试

- [x] 4.1 AI 房校验:`idiom-chain` 被拒、三个有 AI 的棋种放行、真人房不受影响。
- [x] 4.2 **遍历注册表**断言放行与否与 `IGameAiRegistry.For(key) is not null` 相符,两种结果都要出现过。
- [x] 4.3 未登记的键在 `GameKey` 上只报一条错(两条路径各一条用例)。
- [x] 4.4 描述符:`gomoku` → `supportsAi: true`,`idiom-chain` → `false`;外加一条断言
      **描述符与校验器读同一份注册表** —— 逐个棋种比对"公布的 supportsAi"与"校验器接不接受"。
- [x] 4.5 Web:`idiom-chain` 大厅不渲染 AI 卡;`gomoku` 渲染;未加载时不渲染。

## 5. 验证

- [x] 5.1 `dotnet build` 0 warning;`dotnet test` 930 全绿(236 + 84 + 610);
      `npm run lint` 通过;`npm run test:ci` 482 全绿。
- [x] 5.2 复现漏洞、再验证它没了 —— 见下。
- [x] 5.3 `gomoku` 的 AI 房仍然建得出来,**机器人 2 秒内真的走了第一手**(`row=2 col=9 stone=Black`)。
- [x] 5.4 重启后日志里 `has no AI` 出现 **0** 次;`AiMoveWorker` 只剩启动那一行。

## 6. 实测记录

### 改之前 —— 这是一个计分漏洞,不是一个多余的房间

同一个 scratch 库,同一个账号:

```
rating before: 1162 | games: 2
POST /api/rooms/ai { gameKey: "idiom-chain", humanSide: White }  -> 201
  t+65s: status=Finished result=WhiteWin reason=TurnTimeout
rating after:  1208 | games: 4 | wins: 2
```

**零手棋,+46 ELO,可无限重复。** 期间 `AiMoveWorker` 每 1500 ms 抛一次
`RoomNotFoundException: Room '…' declares game 'idiom-chain', which has no AI.`

`games` 从 2 涨到 4 而不是 3 —— 更早那几次探测建的孤儿房也在同一窗口里陆续超时结算了。
每一间孤儿房都是一场落到榜上的白捡胜利。

### 改之后

```
GET /api/games
  gomoku       supportsHumanVsHuman: true   supportsAi: true    15×15
  idiom-chain  supportsHumanVsHuman: true   supportsAi: false   null×null
  tictactoe    supportsHumanVsHuman: false  supportsAi: true    3×3
  xiangqi      supportsHumanVsHuman: false  supportsAi: true    10×9

POST /api/rooms/ai
  gomoku       -> 201
  tictactoe    -> 201
  xiangqi      -> 201
  idiom-chain  -> 400  {"GameKey":["'idiom-chain' has no computer opponent on this platform."]}
  go           -> 400  {"GameKey":["'go' is not a game on this platform."]}   ← 仍然只有一条

POST /api/rooms  { gameKey: "idiom-chain" } -> 201   ← 人人对战不受影响
```

### 浏览器

`/g/gomoku/lobby` 有「人机对战」卡;`/g/idiom-chain/lobby` **没有**,活跃房间与排行榜都在。
375 px 下 `scrollWidth == clientWidth == 375`,无横向溢出 —— 而且这次列表里**有一间真房间**,
`generalize-lobby` 记过的那条:空列表上跑"无横向滚动"是白跑。

顺带量到一件本变更之外、但值得记的事:`/g/idiom-chain/lobby` **在成语接龙的 manifest 还是
`planned` 的情况下就已经完整渲染** —— 标题、房间列表、创建房间、排行榜,全靠路由参数加服务端
描述符。大厅这条接缝此前只有五子棋一个消费者,现在有了第二个,而它要的大厅代码是**零行**。

## 7. 这次修的不只是一个漏洞

### 同一个夹具缺陷,第三次

`GomokuRules.AiRegistry` 手写成 `{ GomokuAiFactory, TicTacToeAiFactory }`,注释写着
「与生产 DI 一致」。生产 DI 从 `add-xiangqi-ai` 起注册**三个**。于是整个
`Gewu.Application.Tests` 都跑在一个「象棋没有 AI」的世界里。

这是同一个缺陷第三次出现,而且这次就在上一次修好的那行**下面七行**:

1. `add-xiangqi` 删掉 `AllBuiltInRules()`(自称遍历注册表,数据源手写),造出 `BuiltInGameRules.All`。
2. `enforce-human-vs-human` 发现隔壁 `GomokuRules.Registry` 一模一样,接到 `All` 上。
   当时记的教训是「**造出机制不等于采用机制**」。
3. 现在:`GomokuRules.AiRegistry`,同一个文件,隔七行,同一句注释,同样漂着。

**而这次它差点把错误答案钉成规范。** 本变更要新增的正是一条"没有 AI 的棋种不许开 AI 房"的
遍历断言;用那个夹具写,它会断言象棋**没有** AI —— 一条全绿的、把生产行为写反的测试。
所以先造 `BuiltInGameAis.All`,DI 与夹具都从它取,并补一条断言两边键集合相等的用例。

### 一次推迟,理由对、代价估错

`web-lobby` 里那段"AI 卡无条件渲染,并且这不是疏漏"写下了自己的触发条件:

> 于是它留到第一个"有人人对战、但没有 AI"的棋种出现那天再做。

触发条件到了,而**推迟本身是对的** —— 为一个不存在的情况建一个测不了的分支确实是这个仓库
反复付过的账。错的是它对代价的估计:那段理由从头到尾在谈**卡片**,而 `POST /api/rooms/ai`
从来不看有没有大厅。**一条结论对着 Web UI 成立、对着 API 不成立** —— 与
`enforce-human-vs-human` 完全同一种错法,只是这次的赔付是 ELO。

## 8. 本变更**不**做的事

- **不清理既有的孤儿房。** 它们会自己超时结算(并再付一次 ELO)。本仓库没有生产数据,
  写一段一次性清理脚本是为零行数据服务。与 `enforce-human-vs-human` 同样只管建房时。
- **不给"既无人人对战、也无 AI"的棋种设计界面。** 今天没有这种棋种;
  `unavailable` 仍然只分「未登记」与「只有人机」两种。为不存在的情况造分支正是上面刚说过的那条。
- **不改 `ExecuteBotMoveCommandHandler` 那句 404。** 它现在是真正的兜底了 ——
  能走到那里只剩"库里有一间本变更之前留下的孤儿房"。改它的类型是一次单独的清理。
