# web-idiom-chain 的规格变化

## MODIFIED Requirements

### Requirement: `ChainBoard` 组件 —— 一条词链加一个输入框

Web 客户端 SHALL 提供 `ChainBoard`(`src/app/games/idiom-chain/chain-board/chain-board.{ts,html}` + spec),standalone、`OnPush`、纯展示,输入输出为:

```
readonly state = input<RoomState | null>(null);
readonly mySide = input<'black' | 'white' | 'spectator'>('spectator');
readonly submitting = input<boolean>(false);
readonly readonly = input<boolean>(false);
readonly wordSay = output<string>();
```

与 `Board` / `XiangqiBoard` 完全同形 —— 读过任何一个就读懂这一个。它 MUST NOT 注入 hub、路由或目录服务:走子由 `RoomPage` 发出。

组件渲染两部分:

1. **词链** —— 按 `ply` 升序列出本局每一步的 `text`,标出走它的一方(黑/白)。最新一条在末尾。
2. **输入区** —— 一个文本框 + 一个提交按钮,外加**下一个词必须以哪个字开头**的提示。

禁用条件与既有两个棋盘逐字相同:`readonly` / `submitting` / `mySide === 'spectator'` / `status !== 'Playing'` / 非本方回合。围观者 MUST NOT 看到输入区。

#### Scenario: 渲染既有词链
- **WHEN** 房间历史为「一心一意」→「意气风发」
- **THEN** 两条按序渲染,各自标出走子方

#### Scenario: 轮到自己时可输入
- **WHEN** `mySide` 对应的座位号等于 `currentSeat` 且 `status === 'Playing'`
- **THEN** 输入框与提交按钮可用

#### Scenario: 不是自己的回合时只读
- **WHEN** 轮到对方
- **THEN** 输入框与提交按钮禁用

#### Scenario: 围观者没有输入区
- **WHEN** `mySide === 'spectator'`
- **THEN** MUST NOT 渲染输入框

#### Scenario: 提交后发出词
- **WHEN** 玩家输入「意气风发」并提交
- **THEN** `wordSay` emit `'意气风发'`;组件自身 MUST NOT 调 hub

#### Scenario: 空白不发出
- **WHEN** 输入框为空或只有空白
- **THEN** 提交 MUST NOT emit

