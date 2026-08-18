# in-room-chat Specification Delta

## ADDED Requirements

### Requirement: 聊天在 375 px 下不横向溢出,包括最长的一条消息

聊天面板 SHALL 在 375 px 宽度下不产生横向滚动,且该断言 MUST 在**面板里有一条服务端上限长度
(500 字符)的无断点消息**时验证。

这条要求存在的理由与 `web-idiom-chain` 那条同源,而它们防的是同一种脆弱:一条长内容只因为
`overflow-wrap: break-word` 才留在面板里,而那个 class 一直没有任何断言守着 —— 一次样式重写
会发出一个在 375 px 横向滚动的房间页。

**断言分两半,而且各自只证明一半。** 单元测试 MUST 断言渲染消息的元素带有一个会断长词的工具类;
它抓得住 class 被删掉。它 MUST NOT 被当成「样式表仍然定义了那条规则」的证明 —— jsdom 没有
布局引擎也没有样式表,`getComputedStyle` 读不到有效值而 `scrollWidth` 恒为 0。后半句只有浏览器
能给,而浏览器验证是证据不是守卫。

断言 MUST NOT 只认一个 class 名。`break-words` / `break-all` / `wrap-anywhere` 都能防住溢出,
选哪个取决于内容;只认一个会让一次合理的替换变成假失败,而认这一组仍然抓得住「彻底去掉换行」。

#### Scenario: 上限长度的无断点消息不撑破布局
- **WHEN** 面板里含一条 500 字符的无断点消息,视口宽 375 px
- **THEN** `document.documentElement.scrollWidth === clientWidth`

#### Scenario: 渲染消息的元素带有断词工具类
- **WHEN** 审阅渲染消息内容的元素
- **THEN** 它带有 `break-words` / `break-all` / `wrap-anywhere` 之一
