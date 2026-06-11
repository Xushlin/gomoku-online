## MODIFIED Requirements

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

`SoundEventName = 'move-place' | 'game-win' | 'game-lose' | 'game-draw' | 'urge'`(共 5 个事件);新增事件 MUST 同时更新 type 与所有已注册 pack。

`DefaultSoundService` SHALL:

- 构造时注册内置 `wood` pack(见下条 Requirement),并把它设为默认 active pack。
- 从 `localStorage` 读 `gomoku:sound-muted`(`'1'` → muted、`'0'` → not muted、缺省 → not muted)。
- 从 `localStorage` 读 `gomoku:sound-pack`,如该 pack 已注册则激活,否则 fall back 到 `wood`。
- 从 `localStorage` 读 `gomoku:sound-volume`,解析为 `[0, 100]` 区间整数;缺省、非数字或越界值一律 fall back 到 `100`(与历史行为等响)。
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
- **THEN** `localStorage.getItem('gomoku:sound-muted') === '1'`;新一次 service 构造后 `sound.muted()` 返回 true

#### Scenario: volume 持久化与恢复
- **WHEN** 用户调 `sound.setVolume(40)`,重启 app
- **THEN** `localStorage.getItem('gomoku:sound-volume') === '40'`;新一次 service 构造后 `sound.volume()` 返回 40

#### Scenario: volume 越界输入被 clamp
- **WHEN** 调 `sound.setVolume(150)` 或 `sound.setVolume(-5)` 或 `sound.setVolume(33.7)`
- **THEN** `sound.volume()` 分别返回 100、0、34(四舍五入);存储值与 signal 一致

#### Scenario: localStorage 垃圾值 fall back 100
- **WHEN** `localStorage['gomoku:sound-volume']` 为 `'abc'`、`'-3'` 或 `'999'`,service 构造
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
- **WHEN** 测试用 `{ provide: SoundService, useValue: { play: vi.fn(), muted: signal(false), volume: signal(100), ... } }`
- **THEN** 组件 `inject(SoundService)` 拿到 stub,无需修改组件源码

#### Scenario: register + activate 工作流
- **WHEN** 测试调 `sound.register('custom', customPack)`,然后 `sound.activate('custom')`
- **THEN** `sound.packName() === 'custom'`;`sound.availablePacks()` 含 `'custom'`;后续 `play()` 委托给 `customPack.play`

## ADDED Requirements

### Requirement: 内置 `minimal` pack 通过 Web Audio API 合成 5 个事件

`src/app/core/sound/packs/minimal.ts` SHALL 导出 `minimalPack: SoundPack`。`play(event, ctx, masterGain)` 方法 MUST 仅用 `OscillatorType: 'sine'` 合成所有事件,定位是"安静、不打扰":peak gain MUST 约为 wood pack 同类事件的 50%,单事件总时长 MUST ≤ 400ms(`move-place` ≤ 80ms)。

事件设计(允许微调,不允许偏离"轻"的风格):

- `move-place`:单个 sine 软点击,~660 Hz,≤ 80ms,柔和 attack/decay。
- `urge`:两个相距 ~80ms 的短 sine 点击,~880 Hz。
- `game-win`:两音上行(如 C5 → G5),每音 ≤ 150ms,无琶音无尾音堆叠。
- `game-lose`:两音下行(如 G4 → C4),每音 ≤ 150ms。
- `game-draw`:单个 440 Hz 软脉冲,~120ms。

每个事件创建的 audio nodes MUST 在播放结束后通过 `oscillator.stop(when)` 自动停止;MUST NOT 持有长引用导致泄漏;MUST NOT fetch 任何外部资源。

#### Scenario: 5 个事件都被覆盖
- **WHEN** TS 编译期 `SoundEventName` 联合中任一值传入 `minimalPack.play(event, ctx, masterGain)`
- **THEN** 每个分支至少创建一个 `OscillatorNode`,均连接到 `masterGain`,且 `oscillator.type` 为 `'sine'`

#### Scenario: 不引用外部资源
- **WHEN** 静态 grep `core/sound/packs/minimal.ts`
- **THEN** 0 个 `fetch(`、`new Audio(`、`new Image(`,0 个 `.mp3` / `.ogg` / `.wav` 字符串

#### Scenario: 只用 sine 波
- **WHEN** grep `minimal.ts` 寻找 `'square'`、`'triangle'`、`'sawtooth'`
- **THEN** 0 匹配

---

### Requirement: `DefaultSoundService` 默认注册 `wood`、`chiptune`、`minimal` 三个 pack

`DefaultSoundService` 构造时 SHALL 调用 `register('wood', woodPack)`、`register('chiptune', chiptunePack)` 与 `register('minimal', minimalPack)`,均注册成功。`availablePacks()` MUST 返回包含三者的数组。

初始 active pack 解析顺序仍为:`localStorage('gomoku:sound-pack')` → 已注册 → 否则 `'wood'`。

#### Scenario: 默认 packs 数 ≥ 3
- **WHEN** 全新 service 构造
- **THEN** `availablePacks()` 至少含 `'wood'`、`'chiptune'` 与 `'minimal'`

#### Scenario: localStorage 选择 minimal 持久化
- **WHEN** 调 `service.activate('minimal')`,然后重启 app
- **THEN** 新一次构造的 service `packName() === 'minimal'`
