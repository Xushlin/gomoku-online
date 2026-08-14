## ADDED Requirements

### Requirement: `GameReplayDto` 携带棋种键

`GameReplayDto` SHALL 带一个非空的 `GameKey` 字段,取自 `Room.GameKey`。

理由与房间状态 DTO 完全相同,而且这里更迫切:回放页**自己拼一个 `RoomState` 形状的对象**喂给同一个 `Board` 组件,所以它必须知道盘面几格。回放链接常常是冷启动打开的(分享、收藏、从战绩列表点进),那时客户端手上只有一个房间 id。

没有本字段时唯一能编译过的写法是在回放页里写死 `'gomoku'` —— 那会让一字棋的回放画成 15×15。而「刚下完一局 → 点查看回放」是主路径,不是边角。

#### Scenario: 一字棋回放带对棋种
- **WHEN** 请求一局 `tictactoe` 对局的回放
- **THEN** `GameKey == "tictactoe"`,回放页据此渲染 9 格

#### Scenario: 五子棋回放不受影响
- **WHEN** 请求一局 `gomoku` 对局的回放
- **THEN** `GameKey == "gomoku"`,回放页渲染 225 格

#### Scenario: 只增字段
- **WHEN** 比对本变更前后的 `GameReplayDto`
- **THEN** 既有字段的名称与类型 MUST NOT 改变
