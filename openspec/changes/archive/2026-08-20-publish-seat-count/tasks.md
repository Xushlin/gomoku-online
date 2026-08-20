# publish-seat-count — tasks

## 1. 服务端

- [x] `GameDescriptorDto` 加 `int SeatCount`(**非空**),投影自 `IGameRules.SeatCount`。
- [x] `The_dto_does_not_carry_WinLength` 会红 —— 它断言整个属性集合,注释说的就是
      「加字段时它会红,那正是想要的」。加上新字段。
- [x] `Every_field_mirrors_the_rules_instance` 补一行。
- [x] 一条断言:`SeatCount` 的取值集合里**同时**有 `2` 与大于 `2` 的值 ——
      只走到 2 的遍历在一个恒返回 2 的实现下是绿的。
- [x] `AiSmoke`:挖坑 / 斗地主报 3,五子棋报 2。

## 2. 客户端

- [x] `GameDescriptor` 模型加 `seatCount`。
- [x] 侧栏 `moreThanTwoSeats()` 改读 `capabilities.of(gameKey)?.seatCount`。
      **`GameCapabilitiesService` 一行不改** —— `of()` 已经返回整个描述符。
- [x] 泛化那一支把**空座位**也画出来(座位数已知之后才画得出)。
- [x] 描述符未到达时不猜:`room-page` 的 loading 里已经含 `!capabilities.loaded()`,
      核对它确实盖住侧栏。

## 3. 测试

- [x] 侧栏:`seatCount == 3` 且只坐两个人 → 走泛化支、不出现颜色词(**今天红的那一格**)。
- [x] 侧栏:`seatCount == 2` → 仍然说颜色。
- [x] 变异:`moreThanTwoSeats()` 退回读 `seats.length` MUST 红;投影写死 `2` MUST 红。
      每处变异都要**真的跑起来**。

## 4. 收尾

- [x] 前后端全绿、lint 干净、预算不红。
- [x] 起临时 API 真发一次 `GET /api/games` 核对 `seatCount`。
- [x] PR;合并后 `openspec archive publish-seat-count`。

## 5. 计划之外

- [x] **AiSmoke 里那段注释又错了一次,而错法完全相同。** 它上一版(add-wakeng 改的)说
      「`RoomSummaryDto` 至今只有 Black/White,触发条件是 add-web-wakeng」,而
      `fix-lobby-seats` 已经付了那笔账 —— 于是它连着两版都在描述**另一个 DTO 的**状态。
      现在那两条断言改成更窄、更诚实的一对:`White` **仍然只是 1 号座位**,而座位列表
      自己在 `Seats` 里 —— **两句话同时成立**,才说明那个字段是加上去的、不是把旧字段
      改了意思。**一条描述另一个 DTO 的注释,会在自己这个 DTO 被修好之后继续错着。**
- [x] **`The_dto_does_not_carry_WinLength` 按它自己的注释红了。** 它断言的是**整个**属性
      集合,注释写着「加字段时它会红,那正是想要的:对外契约多一个字段该是一次有意的决定,
      不是一次顺手的提交」。这次就是那个决定 —— 而它是本变更唯一一条**预告过**的红灯。
- [x] **一条只比 DTO 与规则是否一致的遍历,守不住「投影写死 2」。** `Every_field_mirrors_the_rules_instance`
      会红,但**只在注册表里真有一个座位数不是 2 的棋种时**才会。所以另加一条钉**样本**的:
      取值集合里 MUST 同时有 2 和大于 2 的值。与 `enable-xiangqi-human-play` 记的
      「一条只走一边的遍历会全绿地什么都不验」是同一条。
- [x] **`grep -c` 找不到东西时返回 1,把 `&&` 链掐断了**,于是那次 smoke 根本没跑而我去 tail
      一个不存在的文件。与本文件已记的「管道会吃掉你想量的退出码」同族:
      **一个用来验证的命令,自己的退出码也会说话。**
