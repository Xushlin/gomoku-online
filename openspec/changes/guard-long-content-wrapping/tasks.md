# Tasks — guard-long-content-wrapping

## 1. 共享判定

- [x] 1.1 `src/app/testing/wrapping.ts` —— `wrapsLongWords(element)` + `WRAPPING_UTILITIES`。
- [x] 1.2 文档写明它证明什么、不证明什么(class 被删 vs 样式表不再定义),以及为什么接受一组名字。

## 2. 两条守卫

- [x] 2.1 `chat-panel.spec.ts`:渲染一条 **500 字**(服务端上限)无断点消息并断言断词。
- [x] 2.2 `chain-board.spec.ts`:渲染 **15 字**的最长条目并断言断词 ——
      这是把 `web-idiom-chain` 已有的 requirement 真正实现。
- [x] 2.3 `chat-panel.spec.ts` 补 `provideRouter([])` —— 见 §4。

## 3. spec

- [x] 3.1 `in-room-chat` 加一条 requirement,与 `web-idiom-chain` 那条对称,
      并写明单测能证明的那一半与需要浏览器的那一半。

## 4. 顺带发现:这个文件从来没渲染过一条消息

新断言第一次跑就红在 `NG0201: No provider found for ActivatedRoute`,因为发言者名字是一个
`routerLink`。`chat-panel.spec.ts` 原有的五条测试全在测标签页与输入框 —— **一条消息都没渲染过**,
所以 harness 从来不需要 router。补上 `provideRouter([])` 之后它才第一次画出消息。

这本身就是那条 deferred 记录的注脚:说「没有测试守着换行」时,实际情况比这更空 ——
连消息渲染路径都没被单测走过。

## 5. 变异验证

| 改坏什么 | 结果 |
| --- | --- |
| `chat-panel.html` 的 `<p class="break-words">` → `<p>` | 1 条红 |
| `chain-board.html` 的 `break-words` 去掉 | 1 条红 |

## 6. 浏览器验证(375 px,真实内容)

单测看不见的那一半 —— 样式表到底有没有定义那条规则:

| | 聊天面板 | 词链 |
| --- | --- | --- |
| 内容 | 从 UI 发出的 **500 字**无断点消息(经 SignalR) | 词典最长的一条,**15 字**,作为开局词从 UI 打出 |
| `getComputedStyle(...).overflowWrap` | **break-word** | **break-word** |
| 元素宽度 | 285 px | 235 px |
| `scrollWidth` / `clientWidth` | 375 / 375 | 375 / 375 |
| 页面横向溢出 | **0** | **0** |

两条都是**真数据经真路径**:聊天那条走 SignalR 发出来,词链那条是真的从 30,895 条词典里
取最长的一条作为开局(开局无接龙约束,所以它可以被真的打出去)。

## 7. 顺带:initial 预算从 500 kB 收紧到 480 kB

用户的决定。当前 470.37 kB,余量 **9.63 kB** —— 刻意窄:这个信号的全部意义是它在有人
去查之前先响。

变异验证阈值是活的:设成 460 kB 时构建报
`Budget 460.00 kB was not met by 10.37 kB with a total of 470.37 kB`。

**关于这次核查本身的一条教训**:第一次找那条告警用的 grep 是 `not be met`,而消息里写的是
`was not met`,于是它一度读成「没有告警」。**用来验证信号的工具本身也可能是坏的那个。**
