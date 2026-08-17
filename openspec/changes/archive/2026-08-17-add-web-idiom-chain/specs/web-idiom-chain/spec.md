# web-idiom-chain Specification Delta

## ADDED Requirements

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
- **WHEN** `mySide` 与 `currentTurn` 一致且 `status === 'Playing'`
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

---

### Requirement: 棋盘不判合法性,只把"下一个字"读出来

`ChainBoard` SHALL 显示下一个词必须以哪个字开头(取自历史最后一项 `text` 的末字;历史为空时不显示),并 MUST NOT 据此禁用提交,MUST NOT 自行判定任何一条合法性。

**这是本仓库对同一个问题的第三个答案,所以它需要被写下来。** `add-web-klotski` 立下的判据是:

> 判据不是"客户端该不该懂规则",而是"懂了会不会造出一个可以分叉的第二真源"。

成语接龙在这条判据下**是分裂的**:三条规则里有两条(接得上、没说过)只用屏幕上已有的东西就能判,第三条(在不在词典里)需要 30,895 条词 —— 那份数据客户端没有、也不该有。

因此:**显示,不判定。** 把末字读出来是展示 —— 那个字本来就在已渲染的某一条词里。理由按权重排:

1. **一个"部分权威"的输入框比一个完全不权威的更糟。** 两种拒绝是即时的、第三种要一个往返,而玩家看不出区别在哪。
2. **客户端的历史可能落后一手。** 按本机最后渲染的那条去拦,会拦掉一个在服务端合法的词。
3. **拒绝现在是有信息的** —— 三条规则各有自己的错误码,问一次服务端就能知道到底哪条不满足。

#### Scenario: 提示下一个字
- **WHEN** 最后一步是「一心一意」
- **THEN** 界面显示下一个词须以「意」开头

#### Scenario: 开局不提示
- **WHEN** 历史为空
- **THEN** MUST NOT 显示首字提示 —— 第一步任何成语都合法

#### Scenario: 接不上的词照样发得出去
- **WHEN** 最后一步是「一心一意」,玩家输入「风和日丽」并提交
- **THEN** `wordSay` emit;提交按钮 MUST NOT 因为接不上而禁用 —— 判定权在服务端

#### Scenario: 说过的词照样发得出去
- **WHEN** 玩家输入本局已出现过的成语
- **THEN** `wordSay` emit;由服务端拒绝

---

### Requirement: 输入框 MUST NOT 假设成语是四个字

输入框 SHALL NOT 施加任何服务端没有的长度或字符限制。唯一允许镜像的上界是 `Move.Text` 的 `HasMaxLength(64)`。

理由是量出来的,不是猜的 —— 随仓库发布的词典里:

| 长度 | 条数 |
| --- | --- |
| 4 | 29,502 |
| 其余(3、5–13、15) | **1,393** |

`maxlength="4"` 会让 1,393 条合法成语打不进去,例如「一不做,二不休」与「各人自扫门前雪,莫管他家瓦上霜」。部分条目内含全角逗号,所以也 MUST NOT 做字符类过滤。

#### Scenario: 长成语打得进去
- **WHEN** 玩家输入一条 15 字的成语
- **THEN** 输入框接受它,提交照常 emit

#### Scenario: 含标点的成语打得进去
- **WHEN** 玩家输入「一不做,二不休」
- **THEN** 输入框接受它

---

### Requirement: 词链在 375 px 下不横向溢出,包括最长的那一条

`ChainBoard` SHALL 在 375 px 宽度下不产生横向滚动,且该断言 MUST 在**词链里有一条最长条目**时验证。

`generalize-lobby` 记下过这条的反面:一次"无横向滚动"检查在内容不存在时会白白通过。四字词几乎不可能溢出,15 字加标点的那条才是真正的用例。

#### Scenario: 长条目不撑破布局
- **WHEN** 词链里含一条 15 字成语,视口宽 375 px
- **THEN** `document.documentElement.scrollWidth === clientWidth`

---

### Requirement: 成语接龙 manifest 从「即将上线」翻到「可玩」

`idiomChainManifest` SHALL 为 `status: 'available'`,`launchRoute: '/g/idiom-chain/lobby'`。

大厅本身不需要任何新代码 —— `/g/:gameKey/lobby` 已经完整渲染这个棋种(见 `enforce-ai-availability`)。

#### Scenario: 目录里可玩
- **WHEN** 打开 `/games`
- **THEN** 成语接龙卡片可点,指向 `/g/idiom-chain/lobby`

#### Scenario: 主页入口条列出它
- **WHEN** 打开 `/home`
- **THEN** 游戏入口条中出现成语接龙

---

### Requirement: i18n —— `idiom-chain.*` 键在两份 locale 中齐备

两份 locale 文件 SHALL 各含一个顶层 `idiom-chain` 块,与 `xiangqi` / `tictactoe` 同构(棋种自己的玩法文案不放在 `games.*` 下 —— 那一块只有目录用的标题与简介)。

模板 MUST NOT 硬编码任何中英文显示串。

#### Scenario: 双语齐备
- **WHEN** 跑 i18n 平价测试
- **THEN** `idiom-chain.*` 的键集合在 `zh-CN` 与 `en` 中完全一致
