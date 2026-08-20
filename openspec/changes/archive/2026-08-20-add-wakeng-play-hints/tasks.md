# add-wakeng-play-hints — tasks

## 1. 一个函数,两个消费者

- [x] `WakengFollows.For(hand, onTable)` —— 全部合法出法,按先弱后强。
- [x] `WakengFollows.CanFollow` = `For(...).Count > 0`,而**一条断言把两者钉在一起**。
- [x] 断言覆盖:自由首出、同型同张数更大、**跨型压不住**、以及**空列表 + 正面对照**。

## 2. 两个出口

- [x] `WakengSeatView.CanFollow` —— 定义写死成「假如此刻轮到你,你出得起吗」,
      **与轮次无关**(`ViewFor` 收的 `MatchState` 里根本没有当前回合)。
- [x] `GET /api/rooms/{id}/hints` —— 按需,只回答调用者自己那一份;围观者与非玩家拿到空。
- [x] handler 断言:候选只来自自己的手牌、逐项过 `TryRecognise` + `Beats`、
      **`canFollow` 与候选列表逐座位一致**、围观者拿不到、叫分阶段没有、非挖坑房间没有。

## 3. 客户端

- [x] `canFollow === false` + 轮到我 + 桌上有牌 → **自动发一手真的 `pass`**。
- [x] 提示按钮:第一次点去拉,再点轮换,到末尾绕回。
- [x] 断言两个方向 + 「一个回合最多过一次」。

## 4. 变异

- [x] 忽略 `canFollow` → 红(「出得起时不该替我过」那条)。
- [x] 去掉「一个回合最多一次」的哨兵 → **第一次活了下来**,因为同步的两次 `detectChanges`
      是被 `submittingMove` 挡住的 —— 测试测的是另一个守卫。两次之间 await 之后它红了。
- [x] 底牌那两条(上一个 PR)与提示那三条(kittySize / compareForDisplay / showsFirstBidder)复核仍红。

## 5. 计划之外

- [x] **一条按位置取按钮的夹具指错了。** `card-table.spec.ts` 用
      `actionButtons(fixture).at(-1)` 拿「不要」,而加一个「提示」按钮就把它指到了新按钮上。
      改成按 `data-testid` 取 —— **一个按位置找元素的夹具,会在任何一次加按钮时静静指错。**
- [x] **房间页那条测试里的 `seatView` 我用字符串拼,留了个没被替换的 `%s`。**
      于是 JSON 解不出来,`parseView` 返回 null —— 而「不该自动过」那条**因此也是绿的**:
      它通过的理由是「解析失败」,不是「出得起」。改成 `JSON.stringify`,没有可拼错的东西。
- [x] 牌桌里我先写了一份 `shouldAutoPass`,又在房间页写了一份判断 —— **同一个决定两个家**。
      删掉牌桌那份:牌桌只在用户点击时发动作,而「替他过牌」是页面的决定。
