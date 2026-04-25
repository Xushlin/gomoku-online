## ADDED Requirements

### Requirement: Header 多一个"音效皮肤"下拉切换器

`src/app/shell/header/header.{ts,html}` SHALL 在现有 sound on/off toggle **之前**(语言 → 主题 → 棋盘 → **音效皮肤** → 音效开关 → 深色 → 用户)新增一个 CDK menu trigger,样式跟 `theme` / `board-skin` 触发器完全一致(`<button>` + `[cdkMenuTriggerFor]`)。

- 标签 `header.sound-pack.label`(en: "Sound pack" / zh-CN: "音效皮肤")
- 当前激活 pack 名通过 `sound.packName()` signal 提供,文本走 `header.sound-pack.{packName}` 翻译键(`wood` / `chiptune`)
- 下拉列表通过 `sound.availablePacks()` 渲染,每项点击调 `sound.activate(name)` —— 并立即 `sound.play('move-place')` 作为预览(被 `muted()` 短路时跳过)

#### Scenario: 下拉列出全部已注册 pack
- **WHEN** 用户点击 sound-pack trigger
- **THEN** 出现的 menu 列出 `wood` 和 `chiptune` 两项(数量与 `availablePacks()` 一致)

#### Scenario: 选择切换 + 持久化
- **WHEN** 用户点 chiptune
- **THEN** `sound.activate('chiptune')` 被调一次;`sound.packName() === 'chiptune'`;`localStorage.gomoku:sound-pack === 'chiptune'`

#### Scenario: 选择后预览
- **WHEN** `muted() === false`,用户点 chiptune
- **THEN** 紧随 `activate` 后调 `sound.play('move-place')` 一次

#### Scenario: muted 时不预览
- **WHEN** `muted() === true`,用户点 chiptune
- **THEN** `sound.activate('chiptune')` 被调;`sound.play` MUST NOT 被调

---

### Requirement: i18n —— `header.sound-pack.*` 双语对齐

`public/i18n/en.json` 与 `public/i18n/zh-CN.json` SHALL 同步新增以下键:

- `header.sound-pack.label`
- `header.sound-pack.wood`
- `header.sound-pack.chiptune`

flatten 后两份 JSON 的 key 集合 MUST 完全相等(零漂移)。

#### Scenario: parity
- **WHEN** 比对 `en.json` 与 `zh-CN.json` flatten key 集合
- **THEN** 差集为空
