## Why

俄罗斯方块一个音都没有 —— `grep SoundService` 在 `games/tetris/` 下 0 命中。七款游戏里它是唯一一个把整套规则放在客户端、却什么都不响的。

**而象棋的答案是「已经有了」。** 它走 `RoomPage`,而 `RoomPage` 的落子音是按 `moves.length` 变化触发的、与棋种无关,所以 `add-web-xiangqi` 那天象棋就免费拿到了落子 / 胜 / 负 / 平 / 催促五个事件 —— 用户问的那半个条件不成立。

它缺的是**另一件事**:吃子和平移是同一个声音。象棋里「他动了一步」和「他吃了我的車」是两条完全不同的信息,而客户端**已经知道**是哪一条 —— `positionAfter(moves)` 是它画棋盘的方式,目标格上原来有没有子,是它画每一帧都要读的事实。

## 一个测出来是假的机制,恰好在这次要承重

`sound.tokens.ts` 的注释写着:新增第六个事件「TS exhaustiveness then forces every registered pack to render it — or fall through silently」。这句话的两半互相矛盾,而**测出来只有后半句是真的**:

| 探针 | 结果 |
| --- | --- |
| 往 `SoundEventName` 加第六个成员,三个 pack 一行不改,`tsc -p tsconfig.app.json` | **exit 0,零报错** |
| 正对照:往 `wood.ts` 塞一个真类型错误 | 4 条报错 —— 编译器确实在检查这个文件 |

原因是 pack 的 `play` 返回 `void`,`switch` 少一个 `case` 只是走完不返回,TS 没有任何理由报错。`web-sound` spec 自己写了「新增事件 MUST 同时更新 type 与所有已注册 pack」,还有一条 Scenario 断言「**WHEN** TS 编译期……」—— **要求写下来了,机制不存在。**

这在今天之前无所谓:五个事件是一次写完的,从来没有人加过第六个。而这次要加 4 个事件 × 3 个 pack = 12 个声音,漏掉任何一个的表现是「换到 chiptune 之后俄罗斯方块消行没声音」—— 只有人戴着耳机逐个 pack 试听才会发现。

> 一条没有机制的要求,和没写这条要求的区别,只在有人恰好记得的时候。

**关于这次测量本身还有一条:第一次探针跑的是 `tsc -p tsconfig.json`,而那个文件是 `"files": []` + `references`,编译的文件数是 0。** 探针「通过」了,正对照也「通过」了 —— 是正对照把它戳破的。*用来验证机制的工具本身可能什么都没测。*

## What Changes

### 机制:两条,各管一半

- **`SOUND_EVENTS` 数组成为唯一来源,类型从它派生**(`type SoundEventName = (typeof SOUND_EVENTS)[number]`)。运行期清单因此不可能陈旧 —— 而它今天是陈旧的:`minimal.spec.ts` 手抄了一份 `ALL_EVENTS`,正是这个仓库修过四次的那个形状(一份手写清单,被一个「遍历注册表」的测试当成注册表)。
- **每个 pack 的 `switch` 末尾加 `default: return unhandledSoundEvent(event)`**,参数类型是 `never`。漏一个事件时,`event` 在 default 分支里不是 `never`,编译**报错并点名漏掉的那个**。这就是那句注释一直声称、而从来不存在的东西。

`unhandledSoundEvent` 运行期是静默 no-op,不抛 —— spec 要求 pack MUST NOT throw,而编译期已经证明这行到不了。

### 事件:加 4 个,复用 2 个

`capture`、`line-clear`、`line-clear-quad`、`level-up`。

**取舍的规则是:声音播报「发生了什么」,不播报「你按了什么」。** 玩家自己按下的硬降不需要被告知;消掉的行、升的级、被吃的子是结果。按这条规则:

- 方块**锁定**复用 `move-place` —— 它就是「一次落子生效了」,pack 决定它听起来像什么。
- **顶到天花板**复用 `game-lose` —— 一局 score-attack 只会以爆顶结束,而下扫的「结束了」正是这个事件的音色。新造一个 `game-over` 会是同一件事的第二个名字。
- **四行同时消**单独一个事件:`LINE_SCORES` 的注释说 100 vs 800 那个差值「是整个『攒一个 tetris』的决定」。如果声音不区分它,音频就在和计分表唱反调。

事件集是**平台级的,不是按游戏分的**:一款游戏播它需要的子集。`capture` 在俄罗斯方块里永不触发,`line-clear` 在象棋里永不触发,这不需要任何机制去阻止。

### 俄罗斯方块:一个纯函数决定播什么

引擎是无 Angular 的纯状态机,被 34 条测试压着,不能往里注入服务。所以组件**观察**引擎:记一份 `{locks, lines, level, over}` 快照,每次重力步之后比对 —— 和 `RoomPage` 的 `previousMoveCount` 是同一个模式。

比对结果交给 `soundForStep(before, after)`,一个纯函数,**一次落子只播一个声音**,优先级 `over > level-up > quad > clear > lock`。同时响两个是浑的;而升级排在四行消之前,因为**升级改变游戏**(重力变快,`gravityIntervalMs`),四行消只是奖励,而奖励玩家已经在计分板上看见了。

### 象棋:一个分支

`lastMoveCaptured(moves)` 加在 `games/xiangqi/position.ts` —— 把 `moves` 去掉最后一手算出局面,看目标格原来有没有子。`RoomPage` 在 `isXiangqi()` 时用它选 `capture` 或 `move-place`。

这不是注册表:有吃子概念的棋种就这一个。*一个只有一条分支的 switch 仍然是 switch。* `RoomPage` 本来就有 `isXiangqi()` / `isIdiomChain()` 两个分支在选棋盘组件。

### 顺带修掉三个空壳

1. **`BUILT_IN_PACKS`** —— `DefaultSoundService` 构造函数里三行 `register(...)` 与测试里的 pack 清单合成一份,`availablePacks()` 必须等于它的 keys。`BuiltInGameRules.All` / `BuiltInGameAis.All` 的第三次应用,理由一样。
2. **一个遍历所有 pack × 所有事件的契约测试** —— 今天 `wood.ts` 和 `chiptune.ts` **一条测试都没有**,只有 `minimal.spec.ts` 有。新增 12 个声音里有 8 个落在从来没被测过的文件里。fake audio graph 从 `minimal.spec.ts` 抽出来共用,minimal 自己的「安静 / 短」身份断言留在原处。
3. **`room-page.spec.ts` 的 SoundService stub 是个没类型的对象字面量,少了 `volume` 和 `setVolume`** —— `useValue` 是 `any`,所以少了也不报。这正是 `StubHub` 那个缺陷,已经修过一次。改成一个共用的、按 `SoundService` 类型标注的 stub:少一个成员就编译不过。它还是本次断言 `play` 到底被喂了什么的地方 —— **今天三个 stub 的 `play: vi.fn()` 没有任何一条断言看过。整个音效功能没有一条行为测试。**

## Impact

- 受影响 spec:`web-sound`(tokens / 三个 pack / 新增内置清单)、`web-tetris`(新增音效)、`web-game-board`(落子音从 5 事件到 6 事件)。
- 受影响代码:`core/sound/`(tokens + 3 packs + service 注册)、`games/tetris/play/`、`games/xiangqi/position.ts`、`pages/rooms/room-page/`、3 个测试 stub。
- **后端零改动。** 无新 i18n key(音效没有文案)。
- 顺带修一处 spec 漂移:`web-game-board` 的 game-win Scenario 里 `endReason: 'Connected5'` 早在 `generalize-match-domain` 改名成 `Decided` 了。这条 Scenario 本来就要重写,顺手改对。
