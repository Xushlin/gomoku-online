# web-doudizhu 的规格变化

## ADDED Requirements

### Requirement: `CardTable` 组件收座位号,而不是棋色

`src/app/games/doudizhu/card-table/` SHALL 提供 `CardTable`,输入
`state: RoomState | null`、**`mySeat: number | null`**、`submitting`、`readonly`,输出
`action: string`(直接就是 `Move.Text` 的内容)。

**它 MUST NOT 收 `mySide`。** `'black' | 'white' | 'spectator'` 对第三个座位无话可说:2 号座位
上的人在那套词汇里是 `'spectator'`,于是牌不给他画。座位号是线上契约自
`generalize-match-contract` 起就说的东西,而颜色只是棋盘家族在显示层的读法。

`mySeat` 为 `null` 表示不占座位(围观者 / 尚未入座):此时 MUST NOT 渲染手牌区与任何动作按钮,
而公开信息(阶段、地主、底分、底牌、张数、桌面)照常渲染。

`RoomPage.mySeat` SHALL 读 `RoomState.seats`,MUST NOT 读 `black` / `white` —— 后两个字段里
2 号座位根本不出现。`mySide` SHALL 由 `mySeat` 派生,只给那三个两座位棋盘用。

#### Scenario: 三号座位是玩家
- **WHEN** `mySeat === 2` 且轮到 2 号座位
- **THEN** 手牌 MUST 渲染且可点;MUST NOT 被当成围观者

#### Scenario: 围观者没有手牌也没有按钮
- **WHEN** `mySeat === null`
- **THEN** 手牌区与动作按钮 MUST 都不存在

#### Scenario: 非自己回合全部禁用
- **WHEN** `currentSeat` 不是我的座位
- **THEN** 手牌按钮与动作按钮 MUST 全部 `disabled`

#### Scenario: 对局结束之后没有动作
- **WHEN** `phase === 'Finished'`
- **THEN** MUST NOT 渲染任何动作按钮 —— 一个点不动的按钮在屏幕上是个问句

### Requirement: 牌桌不判任何合法性

`CardTable` MUST NOT 实现牌型识别、压牌比较,或任何需要斗地主规则的判断。

判据是 `add-web-klotski` 定下的那把尺子:不问"客户端该不该知道规则",而问**知道了会不会造出
一个能与服务端分叉的第二真源**。斗地主整个落在不该知道的一侧 —— 牌型与压牌都在服务端,并且是
这一局唯一的判据;在客户端再写一遍,分叉在玩家眼里是"这游戏有 bug"。

它 SHALL 只做**不需要规则**的事:

- 能选中 / 取消自己的牌;
- 非自己回合、非 `Playing`、围观者、`readonly` 时禁用;
- 出牌前至少选一张;
- **首出时不能过牌** —— 桌上没牌就是没牌,而这正是"客户端判得出"那一侧的边界。

代价是"这手压不住"要走一趟服务端,而那一趟带回来的是有错误码的具体理由。

#### Scenario: 首出不能过牌
- **WHEN** `tableCards` 为 `null`(自由首出)
- **THEN**「不要」MUST `disabled`;`tableCards` 非空时 MUST 可点

#### Scenario: 一张都没选时出不了牌
- **WHEN** 没有选中任何牌
- **THEN**「出牌」MUST `disabled`

### Requirement: 牌的一字符编码在客户端有一份副本,且只用于显示

`games/doudizhu/cards.ts` SHALL 携带服务端 `Card.Alphabet` 的一份副本
(`A-Za-z@#`,52 张 + 两张王),把编码解成点数 / 花色 / 牌面文字。

这份副本是必需的:服务端送的是编码串,不解码就没有 UI。它能被接受是按这个仓库自己的尺子 ——
**一份副本能不能接受,看的不是它多小,而是它错了会不会有人发现** —— 错一个字符,牌面上立刻是
一张错的牌。它 MUST NOT 反过来用于任何判断。

编码是持久化格式,所以它**永远不变**。认不出的字符 MUST 被跳过而 MUST NOT 抛异常:一个未来的
服务端多送一张这个构建不认识的牌,该表现为那一张画不出来,而不是整页崩掉。

回传给服务端时 MUST 按点数升序拼串 —— 服务端的编码是排序过的,同一手牌只有一种写法。

#### Scenario: 字母表钉死
- **WHEN** 解 `'A'` / `'C'` / `'@'` / `'#'`
- **THEN** 分别是 ♣3 / ♥3 / 小王 / 大王

#### Scenario: 认不出的字符跳过
- **WHEN** 解一个不在字母表里的字符
- **THEN** 返回 `null`,而整手牌里的其余字符照常解出

### Requirement: `seatView` 解不出来时不画,而不是崩

`games/doudizhu/seat-view.ts` SHALL 提供 `parseSeatView(raw)`,把 `GameSnapshotDto.seatView`
解成一个带类型的局面;**解不出来时返回 `null`**。

三种"解不出来"都走这条路:字段不在(棋种没有隐藏状态)、对局还没开始(服务端给 `null`)、
以及一个这个构建读不懂的形状。三种的正确反应都是"这一块先不画",而不是让房间页整页挂掉。

`kitty` 为 `null` 与 `kitty` 为空 MUST 是两个不同的答案:前者是"底牌还没翻开",在叫分阶段是常态。

#### Scenario: 三种解不出来都是 null
- **WHEN** 传入 `null` / `''` / 非 JSON / `phase` 不认识的对象
- **THEN** 都返回 `null`,且房间页 MUST NOT 抛异常

#### Scenario: 没有局面时画占位
- **WHEN** `seatView` 解不出来
- **THEN** 牌桌 MUST 渲染一个「等待发牌」占位,MUST NOT 渲染手牌区

### Requirement: 侧栏在座位多于两个时说座位号

房间侧栏的"轮到谁"文字 SHALL 在 `seats.length > 2` 时说**座位号**(`game.turn.seat-turn`),
而不是「黑方 / 白方」。

**这是在浏览器里发现的**:一局斗地主轮到 2 号座位时,侧栏写的是「白方走棋」—— 而那一桌上
没有白方。判据是 `seats.length`,MUST NOT 是棋种键,也 MUST NOT 去问棋种注册表要 `seatCount`:
座位表就在这份快照里,而多要一个异步依赖只为知道一个已经在手上的数字,是把一个同步事实
变成一个加载态。

#### Scenario: 三座位说座位号
- **WHEN** `seats.length === 3` 且 `currentSeat === 2`
- **THEN** 文字 MUST 是「轮到 3 号座位」,MUST NOT 出现「白方」

#### Scenario: 两座位一字不变
- **WHEN** `seats.length === 2`
- **THEN** 文字仍是「黑方 / 白方走棋」

### Requirement: i18n —— `doudizhu.*` 与 `games.doudizhu.*` 在两份 locale 中齐备

`public/i18n/zh-CN.json` 与 `en.json` SHALL 各增加 `games.doudizhu.{title,description}` 与
`doudizhu.*`(阶段三个、牌桌九个、动作五个),外加 `game.turn.seat-turn`。
两份文件的键集合 MUST 完全一致 —— 缺键在运行时表现为屏幕上出现原始键名。

#### Scenario: 键集合一致
- **WHEN** 比较两份 locale 的键集合
- **THEN** 完全相等
