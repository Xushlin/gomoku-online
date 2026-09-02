## ADDED Requirements

### Requirement: 人机对局入口 SHALL 按 `supportsAi` 出现,MUST NOT 按一份清单

哪些棋种能开人机房,SHALL 读 `GameDescriptorDto.supportsAi`,MUST NOT 在客户端写一份「哪些棋种有 AI」的名单。

**那个字段存在的理由就是这件事:** 它投影自 `IGameAiRegistry.For(gameKey) is not null`,与 `POST /api/rooms/ai` 的校验读**同一份**注册表,所以客户端看到的与服务端会接受的不可能不一致。它自己的文档写着:一份手写副本的症状是**一个永远 400 的按钮**。

#### Scenario: 入口跟着描述符走,两个方向都要
- **WHEN** 某棋种 `supportsAi` 为 true
- **THEN** 它的大厅 MUST 有人机对局入口
- **AND** `supportsAi` 为 false 的棋种 MUST 没有 —— 少了后半条,一个「永远显示」的实现
  同样通过前半条,而它在那些棋种上就是那个永远 400 的按钮

---

### Requirement: 不支持人人对战的棋种 MUST NOT 显示建房入口

`supportsHumanVsHuman` 为 false 的棋种,大厅 MUST NOT 显示建房入口,MUST 显示「目前只有人机对战」。

**这是一个实测出来的、已经存在的隐患:** 大厅现在**无条件**显示建房 FAB,而

```
POST /api/rooms  {"gameKey":"tictactoe"}
→ 400  "'tictactoe' has no human-vs-human mode on this platform."
```

今天够不着(一字棋在目录里是禁用的),**而它一有 renderer 就够得着了** —— 所以这一条 MUST 与一字棋的 renderer **同一笔落地**。

#### Scenario: 一字棋的大厅没有建房按钮
- **WHEN** 打开一字棋的大厅
- **THEN** MUST NOT 有建房入口,MUST 显示 `lobby.game-lobby.unavailable.ai-only-title`
- **AND** MUST 有人机对局入口

#### Scenario: 五子棋的大厅两个都有
- **WHEN** 打开五子棋的大厅
- **THEN** MUST 同时有建房入口与人机对局入口
- **AND** 这一条与上一条 MUST 同时存在:少了它,一个「永远隐藏建房」的实现同样通过

---

### Requirement: 一字棋 SHALL 复用五子棋的棋盘,而注册表 MUST 允许一个 renderer 服务多个棋种

一字棋 SHALL 用与五子棋**同一个** renderer,按描述符给的 3×3 渲染。

服务端把它注册成 `NInARowRules("tictactoe", 3, 3, 3)`,一行胜负判定都没自己写;平台自己的说法是「一字棋是缩小的五子棋,同一套读法」。星位**已经是从尺寸推出来的**,3×3 推出空集。

**注册表因此第一次不是一对一。** 「启用条数 == 注册表键数」那条不变量按**键**算,所以仍然成立 —— 但「一个 renderer 只服务一个棋种」此前是巧合,而巧合不该被下一个人当成保证。

#### Scenario: 两个键指向同一个 renderer
- **WHEN** 检查棋盘注册表
- **THEN** 一字棋与五子棋 MUST 映射到同一个 renderer 实例
- **AND** 目录里启用的条数 MUST 仍然等于注册表的**键**数

#### Scenario: 3×3 上没有星位
- **WHEN** 以 3×3 渲染
- **THEN** MUST 没有任何装饰落在盘面之外,也 MUST 没有星位
- **AND** 判据 MUST 是渲染出来的像素或推导出来的行号,MUST NOT 是「代码看起来对」

---

### Requirement: 人机房 SHALL 走 `POST /api/rooms/ai`,难度与执边显式给出

请求体为 `{name, difficulty, gameKey, humanSide}`;`difficulty` 是 `Easy` / `Medium` / `Hard`,`humanSide` 是 `Black` / `White`。三者 MUST 由调用方给出。

**路由 MUST 先对着 controller 的特性核过,再写代码。** 上一笔在解散那条路由上猜错了(`POST .../dissolve` 不存在),而旁边的单测断言了那个错的并且通过 —— 假 adapter 接受任何 URL。`test/room_route_contract_test.dart` 是那次补上的机制,这一笔 MUST 把新路由也纳入它。

难度写错时服务端返回 **400**,但那是一个**绑定层**错误(`"The body field is required"` 加 `$.difficulty` 的转换失败),不是领域错误 —— 客户端 MUST NOT 试图从它里面取字段级消息。

#### Scenario: 建出来的房间当即可下
- **WHEN** 建一个人机房
- **THEN** 服务端 MUST 返回 201,状态 MUST 是 `Playing`,两个座位 MUST 都坐上人(一个是 `AI_<难度>`)
- **AND** 判据 MUST 是服务端返回的房间,MUST NOT 是「调用没抛异常」

---

### Requirement: 人执白时,AI 的第一步 SHALL 自己出现在屏幕上

选「执白」时,棋盘开局是空的,**AI 的第一步经 hub 推送到达**。客户端 MUST 不需要任何交互就把它画出来。

**这是实测的,不是推的:** 创建响应里 `moves=0`,八秒后那个房间有 1 步 `(0,0) seat=0`、`currentSeat=1`。**AI 是异步走的。**

**所以 `fix-mobile-hub-inbound` 是这一条的前置条件** —— 在入向修好之前,选执白会看到一块永远空的棋盘,而那看起来像 AI 坏了。

#### Scenario: 一下都不碰屏幕
- **WHEN** 建一个执白的人机房,然后什么都不做
- **THEN** AI 的那一子 MUST 自己出现,回合 MUST 变成我
- **AND** 判据 MUST 是屏幕上那一子,MUST NOT 是服务端有那一步 —— 后者只证明服务端动了,
  不证明客户端听得到,而这个区别上一轮刚付过一次学费

#### Scenario: 执黑时轮到我
- **WHEN** 建一个执黑的人机房
- **THEN** 回合 MUST 是我,盘面 MUST 是空的
- **AND** 这一条与上一条 MUST 同时存在:少了它,一个「总是等 AI」的实现在执黑时会永远等
