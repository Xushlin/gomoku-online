# web-sound Specification

## Purpose
TBD - created by archiving change add-web-sound-fx. Update Purpose after archive.
## Requirements
### Requirement: `SoundService` 抽象 DI token,Signal-backed mute,注册式声音皮肤

`src/app/core/sound/sound.service.ts` SHALL 定义 `abstract class SoundService` 与 `DefaultSoundService` 实现,并通过 `{ provide: SoundService, useClass: DefaultSoundService }` 在 `app.config.ts` 注册。组件 MUST 通过 `inject(SoundService)` 消费抽象类(测试可 stub),MUST NOT 直接 inject 实现类。

API 契约:

```ts
abstract class SoundService {
  abstract readonly muted: Signal<boolean>;
  abstract readonly volume: Signal<number>; // 0–100 整数
  abstract readonly packName: Signal<string>;
  abstract play(event: SoundEventName): void;
  abstract setMuted(muted: boolean): void;
  abstract setVolume(volume: number): void;
  abstract register(name: string, pack: SoundPack): void;
  abstract activate(name: string): void;
  abstract availablePacks(): readonly string[];
}
```

事件集见下面的 tokens requirement —— 它是**平台级**的,不按游戏划分:一款游戏播它需要的子集,不需要任何机制阻止它不播其余的。

`DefaultSoundService` SHALL:

- 构造时注册 `BUILT_IN_PACKS` 里的每一个 pack(见对应 requirement),并把 `wood` 设为默认 active pack。
- 从 `localStorage` 读 `gewu:sound-muted`(`'1'` → muted、`'0'` → not muted、缺省 → not muted)。
- 从 `localStorage` 读 `gewu:sound-pack`,如该 pack 已注册则激活,否则 fall back 到 `wood`。
- 从 `localStorage` 读 `gewu:sound-volume`,解析为 `[0, 100]` 区间整数;缺省、非数字或越界值一律 fall back 到 `100`(与历史行为等响)。
- `setMuted`、`setVolume` 与 `activate` MUST 立即写入 `localStorage`。
- `setVolume(v)` MUST 将输入 clamp 到 `[0, 100]` 并取整后再存储 / 应用;若 AudioContext 已构造,MUST 同步更新 master GainNode。
- master GainNode 的增益 MUST 按感知曲线映射:`gain = (volume / 100)²`(volume 100 → gain 1,与历史行为一致);lazy 构造 AudioContext 时 MUST 用当前 volume 初始化增益,而非字面量 `1`。
- mute 与 volume 是两个独立状态:`setMuted(false)` MUST 恢复此前的 volume 值,MUST NOT 重置或改写它;`setVolume` MUST NOT 改变 muted 状态。
- `play(event)` 当 `muted() === true` **或** `volume() === 0` 时 MUST 早返,不创建任何 AudioContext / Node;否则 lazy 初始化 AudioContext + 单例 master GainNode,然后委托给当前 active pack 的 `play(event, ctx, masterGain)`。
- AudioContext 构造抛出(浏览器拒绝 / jsdom)时 MUST 静默捕获,后续 `play` 一律 no-op,不抛错不打 `console.error`。

#### Scenario: 默认未静音
- **WHEN** 全新用户首次打开 app
- **THEN** `sound.muted()` 返回 false;`sound.packName()` 返回 `'wood'`;`sound.volume()` 返回 100

#### Scenario: muted 状态持久化
- **WHEN** 用户调 `sound.setMuted(true)`,重启 app
- **THEN** `localStorage.getItem('gewu:sound-muted') === '1'`;新一次 service 构造后 `sound.muted()` 返回 true

#### Scenario: volume 持久化与恢复
- **WHEN** 用户调 `sound.setVolume(40)`,重启 app
- **THEN** `localStorage.getItem('gewu:sound-volume') === '40'`;新一次 service 构造后 `sound.volume()` 返回 40

#### Scenario: volume 越界输入被 clamp
- **WHEN** 调 `sound.setVolume(150)` 或 `sound.setVolume(-5)` 或 `sound.setVolume(33.7)`
- **THEN** `sound.volume()` 分别返回 100、0、34(四舍五入);存储值与 signal 一致

#### Scenario: localStorage 垃圾值 fall back 100
- **WHEN** `localStorage['gewu:sound-volume']` 为 `'abc'`、`'-3'` 或 `'999'`,service 构造
- **THEN** `sound.volume()` 返回 100

