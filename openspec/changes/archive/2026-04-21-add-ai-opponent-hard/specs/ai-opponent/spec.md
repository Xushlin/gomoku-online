## ADDED Requirements

### Requirement: `HardAi` 实现 Minimax + α-β 的两层前瞻搜索

系统 SHALL 在 `Gomoku.Domain/Ai/HardAi.cs` 实现 `IGomokuAi`,代表"Hard"难度。构造签名:

```
public HardAi(Random random, int searchDepth = 2);
```

- `random`:用于分相同候选的破平;MUST 非 null 否则抛 `ArgumentNullException`。
- `searchDepth`:默认 2;MUST ≥ 1,否则抛 `ArgumentOutOfRangeException`。生产通过 `GomokuAiFactory` 使用默认值,暴露该参数仅为单元测试。

`SelectMove(Board, Stone myStone)` 行为:

1. `myStone == Stone.Empty` → 抛 `ArgumentOutOfRangeException`。
2. 棋盘完全为空 → 返回 `Position(7, 7)`。
3. 否则生成候选集(已有子 Chebyshev 距离 ≤ 2 的空格,去重);
4. 对每个候选 `c` 先试走自己一手:若直接连五,立即返回 `c`(与 MediumAi 的第一层一致);
5. 否则用 **Minimax + α-β** 在该候选下递归搜索 `searchDepth - 1` 层(对方先行),得到 `score`;
6. 返回 `score` 最大的候选;并列使用 `random` 破平。

MUST 为纯函数:不修改入参 `board`,不读时钟 / 磁盘 / 网络 / 静态状态;相同 `random` 种子 + 相同盘面 → 相同返回值。

#### Scenario: 空盘首手中心
- **WHEN** 空盘,`myStone = Black`,`HardAi(new Random(42))` 调 `SelectMove`
- **THEN** 返回 `Position(7, 7)`

#### Scenario: 能连五立即选
- **WHEN** 黑方 `(7,3)..(7,6)` 已 4 连、我方是黑、任一方空点可连五
- **THEN** 返回 `(7, 2)` 或 `(7, 7)`(两者皆可;`random` 破平)

#### Scenario: 对手能连五必堵
- **WHEN** 白方 `(7,3)..(7,6)` 已 4 连,我方是黑,黑方自己无连五机会
- **THEN** 返回 `(7, 2)` 或 `(7, 7)` —— 堵对手必胜点

#### Scenario: 识别并阻止对手活三
- **WHEN** 对手在 `(7,5)(7,6)(7,7)` 三连,两端 `(7,4)` 与 `(7,8)` 均空(活三),我方现在走棋
- **THEN** 返回的 `Position` MUST 是 `(7, 4)` 或 `(7, 8)` 之一(堵活三);**不**选盘面远处格子

#### Scenario: 从不选已有子位置
- **WHEN** 对有若干已占子的盘面反复调 `SelectMove`(不同随机源 推进)
- **THEN** 返回的 `Position` 每次都是空格;从不落在已有子上

#### Scenario: 不修改入参 Board
- **WHEN** 调 `SelectMove`
- **THEN** 入参 `board` 的每一格在返回前后完全一致(实现内部只对 `board.Clone()` 做试探)

#### Scenario: 固定种子复现
- **WHEN** 两个独立 `HardAi(new Random(42))` 对同一盘面调 `SelectMove`
- **THEN** 两次返回的 `Position` 相等

#### Scenario: 非法参数拒绝
- **WHEN** `random == null` 构造 HardAi;或 `searchDepth == 0`;或 `myStone == Empty`
- **THEN** 抛 `ArgumentNullException` / `ArgumentOutOfRangeException`

---

### Requirement: `HardAi` 的评估函数按模式 × 封闭度打分

`Evaluate(Board, myStone)` 内部 MUST 沿 4 方向(水平、垂直、主对角、反对角)扫描所有**长度 ≥ 2 的同色连续段**,识别"活 / 眠 / 四 / 三 / 二"组合。打分表(己方正分;对手分乘 `-1.1`,偏防守):

| 模式 | 含义 | 己方分 |
|---|---|---|
| 活四 | 长度 4,两端皆空 | +10000 |
| 冲四 | 长度 4,一端封(对方子或边界) | +1000 |
| 活三 | 长度 3,两端皆空 | +500 |
| 眠三 | 长度 3,一端封 | +100 |
| 活二 | 长度 2,两端皆空 | +50 |
| 眠二 | 长度 2,一端封 | +10 |

"连五"本身 MUST 在 Minimax 的**终局判定**里以 `±100000` 直接返回,不走 `Evaluate` 累加路径。

