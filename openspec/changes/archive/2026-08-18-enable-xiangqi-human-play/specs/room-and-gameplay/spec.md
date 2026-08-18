# room-and-gameplay Specification Delta

## MODIFIED Requirements

### Requirement: REST 端点管理房间聚合(关系 / 状态)

Api 层 SHALL 暴露以下端点(均要求 `Authorize`):

| HTTP | 路径 | Body | 成功 | 描述 |
|---|---|---|---|---|
| POST | `/api/rooms` | `{ name, gameKey }` | 201 + `RoomSummaryDto` | 创建房间(调用方成为 Host 与黑方) |
| GET | `/api/rooms?gameKey=` | — | 200 + `RoomSummaryDto[]` | 指定棋种的活跃房间列表(Waiting + Playing) |
| POST | `/api/rooms/ai` | `{ name, difficulty, humanSide?, gameKey }` | 201 + `RoomStateDto` | 创建人机房间 |
| GET | `/api/rooms/{id}` | — | 200 + `RoomStateDto` | 完整房间状态(含 Moves) |
| POST | `/api/rooms/{id}/join` | — | 200 + `RoomStateDto` | 以当前用户身份加入为白方 |
| POST | `/api/rooms/{id}/leave` | — | 204 | 离开房间(玩家或围观者) |
| POST | `/api/rooms/{id}/spectate` | — | 204 | 加入围观 |
| DELETE | `/api/rooms/{id}/spectate` | — | 204 | 离开围观 |

`gameKey` 在这三个端点上 MUST 为**必填**。Api 层 MUST NOT 为它填任何缺省值 —— 调用方不说自己要哪个棋种时,服务端 MUST 回 400,而不是替它选一个。

缺省曾经存在,理由写的是「已发布的客户端不会送这个字段」。已发布的客户端有**零个**:本仓库没有部署,唯一的客户端就在 `frontend-web/`,而它从未送过这个字段。那不是兼容层,是一处写在服务端、因而任何客户端读者都看不见的硬编码。

`humanSide` 仍然可缺省(填 `Stone.Black`),两者**不对称是刻意的**:给一个缺省的边,是在调用方已经指名的棋种**之内**补全一个不完整的请求;给一个缺省的棋种,是换掉他在玩的游戏。

**落子、聊天、催促不走 REST**,由 SignalR Hub 路由(见下一个 Requirement)。

#### Scenario: 列表只含活跃房间
- **WHEN** 已有 3 个 `Waiting`、2 个 `Playing`、1 个 `Finished` 五子棋房间,调 `GET /api/rooms?gameKey=gomoku`
- **THEN** 返回 5 个摘要,不含 `Finished` 房间

#### Scenario: 加入不存在的房间
- **WHEN** `POST /api/rooms/{id}/join` 指向不存在的 id
- **THEN** HTTP 404,错误类型 `RoomNotFoundException`

#### Scenario: 列表按棋种隔离
- **WHEN** 存在 2 个 `gomoku` 活跃房间与 3 个 `tictactoe` 活跃房间,调 `GET /api/rooms?gameKey=tictactoe`
- **THEN** 只返回那 3 个一字棋房间

#### Scenario: 缺少棋种是 400,不是 gomoku
- **WHEN** 调 `GET /api/rooms`(不带查询串),或 `POST /api/rooms` 送 `{ name }`,或 `POST /api/rooms/ai` 送 `{ name, difficulty }`
- **THEN** HTTP 400,错误点名 `GameKey` 字段;MUST NOT 建出任何房间,MUST NOT 返回五子棋房间列表

#### Scenario: 未登记的棋种建房被拒
- **WHEN** `POST /api/rooms` 送 `{ name, gameKey: "go" }`(围棋不在本平台上)
- **THEN** HTTP 400 —— 房间尚不存在,这是请求本身不合法,而不是资源缺失

#### Scenario: 未登记的棋种查列表返回空
- **WHEN** `GET /api/rooms?gameKey=go`
- **THEN** HTTP 200 + 空数组 —— 集合端点上"没有这种房间"与"没有这个棋种"对调用方无区别,MUST NOT 报错

#### Scenario: 已登记但无人人对战的棋种建真人房被拒
- **WHEN** `POST /api/rooms` 送 `{ name, gameKey: "tictactoe" }`
- **THEN** HTTP 400 —— 理由是 `SupportsHumanVsHuman == false`,**不是**"这个棋种不存在"

#### Scenario: 象棋现在开得出真人房
- **WHEN** `POST /api/rooms` 送 `{ name, gameKey: "xiangqi" }`
- **THEN** HTTP 201 —— 象棋自 `enable-xiangqi-human-play` 起开放人人对战。**本条此前举的例子就是象棋,而它已经过期。** 举例用的棋种会随能力变化而失效,而一条把过期事实钉成正确的断言会一直是绿的 —— `enforce-human-vs-human` 为这件事付过一次账

#### Scenario: 缺省的边仍然被补全
- **WHEN** `POST /api/rooms/ai` 送 `{ name, difficulty, gameKey }` 而不带 `humanSide`
- **THEN** HTTP 201,真人执黑 —— 本条与棋种的必填不对称,是有意为之