#### Scenario: muted 时 play 早返
- **WHEN** `sound.muted() === true`,然后 `sound.play('move-place')`
- **THEN** MUST NOT 构造 AudioContext / Oscillator / Buffer;无副作用

#### Scenario: volume 0 时 play 早返
- **WHEN** `sound.muted() === false` 且 `sound.volume() === 0`,然后 `sound.play('move-place')`
- **THEN** MUST NOT 构造 AudioContext / Oscillator / Buffer;无副作用

#### Scenario: 感知增益曲线
- **WHEN** AudioContext 已构造,调 `sound.setVolume(50)`
- **THEN** master GainNode 的 `gain.value` 为 0.25(= (50/100)²)

#### Scenario: mute / volume 互不干扰
- **WHEN** `volume() === 40`,调 `setMuted(true)` 再 `setMuted(false)`
- **THEN** `volume()` 仍为 40;反之 `setVolume(70)` 不改变 `muted()`

#### Scenario: AudioContext 不可用静默降级
- **WHEN** `window.AudioContext` 为 undefined(jsdom)或构造抛出
- **THEN** `sound.play(...)` 不抛错;后续调用一律 no-op

#### Scenario: 抽象 DI 可被 stub
- **WHEN** 测试用共享的 `stubSoundService()`(按 `SoundService` 类型标注,`play` 是 `vi.fn()`)
- **THEN** 组件 `inject(SoundService)` 拿到 stub,无需修改组件源码;`SoundService` 新增抽象成员时 stub **编译不过**

#### Scenario: register + activate 工作流
- **WHEN** 测试调 `sound.register('custom', customPack)`,然后 `sound.activate('custom')`
- **THEN** `sound.packName() === 'custom'`;`sound.availablePacks()` 含 `'custom'`;后续 `play()` 委托给 `customPack.play`

---

### Requirement: `SoundPack` 接口 + tokens 文件

`src/app/core/sound/sound.tokens.ts` SHALL 以 `SOUND_EVENTS` 数组为**唯一来源**,并从它派生 `SoundEventName`:

```ts
export const SOUND_EVENTS = [/* … 平台全集,见源码 */] as const;

export type SoundEventName = (typeof SOUND_EVENTS)[number];

export interface SoundPack {
  readonly play: (event: SoundEventName, ctx: AudioContext, masterGain: GainNode) => void;
}
```

**这条 requirement MUST NOT 把事件名逐个抄在这里。** 它原来抄了九个,而 `add-card-sounds` 加了
`card-deal` / `card-play` —— 一条把源码整段抄进来的 requirement,会在每一次那段源码变化时静静过期,
而这个仓库已经为同一件事付过账(`web-game-board` 抄过一整个源文件,过期了四次)。清单的位置是
`sound.tokens.ts`,而这里说的是**规则**:数组是唯一来源、联合类型从它派生、每个 pack 的 `switch`
必须以 `default: return unhandledSoundEvent(event)` 结尾。

事件集是**平台级的,不是按棋种分的**:一个棋种放它需要的子集。`capture` 在俄罗斯方块里永不触发,
`line-clear` 在象棋里永不触发,而这不需要任何东西去强制。

数组与联合类型 MUST NOT 各写一份。**理由是这个仓库修过四次的同一个缺陷**:一份手写清单,被一个自称「遍历全部」的测试当成全集(`minimal.spec.ts` 的 `ALL_EVENTS` 就是其中一份)。派生之后运行期清单不可能陈旧,遍历它的测试就真的覆盖了每个事件。

`SoundPack.play` 实现 MUST 是同步的(派发音频图后立刻返回,音频本身在浏览器自调度下播放),MUST NOT 返回 Promise,MUST NOT 抛出。

每个 pack 的 `switch` MUST 以 `default: return unhandledSoundEvent(event)` 结尾,其中 `unhandledSoundEvent(event: never): void`。

**这条是机制,不是风格。** 在它之前,往联合类型里加一个事件、三个 pack 一行不改,`tsc` 是 **exit 0** 的 —— 已实测,并用一个故意的类型错误做过正对照证明编译器确实在检查那些文件。`play` 返回 `void`,少一个 `case` 只是走完不返回,TS 没有任何理由报错。tokens 文件的注释曾声称「TS exhaustiveness then forces every registered pack to render it」,那句话是假的。加上 `never` 参数之后,漏掉的事件会让 `event` 在 default 分支里不是 `never`,编译**报错并点名它**。

`unhandledSoundEvent` 运行期 MUST 是静默 no-op,MUST NOT 抛 —— pack「不抛异常」是上面的硬要求,而这一行能被执行到本身已被编译期排除。

