# add-web-doudizhu

## Why

斗地主的规则、传输、按座位可见都通了 —— 剩下的是**没有一张牌画在屏幕上**。
这是这个棋种的最后一块,也是平台的第八个游戏。

## What Changes

- `games/doudizhu/`:棋种键、manifest(`status: 'available'`,落在 `/g/doudizhu/lobby`)、
  `cards.ts`(解码)、`seat-view.ts`(解 `seatView`)、`card-table/`(牌桌)。
- `RoomPage` 第四个分支 `@else if (isDoudizhu())`。
- **`mySide` 换成 `mySeat`**:房间页与侧栏都改读座位号。

## 三个决定

**一、牌桌收 `mySeat: number | null`,不收 `mySide`。**
`'black' | 'white' | 'spectator'` 对第三个座位无话可说 —— 2 号座位上的人在那套词汇里是
`'spectator'`,于是牌都不给他画、辞局与离开按钮也不给他。座位号是线上契约自
`generalize-match-contract` 起就说的东西,而颜色只是棋盘家族在显示层的读法。
`RoomPage.mySeat` 读 `seats`,而 `mySide` 由它派生 —— 同一个事实两处读法就是两个真源。

**二、牌桌不判任何合法性。**
`add-web-klotski` 的尺子:不问"客户端该不该知道规则",而问"知道了会不会造出一个能与服务端
分叉的第二真源"。斗地主**整个落在不该知道的一侧** —— 牌型识别(单/对/三带/顺子/连对/飞机/
四带二/炸弹)加压牌比较都在服务端,再写一遍就是一份会悄悄分叉的第二真源,而分叉在玩家眼里
是"这游戏有 bug"。只做**不需要规则**的事:选自己的牌、非自己回合只读、首出不能过牌
(桌上没牌就是没牌 —— 这正是"客户端判得出"那一侧的边界)。

**三、`cards.ts` 是服务端字母表的一份副本,而它可以被接受。**
不解码就没有 UI(服务端送的是 `myHand: "ABDFI…"`)。按这个仓库自己的尺子 ——
**一份副本能不能接受,看的不是它多小,而是它错了会不会有人发现** —— 错一个字符,牌面上立刻是
一张错的牌,是最显眼的一种坏。而它 MUST NOT 反过来用于判断。

## Impact

- Affected specs: **新增 `web-doudizhu`**;`web-game-board`(侧栏改读座位)
- Affected code: `games/doudizhu/*`、`room-page`、`sidebar`、`room.model.ts`(`seats` / `seatView`)、
  `games/index.ts`、两份 locale
- 后端:**零改动**
