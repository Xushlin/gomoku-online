# web-lobby Specification Delta

## MODIFIED Requirements

### Requirement: 无人人对战 / 未知棋种的大厅显示说明,而不是重定向

`/g/:gameKey/lobby` 在棋种未登记、或其 `supportsHumanVsHuman === false` 时 SHALL 渲染一个说明性面板并给出去处链接,MUST NOT 静默重定向。

重定向会把一个拼错的 URL 伪装成别的东西 —— 用户看到的是一个他没要求的页面,却没有任何提示说明为什么。

面板 MUST 区分两种情况:未登记的键说"本平台没有这个游戏"(链接到 `/games`);已登记但无人人对战的说"这个游戏目前只有人机对战"(链接到该棋种的 `launchRoute`)。

能力来自 `GameCapabilitiesService`。页面 MUST 在 `capabilities.loaded()` 为 false 时保持骨架 —— 沿用 `remove-manifest-board` 立下的门:**"描述符还没到"与"这个键不认识"是两件事**,把后者的界面画在前者身上,就是在用户即将得知答案的那一刻先给他一个错的。

这是**展示决定**。服务端无论客户端画什么都会拒绝为这类棋种创建人人对战房间(见 `game-rules-registry` 的强制要求),本条 MUST NOT 被当作强制手段。

#### Scenario: 未登记的键
- **WHEN** 用户访问 `/g/go/lobby`
- **THEN** 显示"本平台没有这个游戏" + 指向 `/games` 的链接;MUST NOT 发出房间列表请求;MUST NOT 重定向

#### Scenario: 只有人机的棋种
- **WHEN** 用户访问 `/g/tictactoe/lobby`
- **THEN** 显示"目前只有人机对战" + 指向 `/g/tictactoe` 的链接;MUST NOT 渲染 Active rooms 卡片

#### Scenario: 象棋不再走这条路
- **WHEN** 用户访问 `/g/xiangqi/lobby`
- **THEN** 渲染完整大厅(房间列表 + 人机卡 + 排行榜),MUST NOT 显示"目前只有人机对战"。**本场景此前正是以象棋举例的**,而象棋自 `enable-xiangqi-human-play` 起开放人人对战 —— 一字棋现在是这条路径唯一的真实用例

#### Scenario: 描述符未到时不下结论
- **WHEN** `capabilities.loaded()` 为 false
- **THEN** 页面显示骨架,MUST NOT 显示上述任何一种说明面板
