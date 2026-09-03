# fix-mobile-game-over

一局下完了,手机端**什么都不说**。补上。

## 在真机上发现的,而客户端把同一件事扔了两次

用户在自己手机上下完一局,**界面停在那**:棋盘还在,点哪儿都没反应(服务端在拒),没有任何一句话说赢了输了。

查下去两个成因,各自独立:

1. **服务端每一份房间快照都带着结果** —— `GameSnapshotDto` 有 `Result` / `WinnerUserId` / `EndReason` / `EndedAt`,而客户端的 `GameSnapshot.fromJson` **只解析 `moves` 和 `currentSeat`**。
2. **hub 的 `GameEnded` 推送订阅了,却推进一个没有任何人消费的 `_errors` 流。**

所以数据到了两次,被扔了两次。

## 一个来源,不是两个

从源码量出来的顺序(`MakeMoveCommandHandler` 与 `ResignCommandHandler` 都一样):

```
SaveChangesAsync            <- 结果已经写进聚合
RoomStateChangedAsync       <- 这一份带着 Result / WinnerUserId / EndReason
GameEndedAsync              <- 之后才发
```

**所以 `RoomState` 就够了。** 于是这一笔**删掉** `GameEnded` 订阅和那条 `_errors` 流 —— 两个来源描述同一件事,正是这个仓库反复付账的形状;而**最好的机制是能被删掉的那种**。

`hub_contract_test` 的订阅数会从 3 变回 2,走查仍然绿(它断言的是「订阅的 ⊆ 服务端发的」)。

## 文案一个键都不用加

包里全都有:

```
game.ended.title-win 你赢了!   title-lose 你输了。   title-draw 平局。
game.ended.reason-decided / -resigned / -timeout
game.ended.back-to-lobby 返回大厅    game.ended.dismiss 重新查看
```

`dismiss` 那个键(「重新查看」)本身就说明了形状:**一个可以关掉、关掉之后还能看棋盘**的东西,所以是对话框而不是一块永久占位的横幅。

## 赢还是输,靠 `WinnerUserId` 和我自己的 id 比

`GameResult` 只有 `Ongoing=0` / `Decided=1` / `Draw=3`。`Decided` 时拿 `winnerUserId` 与 `AuthRepository.currentUser.id` 比 —— **按 id,不按用户名**:用户名是显示名,这个平台已经为「把显示名当身份」付过两次账。

## 一处文案与现实的错位,先记下不改

`game.ended.reason-resigned` 写的是「对手认输。」—— 那是**从赢家视角**写的。手机端今天没有认输按钮,所以你只可能作为**赢家**看到它;但如果将来手机端能认输,输的那一方会看到一句说反了的话。**改文案要动 web 端那份产物**,是另一笔账。触发条件:手机端上认输。

## 不做

- **认输**。它是另一个出口,而平台上三座位棋种的认输本来就还欠着(见 `CLAUDE.md` 那张表)。
- **回放**。`game.ended.view-replay` 那个键在包里,但手机端没有回放屏。触发条件:手机端上回放。
- **429 没有映射**(限流显示成「出了点问题」)—— 真机上撞到过,但它属于错误映射那一摊,不属于对局结束。

## 规模

模型四个字段、一个 outcome 计算、一个对话框、删掉一条流。**估计 200–300 行**,大头是测试:赢 / 输 / 和 / 未结束四个方向,和一局真的下到底。
