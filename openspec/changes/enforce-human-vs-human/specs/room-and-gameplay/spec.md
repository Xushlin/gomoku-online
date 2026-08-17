# room-and-gameplay Specification Delta

## MODIFIED Requirements

### Requirement: 建房路径校验棋种已登记

`CreateRoomCommandValidator` 与 `CreateAiRoomCommandValidator` SHALL 各校验 `GameKey` 非空、且 MUST 能在 `IGameRulesRegistry` 中解析出规则,否则校验失败(映射为 HTTP 400)。

`CreateRoomCommandValidator` SHALL **额外**要求解析出的规则 `SupportsHumanVsHuman == true`,否则同样 400。`CreateAiRoomCommandValidator` MUST NOT 有这条规则 —— 人机正是这些棋种支持的玩法,在那条路径上拦住等于把它们逐出平台。

校验 MUST 发生在聚合被构造之前 —— 一个 `GameKey` 无人认识的 `Room` 一旦落库就再也玩不了,
只能靠手工改数据修复。

Validator MUST 通过注入的 `IGameRulesRegistry` 判断,MUST NOT 内联一份棋种白名单 ——
两处清单迟早会不一致,而不一致的那一天不会有人发现。同理,两条规则 MUST 各只有一处定义
(`Common/Validation` 下的 `IRuleBuilder` 扩展),由两条建房路径按需组合。

#### Scenario: 已登记且支持人人对战的键通过
- **WHEN** 以 `gameKey = "gomoku"` 建真人房
- **THEN** 校验通过

#### Scenario: 未登记的键被拒
- **WHEN** 以一个未在注册表中登记的 `gameKey`(如 `"go"`)建房
- **THEN** 校验失败,HTTP 400,错误信息点名该字段;真人房与 AI 房两条路径 MUST 表现一致

#### Scenario: 已登记但无人人对战的键在真人房路径被拒
- **WHEN** 以 `gameKey = "tictactoe"` 或 `"xiangqi"` 调 `POST /api/rooms`
- **THEN** 校验失败,HTTP 400 —— 该棋种 `SupportsHumanVsHuman == false`

#### Scenario: 同一个键在 AI 房路径通过
- **WHEN** 以 `gameKey = "tictactoe"` 或 `"xiangqi"` 调 `POST /api/rooms/ai`
- **THEN** 校验通过 —— 人机不受本规则约束

#### Scenario: 判定遍历注册表,不是一份名单
- **WHEN** 遍历 `IGameRulesRegistry` 中每一个规则,对其键跑 `CreateRoomCommandValidator`
- **THEN** 校验通过当且仅当该规则 `SupportsHumanVsHuman == true`;该遍历 MUST 另有一条断言证明它同时覆盖到了两类棋种(一个只走到空集合的遍历会全绿地什么都不验)

#### Scenario: 校验器不持有白名单
- **WHEN** 检视两个 validator 的实现
- **THEN** 它们 MUST 依赖 `IGameRulesRegistry`,MUST NOT 出现硬编码的棋种字符串集合

---

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
Application 层不猜自己在被问哪个棋种。

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

#### Scenario: 缺省棋种取 gomoku
- **WHEN** 调 `GET /api/rooms`(不带查询串)或 `POST /api/rooms` 送 `{ name }`(无 `gameKey`)
- **THEN** 列表只含 `gomoku` 房间,建出的房间 `GameKey == "gomoku"`

#### Scenario: 未登记的棋种建房被拒
- **WHEN** `POST /api/rooms` 送 `{ name, gameKey: "go" }`(围棋不在本平台上)
- **THEN** HTTP 400 —— 房间尚不存在,这是请求本身不合法,而不是资源缺失

#### Scenario: 未登记的棋种查列表返回空
- **WHEN** `GET /api/rooms?gameKey=go`
- **THEN** HTTP 200 + 空数组 —— 集合端点上"没有这种房间"与"没有这个棋种"对调用方无区别,MUST NOT 报错

#### Scenario: 已登记但无人人对战的棋种建真人房被拒
- **WHEN** `POST /api/rooms` 送 `{ name, gameKey: "xiangqi" }`
- **THEN** HTTP 400 —— 理由是 `SupportsHumanVsHuman == false`,**不是**"这个棋种不存在"。象棋自 `add-xiangqi` 起就已登记,任何仍以它举例"未登记"的场景都是过期的
