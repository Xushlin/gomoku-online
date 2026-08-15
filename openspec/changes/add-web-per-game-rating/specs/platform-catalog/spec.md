## ADDED Requirements

### Requirement: `GamesApiService` 拉取服务端的棋种能力

Web 客户端 SHALL 提供 `GamesApiService`(抽象类作为 DI token + 默认实现),对 `GET /api/games` 发一次请求,返回 `GameDescriptor[]`。

```
export interface GameDescriptor {
  readonly gameKey: string;
  readonly isRated: boolean;
  readonly supportsHumanVsHuman: boolean;
  readonly rows: number;
  readonly cols: number;
}
```

组件 MUST 注入抽象 token,MUST NOT 注入默认实现 —— 与既有四个 API service 同一形状。

#### Scenario: 抽象 token
- **WHEN** 审阅任何消费它的组件
- **THEN** 注入的是抽象类,测试可以替换成 stub

### Requirement: `GameCatalogService` 按 key 合并服务端能力,合不上的不填缺省

`GameCatalogService` SHALL 把 `GamesApiService` 返回的能力按 `gameKey` 合并进 `GAME_REGISTRY` 的清单,合并结果对每份清单暴露一个可空的能力对象。

合不上的清单(谜题类、以及尚未实现的规划中游戏)MUST **没有**能力信息 —— MUST NOT 用
`false` / `0` 之类的缺省值填。它们没有 `IGameRules`,那不是"能力为 false",是"这个问题不适用"。
两者一旦被同一个 `false` 表示,"一字棋不计分"和"成语纵横不是对战游戏"就再也分不开了。

`GAME_REGISTRY`(manifest 清单)**仍然是唯一的注册点**,并且仍然并排列出三个类别 —— 目录页确实
要并排展示它们。服务端能力是**叠加**上去的一层,不替换它。

#### Scenario: 对战棋种合上
- **WHEN** 服务端返回 `gomoku` 的能力
- **THEN** `gomoku` 的清单带上 `isRated === true`

#### Scenario: 谜题游戏没有能力信息
- **WHEN** 查询 `idiom-crossword` 的能力
- **THEN** 结果为"不适用"(`undefined` / `null`),MUST NOT 是一个 `isRated: false` 的对象

#### Scenario: 规划中的游戏没有能力信息
- **WHEN** 查询尚未在服务端登记的 `xiangqi`
- **THEN** 同样是"不适用"

### Requirement: 目录卡片为计分的可玩棋种提供排行榜入口

`/games` 目录页 SHALL 为同时满足 `status === 'available'` 与服务端 `isRated === true` 的游戏卡片渲染一个次级入口"排行榜",指向 `/g/<key>/leaderboard`。

不满足的卡片 MUST NOT 渲染这个入口。具体地:

- **一字棋 MUST NOT 有**(`isRated === false`)。这条要有测试 —— 它是"为什么用服务端投影而不是
  manifest 上一个布尔副本"那份论证的唯一可执行形式。测试挂掉,就说明那份副本又爬回来了。
- **谜题类 MUST NOT 有**(没有能力信息,不适用)。
- **规划中的游戏 MUST NOT 有**(卡片本身就不可交互)。

入口是**次级**的:主入口仍然是"开始游戏"。它 MUST 键盘可达、有可见 focus 环,
并 MUST NOT 触发卡片本身的导航(与既有 username 链接的 `stopPropagation` 约定一致)。

#### Scenario: 五子棋卡片有榜入口
- **WHEN** 目录页渲染,服务端说 `gomoku` 计分
- **THEN** 该卡片有一个指向 `/g/gomoku/leaderboard` 的次级入口

#### Scenario: 一字棋卡片没有
- **WHEN** 目录页渲染,服务端说 `tictactoe` 不计分
- **THEN** 该卡片 MUST NOT 出现排行榜入口

#### Scenario: 成语纵横没有
- **WHEN** 目录页渲染一张谜题卡片
- **THEN** 它 MUST NOT 出现排行榜入口 —— 谜题阶梯是星数 + 用时,不是 ELO

#### Scenario: 点榜入口不触发卡片导航
- **WHEN** 点击排行榜入口
- **THEN** 导航到榜页,MUST NOT 同时触发"开始游戏"
