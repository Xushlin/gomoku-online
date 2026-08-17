# Tasks — add-idiom-chain-transport

## 1. 命令

- [x] 1.1 `MakeMoveCommand`:`Row` / `Col` 改 `int?`,新增 `string? Text`。
- [x] 1.2 Handler 按载荷选**恰好一个** `MoveIntent` 工厂,不再自己实现一遍不变量。
- [x] 1.3 Validator:坐标存在时非负(既有规则,加个存在判断);文本存在时非空白。

## 2. Hub

- [x] 2.1 `SayWord(Guid roomId, string word)` —— **第三个方法**,不是给 `MakeMove` 加可选参数。
- [x] 2.2 Hub 只搬参数,不知道哪个棋种收哪种载荷。

## 3. 测试

- [x] 3.1 Handler 三条:三种载荷各选对工厂 —— **这一条当时打勾打错了**,补在 #49。
      写下它的时候,`MakeMovePayloadTests` 从头到尾只驱动校验器,
      `MakeMoveCommandHandlerTests` 全是落子,三条 Scenario 一条都没有测试钉着。
      行为其实是对的 —— 一整局接龙走真实连接落进了库 —— 但那是端到端证据,不是这一层的,
      而端到端那条路只在有人手动跑的时候存在。**一次成功的手动验证会让人觉得这条测试已经有了。**
- [x] 3.2 Validator:负坐标仍失败(错误路径未变)、空白文本失败、坐标缺失时不被当成合法。
- [x] 3.3 既有 `MakeMoveCommandHandlerTests` 按新签名更新 —— **一行没改**。`Row` / `Col` 从
      `int` 变成带默认值的 `int?`,位置参数写法原样编译。这是"加可选参数"在 C# 里成立、
      在 SignalR 上不成立的那条分界:同一个变更里,两边的答案是相反的。

## 4. 验证

- [x] 4.1 `dotnet build` 0 warning;`dotnet test` 全绿。
- [x] 4.2 **走真实 SignalR 连接**下一整局接龙 —— 参数个数这类问题对每一层单测都是隐形的,这个仓库已经为它付过一次账(`generalize-match-domain`,由 AiSmoke 抓到)。
- [x] 4.3 同一条连接上验证一次**非法接法**:服务端回 `HubException`,携带 `invalid-move` 码。
- [x] 4.4 前端零改动。

## 5. 实测记录

跑法:全新 scratch SQLite,先以 Development 起一次(建表 + 灌词典,**30,895** 条),
再以 **Production** 起同一个库跑验证 —— `EnableDetailedErrors` 在那里是关的,而
`add-hub-error-codes` 修的正是"错误码只在 Development 到得了客户端"。两个真实
长轮询连接,两个真实账号。

| 探针 | 线上回帧 |
| --- | --- |
| 四手接龙,两条连接交替 | 全部 `result: null`(成功) |
| `风和日丽` 接 `令行禁止` | `HubException: invalid-move` |
| `止步不前吧`(不在词典) | `HubException: invalid-move` |
| `MakeMove(room, 7, 7)` 打进无盘面棋种 | `HubException: invalid-move` |
| 同一 build 的五子棋 `MakeMove(7,7)` | 成功,AI 应了第 2 手 |

落库的四手,`Row` / `Col` / `FromRow` / `FromCol` **全为 `NULL`**,`Text` 是成语;
同一张 `Moves` 表里的五子棋两手则是 `Row=7, Col=7` / `Text=NULL`。
`generalize-match-payload` 的迁移到这里才第一次被两种载荷同时验过。

### 参数个数:引用的那句话,这次是量出来的

本变更的 spec 里引了一句 `InvalidDataException: Invocation provides N argument(s)
but target expects M`。那句是从 `generalize-match-domain` 继承来的 —— 继承一句
实测结论和自己测一次,只有在它变了的时候才有区别。所以测了(Development,
详细错误开着):

```
SayWord 1 个参数 → InvalidDataException: Invocation provides 1 argument(s) but target expects 2.
SayWord 3 个参数 → InvalidDataException: Invocation provides 3 argument(s) but target expects 2.
MakeMove 2 个参数 → InvalidDataException: Invocation provides 2 argument(s) but target expects 3.
```

原话成立,并且多出一条原来没写下来的:**多一个参数也被拒**。这让"加参数"这条路
比原先记的更死 —— 不只是旧客户端少发一个会断,新客户端也没法先按新签名发着、
等服务端跟上。三个方法不是保守,是唯一能滚动升级的形状。

在 Production 那条连接上,参数个数错只回一句 `Failed to invoke 'SayWord' due to an
error on the server.`,而**按本仓库出厂的日志配置,服务端一行都不记**:
`appsettings.json` 把 `Microsoft.AspNetCore` 压到 `Warning`,SignalR 的参数绑定失败
低于这一级。准确的说法是"在配置的级别之上没有任何记录",不是"框架什么都不发" ——
但操作者手上就是这个配置。

对照才是关键:同一个方法的**领域**拒绝是记下来的(`[ERR] Failed to invoke hub
method 'SayWord'`)。所以日志里看得见一类拒绝、看不见另一类,而看不见的那类恰好
是签名不一致 —— 客户端只有"调用失败",服务端什么都没有。不是本变更引入的。

## 6. 留给 `add-web-idiom-chain` 的一条

服务端拒绝时,日志里的话是准的:

```
invalid-move — '风和日丽' must start with '止', the last character of '令行禁止'.
invalid-move — '止步不前吧' is not an idiom in the dictionary.
```

到客户端只剩一个 `invalid-move`。象棋可以这样 —— 不合法就是不合法,玩家看着盘面
能自己想明白。接龙不行:**"不是成语" / "接不上" / "说过了"是三种完全不同的纠正**,
只说"这步不合法"等于什么都没说。

这不是本变更的洞 —— `add-hub-error-codes` 的设计就是"传码不传散文",而这里三种
情况共用一个码是**成语接龙自己的粒度需求**,得由要显示它的那个变更来分。
