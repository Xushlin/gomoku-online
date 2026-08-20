# fix-lobby-seats — tasks

## 1. 服务端一个字段

- [x] `RoomSummaryDto` 加 `IReadOnlyList<RoomSeatDto> Seats`,与 `RoomStateDto` 同类型同形状。
      `Black` / `White` 保留(与 `RoomStateDto` 加 `Seats` 时的先例一致)。
- [x] 投影处照 `RoomStateDto` 的写法补上。
- [x] 断言:一个三座位房间的 summary 里 `Seats` 有三项,而 `White` 仍然只是 1 号座位 ——
      **两句话同时成立**才说明这个字段是加上去的、不是把旧字段改了意思。

## 2. 客户端两处读法

- [x] `RoomSummary` 模型加 `seats`。
- [x] `active-rooms` 行改成渲染在座玩家;删掉 `seat-black` / `seat-white` / `seat-empty`
      三个键的用法,新增 `players`。**行上不许出现颜色词**(`board-seats.ts` 自己的约束)。
- [x] `my-active-rooms.sideKey()` 查 `seats`;`you-are-black` / `you-are-white` 合成
      `you-are-seated`。
- [x] i18n 两份 JSON 同步;`i18n-parity.spec.ts` 必须仍然绿。

## 3. 测试

- [x] **遍历**断言:2 个座位与 3 个座位各走一遍,渲染出的玩家链接数 == `seats.length`。
- [x] 「第三个座位上的人不是观战者」。
- [x] 变异:渲染换回读 `black`/`white` MUST 红;`sideKey` 忽略 `seats` MUST 红。
      每一处变异都要**真的跑起来** —— 一个编译不过 / 模板编译失败的变异不是变异。

## 4. 浏览器

- [x] 375 px,**三个人名都在行上**时页面级 `scrollWidth - clientWidth == 0`,且没有任何元素
      `scrollWidth > clientWidth`。空列表 / 两个人名的行上这条检查是白过的。
- [x] 暗色下看一眼。

## 5. 收尾

- [x] 后端 + 前端全绿,lint 干净,bundle 预算不红。
- [x] PR;合并后 `openspec archive fix-lobby-seats`。

## 6. 计划之外

- [x] **一处既有缺陷,被最长用户名铺出来的数据翻出来。** `hero-card` 的 `<h1>`
      (「Welcome, <用户名>!」)在 375 px 下横向溢出 1 px —— 用户名上限是 20 个字符,
      而一个 20 字符的名字里没有一个换行机会:335 px 的文字挤在 293 px 的盒子里。
      加一个 `break-words`。**它此前每一次 375 px 检查都白过**,因为那些检查用的是
      alice / bob。与 `guard-long-content-wrapping` 修聊天面板和成语列表是同一族,
      而这一条是那次漏掉的第三处。
- [x] **名字之间的分隔符是量出来才加的。** 第一版靠外层 flex 的 `gap-x-1`(4 px),而
      两个 20 字符的名字挨在一起读不开(`B…WWWWC…WWWW`)。改成 `·` —— 它本来就是这一行
      的分隔符,语言中立,**不是**一个显示字符串。第一版曾经写过 `、`,那是中文标点,
      而模板里不许写死显示字符串。
- [x] **第五处在浏览器里被确认了**(见提案):一个两人在座的斗地主房间,侧栏原文是
      `Black: Baa11… White: Caa11…`。它留给 `publish-seat-count`。
- [x] **一个 Playing 的三座位房间会在约一分钟内自己消失**,而那让「大厅里看三个人名」
      一开始扑空:超时兜底连叫三次 `bid:0` → 流局 → `Finished`,而列表过滤掉 Finished。
      重新铺一次数据、立刻看,就量到了。**这不是缺陷,是斗地主的规则在起作用** ——
      记下来是因为下一个人也会扑一次。
