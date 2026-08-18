# Tasks — add-game-sounds

## 0. 先回答用户问的那半个问题

- [x] 0.1 **象棋已经有音效。** 它走 `RoomPage`,而落子音是按 `moves.length` 变化触发的、与棋种无关 ——
      所以 `add-web-xiangqi` 那天它就免费拿到了落子 / 胜 / 负 / 平 / 催促五个事件。
      「如果没有也要加上」这个条件不成立。
- [x] 0.2 它缺的是**别的东西**:吃子和平移同一个声音。而客户端**已经知道**是哪一条 ——
      `positionAfter(moves)` 是它画棋盘的方式,目标格上原来有没有子,是它画每一帧都要读的事实。

## 1. 先把那句注释测掉

- [x] 1.1 往 `SoundEventName` 加第六个成员,三个 pack 一行不改:`tsc --noEmit -p tsconfig.app.json`
      **exit 0,零报错**。
- [x] 1.2 正对照:往 `wood.ts` 塞 `const now: string = ctx.currentTime`,同一条命令 **4 条报错** ——
      编译器确实在读这些文件。
- [x] 1.3 **第一次探针跑的是 `tsc -p tsconfig.json`,而那个文件是 `"files": []` + `references`,
      `--listFilesOnly` 数出来 0 个文件。** 探针「通过」了,正对照也「通过」了 —— 是正对照戳破的。
      *用来验证机制的工具本身可能什么都没测。*

## 2. 机制

- [x] 2.1 `SOUND_EVENTS` 数组成为唯一来源,`type SoundEventName = (typeof SOUND_EVENTS)[number]`。
- [x] 2.2 `unhandledSoundEvent(event: never): void` —— 静默 no-op,理由写在函数上
      (pack MUST NOT throw,而这行能被执行到本身已被编译期排除)。
- [x] 2.3 三个 pack 的 `switch` 加 `default: return unhandledSoundEvent(event)`。
- [x] 2.4 `packs/index.ts` 的 `BUILT_IN_PACKS` 一份清单;`DefaultSoundService` 遍历它注册。
- [x] 2.5 tokens 文件那句假注释改成实测结果。

## 3. 四个新事件 × 三个 pack

- [x] 3.1 `capture` / `line-clear` / `line-clear-quad` / `level-up` 进 `SOUND_EVENTS`。
- [x] 3.2 `wood`:噪声 clack + 低频闷响(capture)、三/四音上行(消行)、八度上行(升级)。
- [x] 3.3 `chiptune`:square 下扫(capture)、blip 串(消行)、triangle 收尾(升级)。
- [x] 3.4 `minimal`:守住 peak ≤ 0.18、总时长 ≤ 400ms —— 四音里最长的 quad 停在 340ms。

**写 chiptune 时自己踩了一次「两个事件同一个声音」**:`line-clear` 一开始用的是
`[523.25, 659.25, 783.99, 1046.5]`,和 `game-win` 的音**一模一样**,只是快一倍。
指纹能区分(停止时刻不同),耳朵基本不能。改到 E 系上行之后才真的不同。
§6.2 那条「没有两个事件的图相同」的断言就是为这种错误存在的 —— 而它是我自己第一个犯的。

## 4. 俄罗斯方块

- [x] 4.1 `play/announce.ts`:纯函数 `soundForStep(before, after)`,
      优先级 `over > level-up > quad > clear > lock`,一次落子只播一个。
- [x] 4.2 `play.ts` 注入 `SoundService`,记 `{locks, lines, level, over}` 快照,每次重力步后比对。
      **引擎零改动** —— `git diff --name-only` 里没有 `engine/` 下的任何文件。
- [x] 4.3 锁定复用 `move-place`、爆顶复用 `game-lose`,理由写在 spec 与代码注释里。

## 5. 象棋

- [x] 5.1 `position.ts`:`lastMoveCaptured(moves)` —— 去掉最后一手算局面,看目标格原来有没有子。
- [x] 5.2 `room-page.ts`:`isXiangqi()` 时按它选 `capture` / `move-place`。一条分支,不是注册表。

## 6. 测试

- [x] 6.1 `testing/audio-graph.ts` —— 从 `minimal.spec.ts` 抽出并扩到支持 buffer / filter
      (wood 要用),另外把 `ctx.destination` 也建模了:pack 直连它仍然会响,却会绕过音量滑杆。
- [x] 6.2 `packs/pack-contract.spec.ts` —— 遍历 `BUILT_IN_PACKS` × `SOUND_EVENTS`,
      27 个组合 × 5 条断言。**在此之前 `wood.ts` 与 `chiptune.ts` 一条测试都没有。**
- [x] 6.3 `minimal.spec.ts` 只留身份断言(安静 / 短),`ALL_EVENTS` 那份手抄清单删掉。
- [x] 6.4 `sound.service.spec.ts`:`availablePacks()` **逐项等于** `Object.keys(BUILT_IN_PACKS)`,
      替掉三条 `toContain`(三条 `toContain` 在漏掉第四个 pack 时一样绿)。
