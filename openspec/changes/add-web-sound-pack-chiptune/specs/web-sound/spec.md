## ADDED Requirements

### Requirement: 内置 `chiptune` pack 通过 Web Audio API 合成 5 个事件

`src/app/core/sound/packs/chiptune.ts` SHALL 导出 `chiptunePack: SoundPack`。`play(event, ctx, masterGain)` 方法 MUST 用 `OscillatorType: 'square'` 与 `'triangle'`(MUST NOT 使用 `'sawtooth'`)合成所有事件,音色与 `wood` pack 显著不同。

事件设计:

- `move-place`:square 波 ~50ms,~150 Hz,steep envelope。
- `urge`:triangle 波扫频 300 → 700 Hz,100ms。
- `game-win`:升 C 大三和弦 C5/E5/G5 + 高八度收尾 C6,square 波。
- `game-lose`:square 波下扫 640 → 160 Hz,700ms。
- `game-draw`:两次 triangle 波 440Hz 脉冲。

每个事件创建的 audio nodes MUST 在播放结束后通过 `oscillator.stop(when)` 自动停止;MUST NOT 持有长引用导致泄漏。

square 波的 peak gain MUST 比同类 sine 波低约 30–50%(per design D2),保持感知音量持平。

#### Scenario: 5 个事件都被覆盖
- **WHEN** TS 编译期 `SoundEventName` 联合中任一值传入 `chiptunePack.play(event, ctx, masterGain)`
- **THEN** 每个分支至少创建一个 `OscillatorNode`,均连接到 `masterGain`,且 `oscillator.type` 为 `'square'` 或 `'triangle'`

#### Scenario: 不引用外部资源
- **WHEN** 静态 grep `core/sound/packs/chiptune.ts`
- **THEN** 0 个 `fetch(`、`new Audio(`、`new Image(`,0 个 `.mp3` / `.ogg` / `.wav` 字符串

#### Scenario: 不使用 sawtooth
- **WHEN** grep `chiptune.ts` 寻找 `'sawtooth'`
- **THEN** 0 匹配

---

### Requirement: `DefaultSoundService` 默认注册 `wood` 与 `chiptune` 两个 pack

`DefaultSoundService` 构造时 SHALL 调用 `register('wood', woodPack)` 与 `register('chiptune', chiptunePack)`,均注册成功。`availablePacks()` MUST 返回包含 `'wood'` 与 `'chiptune'` 的数组。

初始 active pack 解析顺序仍为:`localStorage('gomoku:sound-pack')` → 已注册 → 否则 `'wood'`。

#### Scenario: 默认 packs 数 ≥ 2
- **WHEN** 全新 service 构造
- **THEN** `availablePacks()` 至少含 `'wood'` 与 `'chiptune'`

#### Scenario: localStorage 选择 chiptune 持久化
- **WHEN** 调 `service.activate('chiptune')`,然后重启 app
- **THEN** 新一次构造的 service `packName() === 'chiptune'`

#### Scenario: 切换不影响 mute 状态
- **WHEN** `muted() === true`,调 `activate('chiptune')`
- **THEN** `muted()` 仍为 `true`;切换不偷偷解除静音
