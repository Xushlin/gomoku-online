# tasks — per-game-seat-labels

## 0. 先把「这不是合规修复」这件事钉住

- [ ] 记下判据:`web-game-board` 的操作条要求**明文规定**了两座位棋种读 `black-turn` / `white-turn`,所以代码是**合规的**,而规格是错的 —— 因此必须走提案,不能当 bug 直接改。
- [ ] 侧栏那条要求里的「(象棋读作红 / 黑)」是一个**没有机制的括号**,而同一条的 Scenario 与它相反。两处都要改,否则改完还是自相矛盾。

## 1. manifest 与解析

- [ ] `GameManifest.seatLabelKeys?: readonly string[]`。
- [ ] 五子棋 / 一字棋 `['game.seat.black','game.seat.white']`;象棋 `['game.seat.red','game.seat.black']`;成语接龙 `['game.seat.first','game.seat.second']`;斗地主 / 挖坑 **不填**。
- [ ] 顺带把 `companionRoomKeys` 补进那条「唯一声明形状」的要求 —— 它上个变更漏了。
- [ ] `GameCatalogService.byRoomKey(key)`:匹配 `key` 或任一 `companionRoomKeys`。
- [ ] **变异:让 `byRoomKey` 退化成 `byKey`** → 残局房那几条必须红(否则伴生键那一支恒真)。
- [ ] 一条断言:`byKey('xiangqi-endgame')` MUST 是 `undefined`(残局不上目录页),而 `byRoomKey` MUST 给出象棋 —— **两个方向都要**,否则「不能合成一个」这句话没有证据。

## 2. 三处调用点用同一份解析

- [ ] 一个共享函数,侧栏 / 操作条 / 回放标题区都调它。
- [ ] **删掉操作条的「座位数大于二」分支** —— 三座位就是「没声明席位名」。删得掉是这个设计对的证据;删不掉说明判据还没换对。
- [ ] 大厅房间行的纹章改用 `byRoomKey`,删掉那张私拼的伴生键表。
- [ ] `seatCount` 仍然决定侧栏**画几行**(含空座位)—— 别把这个也换掉。

## 3. i18n

- [ ] 新增 `game.seat.{black,white,red,first,second}`、`game.turn.side-turn`(`{{side}}`)、`game.room.seat-n`(`{{seat}}`)。
- [ ] 退役 `game.room.seat-{black,white}`、`game.turn.{black,white}-turn`,**且不许重用这些键名**。
- [ ] 两份 locale 齐备;parity 测试本来就在。

## 4. 测试

- [ ] 遍历 `GAME_REGISTRY`:每个声明的键在两份 locale 都存在且非空。
- [ ] 走查两支都在样本里(声明 / 不声明各至少一个),席位名组合**恰好三种**。
- [ ] 侧栏:象棋房说「红方 / 黑方」且**不出现「白方」**;成语接龙说「先手 / 后手」;斗地主说座位号;五子棋一字不变。
- [ ] 操作条:**一条断言读整句** —— 拼出来的文案要整句断言,不能分别断言两段(`add-xiangqi-endgames` 的「谱评:谱评:黑优」是这么漏掉的)。
- [ ] 回放标题区:象棋回放说红黑。
- [ ] **变异各一条**:席位名解析恒返回黑白 → 象棋那几条必须红;缺省改回黑白 → 斗地主那条必须红。
- [ ] 每条变异先确认 `ng build` 0 error —— 模板里一个 `@if (false)` 会产出「exit 1 没跑测试」,而那读起来像击杀。

## 5. 在真浏览器里看

- [ ] 一个象棋房:侧栏「红方 / 黑方」,回合指示「红方走棋」,页面上**搜不到「白方」**。
- [ ] 一个成语接龙房:「先手 / 后手」。
- [ ] 一个斗地主房:座位号,而且**等待中只坐两人时也说座位号**(那是 `seats.length` 判据留下的坑)。
- [ ] 375 px:先读 `innerWidth` 再决定量不量得到;「红方走棋」比「黑方走棋」不长,但成语接龙的「先手」更短,所以要看**最长**的那一种。

## 6. 收尾

- [ ] 后端不动;前端全绿,lint 0,build 0;初始包与归因(应当 ±0,只是键名换了)。
- [ ] `openspec validate per-game-seat-labels --strict` 绿。**而那个绿只证明形状** —— 六条 MODIFIED 都是从 live spec 抽出来改的,不是重打的,所以无关句子不会被静默回退。
- [ ] 把「回放丢掉三座位」那个缺陷记进 `CLAUDE.md` 的延期表,并写清触发条件。
