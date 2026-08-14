## ADDED Requirements

### Requirement: 房间 DTO 携带棋种键

`RoomStateDto` 与 `RoomSummaryDto` SHALL 各带一个非空的 `GameKey` 字段,取自 `Room.GameKey`。

这不是装饰性字段,而是客户端**画不出棋盘就得靠它**:玩家进入 `/rooms/{id}` 有四条路 ——
从建房页跳转、刷新页面、点收藏链接、从"我的对局"进入 —— 只有第一条路上客户端知道棋种
(是它自己刚选的)。另外三条它手上只有一个房间 id,而没有本字段时 DTO 里没有任何东西能
区分 3×3 与 15×15。所以"棋种从路由参数带过来"这条捷径只在四条路里的一条上成立。

映射 MUST NOT 因此获得新依赖:`Room.GameKey` 已经存在且已填好,`ToState` / `ToSummary`
就是把它映出来。

本变更 MUST NOT 在 DTO 里下发盘面尺寸(`Rows` / `Cols`)—— 那需要把 `IGameRulesRegistry`
穿过九处 `ToState` / `ToSummary` 调用点。见 `add-web-tictactoe-ai` design D1:客户端从自己的
游戏注册表解析尺寸,该重复此刻比它的替代方案便宜,且 `generalize-match-contract` 反正要
重写这两个 DTO,届时再改为服务端下发。

#### Scenario: 五子棋房间
- **WHEN** 读取任意 `gomoku` 房间的状态或摘要
- **THEN** `GameKey == "gomoku"`

#### Scenario: 一字棋房间
- **WHEN** 读取任意 `tictactoe` 房间的状态或摘要
- **THEN** `GameKey == "tictactoe"`

#### Scenario: 只增字段,不改既有字段
- **WHEN** 比对本变更前后的 DTO
- **THEN** 既有字段的名称、类型、顺序语义 MUST NOT 改变 —— 已发布客户端反序列化行为不变

### Requirement: `POST /api/rooms/ai` 接受棋种键

`POST /api/rooms/ai` 的请求体 SHALL 接受可选的 `gameKey`,缺省 `"gomoku"`,与 `POST /api/rooms` 的处理一致。

未登记的棋种 MUST 返回 400(由 `CreateAiRoomCommandValidator` 判定),与人人建房路径同一行为
—— 该棋种是否**有 AI** 则是另一件事,由落子时的 AI 注册表解析决定。

#### Scenario: 建一字棋 AI 房
- **WHEN** `POST /api/rooms/ai` 送 `{ name, difficulty: "Hard", humanSide: "Black", gameKey: "tictactoe" }`
- **THEN** 201 + `RoomStateDto`,`GameKey == "tictactoe"`,`Status == Playing`,白方是 `BotAccountIds.Hard`

#### Scenario: 缺省仍是五子棋
- **WHEN** 请求体不含 `gameKey`
- **THEN** 建出的房间 `GameKey == "gomoku"`
