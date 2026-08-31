# web-idiom-guess Specification

## Purpose
TBD - created by archiving change add-idiom-guess. Update Purpose after archive.
## Requirements
### Requirement: `/g/idiom-guess` 是受保护的懒加载关卡列表与关卡页

`/g/idiom-guess` SHALL 是懒加载路由,带鉴权守卫,展示关卡列表(星级、最好用时、锁定状态),并进入单关页面。

它 MUST 复用既有的 puzzle REST 面 —— 关卡、进度、发起尝试、check、hint、submit 六条都已存在,本变更 MUST NOT 新增任何端点。

manifest 的 `status` 由 `'planned'` 改为 `'available'`,并 MUST 提供非空 `launchRoute`(平台不变量:`available ⇒ launchRoute` 非空)。

#### Scenario: 未登录进不去
- **WHEN** 未登录访问 `/g/idiom-guess`
- **THEN** 跳转登录页

#### Scenario: 目录里不再是「即将上线」
- **WHEN** 打开游戏目录
- **THEN** 猜成语的卡片 MUST NOT 显示 `catalog.coming-soon`,且可点击进入

### Requirement: 客户端不持有答案,也不自行计分

关卡响应 SHALL 只含题面(释义 + 哪几格是空的),MUST NOT 含任何答案;星级与对错一律由服务端给。

与成语纵横同一条,理由也同一个:答案在客户端就是第二份真源,而它一旦下发,任何人打开开发者工具就通关了。

答对后的**出处**由服务端在 `check` 的载荷里回传 —— 客户端拼不出来,词典没有 HTTP 面。

#### Scenario: 关卡响应里没有答案
- **WHEN** 拉取任意一关
- **THEN** 响应体 MUST NOT 包含被挖格子的正确字

#### Scenario: 出处来自服务端
- **WHEN** 答对一条
- **THEN** 出处文本来自 `check` 响应的载荷,MUST NOT 由客户端本地拼装

### Requirement: 没有出处的题答对后不画空纸条

答对一条**没有出处**的成语时,界面 SHALL NOT 渲染一张空的出处纸条。

池子里 9,615 条有 252 条没有出处。**一张空纸条看起来像加载失败**,而它其实是数据本来就没有 —— 两者在屏幕上长得一样。

#### Scenario: 有出处画纸条
- **WHEN** 答对一条有出处的
- **THEN** 显示出处文本

#### Scenario: 没出处不画
- **WHEN** 答对一条没有出处的
- **THEN** MUST NOT 出现空的出处容器
- **AND** 这两条 MUST 同时存在 —— 只有后一条时,一个从不画纸条的实现也能通过

### Requirement: 375 px 下用最长的真实释义量,不用构造的长串

375 px 的布局断言 SHALL 用**语料里最长的那条释义**(74 字),MUST NOT 用一条编出来的长字符串。

释义长度实测:最短 3 字,中位 18,p95 **41**,最长 **74**。74 是真实上界 —— 比它更长的串是不可能出现的输入,拿它测出来的溢出不是缺陷。

**要按 74 量而不是按产物里的 38 量**,理由是那两个数会分叉:当前 12 关里最长的释义是 38 字,而关卡是**可重新生成**的,换一个种子随时会抽到那条 74 字的。按 38 量出来的「不溢出」在重新生成的那天可能不再成立,而没有任何东西会报告它。

而空数据下这条断言恒真:这个仓库四次横向溢出缺陷里有三次是空数据下看不见的。所以断言 MUST 带一个前提 —— 屏幕上确实渲染出了题。

#### Scenario: 最长释义不横向溢出
- **WHEN** 渲染那条 74 字释义的题,视口 375 px
- **THEN** 页面 `scrollWidth == clientWidth`,零元素越界

#### Scenario: 空页面不算通过
- **WHEN** 做上面那条测量
- **THEN** MUST 先断言屏幕上题目数 > 0 —— 一个还没渲染完的页面「零元素越界」,
  而那和布局正确长得一模一样

