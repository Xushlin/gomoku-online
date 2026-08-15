## ADDED Requirements

### Requirement: 资料页提供棋种切换,只列计分棋种,缺省五子棋

`ProfilePage` 的 header card SHALL 提供一排棋种切换,其选项为 `GameCatalogService` 合并后**服务端说 `isRated === true`** 的棋种,缺省选中 `gomoku`。

切换时 MUST 重新拉 `GET /api/users/{id}?gameKey=<key>`,并把 header 上的 Rating 与战绩四项换成
该棋种的。切换过程 MUST 有 loading 态,MUST NOT 让旧棋种的数字停在屏幕上假装是新棋种的。

**只有一个计分棋种时切换器仍然渲染。** 它是"当前显示的是哪个棋种的分"这个信息的唯一载体,
今天正好只有一个选项 —— 隐藏它会让用户以为那个 1500 是"他的分",而不是"他的五子棋分"。

对局列表(`GamesList`)**不随切换过滤**。`GET /api/users/{id}/games` 没有 `gameKey` 参数,
给它加是另一件事;此刻列表是"全部对局",这与上方"某一个棋种的战绩"并置会让人以为对不上。
→ 列表的标题 MUST 说明它是全部棋种的对局。记为缺口。

#### Scenario: 缺省五子棋
- **WHEN** 首次打开 `/users/:id`
- **THEN** 切换器选中 `gomoku`,请求 MUST NOT 带 `gameKey` 参数(让后端缺省生效)

#### Scenario: 切换重新拉取
- **WHEN** 切到某个别的计分棋种
- **THEN** 发出带 `?gameKey=<key>` 的请求,header 数字换成该棋种的

#### Scenario: 只列计分棋种
- **WHEN** 平台上有 `gomoku`(计分)与 `tictactoe`(不计分)
- **THEN** 切换器只有 `gomoku` 一项,MUST NOT 出现一字棋

#### Scenario: 单选项也渲染
- **WHEN** 只有一个计分棋种
- **THEN** 切换器 MUST 仍然渲染,标明当前显示的是哪个棋种的分

#### Scenario: 切换有 loading 态
- **WHEN** 请求在途
- **THEN** 显示 loading,MUST NOT 继续展示上一个棋种的数字

### Requirement: 该棋种零对局时显示空态,不显示 1200

`gamesPlayed === 0` 时,header card MUST 渲染"尚无对局"空态,MUST NOT 把 `rating` 的 1200 当作战绩展示。

后端对没有战绩行的用户返回 200 + 初始值(`Rating = 1200`、战绩全 0)而不是 404 —— "这个人存在
但没下过这个棋种"是正常答案,404 会被前端误报成"用户不存在"。但直接渲染那份初始值就成了
"1200 分、0 胜 0 负",看起来像**一个下过棋的新手**,而不是一个从没碰过这个棋种的人。

这不是边角:一个新棋种刚上线时,这对**几乎每个用户**都成立。DTO 给的是"真值 + 一个能判断真值
有没有意义的字段"(`gamesPlayed`),判断留给 UI —— 这条 requirement 就是那个判断。

胜率在 `wins + losses + draws === 0` 时已经显示 `—`(既有 `winRateLabel` 逻辑),切换后 MUST 仍然如此。

#### Scenario: 没下过该棋种
- **WHEN** 查看某人在一个他从未下过的棋种上的资料
- **THEN** 渲染"尚无对局"空态;屏幕上 MUST NOT 出现 `1200` 这个数字

#### Scenario: 下过就正常显示
- **WHEN** `gamesPlayed > 0`
- **THEN** 正常渲染 Rating 与战绩四项

#### Scenario: 胜率
- **WHEN** 该棋种零对局
- **THEN** 胜率显示 `—`

### Requirement: `UsersApiService.getProfile` 接受可选 `gameKey`

`UsersApiService.getProfile(userId: string, gameKey?: string)` SHALL 在 `gameKey` 给出时附加 `?gameKey=`,未给出时**不附加该参数**。

这里可选而 `LeaderboardApiService` 必填,不是不一致:资料页首屏就是"不带参数,拿后端的五子棋
缺省"这个语义,而榜页永远知道自己在看哪个棋种。**省略在这里是一个有意义的值**(= 走后端缺省),
在榜页则只会是"忘了传"。

#### Scenario: 不带参数
- **WHEN** 调 `getProfile(id)`
- **THEN** 请求 URL 为 `/api/users/{id}`,MUST NOT 出现 `gameKey` query

#### Scenario: 带参数
- **WHEN** 调 `getProfile(id, 'xiangqi')`
- **THEN** 请求 URL 为 `/api/users/{id}?gameKey=xiangqi`
