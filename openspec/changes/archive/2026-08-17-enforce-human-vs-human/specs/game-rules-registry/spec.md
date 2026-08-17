# game-rules-registry Specification Delta

## ADDED Requirements

### Requirement: `SupportsHumanVsHuman` 由服务端强制,不只是被声明

平台 SHALL 在服务端拒绝为 `SupportsHumanVsHuman == false` 的棋种创建人人对战房间;该字段 MUST NOT 只是一个提供给客户端参考的声明。

理由是这个字段的措辞本身:它的定义是「平台是否提供人人对战入口」。若 `POST /api/rooms` 接受该棋种,平台就**确实**提供了一个入口,字段的值与事实相反 —— 而 `IsRated ⇒ SupportsHumanVsHuman` 这条不变量正是靠它作为**结构性事实**才成立的。判断会过期,结构性事实不会;但一个没人强制的"结构性事实"只是另一个判断。

客户端据此隐藏"创建房间"入口是**展示决定**,MUST NOT 被当作强制手段 —— 任何人都可以直接调 API。

不变量与本条的分工:`IsRated ⇒ SupportsHumanVsHuman` 由构造器强制(见上),回答"这个棋种能不能计分";本条由建房校验强制,回答"平台给不给它开人人对战入口"。

#### Scenario: 声明与行为一致
- **WHEN** 对注册表中每一个 `IGameRules`,尝试以其键创建人人对战房间
- **THEN** 成功当且仅当 `SupportsHumanVsHuman == true`

#### Scenario: 人机不受约束
- **WHEN** 以 `SupportsHumanVsHuman == false` 的棋种创建人机房间
- **THEN** 成功 —— 本条只约束人类对手池,与 AI 无关

#### Scenario: 一字棋不计分的前提为真
- **WHEN** 检视 `tictactoe` 为何 `IsRated == false`
- **THEN** 其依据「唯一的对手是机器人」MUST 是服务端强制的事实,而不只是当前 Web 界面恰好没有入口
