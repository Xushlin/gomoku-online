# room-and-gameplay Specification Delta

## MODIFIED Requirements

### Requirement: `Game` 子实体承载对局运行状态

`Game` MUST 包含字段:
- `Id: Guid`
- `RoomId: RoomId`
- `StartedAt: DateTime`(UTC)
- `EndedAt: DateTime?`
- `Result: GameResult?`(对局进行时为 `null`)
- `WinnerUserId: UserId?`
- `EndReason: GameEndReason?`(结束时非 null,与 `Result` 同时为 null 或同时非 null)
- **`CurrentTurn: int`** —— **座位号**,`0` 到 `SeatCount - 1`
- `Moves: IReadOnlyCollection<Move>`
- `RowVersion: byte[]`(乐观并发令牌,由 Infrastructure 层维护)

`CurrentTurn` MUST 是座位号而 MUST NOT 是 `Stone`。轮转 MUST 为 `(CurrentTurn + 1) % SeatCount`,而 MUST NOT 是两值之间的布尔翻转。

**`Stone` MUST NOT 出现在 `Gewu.Domain/Rooms/` 下的任何文件中。** 这是"内核不知道一个游戏有几个人"的可执行形式,与 `in-room-chat` 那条「`JoinAsSpectator` 不许提到 `GameKey`」是同一种断言,且同样 MUST 由一条测试强制而不是靠约定。

`Stone` 本身不废弃,它下沉到棋盘类棋种的规则内部:座位 0/1 由 `INInARowRules` / `XiangqiRules` **在自己内部**映成 `Stone.Black` / `Stone.White`。`add-xiangqi` 立下的「`Stone.Black` 就是红」那条读法**一个字不动** —— 它本来就是棋种内部的事,而这次改动恰好把这一点变成了结构。

`Game` 不独立于 `Room` 存活;构造仅由 `Room.JoinAsPlayer` 内部发生。`Game.FinishWith` 的签名 MUST 为 `FinishWith(GameResult, UserId?, GameEndReason, DateTime)`。

#### Scenario: 初始 Game 状态
- **WHEN** 白方加入触发 `JoinAsPlayer`
- **THEN** `Game.StartedAt == now`;**`CurrentTurn == 0`**(先手座位);`Moves` 空;`EndedAt == null`;`Result == null`;`EndReason == null`

#### Scenario: 两座位游戏的轮转不变
- **WHEN** 一个 `SeatCount == 2` 的棋种连走 3 步
- **THEN** `CurrentTurn` 依次为 `0 → 1 → 0 → 1` —— 与改动前的 `Black → White → Black → White` 逐步等价,行为零变化

#### Scenario: 三座位游戏按环轮转
- **WHEN** 一个 `SeatCount == 3` 的规则连走 3 步
- **THEN** `CurrentTurn` 依次为 `0 → 1 → 2 → 0`

  这一条用一个假的三座位规则验证,而它证明的是**取模算术**,MUST NOT 被当成"这个接缝对牌类够用"的证据 —— 后者只有真游戏能证。`add-puzzle-core` 用一个照着唯一实现捏的 fake 声称证过接缝通用,华容道一到 `Validate` 与 `Score` 两个都得改。

#### Scenario: Game 结束状态
- **WHEN** 某方获胜或平局或认输或超时后
- **THEN** `EndedAt != null`;`Result != null`;若有胜方则 `WinnerUserId != null`;`EndReason != null` 且对应路径

---

### Requirement: `Move` 子实体记录每一步的上下文

`Move` MUST 包含:`Id: Guid`、`GameId: Guid`、`Ply: int (1-based)`、**`Seat: int`(座位号)**、`PlayedAt: DateTime`(UTC),外加**恰好一种载荷**(见下一条 Requirement)。数据库持久化:`(GameId, Ply)` 唯一。

出手方 MUST 记为座位号而 MUST NOT 记为 `Stone`。**持久化格式不变**:两座位棋种的历史数据里,原先的 `Black`/`White` 底层值就是 `1`/`2`,而座位号是 `0`/`1` —— 因此本次 MUST 附带一次值迁移,或以映射保持读写一致,二者选一并写明。MUST NOT 出现"看起来对、但历史局重放后出手方错位"的第三种做法。

线上 DTO **不变**:`MoveDto.stone` 仍为 `'Black' | 'White'`,由 Api 边界从座位 0/1 映射。这是**带触发条件的债** —— 第一个 `SeatCount != 2` 的棋种落地那天,DTO 加座位字段、映射删除。写下这条的理由是:一层没有理由的边界映射,下一个读到它的人会当成手滑。

#### Scenario: Ply 从 1 起严格递增
- **WHEN** 在同一局依次走 3 步
- **THEN** 三个 `Move` 的 `Ply` 分别为 1、2、3

#### Scenario: 历史对局重放后出手方不变
- **WHEN** 取一局改动前存下的两座位对局,按新代码重放
- **THEN** 每一步的出手方与改动前一致 —— 这条 MUST 有测试,因为"错位一位"在棋盘上表现为整局颜色反转,而在计分上表现为赢家错人
