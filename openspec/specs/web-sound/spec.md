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
  abstract readonly packName: Signal<string>;
  abstract play(event: SoundEventName): void;
  abstract setMuted(muted: boolean): void;
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
- `setMuted` 与 `activate` MUST 立即写入 `localStorage`。
- `play(event)` 当 `muted() === true` 时 MUST 早返,不创建任何 AudioContext / Node;否则 lazy 初始化 AudioContext + 单例 master GainNode,然后委托给当前 active pack 的 `play(event, ctx, masterGain)`。
- AudioContext 构造抛出(浏览器拒绝 / jsdom)时 MUST 静默捕获,后续 `play` 一律 no-op,不抛错不打 `console.error`。

#### Scenario: 默认未静音
- **WHEN** 全新用户首次打开 app
- **THEN** `sound.muted()` 返回 false;`sound.packName()` 返回 `'wood'`

#### Scenario: muted 状态持久化
- **WHEN** 用户调 `sound.setMuted(true)`,重启 app
- **THEN** `localStorage.getItem('gomoku:sound-muted') === '1'`;新一次 service 构造后 `sound.muted()` 返回 true

#### Scenario: muted 时 play 早返
- **WHEN** `sound.muted() === true`,然后 `sound.play('move-place')`
- **THEN** MUST NOT 构造 AudioContext / Oscillator / Buffer;无副作用

#### Scenario: AudioContext 不可用静默降级
- **WHEN** `window.AudioContext` 为 undefined(jsdom)或构造抛出
- **THEN** `sound.play(...)` 不抛错;后续调用一律 no-op

#### Scenario: 抽象 DI 可被 stub
- **WHEN** 测试用 `{ provide: SoundService, useValue: { play: vi.fn(), muted: signal(false), ... } }`
- **THEN** 组件 `inject(SoundService)` 拿到 stub,无需修改组件源码

#### Scenario: register + activate 工作流
- **WHEN** 测试调 `sound.register('minimal', minimalPack)`,然后 `sound.activate('minimal')`
- **THEN** `sound.packName() === 'minimal'`;`sound.availablePacks()` 含 `'minimal'`;后续 `play()` 委托给 `minimalPack.play`

---

### Requirement: 内置 `wood` pack 通过 Web Audio API 合成 5 个事件

`src/app/core/sound/packs/wood.ts` SHALL 导出 `woodPack: SoundPack`,实现 `play(event, ctx, masterGain)` 方法。每个事件 MUST 用 OscillatorNode / Buffer + 短包络合成,MUST NOT fetch 任何外部资源,MUST NOT 引用 `<audio>` 元素。

事件设计(允许微调,不允许偏离风格):

- `move-place`:短噪声脉冲(60ms 内),steep attack & decay。木纹敲击感。
- `urge`:正弦扫频 220Hz → 520Hz,120ms。引起注意。
- `game-win`:升 C 大三和弦琶音(C5 → E5 → G5),每音 100ms,带短尾音。
- `game-lose`:正弦下扫 600Hz → 180Hz,600ms,gain 衰减。
- `game-draw`:两次 400Hz 软脉冲,中性。

每个事件创建的 audio nodes MUST 在播放结束后通过 `oscillator.stop(when)` / `bufferSource.stop(when)` 自动停止;MUST NOT 持有长引用导致泄漏。

#### Scenario: 5 个事件都被覆盖
- **WHEN** TS 编译期 `SoundEventName` 联合中任一值传入 `woodPack.play(event, ctx, masterGain)`
- **THEN** 每个分支产生至少一个 OscillatorNode 或 BufferSourceNode,均连接到 `masterGain`

#### Scenario: 不引用外部资源
- **WHEN** 静态 grep `pages/`、`core/sound/` 下的 `wood.ts`
- **THEN** 0 个 `fetch(`、`new Audio(`、`new Image(`,0 个 `.mp3` / `.ogg` / `.wav` 字符串

#### Scenario: 节点正确停止
- **WHEN** wood pack 播放任意事件 X 毫秒,X 大于该事件的合成时长
- **THEN** 对应 OscillatorNode / BufferSourceNode 已 `stop()`;不出现"停留"的长尾(测试可通过监听 `node.onended` 断言)

---

### Requirement: `SoundPack` 接口 + tokens 文件

`src/app/core/sound/sound.tokens.ts` SHALL 导出 `SoundEventName` 联合类型和 `SoundPack` 接口:

```ts
export type SoundEventName =
  | 'move-place'
  | 'game-win'
  | 'game-lose'
  | 'game-draw'
  | 'urge';

export interface SoundPack {
  readonly play: (event: SoundEventName, ctx: AudioContext, masterGain: GainNode) => void;
}
```

`SoundPack.play` 实现 MUST 是同步的(派发音频图后立刻返回,音频本身在浏览器自调度下播放),MUST NOT 返回 Promise,MUST NOT 抛出。

#### Scenario: pack 接口契约
- **WHEN** 任何新 pack 模块被 `import` + `register`
- **THEN** TS 静态检查 `pack` 满足 `SoundPack` 形状;在编译期可发现签名错误

---

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

