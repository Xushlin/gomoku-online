## MODIFIED Requirements

### Requirement: `User.RecordGameResult(GameOutcome, int newRating)` 原子更新战绩与 Rating

系统 SHALL 在 `User` 聚合根上提供 `RecordGameResult(GameOutcome outcome, int newRating)` 方法。调用后 MUST 原子完成:

- `GamesPlayed = GamesPlayed + 1`
- 根据 `outcome`:若 `Win` 则 `Wins++`,若 `Loss` 则 `Losses++`,若 `Draw` 则 `Draws++`
- `Rating = newRating`
- **`RowVersion` 通过 `TouchRowVersion()` 替换为新 16 字节值**(本次 `add-concurrency-hardening` 新增;保证乐观并发令牌推进,让并发 SaveChanges 能被 EF 捕获)

`outcome` 传入未定义的枚举值时 MUST 抛 `ArgumentOutOfRangeException`,抛出时 User 状态 MUST 保持不变(包括 `RowVersion`)。

调用后 MUST 保持不变量:`Wins + Losses + Draws == GamesPlayed`。

#### Scenario: 胜场更新
- **WHEN** 新用户(`GamesPlayed=0, Wins=0, Rating=1200`)调用 `RecordGameResult(GameOutcome.Win, 1216)`
- **THEN** `GamesPlayed=1`,`Wins=1`,`Losses=0`,`Draws=0`,`Rating=1216`,`RowVersion` 不同于调用前

#### Scenario: 负场更新
- **WHEN** 新用户调用 `RecordGameResult(GameOutcome.Loss, 1184)`
- **THEN** `GamesPlayed=1`,`Losses=1`,`Rating=1184`,`RowVersion` 更新

#### Scenario: 平局更新
- **WHEN** 新用户调用 `RecordGameResult(GameOutcome.Draw, 1200)`
- **THEN** `GamesPlayed=1`,`Draws=1`,`Rating=1200`,`RowVersion` 更新

#### Scenario: 多局累积
- **WHEN** 同一用户连续调用 `RecordGameResult(Win, 1216) → RecordGameResult(Loss, 1200) → RecordGameResult(Draw, 1200)`
- **THEN** `GamesPlayed=3`,`Wins=1`,`Losses=1`,`Draws=1`,`Rating=1200`,且 `Wins+Losses+Draws == GamesPlayed`;三次调用间 RowVersion 两两不等

#### Scenario: 非法枚举值
- **WHEN** 传入 `(GameOutcome)99` 或其他非定义值
- **THEN** 抛 `ArgumentOutOfRangeException`;`User` 状态 MUST 保持不变,包括 `RowVersion`
