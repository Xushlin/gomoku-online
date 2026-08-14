## MODIFIED Requirements

### Requirement: `Room.Create` 静态工厂构造新房间

系统 SHALL 提供 `Room.Create(RoomId id, string name, UserId hostUserId, DateTime createdAt, string gameKey)`。返回的 `Room` MUST 满足:

- `Id / HostUserId / CreatedAt / GameKey` 等于入参
- `Name` 经过 trim 后长度在 [3..50];非法名称抛 `InvalidRoomNameException`
- `BlackPlayerId = hostUserId`(创建者默认黑方)
- `WhitePlayerId = null`
- `Status = Waiting`
- `Game = null`
- `LastUrgeAt = null`, `LastUrgeByUserId = null`
- `Spectators` 为空,`ChatMessages` 为空

`gameKey` MUST 为非空字符串;`Room.Create` 本身 MUST NOT 校验该键是否已登记 —— `Domain`
不认识注册表,校验属于 Application 层(见下方"建房路径校验棋种"),这是 `Domain` 零外部
依赖约束的直接后果。

#### Scenario: 成功创建
- **WHEN** 以合法参数调用 `Room.Create(...)`
- **THEN** 返回 `Room` 实例,字段等于上述初始值

#### Scenario: 名称非法
- **WHEN** `name` 为 `null` / 空 / 全空白 / 短于 3 / 超过 50 字符
- **THEN** 抛 `InvalidRoomNameException`,消息明确违反规则

#### Scenario: 棋种为空
- **WHEN** `gameKey` 为 `null` / 空 / 全空白
- **THEN** 抛 `ArgumentException`

### Requirement: `Room` 记录自己是哪一种棋

`Room` SHALL 持有 `GameKey`(非空字符串),标识该房间玩的是哪个棋种。既有房间一律为 `'gomoku'`。

`GameKey` MUST 是字符串而非枚举 —— 新增棋种的全部意义就在于不必修改一个共享类型,与游戏目录、`IPuzzleRules` 注册表的选择一致。

创建房间的路径 SHALL 接受调用方指定棋种,并 MUST 在建房前校验该键能在 `IGameRulesRegistry`
中解析 —— 未登记的键 MUST 在聚合被构造之前就被拒绝。

落子路径 SHALL 在解析规则失败时返回 404 —— 那是"房间的 `GameKey` 指向一个本构建不认识的
棋种"的唯一可能来源(手工改过的数据,或降级过的构建)。

#### Scenario: 既有房间是五子棋
- **WHEN** 读取迁移前创建的任意房间
- **THEN** `GameKey == "gomoku"`

#### Scenario: 新建房间写入已登记的棋种
- **WHEN** 通过 `CreateRoom` 或 `CreateAiRoom` 建房并指定 `"tictactoe"`
- **THEN** `GameKey == "tictactoe"`,且该键能在规则注册表中解析出规则

#### Scenario: 房间指向未知棋种时落子返回 404
- **WHEN** 某房间的 `GameKey` 在注册表中不存在,玩家尝试落子
- **THEN** handler 返回 404,MUST NOT 抛未处理异常

### Requirement: REST 端点管理房间聚合(关系 / 状态)

Api 层 SHALL 暴露以下端点(均要求 `Authorize`):

| HTTP | 路径 | Body | 成功 | 描述 |
|---|---|---|---|---|
| POST | `/api/rooms` | `{ name, gameKey? }` | 201 + `RoomSummaryDto` | 创建房间(调用方成为 Host 与黑方) |
| GET | `/api/rooms?gameKey=` | — | 200 + `RoomSummaryDto[]` | 指定棋种的活跃房间列表(Waiting + Playing) |
| GET | `/api/rooms/{id}` | — | 200 + `RoomStateDto` | 完整房间状态(含 Moves) |
| POST | `/api/rooms/{id}/join` | — | 200 + `RoomStateDto` | 以当前用户身份加入为白方 |
| POST | `/api/rooms/{id}/leave` | — | 204 | 离开房间(玩家或围观者) |
| POST | `/api/rooms/{id}/spectate` | — | 204 | 加入围观 |
| DELETE | `/api/rooms/{id}/spectate` | — | 204 | 离开围观 |

