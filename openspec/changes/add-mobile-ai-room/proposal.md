# add-mobile-ai-room

手机端能和机器人下棋。**一字棋是逼出这件事的那个棋种,而这一笔真正加的是「人机房」。**

## 量出来的五件事

### 一、一字棋不需要新棋盘

服务端把它注册成 `NInARowRules("tictactoe", 3, 3, 3)`,**一行胜负判定都没自己写**;web 端的 manifest 直说「一字棋是缩小的五子棋,同一套读法」,座位名也复用 `game.seat.black` / `game.seat.white`。

所以 `GomokuRenderer` 按 3×3 画就是对的:子落在交叉点上,而星位**已经是从尺寸推出来的**(`starLines(3,3)` 返回空 —— `add-mobile-game-catalog` 里量过并钉住了)。

**注册表因此第一次不是一对一:两个键指向同一个 renderer。** 那条「启用条数 == 注册表键数」的不变量按**键**算,所以仍然成立 —— 但它值得被一条断言写下来,因为「一个 renderer 只服务一个棋种」此前是巧合。

### 二、大厅那个建房按钮对一字棋是个永远 400 的按钮

实测:

```
POST /api/rooms  {"gameKey":"tictactoe"}
→ 400  "'tictactoe' has no human-vs-human mode on this platform."
```

大厅现在**无条件**显示建房 FAB。今天够不着(一字棋在目录里是禁用的),而**它一有 renderer 就够得着了** —— 所以「只有人机对战」那个状态必须和 renderer **同一笔落地**,不能等下一笔。

文案全在包里:`lobby.game-lobby.unavailable.ai-only-title`(「目前只有人机对战」)/ `-body` / `-cta`,以及整套 `lobby.ai-game.*`(难度、执边、错误)。**一个键都不用加。**

### 三、人机房的契约

`POST /api/rooms/ai` `{name, difficulty, gameKey, humanSide}` → **201**,`status=Playing`、`currentSeat=0`、`moves=0`。座位实测:

| `humanSide` | 座位 |
| --- | --- |
| `"Black"` | 0 = 我,1 = `AI_Medium` |
| `"White"` | 0 = `AI_Hard`,1 = 我 |

难度写错是 **400**,但它是一个**绑定层**错误(`"The body field is required"` + `$.difficulty` 的 JSON 转换失败),不是领域错误 —— 所以客户端 MUST NOT 试图从它里面取字段级消息。

### 四、人执白时 AI 先走,而它是**异步**走的

**创建响应里 `moves=0`,八秒后那个房间有 1 步 `(0,0) seat=0`、`currentSeat=1`。**

所以选「执白」时棋盘**开局是空的,AI 的第一步靠 hub 推送到达**。这让 `fix-mobile-hub-inbound` 成为这一笔的**前置条件**:在那之前选执白会看到一块永远空的棋盘。

**而这正好给出最锋利的判据:建一个执白的人机房,一下都不碰屏幕,AI 的那一子必须自己出现。**

### 五、同一条路给五子棋和象棋也免费开了人机对战

实测:`gameKey=xiangqi` 走同一条路是 **201**。所以这一笔的能力不是「一字棋专用」。

入口 SHALL 按 `GET /api/games` 的 **`supportsAi`** 决定,MUST NOT 写一份「哪些棋种有 AI」的清单 —— 那个字段本身就是服务端为了「客户端看到的与服务端会接受的不可能不一致」而投影出来的(它的文档写着:一份手写副本的症状是**一个永远 400 的按钮**,而这一笔里已经有一个了)。

## 改什么

- 一字棋进棋盘注册表,指向**同一个** `GomokuRenderer`。
- 大厅按 `supportsHumanVsHuman` 决定给不给建房入口;不给的时候显示「目前只有人机对战」。
- 人机对局弹窗:难度(`Easy` / `Medium` / `Hard`)+ 执边(`Black` / `White`),按 `supportsAi` 出现在**每一个**支持的棋种的大厅里。
- `RoomRepository.createAiRoom(...)` —— **走 `POST /api/rooms/ai`,而这次先对着 controller 的特性核过**(上一笔在解散那条路由上猜错了,单测还确认了那个错的)。

## 不做

- **排行榜。** 一字棋 `isRated: false`,拿它当第一个榜的用例会量到一个永远空的榜。触发条件不变:你想看象棋的榜。
- **认输。** 它是进行中对局的出口,与人机房是两件事。
- **AI 难度的说明文案。** `xiangqi.notice-ai` 那类文案存在,但它是 web 端页面的措辞;手机端要不要在弹窗里解释搜索深度是产品问题,不是这一笔的。

## 规模

renderer 注册一行、大厅分支、弹窗、一条仓库方法、以及判据。**估计 300–400 行**,大头是测试 —— 尤其那条「执白、不碰屏幕、AI 自己落子」的集成测试。
