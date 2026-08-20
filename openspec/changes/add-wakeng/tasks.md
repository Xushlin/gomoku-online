# add-wakeng — tasks

## 1. 共享那一小块必要的东西

- [x] `Games/Cards/CardPlay.cs`:`Decode(encoded, context)` → 非空牌列,畸形输入抛
      `InvalidMoveException`(内含原 `FormatException`)。文档写清它 MUST NOT 下沉到
      `Card.DecodeMany`(两个 `Deal.Decode` 要的正是 `FormatException`)。
- [x] `DoudizhuMove.Parse` 改调它 —— 净删代码。斗地主原有错误消息若被断言,保持不变。
- [x] `CardPlayTests`:`play:!!!` / `play:AA` / `play:` 三种畸形输入,**两个游戏各走一遍**,
      都必须是 `InvalidMoveException` 而不是 `FormatException`。

## 2. 挖坑的一步棋

- [x] `WakengMove` + `WakengMoveKind`:`bid:0`…`bid:3` / `pass` / `play:<cards>`。
      `bid:` 后 MUST 恰好一位数字(持久化格式只该有一种写法)。
- [x] `WakengMoveTests`:编解码往返、越界叫分、`pass` 与 `play:pass` 的区分、
      标签缺失被拒。

## 3. 局面重建

- [x] `WakengTable.Reconstruct(MatchState)`:阶段、首叫者(座位 + 那张 ♣)、挖坑者、叫分、
      已叫次数、三家手牌、底牌、桌面那一手、赢家。
- [x] 叫分结束的两条路径:有人叫 3(立即)/ 三家各叫过一次。三家都不挖 → **首叫者兜底 1 倍**。
- [x] 挖坑者收下 4 张底牌并排序 → 20 张。
- [x] 出牌阶段:连续两家过牌 → 桌面清空,轮回打出那一手的人。
- [x] 没有发牌的 `MatchState` MUST 大声坏掉,而不是发一手空牌。

## 4. 规则接内核

- [x] `WakengRules` 实现五个接口。`SeatCount = 3`、`SupportsHumanVsHuman = true`、
      `IsRated = false`(结构性理由,逐字继承斗地主)。
- [x] `FirstSeat(state)` = `WakengDeal.FirstBidder().Seat`。
- [x] `Apply`:叫分阶段只收 `bid`,出牌阶段只收 `pass` / `play`;叫分 MUST 高于当前最高
      (0 永远合法);出的牌 MUST 在手上、MUST 是合法牌型、跟牌 MUST 压得住。
- [x] **叫分结束时把出手权交给首叫者**,`MoveApplication.OngoingWithTurn(firstBidder)` ——
      两条路径都要,包括「有人叫 3 立即结束」那条(自然轮转会给错人)。
- [x] `MoveOnTimeout`:叫分 → `bid:0`;出牌 → 能过就过,首出则出最小单张。
- [x] `ViewFor`:自己的牌 + 三家张数 + 桌面 + 首叫者与那张 ♣(**公开**)+ 定下挖坑者之后的底牌。
      围观者与未入座者拿到空手牌。**基数不进视图**(理由见提案)。

## 5. 挖坑自己的测试

- [x] `WakengRulesTests`:叫分的两条结束路径、非法叫分、阶段串味(叫分阶段出牌 / 出牌阶段叫分)、
      不持有的牌、认不出的牌型、压不住的跟牌、首出不许过牌。
- [x] **「出手权回到首叫者」的断言 MUST 用一个首叫者不是 0 号的种子** —— 否则
      「turn == firstBidder」与「turn == 0」在同一个断言下不可区分,那条测试会因为别的理由通过。
      (`fix-three-seat-membership` 那条催促断言的同一个形状。)
- [x] `WakengVisibilityTests`:**逐张比对**「没有任何一个座位看得到别人的牌」——
      「我看得到我的 16 张」在一个把三家牌都塞进去的实现上也是绿的。附一条越界座位号的负控制。
- [x] `WakengThroughRoomTests`:真 `Room` 打一整局。含:
      - 坐两个人仍 `Waiting`,第三个人坐满才开局;
      - 开局的 `CurrentTurn` **就是首叫者**,而不是 0 号;
      - 每一步都以 `Text` 落库,四个坐标字段全 `NULL`;
      - `Game.Setup` 非空且等于 `CreateSetup(seed)`;
      - **超时兜底一路打到终局**(带上限的循环)—— 这是「MUST 推进对局」的可执行形式,
        而不是一段论证;
      - **`GameResult.Draw` 在这个棋种上永不出现**;
      - 源码级验收:`Rooms/` 零提及、`Games/Abstractions/` 恰好一行常量,各带文件集非空检查。

## 6. 六条走查按它们自己的注释改

- [x] `FirstSeatTests`:`No_built_in_game_picks_its_first_seat_yet` → 恰好一个(wakeng);
      `Every_built_in_game_still_starts_at_seat_zero` → **没有这个 seam 的**每个棋种仍从 0 开,
      并单独断言挖坑从它自己的首叫者开。
