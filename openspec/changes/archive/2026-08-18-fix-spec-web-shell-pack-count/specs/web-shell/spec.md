# web-shell Specification Delta

## MODIFIED Requirements

### Requirement: Header 多一个"音效皮肤"下拉切换器

`src/app/shell/header/header.{ts,html}` SHALL 在现有 sound on/off toggle **之前**(语言 → 主题 → 棋盘 → **音效皮肤** → 音效开关 → 深色 → 用户)新增一个 CDK menu trigger,样式跟 `theme` / `board-skin` 触发器完全一致(`<button>` + `[cdkMenuTriggerFor]`)。

- 标签 `header.sound-pack.label`(en: "Sound pack" / zh-CN: "音效皮肤")
- 当前激活 pack 名通过 `sound.packName()` signal 提供,文本走 `header.sound-pack.{packName}` 翻译键
- 下拉列表通过 `sound.availablePacks()` 渲染,每项点击调 `sound.activate(name)` —— 并立即 `sound.play('move-place')` 作为预览(被 `muted()` 短路时跳过)

菜单项 MUST 完全由 `sound.availablePacks()` 派生。**本 requirement 与其 Scenario MUST NOT 点名具体 pack,也 MUST NOT 写下项数** —— 这条限制不是风格:上一版这里写着「列出 `wood` 和 `chiptune` 两项」,而第三个 pack 落地那天它就错了,只有在有人恰好去数的时候才会被发现。

#### Scenario: 下拉列出全部已注册 pack
- **WHEN** 用户点击 sound-pack trigger
- **THEN** menu 里的 menuitem **逐项等于** `sound.availablePacks()`(数量与顺序都相同);断言 MUST 从该清单派生,MUST NOT 写死数量

#### Scenario: 选择切换 + 持久化
- **WHEN** 用户点某个非当前 pack
- **THEN** `sound.activate(name)` 被调一次;`sound.packName() === name`;`localStorage.gewu:sound-pack === name`

#### Scenario: 选择后预览
- **WHEN** `muted() === false`,用户点某个 pack
- **THEN** 紧随 `activate` 后调 `sound.play('move-place')` 一次

#### Scenario: muted 时不预览
- **WHEN** `muted() === true`,用户点某个 pack
- **THEN** `sound.activate(name)` 被调;`sound.play` MUST NOT 被调

---

### Requirement: i18n —— `header.sound-pack.*` 双语对齐

`public/i18n/en.json` 与 `public/i18n/zh-CN.json` SHALL 同时含有:

- `header.sound-pack.label`
- `header.sound-pack.<name>` —— `BUILT_IN_PACKS`(`src/app/core/sound/packs/index.ts`)里**每一个** key 一条

键清单 MUST 从 `BUILT_IN_PACKS` 派生,MUST NOT 在 spec 或测试里逐个列出。**上一版列的是 `label` / `wood` / `chiptune`,漏掉 `minimal`;而 `minimal` 的键之所以存在,是因为 `i18n-parity.spec.ts` 里另一份手写清单点名要了它。** 两份手写清单守着同一个事实,于是第四个 pack 的键不会有任何东西要求 —— 派生之后,加一个 pack 而忘记翻译会当场变红。

flatten 后两份 JSON 的 key 集合 MUST 完全相等(零漂移)。

#### Scenario: 每个已注册 pack 都有双语翻译
- **WHEN** 遍历 `Object.keys(BUILT_IN_PACKS)`
- **THEN** 两份 locale 都能解析出非空的 `header.sound-pack.<name>`

#### Scenario: parity
- **WHEN** 比对 `en.json` 与 `zh-CN.json` flatten key 集合
- **THEN** 差集为空