- [x] 6.5 `testing/sound.ts` 的 `stubSoundService()`,`extends SoundService`;三处 stub 换过来。
      **换的时候确认了那条缺陷是真的**:`room-page.spec.ts` 的 stub 从音量滑杆落地那天起
      就缺 `volume` 与 `setVolume`,而 `useValue` 是 `any`,所以一直没人报。
- [x] 6.6 `announce.spec.ts` —— 逐种组合断言优先级。
- [x] 6.7 `play.spec.ts` —— 硬降播 `move-place`;移动 / 旋转 / 暂停不播;爆顶播 `game-lose`;
      **真的从 UI 消掉一行**播 `line-clear`(见下)。
- [x] 6.8 `room-page.spec.ts` —— 象棋吃子 / 象棋平移 / 五子棋 / 成语接龙 / 首次 hydration 五条。
- [x] 6.9 `position.spec.ts` —— 吃子、平移、只看最后一手、无 from、越界五种输入。

### 6.7 的那条:影子对局

组件不暴露引擎,所以那条测试开一局**同种子的影子对局**替它做决策,然后把**同一串动作**
发给两边;两边种子相同、动作相同,于是始终同步。而「始终同步」不是假设 ——
每落一子都断言页面上渲染的 score / lines / level 逐项等于影子的,不同步会当场红。

再加一条正对照:`expect(shadow.lines).toBeGreaterThan(0)`。没有它,整条测试可以在
一行都没消掉的情况下通过 —— 引擎自己的不变量测试就曾经这样在 `0 === 0` 上绿过。

## 7. 变异验证

七条,全部变红:

| 改坏什么 | 结果 |
| --- | --- |
| `room-page` 去掉吃子分支,一律 `move-place` | RED |
| `soundForStep` 把 level-up 排到 quad 之后 | RED |
| `play.ts` 不再调 `announce()` | RED |
| `wood` 的 `capture` 改成 `playMovePlace` 的别名 | RED |
| `minimal` 的 quad 峰值 0.15 → 0.35 | RED |
| `BUILT_IN_PACKS` 多一个没人声明身份的 pack | RED |
| `lastMoveCaptured` 直接 `return false` | RED |

外加机制本身的变异:往 `SOUND_EVENTS` 加第十个成员、三个 pack 不动 ——
`wood.ts(54,36)`、`chiptune.ts(68,36)`、`minimal.ts(63,36)` 三条
`TS2345: Argument of type '"probe-tenth-event"' is not assignable to parameter of type 'never'`。
**改动之前同一个变异是 exit 0。**

## 8. 浏览器验证:真实 AudioContext

单测用的是 fake graph,它证明不了浏览器会真的建出这张图。真实 Chrome + 真实后端下量的:

### 象棋 —— 这是用户问题的直接答案

同一局、同一个 pack、同一个 AudioContext,唯一变量是目标格上有没有子:

| | BufferSource | BiquadFilter | Oscillator | Gain |
| --- | --- | --- | --- | --- |
| **吃子**(红炮 rank8/file2 隔 黑炮 打 rank1/file2 的**黑马**) | 1 | 1 | **1** | 3 |
| **平移**(红兵 rank7/file1 进一步,落空点) | 1 | 1 | **0** | 1 |

吃子那一手是真吃到了:`aria-label` 从「Rank 1, file 2, **black horse**」变成
「Rank 1, file 2, **red cannon**」,原格变 empty。

### 俄罗斯方块

| 动作 | 观测 |
| --- | --- |
| 点 Start a run | **`ctxs: 0`** —— 一局开始不建 AudioContext,也就不发声 |
| 一次硬降锁定 | `ctxs 1`(state **running**)、buf 1、filter 1、gain 2、osc 0 —— 正是 wood 的 `move-place` |
| 爆顶 | buf 0、**osc 1** —— 和锁定**不是同一张图**,是 `game-lose` |
| 整个页面生命周期 | `ctxs` 始终为 1 —— lazy 单例 |

### 没拿到的:浏览器里的消行

**这个 pane 不 compositing,所以拿不到。** 页面不产帧 → zoneless 的
`requestAnimationFrame` 变更检测不同步跑,任何紧跟按键之后读的 DOM 都是旧的;
而「打到消掉一行」需要按当前棋盘选列,棋盘就是那个读不准的东西。
补 `await` 到每一次按键之后能让 DOM 变准,但一整轮扫描要 ~84 次按键,
和 800ms 的重力抢时间 —— 试了三轮:132 个方块堆到 18 行,**每一行都差一格**,然后爆顶。

所以消行这条的证据是 §6.7 那条走真组件、有 `detectChanges()`、有正对照的单测,
而不是浏览器。**「我没看见」和「它没发生」是两回事,反过来也是。**

第二个 pack 的新声音也只有单测证据:chiptune 的 `capture` 与 `move-place` 都是
1 个 square 振荡器,靠计数区分不了 —— 区分它们要频率斜坡信息,而那正是
`pack-contract.spec.ts` 的指纹断言在做的事。分工写在这里,免得以后误读浏览器那半的范围。

## 9. 包体

473.14 kB / 480 kB —— 比改动前的 470.37 kB 多 2.77 kB(三个 pack 都在 eager 路径上,
因为 `SoundService` 由 `app.config.ts` 注册并在 `provideAppInitializer` 里被 inject)。
余量 6.86 kB,无预算告警。