- [x] `GameSetupTests.Exactly_one_built_in_game_deals_a_setup` → 恰好两个。
- [x] `TurnFlowTests.Exactly_one_built_in_game_falls_back_on_timeout` → 恰好两个。
- [x] `GameSetupMigrationTests.Exactly_one_built_in_game_can_produce_a_non_null_setup` → 恰好两个,
      **并重估那笔账**(注释要求的),把结论写进注释与规格。
- [x] 全库搜一遍别的硬编码棋种计数 / 名单 —— 这个仓库已经三次栽在「一份手写清单冒充注册表」上。

## 7. 真 HTTP

- [x] `AiSmoke` 加挖坑的四条描述符事实(`supportsHumanVsHuman: true` / `supportsAi: false` /
      `isRated: false` / `rows: null, cols: null`)与两条建房事实
      (`POST /api/rooms` → 201、`POST /api/rooms/ai` → 400)。两半都量,因为
      `enforce-human-vs-human` 与 `enforce-ai-availability` 都是从一半推另一半栽的。
- [x] 起一个临时 API(不碰用户的 5145),真发一遍 `GET /api/games` 与两个建房请求。

## 8. 变异检查

- [x] `FirstSeat` 忽略发牌(恒返回 0)→ MUST 红。
- [x] 叫分结束时用自然轮转而不是首叫者 → MUST 红(这条要靠 §5 那个种子)。
- [x] `ViewFor` 忽略 `seat` 参数 → MUST 红。
- [x] 三家都不挖时不兜底(走斗地主的流局)→ MUST 红。
- [x] `Beats` 不检查「同型」→ 已在 `add-wakeng-cards` 里钉住,复核仍红。
- [x] 每一处变异都要**真的跑起来**:一个编译不过 / 抛异常的变异不是变异
      (本仓库已栽两次)。

## 9. 收尾

- [x] `dotnet test Gewu.slnx` 全绿;`openspec validate --specs --strict` 全绿。
- [x] PR;合并后 `openspec archive add-wakeng`。
- [x] CLAUDE.md 记录:接内核的验收标准、三处判断、以及「形状相同不等于事实相同」。

## 10. 计划之外发现的(都记下来,因为它们不是本变更造出来的)

- [x] **一条真缺陷:超时兜底会替人把最好的牌打掉。** 照抄斗地主的 `HandOf(seat)[0]` ——
      手牌按 `Card` 的自然序排,而那是**编码**顺序(3、4、…、K、A、2),恰好就是斗地主的
      大小顺序。挖坑是 `3 > 2 > A > … > 4`,所以手上有 3 的时候 `[0]` 是**最强**那张。
      改成按 `WakengRank.Strength` 取最小,并留了一条**需要前提**的断言(那手牌里必须有 3 或 2,
      否则两种实现给出同一个答案)。变异验过:1 条红 —— 而它是唯一那条。
      这与 `hoist-card-model` 修 `CardRank` 注释是同一个巧合在**上面一层**又咬了一次。
- [x] **一条从来没有被实现过的 Scenario。** `game-rules-registry` 里
      「恰好一个内置棋种实现 `IPerSeatViewRules`」自 `add-doudizhu-visibility` 起就写在规格里,
      而 `backend/tests/` 下**一次都没有出现过这个接口名** —— 用阳性对照量过。现在有断言了。
      本仓库同一个缺陷的第四次。
- [x] **`IGameRules.SeatCount` 的注释写着「现有实现全部为 2」**,自 `add-doudizhu` 起就是假的。
      顺手改掉 —— 一句只描述现状的注释会在现状变化时静静过期,而没有任何机制会报告它。
- [x] **AiSmoke 里一段半过期的注释。** 它写着「add-doudizhu-visibility 付这笔账(DTO 加座位
      字段、SeatWire 删除)」—— 两件都做了,而那两条断言**照样是绿的**,因为它们看的是
      `RoomSummaryDto`,而**大厅列表那个 DTO 至今只有 `Black` / `White`**。于是三座位房间的
      第三个人在**大厅的房间行里**不出现:与 `add-doudizhu-table-visuals` 在侧栏修掉的是同一个
      缺陷的第三处。**触发条件:`add-web-wakeng` 要给一个三座位棋种画大厅。**
- [x] **变异测试的恢复步骤会骗过编译器。** `shutil.copy2` 保留 mtime,所以「恢复」之后源文件
      比 `obj/` 里的产物**更旧**,MSBuild 的增量判断认为无事发生 —— `dotnet build` 报 0 errors、
      什么也没编,接着的 `--no-build` 测的是**变异体**。它表现成两条测试莫名变红。
      这一次红的恰好是那两条一眼能认出的断言,所以三分钟就查到了;一个更隐蔽的变异会长得
      像一个真缺陷。与本文件已记的「`--no-build` 会跑磁盘上碰巧存在的那份二进制」同族:
      **一次成功而什么都没做的构建,和一次真的构建,长得一模一样。**
      两条只杀一条测试的变异因此**带强制重编重量了一遍**,结果不变。
