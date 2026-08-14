## MODIFIED Requirements

### Requirement: 首发两套主题 —— `material` 与 `system`

平台 SHALL 交付并注册以下主题,每套都包含明暗两份 token 集合:

- `material`:Angular Material 默认风格 —— 较大圆角、明显阴影、Material 调色(primary 落在蓝紫区)。
- `system`:Apple / Fluent-ish 简洁风 —— 更小圆角(≤ 8px)、更轻阴影、更平。
- `ink`(本次新增):活字印刷风 —— 墨蓝/宣纸底、朱砂作强调色、竹青作成功色,圆角小、阴影重(字块的"厚度")。

`ink` 的加入 SHALL 走既有扩展点(见下一条要求),即一个 token 文件 + 一段 `tokens.css` 规则 + 一行 `register` 调用,MUST NOT 修改任何组件或既有主题。

**`ink` 必须同时定义明暗两套。** 成语纵横原型只有暗色墨蓝一种,但主题层的契约是成对的;浅色一套取宣纸为底、墨为字,朱砂保持强调色 —— 朱砂落纸本来就是这套视觉里更古老的那一半。

全部主题 MUST 在明暗两种模式下都通过对比度校验(WCAG AA 标准,正文 text 对 bg 对比度 ≥ 4.5:1)。

#### Scenario: 三套主题都注册
- **WHEN** 启动后读取 `themeService.availableThemes()`
- **THEN** 返回包含 `'material'`、`'system'` 与 `'ink'` 三项

#### Scenario: 所有 6 种组合都工作
- **WHEN** 依次切换到 (material|system|ink) × (light|dark)
- **THEN** 每一种组合下 header 与 home 都正确渲染,无不可见文本(text 与 bg 对比度通过 WCAG AA)

#### Scenario: `ink` 明暗两套都完整
- **WHEN** 读取 `ink` 注册的 token 集合
- **THEN** `light` 与 `dark` 的键集合完全相同,且都覆盖 `colors` / `radii` / `shadows` 的全部字段

#### Scenario: 主题切换器显示新主题
- **WHEN** 打开 header 的主题菜单
- **THEN** 出现 `ink` 条目,文案取自 `header.theme.ink` 翻译键而非裸 key
