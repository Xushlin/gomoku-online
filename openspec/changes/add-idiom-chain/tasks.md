# Tasks — add-idiom-chain

## 1. 词典口

- [x] 1.1 `Gewu.Domain/Idioms/IIdiomLexicon.cs`:一个同步成员 `bool Contains(string)`。
- [x] 1.2 `InMemoryIdiomLexicon`(`FrozenSet<string>`)放在 **Domain** —— 它没有任何外部依赖,就是一个不可变字符串集合。Infrastructure 负责的是"词从哪来",不是"怎么查"。测试因此也用这份**生产实现**装一本小词典,而不是各造一个假口:假一个出来,测的就变成"规则会调词典"而不是"规则怎么判接龙"。
- [x] 1.3 `DbIdiomLexiconFactory` 开一个 scope 读全部 `Word`,构造一次。
- [x] 1.4 `IIdiomRepository` 一个方法都没删。

## 2. 规则

- [x] 2.1 `IdiomChainRules`,`GameKey = "idiom-chain"`。
- [x] 2.2 三条合法性:词典里有、首字接末字、本局没说过;开局不受第二条约束。
- [x] 2.3 `RequireText()` 挡下位置类载荷。
- [x] 2.4 不实现 `IBoardGameRules`。
- [x] 2.5 `SupportsHumanVsHuman = true`、`IsRated = true`,两者的理由都写进 XML 注释 —— `IsRated` 是判断,判断要有理由,而且它与"没有 AI"是同一件事的两面。
- [x] 2.6 `Apply` 永远返回 `Ongoing`。

## 3. 注册表

- [x] 3.1 `BuiltInGameRules.All` 改为 `All(IIdiomLexicon)`。
- [x] 3.2 DI 用一个工厂构造整份注册表;两处遍历测试与 `GomokuRules` 夹具传一本小词典。
- [x] 3.3 没有给成语接龙单开 DI 注册。**这一条的回报是立刻可见的**:改完之后 `IsRated ⇒ SupportsHumanVsHuman` 与建房能力校验两条遍历测试**未改一个断言**就覆盖到了新棋种,并且直接通过。

## 4. 测试

- [x] 4.1 `IdiomChainRulesTests` 12 条:三条合法性正反、开局任意成语、冷僻层照样接受、位置类载荷被拒、空方被拒。
- [x] 4.2 同音不算接上,外加一条**读源码**断言实现里不出现 `Pinyin` —— 行为断言在这套小词典上可能被一个读拼音的实现碰巧满足,这条断言的是它没有别的路可走。
- [x] 4.3 `IdiomChainThroughRoomTests` 5 条:整局走通、每一步四个坐标列全空、非法接法被拒且历史不变、回合序由内核而非棋种保证、规则不判胜负而超时判负。
- [x] 4.4 全量 **907 passed**(此前 889),0 warning。

## 5. 验证

- [x] 5.1 `dotnet build` 0 warning;`dotnet test` 907 passed。
- [x] 5.2 实调 `GET /api/games`:

  ```
  gomoku         rated=True  hvh=True  rows=15   cols=15
  idiom-chain    rated=True  hvh=True  rows=None cols=None
  tictactoe      rated=False hvh=False rows=3    cols=3
  xiangqi        rated=False hvh=False rows=10   cols=9
  ```

  `generalize-match-payload` 开的那条"没有盘面"分支,第一次有真实棋种走到它。

- [x] 5.3 实调建真人房:`idiom-chain` → **201**,`xiangqi` → **400**(`'xiangqi' has no human-vs-human mode`)。
- [x] 5.4 **词典真的装进去了**:直接查库,`Idioms` 表 30,895 行。一本空词典也会让上面每一步看起来正常 —— 注册表照样构造、端点照样返回 —— 只是每一条成语都会被拒。这条检查是唯一能分辨的。
- [x] 5.5 **内核未被改动**:`git status` 中无 `Rooms/Room.cs`、`Rooms/Game.cs`、`Rooms/Move.cs`、`ValueObjects/MoveIntent.cs`。

## 6. 明确不做

- [x] 6.1 没有 AI。查词典就能写出近乎不可战胜的机器人,而机器人对局计分 —— 那会把阶梯变成刷机器人排行(一字棋正是因此不计分)。没有机器人可刷,`IsRated` 才立得住。
- [x] 6.2 没有 hub 路径、没有 UI:文本类走法还没有传输通道。那是 `add-web-idiom-chain` 的事。

## 7. 一处修正了的预测

- [x] 7.1 `add-idiom-dictionary` 建 `IIdiomRepository.FindByWordAsync` 时,注释里写明它是给成语接龙判"这是不是一条真成语"用的。它实现了、测试了、**至今没有生产调用方**,而且它**不能**是这个游戏用的那个:它是异步的,而 `IGameRules.Apply` 同步、在 Domain、由聚合方法内部调用。

  **那个端口选对了消费者,选错了调用路径。** 一个为尚不存在的消费者建的端口是一次预测;这次预测对了一半。两个口现在并存,各有各的调用方。
