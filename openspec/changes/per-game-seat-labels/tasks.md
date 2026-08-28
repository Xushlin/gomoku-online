# tasks — per-game-seat-labels

## 0. 先把「这不是合规修复」这件事钉住

- [x] 判据:`web-game-board` 的操作条要求**明文规定**两座位棋种读 `black-turn` / `white-turn`,所以代码是**合规的**、规格是错的 —— 因此走提案,不当 bug 直接改。
- [x] 侧栏那条要求里的「(象棋读作红 / 黑)」是一个**没有机制的括号**,而同一条的 Scenario 与它相反。两处都改了,否则改完还是自相矛盾。

## 0.5. 与另一个变更撞在同一条要求上

- [x] `web-replay` 的「标题区元信息使用用户名链接组件」**同时被另一个变更改**(回放丢三座位,单独会话在做)。**MODIFIED 是整条替换**,后归档的那个必须手工合并。
- [x] 本变更对这条要求只删了 3 行、加了 1 个 Scenario,碰撞面已压到最小;delta 是**从 live spec 抽出来打补丁**得到的,不是重打的。
- [ ] **若那个变更先归档:本变更的 `web-replay` delta 要重新从新的 live 文本抽一遍。** 这一条留着不勾 —— 它是给合并那天的人看的,不是给今天的。

## 1. manifest 与解析

- [x] `GameManifest.seatLabelKeys?: readonly string[]`。
- [x] 五子棋 / 一字棋 `['game.seat.black','game.seat.white']`;象棋 `['game.seat.red','game.seat.black']`;成语接龙 `['game.seat.first','game.seat.second']`;斗地主 / 挖坑 **不填**。
- [x] 顺带把 `manualRoute` / `manualLabelKey` / `companionRoomKeys` 补进那条「唯一声明形状」的要求 —— **它此前漏了三个字段**,是后续变更加的而这条没跟上。一份声称自己是「唯一形状」的要求一旦不全,它就只是一份注释。
- [x] `GameCatalogService.byRoomKey(key)`:匹配 `key` 或任一 `companionRoomKeys`。
- [x] **变异:`byRoomKey` 退化成 `byKey`** → **4 红**(含大厅纹章 —— 它现在与席位名共用同一份解析)。
- [x] 两个方向都断言:`byKey('xiangqi-endgame')` 是 `undefined`,`byRoomKey` 给出象棋。

## 2. 三处调用点用同一份解析 —— 实际是**四**处

- [x] 共享函数 `seatNaming(manifest, seat, seatCount)`,侧栏 / 操作条 / 回放标题区都调它。
- [x] **第四处是 grep 出来的**:成语接龙自己的对局列表给每一手贴「黑方 / 白方」,而那个棋种连棋盘都没有。**改完一处就 grep 兄弟**,这次又付了钱。
- [x] **删掉操作条的「座位数大于二」分支** —— 删得掉是判据换对了的证据。侧栏两支也合成一个循环,`room.black` / `room.white` 最后两个读者随之消失;`FIRST_SEAT` 那个只为已删分支存在的常量也删了。
- [x] 大厅房间行的纹章改用 `byRoomKey`,删掉那张私拼的伴生键表。
- [x] `seatCount` 仍然决定侧栏**画几行**(含空座位)。
- [x] **一条量出来才加的规则:全有或全无。** 第一版逐格判断,于是声明两个名字、却有三个座位的棋种渲染出「黑方 / 白方 / 第 3 位」——**半边有名字半边没有,读起来像第三个人不算玩家**。条数对不上就整间房说编号。

## 3. i18n

- [x] 新增 `game.seat.{black,white,red,first,second}` 与 `game.turn.side-turn`(`{{side}}`)。
- [x] 编号那两个键**复用**既有的 `game.room.seat-label` / `game.turn.seat-turn`,不新造。
- [x] 退役 `game.room.seat-{black,white}`、`game.turn.{black,white}-turn`,且有一条断言钉着它们**不许再出现**。
- [x] 两份 locale 齐备;parity 测试本来就在。

## 4. 测试

- [x] 遍历 `GAME_REGISTRY`:每个声明的键在**真的 locale 文件**里存在且非空(手写小词典守不住这条)。
- [x] 两支都在样本里(声明的**恰好**四个,不声明的至少一个)。
- [x] 侧栏:象棋 / 残局说「红方 / 黑方」且**不出现「白方」**;成语接龙说「先手 / 后手」;斗地主说座位号;五子棋一字不变。
- [x] 操作条:**一条断言读整句**,并额外断言席位名只出现一次(拼重的实现在分段断言下是绿的)。
- [x] 五个受影响的 spec 全部注入**真的** `DefaultGameCatalogService`,不是桩 —— 桩会让它们在「象棋没有席位名」的世界里跑。
- [x] **变异各一条**:象棋不声明席位名 → 5 红;去掉全有或全无 → 1 红;`byRoomKey` 退化 → 4 红。三条都先确认 `ng build` 0 error。
- [x] 一条既有断言的**行为**被改掉并写清楚:描述符没到时侧栏此前画两个人,现在一个座位都不画。

## 5. 在真浏览器里看

- [x] 象棋房:侧栏「红方:lbl1 黑方:lbl2」,操作条「红方回合」,**整页搜不到「白方」**。
- [x] 成语接龙房:「先手 / 后手」「先手回合」,搜不到「黑方」「白方」。
- [x] 斗地主房:**等待中只坐一人**时仍是「1 / 2 / 3 号座位」—— 那正是 `seats.length` 判据留下的坑。
- [x] 375 px 用**最长**的那一种文案量(英文 "Second player" / "First player to play"):`innerWidth` 375,`scrollWidth == clientWidth == 375`,零元素越界。

## 6. 收尾

- [x] 后端不动;前端 993 → **1010** 绿,lint 0,build 0;初始包 410.19 → **410.49 kB**(预算 480)。
- [x] `openspec validate per-game-seat-labels --strict` 绿。**而那个绿只证明形状** —— 五条 MODIFIED 都是从 live spec 抽出来打补丁的,不是重打的。
- [x] 「回放丢掉三座位」进了 `CLAUDE.md` 延期表,并写明有一个单独变更在做。
- [x] 两条新的坑进了 `CLAUDE.md`:括号里的答案没有机制;稳态下正确的判据可能在用近似量回答问题。
