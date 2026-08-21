# game-rules-registry 的规格变化

## ADDED Requirements

### Requirement: `IPlayHintRules` 承载「给我手牌和桌面,列出我能出的」

`Gewu.Domain` SHALL 定义:

```
public interface IPlayHintRules : IGameRules
{
    IReadOnlyList<string> LegalPlays(MatchState state, int seat);
}
```

只有需要「提示 / 要不起」的棋种实现它。棋盘类棋种与成语接龙**一行不动** ——
它们的合法走子空间不是一份可以列举给玩家点选的清单。

**分出一个接口而不是给 `IGameRules` 加成员**,理由与 `IDealtGameRules` /
`IPerSeatViewRules` / `IFirstSeatRules` 当初分出来时逐字相同:留在基接口上,另外五个棋种
就得各写一个骗人的实现,而**骗人的实现是下一个人删不掉的东西**。这是这个模式的第五次。

返回的每一项 SHALL 是**牌的编码串**(`play:` 后面那一段),对内核**不透明** ——
与 `ViewFor` / `CreateSetup` / 闯关那条线的 `LayoutJson` 同一个做法:内核不该知道什么是牌。

`LegalPlays` MUST 是纯函数,并 MUST 只回答 `seat` 自己的那一份 —— 一个能列别人候选的
实现等于把别人的手牌算出来给你。

**`GET /api/rooms/{id}/hints` 的 handler SHALL 通过注册表解析这个接口,而
MUST NOT 内联棋种键。** 它此前写死了 `GameKeys.Wakeng`;加第二个棋种而不加接缝,
Application 层就会长出一个 `switch (gameKey)`,而本 spec 已经写着「实现 MUST NOT 内联任何
『哪些棋种存在』的硬编码列表」。解析不出这个接口的棋种返回空列表 —— 而那不是
「你要不起」,是「这个棋种没有这个功能」。

#### Scenario: 恰好两个内置棋种实现它
- **WHEN** 遍历 `BuiltInGameRules.All(lexicon)`
- **THEN** 恰好两个实现 `IPlayHintRules`,它们的 `GameKey` 恰好是 `{"doudizhu", "wakeng"}`

#### Scenario: handler 不认识棋种键
- **WHEN** 一个不实现本接口的棋种被请求候选
- **THEN** 返回空列表,而 handler MUST NOT 提到任何具体棋种键
