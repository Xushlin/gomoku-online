# tasks

## 1. 模型

- [x] `ChatChannel` 按名字解析,含 `unknown`。**不塌成默认频道。**
- [x] `ChatMessage` 模型(id / senderUserId / senderUsername / content / channel / sentAt)。
- [x] `Room` 解析 `chatMessages`。

## 2. 传输

- [x] 订阅 hub `ChatMessage` —— 每次**一条**,推给一个可监听的值。
- [x] `MatchHub.sendChat(roomId, content, channel)`,频道**字符串**。
- [x] `hub_contract_test` 仍然绿(`ChatMessage` 在服务端派生的名单里)。

## 3. 仓库 / ViewModel

- [x] 历史来自 `open` 的快照;推送**追加**。
- [x] 空白不发(**这不是判合法性,是没内容**)。
- [x] 服务端错误码 → `game.errors.invalid-chat` / `game.chat.max-length-error`。

## 4. 界面

- [x] 对局页一个可开合的聊天面板;375 px 上是底部弹出。
- [x] 空态 `game.chat.empty`,输入框 `game.chat.placeholder`,发送 `game.chat.send`。
- [x] **没有围观页签。**
- [x] 一个键都不新增。

## 5. 判据

- [x] 单测:历史 3 条 + 推 1 条 = 4 条,且**前 3 条不变**。
      **正面对照:把追加改成替换,看它红。**
- [x] 单测:不认识的频道不等于 `Room`。
- [x] 单测:空白不发(配前置断言证明**有内容时会发**,否则「没发」是因为整条路都断了)。
- [x] 单测:发送时第三个参数是字符串 `'Room'`,且**恰好三个参数**。
- [x] 单测:错误码映射,以及一个**不是**长度问题的失败不许读作长度问题。
- [x] 走查:界面里不出现围观频道的键。
- [x] 集成测试:两个真玩家,一个发,**另一个屏幕上**出现。判据是屏幕不是服务端。

## 6. 不回归

- [x] `flutter analyze` 零问题;`flutter test` 全绿;`shared_sync_test` 绿(零新增键)。
- [x] 既有集成测试逐个跑。

## 7. 收尾

- [x] `JOURNAL.md` 一条。
