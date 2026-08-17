# Tasks — add-web-idiom-chain

## 1. 三个错误码(还清 `add-idiom-chain-transport` 记下的债)

- [x] 1.1 `InvalidMoveException` 加三个具名静态工厂:`IdiomNotFound` / `IdiomDoesNotLink` /
      `IdiomAlreadyUsed`,跟 `SelfCheck` 同形。
- [x] 1.2 `IdiomChainRules` 三处 `throw` 各换成对应工厂。载荷形状错(`RequireText` 抛的那条)
      **保持缺省的 `invalid-move`** —— 那不是三条规则之一。
- [x] 1.3 `DomainErrorCodeTests` 的遍历补上 **public static 工厂**。

## 2. Hub 客户端

- [x] 2.1 `GameHubService` 抽象类加 `sayWord(roomId, word)`;默认实现 `conn.invoke('SayWord', …)`。
- [x] 2.2 `StubHub` 跟上 —— 连带发现它**没有**被类型绑住,见 §7。

## 3. `ChainBoard`

- [x] 3.1 `games/idiom-chain/game-key.ts` —— `IDIOM_CHAIN_KEY`。
- [x] 3.2 `chain-board.{ts,html}`:词链 + 输入区,输入输出与既有两个棋盘同形。
- [x] 3.3 显示"下一个成语要以某字开头",取自历史末项末字;**不据此禁用提交**。
- [x] 3.4 禁用条件逐字沿用 `XiangqiBoard`。
- [x] 3.5 输入框 `maxlength="64"`,不做四字限制、不做字符类过滤。

## 4. 接进 RoomPage / ReplayPage

- [x] 4.1 `room-page.html` 第三条 `@else if`;`handleWordSay` 走既有 `submitMove`(一行)。
- [x] 4.2 更新那段"没有第三种形状在路上"的注释 —— 它的预测被检验了,见 §7。
- [x] 4.3 `replay-page` 同样一条。
- [x] 4.4 `hubErrorToKey` 三行 + `HubErrorKey` 三个成员。

## 5. Manifest 与 i18n

- [x] 5.1 manifest 翻到 `available` + `launchRoute: '/g/idiom-chain/lobby'`。
- [x] 5.2 两份 locale 各加顶层 `idiom-chain` 块 + `game.errors.idiom-*` 三个键。

## 6. 测试

- [x] 6.1 Domain:三条规则各抛对应码。
- [x] 6.2–6.4 `ChainBoard` 15 条用例:渲染、回合内外、围观无输入、提交 emit、空白不 emit、
      **接不上/说过了照样 emit**、15 字与含全角逗号的成语打得进去、`maxlength` 恰为 64。
- [x] 6.5 `RoomPage`:接龙房间渲染 `<app-chain-board>`;提交调 `sayWord` 而非 `makeMove`/`movePiece`;
      无盘面棋种不落到 15×15 缺省盘。
- [x] 6.6 `hubErrorToKey`:三个新码映射到**三个不同**的键,且都不是 generic。
- [x] 6.7 i18n 平价(既有测试)。

## 7. 验证

- [x] 7.1 `dotnet build` 0 warning;`dotnet test` **936** 全绿(236 + 84 + 616);
      `npm run lint` 通过;`npm run test:ci` **503** 全绿(服务全停后重跑过一遍)。
- [x] 7.2 浏览器里真下了一局。
- [x] 7.3 三种拒绝各触发一次,线上回三个**不同**的码。
- [x] 7.4 375 px 无横向溢出,**且词链里有词典里最长的那条**。

### 真下了一局

两个真账号(浏览器一方、脚本一方,两条真实 SignalR 连接 —— 同源 localStorage 让两个
标签页当不了两个用户)。四手,其中**两手是从真实 UI 输入框里打出去的**:

```
1 Black 一心一意   ← 浏览器
2 White 意气风发   ← 脚本
3 Black 发号施令   ← 浏览器
4 White 令行禁止   ← 脚本
```

落库四行 `row/col/fromRow/fromCol` 全为 `NULL`。界面上词链按序渲染、标了黑白,
首字提示随最后一手变化(第 4 手之后显示「止」)。

### 三种拒绝,三个码

```
history ends with 令行禁止 -> the head must be 止
  does not link    风和日丽    -> idiom-does-not-link
  not an idiom     止步不前吧  -> idiom-not-found
  legal            止于至善    -> OK
```

