## ADDED Requirements

### Requirement: 认输入口 SHALL 只在能成功时出现,而「能不能」的三个判据里有一个是座位数

手机端的对局页 SHALL 在满足**全部三个**条件时显示认输入口,否则 MUST NOT 显示:

1. 当前用户坐在这局的某个座位上(不是围观者、不是路人);
2. 房间状态是进行中;
3. **这个房间的座位数恰好是 2**,而该数字 SHALL 读自**房间自身**的 `seatCount`(服务端已随
   `RoomStateDto` 下发),MUST NOT 由客户端按棋种猜、也 MUST NOT 绕道再查一次棋种目录 ——
   被认输的是**这个房间**,而房间自己就说了它有几个座位。

第三条不是保守。平台的 `Room.Resign` 需要恰好两个座位才能指出赢家,三座位棋种上 API 答 409 ——
web 端曾因为客户端假设了座位数而在真实点击上返回 **500**。手机端目前两个棋种都是两座位,所以
这条判据**今天恒真**;它存在是为了第三个棋种落地那天不必重新发现。

认输 SHALL 走 `POST /api/rooms/{id}/resign`。

#### Scenario: 玩家在进行中的两座位对局里看得到认输
- **WHEN** 当前用户坐在一局进行中的五子棋房间里
- **THEN** 对局页显示认输入口

#### Scenario: 围观者看不到
- **WHEN** 当前用户不在任何座位上
- **THEN** 对局页 MUST NOT 显示认输入口

#### Scenario: 等待中的房间看不到
- **WHEN** 房间还在等待对手
- **THEN** 对局页 MUST NOT 显示认输入口

#### Scenario: 座位数不是 2 就看不到
- **WHEN** 房间的 `seatCount` 是 3
- **THEN** 对局页 MUST NOT 显示认输入口(平台无法在三座位下指出赢家)

---

### Requirement: 认输 SHALL 先确认,且 MUST NOT 自己宣布结果

认输不可逆,所以 SHALL 先弹确认;取消 MUST NOT 发出任何请求。

确认之后,客户端 MUST NOT 自行渲染「你输了」——结果 SHALL 走既有的那一条路:房间快照的
`result` / `winnerUserId` / `endReason`,以及 `GameEnded` 推送。**两条宣布结果的路会分叉,而
分叉的表现是其中一条说错了赢家。**

文案 SHALL 复用 `game.actions.resign-confirm-title` / `-body` / `-ok` / `-cancel`,MUST NOT 新增键。

#### Scenario: 取消不发请求
- **WHEN** 玩家点认输,然后在确认框里点取消
- **THEN** MUST NOT 调用 `POST /api/rooms/{id}/resign`,且对局仍在进行中

#### Scenario: 确认才认输
- **WHEN** 玩家点认输并确认
- **THEN** 调用 `POST /api/rooms/{id}/resign`

#### Scenario: 结果由既有那条路显示
- **WHEN** 认输成功,服务端随后推来 `GameEnded`(或快照带上 `result`)
- **THEN** 屏幕上的结果来自那一份数据,而不是客户端在认输成功时自己写下的

---

### Requirement: 催促入口 SHALL 在不可用时说明原因,而冷却 MUST NOT 由客户端判定

催促入口 SHALL 在「当前用户是玩家 且 对局进行中」时显示。**可点**的条件再加一条:当前不是
自己的回合。

不可点时 SHALL 显示原因文案,MUST NOT 只是把按钮变灰:

- 轮到自己 → `game.urge.button-disabled-own-turn`
- 刚催过(收到 429 之后) → `game.urge.button-disabled-cooldown`

客户端 MUST NOT 自己实现 30 秒冷却计时。它 MAY 在收到 429 之后临时禁用按钮,但「服务端会不会
接受这次催促」这个结论 SHALL 由服务端给出。**一份并行的冷却计时器是第二处规则,而两处规则会
分叉,分叉的表现是按钮说「可以」而服务端说「不行」。**

催促 SHALL 走 hub 方法 `Urge(roomId)`。

#### Scenario: 轮到对手时可以催
- **WHEN** 对局进行中且当前回合是对手
- **THEN** 催促入口可点

#### Scenario: 轮到自己时不可点,并说明原因
- **WHEN** 对局进行中且当前回合是自己
- **THEN** 催促入口不可点,且屏幕上出现 `game.urge.button-disabled-own-turn` 的文案

#### Scenario: 冷却由服务端告知
- **WHEN** 服务端以 429 拒绝一次催促
- **THEN** 屏幕上出现 `game.errors.urge-cooldown`,MUST NOT 落到通用错误文案

---

### Requirement: `UrgeReceived` SHALL 出现在屏幕上,且 MUST NOT 需要刷新

客户端 SHALL 订阅 hub 方法 `UrgeReceived`,收到时在对局页上给出可见反馈(`game.urge.toast`)。

这是**推送**,不是快照的一部分 —— 服务端不会把「你被催了」写进 `RoomStateDto`,所以任何靠
重新拉取房间来发现它的实现都会永远发现不了。

被催的那一方 SHALL 收到;催的那一方 MUST NOT 收到自己那一条。

#### Scenario: 被催的人看得见
- **WHEN** 对手催促当前用户,服务端推来 `UrgeReceived`
- **THEN** 对局页上出现催促提示

#### Scenario: 催的人不会被自己催
- **WHEN** 当前用户催促对手
- **THEN** 当前用户的屏幕上 MUST NOT 出现催促提示