#### Scenario: 漏掉一个事件的 pack 编译不过
- **WHEN** 往 `SOUND_EVENTS` 加一个成员,任一 pack 的 `switch` 不加对应 `case`
- **THEN** `tsc` 报错并点名该事件(`Argument of type '<event>' is not assignable to parameter of type 'never'`)

#### Scenario: 运行期清单等于类型
- **WHEN** 测试遍历 `SOUND_EVENTS`
- **THEN** 它逐项等于 `SoundEventName` 的成员集合 —— 因为类型是从它派生的,二者不可能不一致

#### Scenario: pack 接口契约
- **WHEN** 任何新 pack 模块被 `import` + `register`
- **THEN** TS 静态检查 `pack` 满足 `SoundPack` 形状;在编译期可发现签名错误

### Requirement: AppInitializer 早期注入 SoundService

`app.config.ts` 的 `provideAppInitializer` callback MUST 添加一行 `inject(SoundService)`,与现有 `inject(ThemeService)` / `inject(BoardSkinService)` 并列。这保证 service 的构造在第一次 paint 之前发生,pack 注册和 mute 状态读取都已就绪。

#### Scenario: 启动后 packName / muted 状态可读
- **WHEN** app bootstrap 完成,组件树挂载
- **THEN** `inject(SoundService).packName()` 与 `.muted()` 返回 deterministic 值(无 race),后续 `play(...)` 立即可用

---

### Requirement: 颜色 / 组件 / 交互规则继承所有先前立下的约定

新增 `core/sound/` 模块 SHALL 遵守 scaffold / lobby / game-board 立下的全部横切规则,MUST NOT 引入任何绕过这些规则的图样。

- TS 文件无颜色 / palette / Tailwind / CJK 噪声(纯逻辑层)。
- `inject(HttpClient)` 不应出现(无网络调用)。
- 公共方法有 XML / JSDoc 注释解释 *why* 而非 *what*。

#### Scenario: 全局 grep 通过
- **WHEN** 在 `core/sound/` 下跑色值 / palette / CJK 三套 grep
- **THEN** 0 匹配

### Requirement: 内置 `wood` pack 通过 Web Audio API 合成全部事件

`src/app/core/sound/packs/wood.ts` SHALL 导出 `woodPack: SoundPack`,为 `SOUND_EVENTS` 的**每一个**成员合成声音。每个事件 MUST 用 OscillatorNode / Buffer + 短包络合成,MUST NOT fetch 任何外部资源,MUST NOT 引用 `<audio>` 元素。

事件设计(允许微调,不允许偏离风格):

- `move-place`:短噪声脉冲(60ms 内),steep attack & decay。木纹敲击感。
- `capture`:更硬的噪声脉冲(截止频率高于 `move-place`)叠一个低频正弦闷响。「咔 + 一份重量」—— 与平移的软敲击必须能听出区别。
- `line-clear`:三音上行(正弦),行消失的上扬感。
- `line-clear-quad`:四音上行,比 `line-clear` 更高更长。
- `level-up`:相隔一个八度的两音上行。
- `urge`:正弦扫频 220Hz → 520Hz,120ms。引起注意。
- `game-win`:升 C 大三和弦琶音(C5 → E5 → G5),每音 100ms,带短尾音。
- `game-lose`:正弦下扫 600Hz → 180Hz,600ms,gain 衰减。
- `game-draw`:两次 400Hz 软脉冲,中性。

每个事件创建的 audio nodes MUST 在播放结束后通过 `oscillator.stop(when)` / `bufferSource.stop(when)` 自动停止;MUST NOT 持有长引用导致泄漏。

#### Scenario: 每个事件都被覆盖
- **WHEN** 遍历 `SOUND_EVENTS`,逐个传入 `woodPack.play(event, ctx, masterGain)`
- **THEN** 每个事件产生至少一个 OscillatorNode 或 BufferSourceNode,均连接到 `masterGain`

#### Scenario: 不引用外部资源
- **WHEN** 静态 grep `pages/`、`core/sound/` 下的 `wood.ts`
- **THEN** 0 个 `fetch(`、`new Audio(`、`new Image(`,0 个 `.mp3` / `.ogg` / `.wav` 字符串

#### Scenario: 节点正确停止
- **WHEN** wood pack 播放任意事件 X 毫秒,X 大于该事件的合成时长
- **THEN** 对应 OscillatorNode / BufferSourceNode 已 `stop()`;不出现"停留"的长尾