**不做**(显式 non-goal):跳步识别(`X.XX`、`XX.XX`)不在评估范围;Master 档再扩。

#### Scenario: 己方活三得正分
- **WHEN** `Evaluate` 在一个只有黑方 `(7,5)(7,6)(7,7)` 活三的盘面上,`myStone=Black`
- **THEN** 返回 ≥ +500(可能略高,因水平方向的活二等也累加;关键是**正号**)

#### Scenario: 对手活三得负分
- **WHEN** 同一盘面但 `myStone=White`(扫到黑的活三)
- **THEN** 返回 ≤ -500(`-1.1 * 500 = -550`)

#### Scenario: 双活三叠加
- **WHEN** 盘面同时有己方水平活三 + 主对角活三
- **THEN** 返回 ≥ +1000(两次 +500 叠加,外加两条活三重叠位置的活二等次级加成)

---

### Requirement: `HardAi` 的候选生成限于已有子 2 格邻域

`GenerateCandidates(Board)` MUST:

- 空盘返回 `[Position(7, 7)]`;
- 非空盘:枚举所有已占子的位置,取它们的 Chebyshev 距离 ≤ 2 的空格(即 5×5 方块内的空格),去重后返回。

距离选 2 而非 1,以覆盖"跳步活三"附近的关键点(例如对手 `X.XX` 中间的空格 `(c+1)` 与 `(c+2)`)。

#### Scenario: 空盘只一个候选
- **WHEN** 空盘调 `SelectMove`
- **THEN** 候选集等价于 `[(7,7)]`(实际通过特判直接返回,不经历完整候选生成);Hard 不会从 225 个空格随机选

#### Scenario: 单子盘面候选 = 5×5 - 1
- **WHEN** 盘面只有 `(7,7)` 一颗子
- **THEN** 候选集 MUST 是 `(5,5)..(9,9)` 25 格中除 `(7,7)` 外的 24 个空格,去重

#### Scenario: 远离战局的位置不在候选
- **WHEN** 盘面子集中在 `(7,7)` 附近
- **THEN** 候选 MUST NOT 包含 `(0,0)` / `(14,14)` 等远离所有已有子 3+ 格的位置

---

### Requirement: `BotDifficulty.Hard = 2` 作为新难度枚举值

`Gomoku.Domain/Ai/BotDifficulty.cs` MUST 追加 `Hard = 2`。现有 `Easy = 0` / `Medium = 1` 的底层整数值 MUST 保持不变,以维持 `add-ai-opponent` 定下的序列化稳定性。

`GomokuAiFactory.Create(BotDifficulty difficulty, Random random)` MUST 支持新分支:

- `Hard` → 新 `HardAi(random)` 实例(默认 `searchDepth=2`)。

其它分支(`Easy` / `Medium`)与默认 `ArgumentOutOfRangeException` 行为保持不变。

#### Scenario: 枚举值存在
- **WHEN** 审阅 `BotDifficulty` 枚举
- **THEN** 存在值 `Easy=0`、`Medium=1`、`Hard=2`

#### Scenario: Factory 的 Hard 分支
- **WHEN** `GomokuAiFactory.Create(BotDifficulty.Hard, new Random(1))`
- **THEN** 返回 `IGomokuAi`,运行时类型是 `HardAi`

---

### Requirement: `BotAccountIds.Hard` 固定 Guid + 数据库 seed

Application 层 `Gomoku.Application/Abstractions/BotAccountIds.cs` MUST 追加:

```
public static readonly Guid Hard = Guid.Parse("00000000-0000-0000-0000-0000000000ad");
```

`For(BotDifficulty)` 的 switch MUST 覆盖 `Hard => Hard`;`TryGetDifficulty(Guid)` MUST 对该 Guid 返回 `BotDifficulty.Hard`。

数据库 MUST 通过 `AddHardBotAccount` migration 在 `Users` 表插入一行:

| 列 | 值 |
|---|---|
| Id | `BotAccountIds.Hard`(`...00ad`) |
| Email | `hard@bot.gomoku.local` |
| Username | `AI_Hard` |
| PasswordHash | `User.BotPasswordHashMarker`(即 `__BOT_NO_LOGIN__`) |
| Rating | 1200 |
| GamesPlayed / Wins / Losses / Draws | 0 |
| IsActive | true |
| IsBot | true |
| CreatedAt | `2026-01-01T00:00:00Z` |
| RowVersion | 固定 16 字节(migration 写一个常量 byte array;后续 RecordGameResult 会自然推进) |

Migration Down MUST `DeleteData` 该行。

