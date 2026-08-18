## Why

聊天内容服务端上限 500 字符,而一条 500 字的**无断点**字符串只因为 `overflow-wrap: break-word`
才留在 375 px 的面板里。CLAUDE.md 的 deferred 清单为此记了一条:**测试套件里没有任何东西守着它。**
一次样式重写会发出一个在 375 px 横向滚动的房间页,而没有单测能看见。

而 `web-idiom-chain` 对**同一种脆弱**已经有一条 requirement(「词链在 375 px 下不横向溢出,
包括最长的那一条」)—— 只是它也没有实现:那条 spec 说「该断言 MUST 验证」,而套件里没有那条断言。

> 两处一模一样的脆弱,一处写进了 spec 而没实现,另一处两样都没有。

## 单测能证明什么,不能证明什么

**jsdom 没有布局引擎,也没有 Tailwind 样式表。** `getComputedStyle` 读不到有效值,
`scrollWidth` 恒为 0。所以单测唯一能观测的是 class 列表 —— 它钉住的是「让那条 CSS 生效的结构」,
与 `score-leaderboard-page.spec.ts` 为它的 `overflow-x-auto` 做的是同一个妥协。

于是它证明一件事、不证明另一件:**它抓得住 class 被删掉,抓不住样式表不再定义它。**
后半句需要浏览器,而浏览器验证是**证据而不是守卫** —— 它在有人记得的时候才发生,
而这正是这条脆弱长期无人守护的原因。

两半都要有,而且要各自说清自己是哪一半。

## 为什么接受一组 class 名而不是一个

`break-words` / `break-all` / `wrap-anywhere` 都能防住溢出,选哪个取决于内容。
接受其中任意一个,意味着一次合理的替换不会造成假失败,而**彻底去掉换行**仍然会红。
断言跟的是意图,不是它的某一种拼法。

## What Changes

- `src/app/testing/wrapping.ts` —— 一个共享判定 `wrapsLongWords(element)`。两个调用点,
  所以它只存在一份:两份 class 清单就是两次机会互相不一致(与 `board-size.ts` 同一条理由)。
- `chat-panel.spec.ts` 新增一条:渲染一条 **500 字**(服务端上限)无断点消息,断言那个段落会断词。
- `chain-board.spec.ts` 新增一条:渲染词典里**最长**的那条(15 字),同样断言。
  这一条是把 `web-idiom-chain` 已有的 requirement 真正实现。
- `in-room-chat` 加一条 requirement,与 `web-idiom-chain` 那条对称。

## 顺带发现:这个文件从来没渲染过一条消息

加断言时它红在 `NG0201: No provider found for ActivatedRoute` —— 因为发言者名字是一个
`routerLink`。`chat-panel.spec.ts` 之前的五条测试全在测**标签页与输入框**,
一条消息都没渲染过。补 `provideRouter([])` 才让这个 harness 第一次画出消息。

## Impact

- 受影响代码:一个新 helper + 两个 spec 文件各一条测试 + 一处 `provideRouter`。
- 生产代码**零改动** —— 两处 `break-words` 本来就在,这次是给它们装上守卫。
- `in-room-chat` 一条 ADDED;后端零改动。