#### Scenario: 吃子与落子听得出区别
- **WHEN** 分别播 `move-place` 与 `capture`
- **THEN** 两者创建的节点构成不同(`capture` 至少多一个声源),MUST NOT 是同一个函数的别名

---

### Requirement: 内置 `chiptune` pack 通过 Web Audio API 合成全部事件

`src/app/core/sound/packs/chiptune.ts` SHALL 导出 `chiptunePack: SoundPack`,为 `SOUND_EVENTS` 的**每一个**成员合成声音。`play` MUST 用 `OscillatorType: 'square'` 与 `'triangle'`(MUST NOT 使用 `'sawtooth'`)合成所有事件,音色与 `wood` pack 显著不同。

事件设计:

- `move-place`:square 波 ~50ms,~150 Hz,steep envelope。
- `capture`:square 波快速下扫(~220 → 80 Hz,~90ms),「咬掉一口」。
- `line-clear`:square 波四级上行 blip。
- `line-clear-quad`:更长的上行 blip 串 + triangle 高音收尾。
- `level-up`:triangle 两音上行收尾。
- `urge`:triangle 波扫频 300 → 700 Hz,100ms。
- `game-win`:升 C 大三和弦 C5/E5/G5 + 高八度收尾 C6,square 波。
- `game-lose`:square 波下扫 640 → 160 Hz,700ms。
- `game-draw`:两次 triangle 波 440Hz 脉冲。

每个事件创建的 audio nodes MUST 在播放结束后通过 `oscillator.stop(when)` 自动停止;MUST NOT 持有长引用导致泄漏。

square 波的 peak gain MUST 比同类 sine 波低约 30–50%,保持感知音量持平。

#### Scenario: 每个事件都被覆盖
- **WHEN** 遍历 `SOUND_EVENTS`,逐个传入 `chiptunePack.play(event, ctx, masterGain)`
- **THEN** 每个事件至少创建一个 `OscillatorNode`,均连接到 `masterGain`,且 `oscillator.type` 为 `'square'` 或 `'triangle'`

#### Scenario: 不引用外部资源
- **WHEN** 静态 grep `core/sound/packs/chiptune.ts`
- **THEN** 0 个 `fetch(`、`new Audio(`、`new Image(`,0 个 `.mp3` / `.ogg` / `.wav` 字符串

#### Scenario: 不使用 sawtooth
- **WHEN** grep `chiptune.ts` 寻找 `'sawtooth'`
- **THEN** 0 匹配

---

### Requirement: 内置 `minimal` pack 通过 Web Audio API 合成全部事件

`src/app/core/sound/packs/minimal.ts` SHALL 导出 `minimalPack: SoundPack`,为 `SOUND_EVENTS` 的**每一个**成员合成声音。`play` MUST 仅用 `OscillatorType: 'sine'`,定位是"安静、不打扰":peak gain MUST 约为 wood pack 同类事件的 50%,单事件总时长 MUST ≤ 400ms(`move-place` ≤ 80ms)。

事件设计(允许微调,不允许偏离"轻"的风格):

- `move-place`:单个 sine 软点击,~660 Hz,≤ 80ms,柔和 attack/decay。
- `capture`:两个下行软点击(~660 → ~440 Hz),相距 ~60ms。
- `line-clear`:两音上行,音高与 `game-win` 不同(避免两个事件听起来一样)。
- `line-clear-quad`:三音上行,总时长 ≤ 400ms。
- `level-up`:单个高音(~C6)软脉冲。
- `urge`:两个相距 ~80ms 的短 sine 点击,~880 Hz。
- `game-win`:两音上行(如 C5 → G5),每音 ≤ 150ms,无琶音无尾音堆叠。
- `game-lose`:两音下行(如 G4 → C4),每音 ≤ 150ms。
- `game-draw`:单个 440 Hz 软脉冲,~120ms。

每个事件创建的 audio nodes MUST 在播放结束后通过 `oscillator.stop(when)` 自动停止;MUST NOT 持有长引用导致泄漏;MUST NOT fetch 任何外部资源。

#### Scenario: 每个事件都被覆盖
- **WHEN** 遍历 `SOUND_EVENTS`,逐个传入 `minimalPack.play(event, ctx, masterGain)`
- **THEN** 每个事件至少创建一个 `OscillatorNode`,均连接到 `masterGain`,且 `oscillator.type` 为 `'sine'`

#### Scenario: 不引用外部资源
- **WHEN** 静态 grep `core/sound/packs/minimal.ts`
- **THEN** 0 个 `fetch(`、`new Audio(`、`new Image(`,0 个 `.mp3` / `.ogg` / `.wav` 字符串

