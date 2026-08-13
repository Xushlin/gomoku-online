## ADDED Requirements

### Requirement: Header 品牌名走 i18n 键 `header.brand`

`src/app/shell/header/header.html` 的品牌链接 MUST NOT 硬编码任何展示文本。该链接 SHALL 保持 `routerLink="/home"`,文本走 `{{ 'header.brand' | transloco }}`。

平台更名为 格物 / Gewu 后,品牌名成为**随语言变化**的展示字符串(`zh-CN` 为「格物」、`en` 为 "Gewu"),因此不能再像更名前那样以字面量 `Gomoku` 留在模板里 —— 那既违反"模板禁止硬编码展示字符串"的项目硬规则,也无法随语言切换。

键名归入既有的 `header.*` 命名空间(与 `header.language.*` / `header.sound.*` / `header.sound-pack.*` 同级),不新开顶层命名空间。

#### Scenario: zh-CN 下显示中文品牌名
- **WHEN** 活动语言为 `zh-CN`,shell 渲染完成
- **THEN** header 品牌链接的文本为「格物」

#### Scenario: en 下显示英文品牌名
- **WHEN** 活动语言为 `en`,shell 渲染完成
- **THEN** header 品牌链接的文本为 "Gewu"

#### Scenario: 品牌链接仍然回到首页
- **WHEN** 点击 header 品牌链接
- **THEN** 路由跳转到 `/home`

#### Scenario: 模板中不存在硬编码品牌字面量
- **WHEN** 检索 `src/app/shell/header/header.html`
- **THEN** 文件中 MUST NOT 出现 `Gomoku` / `Gewu` / 「格物」 作为展示文本字面量

### Requirement: i18n —— `header.brand` 双语对齐

`public/i18n/en.json` 与 `public/i18n/zh-CN.json` SHALL 同步新增以下键:

- `header.brand`(en: "Gewu" / zh-CN: 「格物」)

flatten 后两份 JSON 的 key 集合 MUST 完全相等(零漂移)。

#### Scenario: parity
- **WHEN** 比对 `en.json` 与 `zh-CN.json` flatten key 集合
- **THEN** 差集为空
