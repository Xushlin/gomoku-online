# idiom-chain Specification

## Purpose
TBD - created by archiving change add-idiom-chain. Update Purpose after archive.
## Requirements
### Requirement: 成语接龙的一步是一个成语,合法性有三条

`IdiomChainRules` SHALL 实现 `IGameRules`,`GameKey` 为 `idiom-chain`,并在 `Apply` 中校验三条,任一不满足 MUST 抛 `InvalidMoveException`:

1. **词典里有** —— 该成语能在 `IIdiomLexicon` 中查到。查询 MUST NOT 按层级过滤:玩家答一条冷僻但合法的成语,拒掉是 bug。
2. **接得上** —— 该成语的**首字**等于上一个成语的**末字**。历史为空时(开局第一步)本条不适用,任何词典里的成语都合法。
3. **没说过** —— 本局历史里没有出现过同一个成语。

载荷 MUST 是文本类:收到位置类的一步 MUST 抛 `InvalidMoveException`(通过 `RequireText()`)。

规则实例 MUST 无状态。判定所需的一切都从 `Apply` 收到的历史里读出来:上一个成语是历史最后一项的 `Text`,已用集合是历史全部 `Text`。

#### Scenario: 开局任意成语
- **WHEN** 历史为空,提交一条词典里的成语
- **THEN** 接受,返回 `Ongoing`

#### Scenario: 接得上
- **WHEN** 上一步是「一心一意」,提交「意气风发」
- **THEN** 接受

#### Scenario: 接不上被拒
- **WHEN** 上一步是「一心一意」,提交「风和日丽」
- **THEN** 抛 `InvalidMoveException`

#### Scenario: 不在词典里被拒
- **WHEN** 提交一个四字词但词典里没有
- **THEN** 抛 `InvalidMoveException`

#### Scenario: 重复被拒
- **WHEN** 「一心一意」在本局早些时候出现过,再次提交
- **THEN** 抛 `InvalidMoveException`,即便它接得上

#### Scenario: 位置类载荷被拒
- **WHEN** 提交一步带坐标的走法
- **THEN** 抛 `InvalidMoveException`,错误信息说明本棋种不在盘面上进行

#### Scenario: 冷僻成语照样接受
- **WHEN** 提交一条 `Obscure` 层但确实在词典里的成语
- **THEN** 接受 —— 校验用的是"在不在词典里",不是"常不常见"

---

### Requirement: 同音**不算**接上

`IdiomChainRules` SHALL 只按**字**判断接龙,MUST NOT 按拼音或声调判断。

这是一条规则选择,不是遗漏。按读音接(说 shuō → 硕 shuò)是常见家规,但它把分支因子翻倍、把判定权从"两边都看得见的字"移到"客户端根本拿不到的音"上,而多音字意味着一条成语可以有好几个"末音"。字是双方从文本本身就能核对的东西。

将来要改,改的是一处比较。

#### Scenario: 同音不同字不算接上
- **WHEN** 上一步末字是「说」,提交以「硕」开头的成语
- **THEN** 抛 `InvalidMoveException`

#### Scenario: 判定不读拼音
- **WHEN** 审阅 `IdiomChainRules` 的实现
- **THEN** 其中 MUST NOT 出现对 `Pinyin` 的任何引用

---

### Requirement: 规则永远不判出胜负

`IdiomChainRules.Apply` SHALL 对每一步合法的走子返回 `GameResult.Ongoing`,MUST NOT 返回任何终局结果。

接龙没有终局局面 —— 一方答不上来才结束,而"答不上来"在时间上,不在棋盘上。它由内核既有的两条非规则路径承接:认输,以及 `Room.TimeOutCurrentTurn`。

这是**第一个规则永不判出胜负的棋种**,而它一行内核都不用改:`MoveApplication` 当初就刻意没有 `EndReason`,理由正是"怎么结束的"有三类而规则只可能是其中一类。

