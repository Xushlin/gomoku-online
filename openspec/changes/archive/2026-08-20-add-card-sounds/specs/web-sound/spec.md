# web-sound 的规格变化

## MODIFIED Requirements

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
