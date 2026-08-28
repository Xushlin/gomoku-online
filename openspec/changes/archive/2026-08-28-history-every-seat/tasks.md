# tasks — history-every-seat

## 0. 顺序

- [x] 本变更**叠在 `replay-every-seat` 之上**(PR #153)。它的 `game-replay` delta 已经改过
      `GET /api/users/{id}/games` 那条要求,所以本变更的 delta 基底 SHALL 从**它那份**里抽,
      不是从 live spec 抽 —— 两份未归档变更改同一条要求,MODIFIED 是整条替换。
      这个坑本轮已经踩过一次(`per-game-seat-labels`),不重复踩。

## 1. 契约

- [x] `UserGameSummaryDto`:删 `Black` / `White`,加 `Seats`。
- [x] `GetUserGamesPagedQueryHandler` 走 `r.ToSeatDtos(usernames)` —— 复用 `replay-every-seat`
      抽出来的那份,**不新写第四份座位投影**。
- [x] 删掉 `var whiteId = r.WhitePlayerId!.Value;`。

## 2. 后端测试

- [x] 三座位那条 `Seats.Count == 3`(**恰好**),两座位那条 `== 2`,**两支同时在样本里**。
- [x] 同一份分页响应里两种对局都有 —— 否则「每个座位都在」在单一形状的样本上恒真。
- [x] **变异**:handler 只投影前两个座位 → 三座位那条必须红、两座位那条必须绿。

## 3. 前端:对手们

- [x] `user-profile.model.ts` 的 `UserGameSummaryDto`:`black`/`white` → `seats: readonly RoomSeat[]`
      (复用 `room.model.ts` 的 `RoomSeat`,不新造)。
- [x] `my-recent-games` 与 `games-list` 的 `opponentOf` → `opponentsOf`,返回除本人以外的每一个。
- [x] 两处模板 `@for` 渲染,数量由数据决定。

## 4. 前端:说不出的那一格

- [x] `resultKey` 从三支改四支,第四支 `profile.result-unrecorded`。
- [x] i18n 两份 locale 各加一个键。
- [x] **两处消费方 MUST 共用同一个判据函数** —— 两份副本会分叉,而症状是同一局对局在大厅说
      「负」、在个人主页说「说不出」。抽到一处,两处都从那里读。

## 5. 前端测试

- [x] 三人局**恰好**两个对手链接,href 互不相同,且都不是本人。
- [x] 两人局**恰好**一个。两支同时存在。
- [x] 三人局赢家不是本人 → `result-unrecorded`,**MUST NOT** 是 `result-loss`。
- [x] 三人局赢家是本人 → `result-win`(说得出就要说)。
- [x] **反面控制**:两人局赢家不是本人 → 仍是 `result-loss`,第四支没把它吞掉。
- [x] 一条断言证明两处消费方对同一局给出同一个键。

## 6. 收口

- [x] `dotnet build` + `dotnet test` 全绿(**不加 `--no-build`**)。
- [x] `npm run lint` + `ng test --watch=false` 全绿;对比度读数不下降。
- [x] `openspec validate history-every-seat --strict`。
- [x] 浏览器:一个参与过三人局的用户,大厅卡片 + 个人主页两处都看一眼,375 px 用
      20 字符用户名(两个对手 + 结果那一格是这一行最长的真实内容)。

## 7. 量到的

- **后端**:三座位那条 `Seats.Count == 3`、两座位 `== 2`,**同一份分页响应里两种形状都在**。
  变异(只投影前两个座位)只杀三座位那条,两座位那条保持绿。后端 1578 绿。
- **前端变异两次**:把第四支去掉 → 三处断言红(helper + 两个消费方);
  把 `opponentsOf` 截成一个 → 五处红。两座位的反面控制两次都是绿的,
  所以「说不出」没有变成一个把所有人胜负都变模糊的开关。前端 1032 绿 / 86 文件。
- **真后端 + 真浏览器**,而且样本里三支同时在场(一页看得全):

  | 对局 | 座位 | 我是不是赢家 | 显示 |
  | --- | --- | --- | --- |
  | ddz-decided | 3 | 否(赢家是地主) | 两个对手 · **Not recorded** |
  | gomoku-two-seat | 2 | 否 | 一个对手 · **Lost**(反面控制) |
  | ddz-long-names | 3 | 流局 | 两个对手 · **Drew** |

  **大厅卡片与个人主页对这三局给出完全一致的答案** —— 规格里那条「两处 MUST 一致」
  在浏览器里成立,不只是结构上共用了一个函数。

## 8. 顺带修的一处既有缺陷(不是本变更改出来的)

**个人主页头部的用户名在 375 px 下把整页撑破**:20 字符用户名(注册上限)
`scrollWidth 413 / clientWidth 375`。溢出的是 `<h1>` 里的用户名 span,**不在战绩列表里**
(`inGamesList: false`),所以与本变更无关 —— 只是量战绩列表时撞见的。

**而它比看起来难一点,这一点是量出来的:** 先加了 `break-words`,没用 ——
那是个 flex item,`min-width: auto` 让它保持在 max-content 宽度(实测 352 px),
`overflow-wrap` 根本没机会生效。要 `min-w-0` **加** `break-words` 两个类。
「以为一个类就够了」在这里会得到一个看起来修过、实际没修的模板。