#### Scenario: 三 bot 并存
- **WHEN** 运行 `dotnet ef database update` 后查询 `SELECT Username_Value FROM Users WHERE IsBot=1 ORDER BY Username_Value`
- **THEN** 返回三行:`AI_Easy`、`AI_Hard`、`AI_Medium`

#### Scenario: 反向查询 Hard
- **WHEN** 调 `BotAccountIds.TryGetDifficulty(BotAccountIds.Hard)`
- **THEN** 返回 `BotDifficulty.Hard`(非 null)

#### Scenario: Hard AI 房间创建
- **WHEN** 登录用户 `POST /api/rooms/ai { name: "x", difficulty: "Hard" }`
- **THEN** HTTP 201,`RoomStateDto.White.Username == "AI_Hard"`,`Status == "Playing"`

## MODIFIED Requirements

### Requirement: `BotDifficulty` 枚举表达 AI 难度

系统 SHALL 在 `Gomoku.Domain/Ai/BotDifficulty.cs` 定义 `enum BotDifficulty { Easy = 0, Medium = 1, Hard = 2 }`(**本次追加 `Hard`**)。底层整数值固定,以便序列化稳定性与未来加 `Master=3` 等扩展。

#### Scenario: 枚举值存在
- **WHEN** 审阅 `Gomoku.Domain/Ai/BotDifficulty.cs`
- **THEN** 存在三个值 `Easy=0`、`Medium=1`、**`Hard=2`**

---

### Requirement: `GomokuAiFactory` 按难度返回 `IGomokuAi` 实例

系统 SHALL 在 `Gomoku.Domain/Ai/GomokuAiFactory.cs` 定义:

```
public static IGomokuAi Create(BotDifficulty difficulty, Random random);
```

支持的分支(**本次追加 `Hard`**):
- `Easy` → 新 `EasyAi(random)`
- `Medium` → 新 `MediumAi(random)`
- **`Hard` → 新 `HardAi(random)`(默认 `searchDepth=2`)**
- 其它 → `ArgumentOutOfRangeException`

工厂本身不持有状态;每次 `Create` 返回一个新实例。

#### Scenario: Easy 分支
- **WHEN** `Create(BotDifficulty.Easy, new Random(1))`
- **THEN** 返回 `IGomokuAi` 实例,运行时类型是 `EasyAi`

#### Scenario: Medium 分支
- **WHEN** `Create(BotDifficulty.Medium, new Random(1))`
- **THEN** 返回 `IGomokuAi` 实例,运行时类型是 `MediumAi`

#### Scenario: Hard 分支
- **WHEN** `Create(BotDifficulty.Hard, new Random(1))`
- **THEN** 返回 `IGomokuAi` 实例,运行时类型是 `HardAi`

#### Scenario: 未定义枚举值
- **WHEN** `Create((BotDifficulty)99, new Random())`
- **THEN** 抛 `ArgumentOutOfRangeException`

---

### Requirement: `BotAccountIds` 固定机器人账号主键

Application 层 SHALL 在 `Gomoku.Application/Abstractions/BotAccountIds.cs` 暴露静态只读字段(**本次追加 `Hard`**):

```
public static readonly Guid Easy   = Guid.Parse("00000000-0000-0000-0000-00000000ea51");
public static readonly Guid Medium = Guid.Parse("00000000-0000-0000-0000-0000000bed10");
public static readonly Guid Hard   = Guid.Parse("00000000-0000-0000-0000-0000000000ad");
public static Guid For(BotDifficulty difficulty);
public static BotDifficulty? TryGetDifficulty(Guid userId);
```

`For` 方法:`Easy → Easy`,`Medium → Medium`,**`Hard → Hard`**,其它 → `ArgumentOutOfRangeException`。

`TryGetDifficulty` 方法:按 Guid 比较返回对应 `BotDifficulty`(三者之一)或 `null`。

**唯一**允许出现这些魔法 Guid 的代码位置:本文件 + `AddBotSupport` migration(Easy/Medium seed)+ `AddHardBotAccount` migration(Hard seed)的 `InsertData` / `HasData` 调用。Handler / Worker MUST 使用 `BotAccountIds.For(...)` / `TryGetDifficulty(...)` 间接访问。

#### Scenario: 三个 Guid 不冲突
- **WHEN** 比较 `BotAccountIds.Easy` / `Medium` / `Hard`
- **THEN** 三者两两不相等

#### Scenario: 按难度解析
- **WHEN** 调 `BotAccountIds.For(BotDifficulty.Hard)`
- **THEN** 返回等于 `BotAccountIds.Hard` 的 `Guid`

#### Scenario: 反向查 Hard
- **WHEN** 调 `BotAccountIds.TryGetDifficulty(BotAccountIds.Hard)`
- **THEN** 返回 `BotDifficulty.Hard`
