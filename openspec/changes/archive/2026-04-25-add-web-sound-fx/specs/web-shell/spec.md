## ADDED Requirements

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
- **THEN** `sound.muted()` 翻转;按钮文本 / `aria-checked` 同步更新;`localStorage.gomoku:sound-muted` 写入新值

#### Scenario: 刷新后状态保留
- **WHEN** 用户切到 muted 后刷新页面
- **THEN** toggle 显示 "Off";`sound.muted() === true`

---

### Requirement: i18n —— `header.sound.*` 双语对齐

`public/i18n/en.json` 与 `public/i18n/zh-CN.json` SHALL 同步新增以下键:

- `header.sound.label`
- `header.sound.on`
- `header.sound.off`

flatten 后两份 JSON 的 key 集合 MUST 完全相等(零漂移)。

#### Scenario: parity
- **WHEN** 比对 `en.json` 与 `zh-CN.json` flatten key 集合
- **THEN** 差集为空
