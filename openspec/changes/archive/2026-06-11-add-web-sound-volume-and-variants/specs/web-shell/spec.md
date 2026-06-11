## ADDED Requirements

### Requirement: 音效菜单包含音量滑杆行

`src/app/shell/header/header.{ts,html}` SHALL 在现有音效皮肤 CDK menu(`[cdkMenuTriggerFor]` 打开的 pack 选项列表)底部追加一个音量滑杆行:

- 行内为一个原生 `<input type="range" min="0" max="100" step="1">`,值绑定 `sound.volume()`,`(change)` 时调 `sound.setVolume(...)`。
- 该行 MUST NOT 标记 `cdkMenuItem` —— 拖动滑杆不得关闭菜单;滑杆本身在 tab 序内,方向键可调(原生行为)。
- 滑杆释放(`change` 事件,非 `input`)且未静音时 SHALL 播放一次 `move-place` 试听,与现有切 pack 试听模式一致。
- 滑杆样式走 token:`accent-color: var(--color-primary)`;MUST NOT 出现色值字面量。
- 行首标签用 `header.sound.volume` 翻译键;`<input>` MUST 带 `[attr.aria-label]`(同键)。

#### Scenario: 拖动滑杆菜单不关闭
- **WHEN** 用户打开音效皮肤菜单,拖动音量滑杆
- **THEN** 菜单保持打开;`sound.setVolume` 在释放时被调用一次

#### Scenario: 滑杆释放播放试听
- **WHEN** 未静音状态下用户把滑杆从 100 拖到 40 并释放
- **THEN** `sound.setVolume(40)` 后播放一次 `'move-place'`;静音状态下不播放

#### Scenario: 键盘可达
- **WHEN** 菜单打开,焦点 tab 到滑杆,按 ←/→
- **THEN** 音量逐步变化;`focus-visible` 样式可见

---

### Requirement: i18n —— `header.sound.volume` 与新变体标签双语对齐

`public/i18n/en.json` 与 `public/i18n/zh-CN.json` SHALL 同步新增:

- `header.sound.volume`(en: "Volume" / zh-CN: "音量")
- `header.sound-pack.minimal`(en: "Minimal" / zh-CN: "极简")
- `header.board-skin.midnight`(en: "Midnight" / zh-CN: "午夜")

两个 locale 文件的 key 集合 MUST 保持一致(parity 测试通过)。

#### Scenario: 双语 key 对齐
- **WHEN** 跑现有 i18n parity 测试
- **THEN** `en.json` 与 `zh-CN.json` key 集合一致,新增 3 个 key 均有非空翻译

#### Scenario: 菜单显示新条目标签
- **WHEN** 打开棋盘皮肤菜单 / 音效皮肤菜单
- **THEN** `midnight` / `minimal` 条目分别显示翻译后的标签,而非裸 key