`gameKey` 在请求体与查询串中 MUST 均为可选,缺省一律取 `"gomoku"`。缺省值 MUST 只存在于
Api 层 —— `CreateRoomCommand.GameKey` 与 `GetRoomListQuery.GameKey` MUST 是必填的非空字段,
Application 层不猜自己在被问哪个棋种。理由是兼容性:本变更不含 Web 客户端,已发布的客户端
不会送这个字段,而让它们从此建不出房是不可接受的回归。

**落子、聊天、催促不走 REST**,由 SignalR Hub 路由(见下一个 Requirement)。

#### Scenario: 列表只含活跃房间
- **WHEN** 已有 3 个 `Waiting`、2 个 `Playing`、1 个 `Finished` 五子棋房间,调 `GET /api/rooms`
- **THEN** 返回 5 个摘要,不含 `Finished` 房间

#### Scenario: 加入不存在的房间
- **WHEN** `POST /api/rooms/{id}/join` 指向不存在的 id
- **THEN** HTTP 404,错误类型 `RoomNotFoundException`

#### Scenario: 列表按棋种隔离
- **WHEN** 存在 2 个 `gomoku` 活跃房间与 3 个 `tictactoe` 活跃房间,调 `GET /api/rooms?gameKey=tictactoe`
- **THEN** 只返回那 3 个一字棋房间

#### Scenario: 缺省棋种向后兼容
- **WHEN** 已发布的客户端调 `GET /api/rooms`(不带查询串)或 `POST /api/rooms` 送 `{ name }`(无 `gameKey`)
- **THEN** 行为与本变更之前完全一致 —— 列表只含 `gomoku` 房间,建出的房间 `GameKey == "gomoku"`

#### Scenario: 未登记的棋种建房被拒
- **WHEN** `POST /api/rooms` 送 `{ name, gameKey: "xiangqi" }`
- **THEN** HTTP 400 —— 房间尚不存在,这是请求本身不合法,而不是资源缺失

#### Scenario: 未登记的棋种查列表返回空
- **WHEN** `GET /api/rooms?gameKey=xiangqi`
- **THEN** HTTP 200 + 空数组 —— 集合端点上"没有这种房间"与"没有这个棋种"对调用方无区别,MUST NOT 报错

## ADDED Requirements

### Requirement: 建房路径校验棋种已登记

`CreateRoomCommandValidator` 与 `CreateAiRoomCommandValidator` SHALL 各增加一条规则:`GameKey`
非空,且 MUST 能在 `IGameRulesRegistry` 中解析出规则,否则校验失败(映射为 HTTP 400)。

校验 MUST 发生在聚合被构造之前 —— 一个 `GameKey` 无人认识的 `Room` 一旦落库就再也玩不了,
只能靠手工改数据修复。这是 `room-and-gameplay` 此前记下的、由本变更偿还的欠债。

Validator MUST 通过注入的 `IGameRulesRegistry` 判断,MUST NOT 内联一份棋种白名单 ——
两处清单迟早会不一致,而不一致的那一天不会有人发现。

#### Scenario: 已登记的键通过
- **WHEN** 以 `gameKey = "gomoku"` 或 `"tictactoe"` 建房
- **THEN** 校验通过

#### Scenario: 未登记的键被拒
- **WHEN** 以 `gameKey = "xiangqi"` 建房
- **THEN** 校验失败,HTTP 400,错误信息点名该字段

#### Scenario: 校验器不持有白名单
- **WHEN** 检视两个 validator 的实现
- **THEN** 它们 MUST 依赖 `IGameRulesRegistry`,MUST NOT 出现硬编码的棋种字符串集合

### Requirement: `GetRoomListQuery` 按棋种过滤

`GetRoomListQuery` SHALL 携带必填的 `GameKey`,handler MUST 只返回 `Room.GameKey` 与之相等的
活跃房间。

大厅是分棋种的:五子棋大厅里出现一字棋房间既无法加入(盘面不同),也让"有几局在等人"这个
数字失去意义。

`GET /api/users/me/active-rooms` MUST NOT 按棋种过滤 —— 它回答的是"我此刻在哪些局里",
跨棋种正是该问题的正确答案,也是玩家唯一希望它们混在一起的地方。

#### Scenario: 只返回本棋种
- **WHEN** 以 `GameKey = "gomoku"` 查询,库中同时存在两种棋的活跃房间
- **THEN** 只返回 `GameKey == "gomoku"` 的房间

#### Scenario: 我的活跃房间跨棋种
- **WHEN** 某用户同时在一个五子棋房间和一个一字棋房间里,调 `GET /api/users/me/active-rooms`
- **THEN** 两个房间都被返回
