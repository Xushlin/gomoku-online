# wakeng 的规格变化

## MODIFIED Requirements

### Requirement: `seatView` 带 `canFollow`,而候选列表走按需查询

`WakengSeatView` SHALL 多一个 `canFollow: bool` —— 「此刻轮到你时你出得起吗」。
它 MUST 只对这个座位可见(它由这个座位的手牌决定),所以它属于 `seatView`。

**它 MUST NOT 是一个列表。** 候选出法可能有几十项,而每次广播都带着它是
「一个没人渲染但所有人付钱的切片」。候选列表 SHALL 走**按需**的
`GET /api/rooms/{id}/hints`,而只有**在座玩家**拿得到自己的那一份。

**围观者与非玩家拿到的是一个空列表,而不是一次拒绝** —— 这一句是修正:上一版写的是
「MUST 被拒」,而实现从来是 `200` 加一份空列表(量过端点,不是读代码猜的)。理由在
`add-wakeng-play-hints` 的记录里:提示是**可有可无的便利**,而「这里没有可提示的东西」的
正确反应是按钮不出现,不是一条错误路径。**空列表与拒绝在「MUST NOT 返回任何一家的候选」
这一条下长得一样**,而那正是这处漂移能活下来的原因。

`canFollow` 在自由首出、以及不轮到自己时的取值 SHALL 有明确定义并被断言 ——
一个「有时是 false 只因为还没轮到你」的字段会让客户端在错的时候自动过牌。

#### Scenario: 围观者拿到空列表而不是别人的候选
- **WHEN** 围观者请求 `/hints`
- **THEN** 返回一个空列表,而 MUST NOT 返回任何一家的候选

#### Scenario: canFollow 与候选列表一致
- **WHEN** 在若干局面上同时取两者
- **THEN** `canFollow == (候选列表非空)`,逐个局面成立

