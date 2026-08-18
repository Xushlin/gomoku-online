# web-shell Specification Delta

## MODIFIED Requirements

### Requirement: Header 多一个"音效"开关 toggle

`src/app/shell/header/header.{ts,html}` SHALL 在现有 dark-mode toggle 旁边新增第三个状态切换按钮,样式跟 dark toggle 完全一致(`<button role="switch" [attr.aria-checked]>`,移动端隐藏标签部分):

- 标签 `header.sound.label`(en: "Sound" / zh-CN: "音效")
- 状态文本 `header.sound.on` / `.off`(en: "On" / "Off",zh-CN: "开" / "关")
- 点击调 `sound.setMuted(!sound.muted())`
- `aria-checked` 反映当前 **non-muted** 状态(true = 有声音)

注入 `SoundService` 抽象类(已在 `app.config.ts` 注册),不直接 inject 实现。

#### Scenario: 默认状态为开
- **WHEN** 全新用户首次打开 `/home`
- **THEN** 音效 toggle 显示 "On";`aria-checked === "true"`

#### Scenario: 切换后 SoundService 状态翻转
- **WHEN** 用户点 toggle
- **THEN** `sound.muted()` 翻转;按钮文本 / `aria-checked` 同步更新;`localStorage.gewu:sound-muted` 写入新值

#### Scenario: 刷新后状态保留
- **WHEN** 用户切到 muted 后刷新页面
- **THEN** toggle 显示 "Off";`sound.muted() === true`

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
- **THEN** `sound.activate('chiptune')` 被调一次;`sound.packName() === 'chiptune'`;`localStorage.gewu:sound-pack === 'chiptune'`

#### Scenario: 选择后预览
- **WHEN** `muted() === false`,用户点 chiptune
- **THEN** 紧随 `activate` 后调 `sound.play('move-place')` 一次

#### Scenario: muted 时不预览
- **WHEN** `muted() === true`,用户点 chiptune
- **THEN** `sound.activate('chiptune')` 被调;`sound.play` MUST NOT 被调
