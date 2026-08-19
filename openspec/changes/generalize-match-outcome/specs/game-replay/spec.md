# game-replay Specification Delta

## MODIFIED Requirements

### Requirement: `GameReplayDto.Moves` 与 `UserGameSummaryDto.MoveCount` 正确反映对局

系统 SHALL 保证 replay / user-games 两个端点返回的 DTO 对 moves 历史无遗漏且有序:

- `GameReplayDto.Moves` 的元素 MUST **按 `Ply` 升序**,不跳过。认输 / 超时的对局可能 `Moves == []`(还没有人落子就结束),此时 DTO 的 `Moves` 是空列表而**非 null**。
- `UserGameSummaryDto.MoveCount` MUST 等于 `game.Moves.Count`(无二次过滤;不做"有效 move 判定")。

`Result` 的取值集合 MUST 是 `Ongoing` / `Decided` / `Draw`。**"谁赢了"由 `WinnerUserId` 一处说明**,
回放的消费方 MUST NOT 从 `Result` 的取值推断赢的是哪一方。

#### Scenario: 认输后的回放 Moves 为空
- **WHEN** Alice 开房 → Bob join → Alice 未落子直接认输 → `GET /replay`
- **THEN** `GameReplayDto.Moves == []`;`Result == Decided`;`WinnerUserId == bobId`;`EndReason == Resigned`

#### Scenario: 落子后结束
- **WHEN** Alice 创建房 → Bob join → Alice 落 (7,7) 一子后 Bob 认输
- **THEN** `GameReplayDto.Moves.Count == 1`;`Result == Decided`;`WinnerUserId == aliceId`;`EndReason == Resigned`

#### Scenario: 连五结束的 MoveCount
- **WHEN** Alice 连五结束,总共 9 步落子
- **THEN** `GameReplayDto.Moves.Count == 9`;对应 `UserGameSummaryDto.MoveCount == 9`