`idiom-already-used` 要一个**既接得上又重复**的词,而这需要词典里的一个二元环。
查出来的:`一五一十 → 十不当一 → 一五一十`。第三手接得上「一」,且已经说过:

```
  A 一五一十 -> OK
  B 十不当一 -> OK
  A 一五一十 -> idiom-already-used
```

这一步不是摆样子:随手构一条历史很容易变成**接不上**而不是**重复**,那样测的就是另一条规则。
本变更在单测里正好撞上这件事,见 §7 最后一条。

### 375 px:这次内容是真的在那儿

词典里最长的一条是 **`各人自扫门前雪，莫管他家瓦上霜`(15 字,含全角逗号)**。把它打进一局,
然后在 375 px 下量:

```
viewport 375 | scrollWidth 375 | overflow 0
该行 scrollWidth 310 == clientWidth 310   (行内也不溢出)
```

`generalize-lobby` 记过反面:内容不在时,这条检查会白白通过。开局时 `overflow` 也是 0 ——
**那个 0 什么都没证明**,两个读数长得一模一样。

### 回放

`/replay/:id` 渲染 `<app-chain-board>`,不渲染网格盘也不渲染象棋盘,且**没有输入框**
(readonly + spectator)。`步数:0 / 1` 时显示空态 —— 那是回放该有的样子;跳到末尾后
显示那条 15 字成语。

## 8. 过程中发现的事

### 那段注释的预测被检验了,成立

`room-page` 的注释写着「这个分支已知只有两个…若真出现第三种形状,那时再抽同样便宜」。
第三种形状到了:**多一条 `@else if` 是六行,两侧绑定仍然类型安全**;换注册表要用动态组件、
并放弃对 `(wordSay)` 的编译期检查。结论不变,而它现在是量过的。注释已改成这么说。

### `StubHub` 没有被类型绑住

`room-page.spec.ts` 里的 `class StubHub {}` 是个裸类,**不** `implements GameHubService`。
所以给抽象类加 `sayWord` 之后,这个替身默默地不完整,编译器一声不响 —— 只有真正走到
接龙那条路的测试才会在**运行时**发现。

试过 `satisfies Partial<GameHubService>`:失败,因为那些 `vi.fn(async () => undefined)`
没有签名。绑好它需要让 `makeRoomState` 返回真的 `RoomState`、并给十二个成员都写上类型,
那是一次独立的清理(已记入 CLAUDE.md)。眼下撑住这件事的机制是「每个 hub 方法都有一条
调它的测试」,所以 `says a word through the hub` 这条用例的注释里写明了它兼任这个角色。

### 拆错误码当场抓出一条名字说谎的测试

`A_word_already_played_is_refused_even_though_it_links_on` 用的历史是
`… 止于至善`,而它提交的 `发号施令` **接不上「善」**—— 于是它其实在验第二条规则,
名字却说第三条。两条规则共用一个无区别的 `InvalidMoveException` 时,这个谎无从暴露;
**拆开错误码的第一个收获就是它。** 已改成用二元环 `一心一意 / 合而为一` 真正触发重复那条。

### 浏览器面板不合成帧时,zoneless 的变更检测会滞后

面板未显示时页面不产生帧,`requestAnimationFrame` 排的 CD 就不跑。于是我一度读到
「按钮 disabled」和「输入框没被清空」——**两个都是这个假象**,不是应用缺陷:下一次调用
再读,按钮已启用、值已更新。

所以本次浏览器会话对**DOM 属性时序**的结论一概不可信,那部分的权威是显式
`detectChanges()` 的单测。浏览器证的是另一些东西:组件渲染、词链内容、首字提示、
真实 hub 往返、真实错误码、以及 375 px 的实际布局。**"我没看见"与"它不发生"不是一回事**,
这条本仓库记过一次(`remove-manifest-board`),这次是它的另一个方向。

## 9. 本变更**不**做的事

- **客户端不判合法性。** 三条规则里两条本可以在客户端判,但那会让输入框"部分权威",
  并且可能按落后一手的历史拦掉一个合法词。理由写在组件的文档注释里。
- **不给 `game.room.seat-black`/`-white` 换成接龙专用的措辞。** 接龙的两个座位在协议上仍是
  `Stone.Black`/`White`,侧栏显示「黑方/白方」。给一个没有棋子的游戏叫「黑方」是有点怪,
  但改它要动共享侧栏与五份 spec,而且没有更好的词——留给愿意想清楚措辞的那次变更。
- **不给 `StubHub` 补类型。** 见 §8,需要一次独立清理。