#### Scenario: 只用 sine 波
- **WHEN** grep `minimal.ts` 寻找 `'square'`、`'triangle'`、`'sawtooth'`
- **THEN** 0 匹配

#### Scenario: 新事件也守住「安静 / 短」
- **WHEN** 遍历 `SOUND_EVENTS` 播完每个事件
- **THEN** 所有包络峰值 ≤ 0.18;每个事件的最大 `stop()` 时刻 ≤ 400ms(`move-place` ≤ 80ms)

---

### Requirement: 内置 pack 清单只有一份,DI 与测试都从它派生

`src/app/core/sound/packs/index.ts` SHALL 导出 `PACK_LOADERS: Readonly<Record<string, () => Promise<SoundPack>>>`,作为内置 pack 的**唯一**清单,以及从它派生的 `PACK_NAMES`。`DefaultSoundService` 构造时 MUST 遍历它登记 loader,MUST NOT 另写一串调用;遍历 pack 的测试 MUST 从它取清单并 await,MUST NOT 手写。

`availablePacks()` MUST 逐项等于 `PACK_NAMES`(外部 `register()` 进来的接在后面),并由一条测试钉住。

**内置 pack MUST 是按需加载的,而这个要求是量出来的。** `SoundService` 在 `provideAppInitializer` 里被 inject,所以静态 import 会把三个 pack 的实现放进**首屏**包:把三个文件换成空实现再构建,初始包 **481.23 → 472.54 kB —— 即 pack 是首屏的 8.69 kB**,而它们在用户第一次与页面交互之前一声都发不出来。改成 loader 之后 **473.29 kB**。

由此产生两条硬要求:

- 当前 pack SHALL 在 service 构造时被**预热而不 await** —— 加载不进启动的关键路径。
- 一个在 pack 解出之前就到达的 `play()` MUST **排队,而不是丢掉**。丢掉的表现是「这一局的第一手是静的」,一个只在会话第一次出现、之后永远复现不出来的缺陷。`AudioContext` 仍 MUST 在 `play()` 里**同步**构造(autoplay 策略要的是用户手势那一帧)。
- 「已知 pack」MUST 是「有实现**或**有 loader」。只查已加载实现的那一版会让持久化的选择在**每次启动**时失效 —— 启动那一刻内置 pack 一个都还没加载。
- pack 的 loader 加载失败 MUST 被吞掉并保持静默(与 `AudioContext` 构造失败同一条:没有声音是可以接受的降级)。

**理由与 `BuiltInGameRules.All` / `BuiltInGameAis.All` 相同,而且这个仓库为它付过三次代价**:一份手写清单被一个「与生产 DI 一致」的注释保护着,而生产早就多了一项。`register` 调用与测试 fixture 分开写就是同一个形状。派生之后,加一个 pack 既不需要改测试,也不会产生第二条 requirement —— 而这条 requirement 之前正是有两条,其中一条从第三个 pack 落地那天起就是错的。

初始 active pack 解析顺序仍为:`localStorage('gewu:sound-pack')` → 已注册 → 否则 `'wood'`。

#### Scenario: availablePacks 等于内置清单
- **WHEN** 全新 service 构造
- **THEN** `availablePacks()` 逐项等于 `PACK_NAMES`(当前:`wood`、`chiptune`、`minimal`)

#### Scenario: 遍历 pack 的测试覆盖每一个内置 pack
- **WHEN** pack 契约测试运行
- **THEN** 它对 `PACK_LOADERS` 的每个成员 × `SOUND_EVENTS` 的每个事件都断言过

#### Scenario: localStorage 选择 minimal 持久化
- **WHEN** 调 `service.activate('minimal')`,然后重启 app
- **THEN** 新一次构造的 service `packName() === 'minimal'`

#### Scenario: 切换不影响 mute 状态
- **WHEN** `muted() === true`,调 `activate('chiptune')`
- **THEN** `muted()` 仍为 `true`;切换不偷偷解除静音

#### Scenario: 第一声不会被丢掉
- **WHEN** service 刚构造,pack 还没解出,就调 `play()`
- **THEN** `AudioContext` MUST 已同步建好,而那一声 MUST 在 pack 解出之后真的响

#### Scenario: 持久化的 pack 在启动时仍然有效
- **WHEN** `localStorage` 里是 `minimal`,新构造一个 service
- **THEN** `packName() === 'minimal'` —— 尽管此刻它还没有被加载