#### Scenario: 合法走子一律 Ongoing
- **WHEN** 连续走十步合法的接龙
- **THEN** 每一步都返回 `Ongoing`

#### Scenario: 超时判负走既有路径
- **WHEN** 当前回合玩家超过 `TurnTimeoutSeconds` 未走
- **THEN** 由 `Room.TimeOutCurrentTurn` 判对方胜,`EndReason` 为 `TurnTimeout` —— 与其它棋种同一条路径

---

### Requirement: 成语接龙计分,且有人人对战

`IdiomChainRules` SHALL 声明 `SupportsHumanVsHuman == true` 与 `IsRated == true`。

`SupportsHumanVsHuman` 是**结构性事实**:这是平台加这个游戏的理由 —— 它需要人类对手。

`IsRated` 是**判断**,所以它需要一个写下来的理由:这个棋种有真实的人类对手池,而胜负取决于词汇量,那是一种棋力。不变量 `IsRated ⇒ SupportsHumanVsHuman` 允许它计分,但从未**要求**它计分。

它**没有 AI**,而这与计分是同一件事的两面:查词典就能写出一个近乎不可战胜的机器人,而一旦有了机器人对局又是计分的,阶梯排出来的就会是"谁刷机器人刷得多"—— 一字棋正是因此不计分。没有机器人可刷,`IsRated` 才立得住。

#### Scenario: 能力声明
- **WHEN** 读取 `idiom-chain` 规则
- **THEN** `SupportsHumanVsHuman == true`、`IsRated == true`

#### Scenario: 没有 AI 工厂
- **WHEN** 以 `idiom-chain` 调 `IGameAiRegistry.For`
- **THEN** 解析不出工厂 —— "有没有 AI"由注册表回答,不由声明字段回答

#### Scenario: 可以建真人房
- **WHEN** 以 `gameKey = "idiom-chain"` 调建真人房校验
- **THEN** 通过 —— 与一字棋 / 象棋相反

---

### Requirement: 成语接龙没有盘面

`IdiomChainRules` MUST NOT 实现 `IBoardGameRules`。

它没有行列可言。`GET /api/games` 因此为它返回 `rows: null` / `cols: null`,客户端据此知道"这个棋种没有棋盘",而不是"尺寸未知"。

#### Scenario: 不是棋盘棋种
- **WHEN** 检查 `idiom-chain` 规则的类型
- **THEN** 它 MUST NOT 可赋值给 `IBoardGameRules`

#### Scenario: 描述符里尺寸为 null
- **WHEN** `GET /api/games` 中取 `idiom-chain` 的描述
- **THEN** `rows == null` 且 `cols == null`,MUST NOT 是 `0`

---

### Requirement: 通过真实聚合走一整局

本变更 SHALL 提供一条测试,用**真实的** `Room` 聚合走一局真实的成语接龙 —— 建房、双方入座、交替提交成语、拒绝非法的接法。

它存在的理由与 `XiangqiThroughRoomTests` 相同,而且更强:象棋验证的是"走子类载荷能穿过聚合",接龙验证的是"**没有盘面、没有坐标、规则永不判胜负**的棋种也能穿过同一个聚合"。这是 `generalize-match-payload` 那个接缝唯一真正的检验。

#### Scenario: 一局能走完
- **WHEN** 两名玩家在一个 `idiom-chain` 房间里交替提交合法成语
- **THEN** 每一步都被接受,`Ply` 依次递增,`Move.Text` 记录成语,四个坐标列全为 `null`

#### Scenario: 非法接法被聚合拒绝
- **WHEN** 玩家提交一个接不上的成语
- **THEN** `Room.PlayMove` 抛 `InvalidMoveException`,历史长度不变,回合不变

#### Scenario: 内核未被改动
- **WHEN** 审阅本变更的 diff
- **THEN** 其中 MUST NOT 含 `Rooms/Room.cs`、`Rooms/Game.cs`、`Rooms/Move.cs`、`ValueObjects/MoveIntent.cs`

