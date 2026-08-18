# web-board-skins Specification

## Purpose
TBD - created by archiving change add-web-sound-volume-and-variants. Update Purpose after archive.
## Requirements
### Requirement: `BoardSkinService` 抽象 DI token + 注册表 + `data-board-skin` 应用

`src/app/core/theme/board-skin.service.ts` SHALL 定义 `abstract class BoardSkinService` 与 `DefaultBoardSkinService` 实现,并通过 `{ provide: BoardSkinService, useClass: DefaultBoardSkinService }` 在 `app.config.ts` 注册(且在 `provideAppInitializer` 中 `inject(BoardSkinService)`,保证首次 paint 前已应用)。组件 MUST 通过抽象类 inject(测试可 stub),MUST NOT 直接 inject 实现类。

API 契约:

```ts
abstract class BoardSkinService {
  abstract readonly skinName: Signal<string>;
  abstract register(name: string, tokens: BoardSkinTokens): void;
  abstract activate(name: string): void;
  abstract availableSkins(): readonly string[];
}
```

`DefaultBoardSkinService` SHALL:

- 构造时注册全部内置 skins,并应用初始 skin。
- 初始 skin 解析顺序:`localStorage('gomoku:board-skin')` → 已注册 → 否则默认 `'wood'`;读到未注册的脏值时重写为默认值。
- `activate(name)` MUST 设置 `<html data-board-skin="...">` 并写入 `localStorage`;未注册的 name MUST 被忽略(`console.warn`,不抛错)。
- `localStorage` 读写抛出(隐私模式 / 配额)MUST 静默吞掉。

#### Scenario: 默认 skin
- **WHEN** 全新用户首次打开 app
- **THEN** `skinName()` 返回 `'wood'`;`<html data-board-skin="wood">` 已设置

#### Scenario: 切换持久化
- **WHEN** 调 `activate('classic')`,重启 app
- **THEN** 新一次构造后 `skinName() === 'classic'`,`data-board-skin="classic"` 在首次 paint 前已应用

#### Scenario: 未注册 skin 被忽略
- **WHEN** 调 `activate('nonexistent')`
- **THEN** `skinName()` 不变;不抛错

---

### Requirement: 皮肤绘制走 CSS 变量,组件零感知

棋盘绘制 SHALL 完全由 CSS 驱动:`src/styles/board-skins.css` 中每个 `[data-board-skin='<name>']` 块 MUST 提供完整变量集 —— `--board-bg-color`、`--board-bg-image`、`--board-line`、`--board-star`、`--board-radius`、`--board-shadow`、`--stone-black-fill`、`--stone-black-shadow`、`--stone-white-fill`、`--stone-white-shadow`、`--last-move-ring`。`.board-grid` 与 `.board-stone` 只消费这些变量;`Board` 组件及其模板 MUST NOT 含任何 skin 名或 skin 条件分支。

每个 skin 同时 SHALL 有一个 `src/app/core/theme/skins/<name>.ts` token 文件(`BoardSkinTokens` 形状),供注册表完整性校验与未来预览 UI 枚举;TS 字面量与 CSS 为镜像关系,CSS 是绘制权威。

#### Scenario: 切 skin 不动组件
- **WHEN** `data-board-skin` 从 `'wood'` 切到任意已注册 skin
- **THEN** 棋盘外观变化完全来自 CSS 级联;`Board` 组件无重新编译、无输入变化

#### Scenario: 变量集完整
- **WHEN** 检查 `board-skins.css` 中任一 skin 块
- **THEN** 上述 11 个变量全部有定义(或经由 `.dark` override 补全)

---

### Requirement: 内置 `wood` skin(默认)含暗色 override

`wood` SHALL 是默认 skin:Kaya 风格暖色木纹面(多层 gradient 合成纹理)、深胡桃色网格与星位、亮泽黑子、米白蛤碁石白子。`[data-board-skin='wood'].dark` MUST 提供暗色 override:面板降亮度偏胡桃色,白子加奶油色保持对比,黑子不变 —— 暗色页面下不刺眼。

#### Scenario: 暗色模式下木纹变深
- **WHEN** `data-board-skin="wood"` 且 `<html class="dark">`
- **THEN** 应用 `.dark` override 的变量集;棋盘整体亮度低于浅色模式

---

### Requirement: 内置 `classic` skin —— 跟随 app 主题

`classic` SHALL 不带自有色板,所有变量引用 app 主题 token(`var(--color-surface)`、`var(--color-border)`、`var(--color-text)` 等),因此自动跟随主题切换与明暗切换,无需 `.dark` override 块。

#### Scenario: 主题切换自动跟随
- **WHEN** `data-board-skin="classic"`,用户切换 app 主题或明暗
- **THEN** 棋盘颜色随主题 token 变化,`board-skins.css` 无需任何额外块

---

### Requirement: 内置 `midnight` skin —— 自含暗色石板风格

`src/styles/board-skins.css` SHALL 新增 `[data-board-skin='midnight']` 块,`src/app/core/theme/skins/midnight.ts` SHALL 导出 `midnightSkin: BoardSkinTokens`,`DefaultBoardSkinService` 构造时注册。

视觉定位(允许微调,不允许偏离):

- 近黑的冷色石板面(深蓝灰系),可带轻微纹理 / vignette,MUST NOT 是纯 `#000`。
- 网格线与星位用淡冷灰,与面板对比足够但不刺眼。
- **黑子可辨识性是硬约束**:黑子 fill MUST 带明显高光(specular highlight)与比 wood 黑子更亮的外缘,在暗面板上轮廓清晰可辨。
- 白子保持高对比,带冷色 rim。
- `--last-move-ring` 用高饱和强调色(非红色,避免与"危险"语义混淆)。
- skin 自含暗色,在浅色与深色 app 主题下外观一致,MUST NOT 需要 `.dark` override 块。

#### Scenario: midnight 已注册且可激活
- **WHEN** 全新 service 构造,调 `activate('midnight')`
- **THEN** `availableSkins()` 含 `'midnight'`;`<html data-board-skin="midnight">` 已设置并持久化

#### Scenario: 明暗主题下外观一致
- **WHEN** `data-board-skin="midnight"`,toggle `<html class="dark">`
- **THEN** 棋盘变量值不变(无 `.dark` override 块)

#### Scenario: 黑子在暗面板上可辨识
- **WHEN** midnight skin 下查看黑子
- **THEN** 黑子 fill 的高光起点亮度高于 wood 黑子(对比 CSS 字面量),且带可见外缘;375px 宽度下人工检查轮廓清晰

---

### Requirement: 扩展点 —— 加 skin 是 drop-one-file 改动

新增一个棋盘皮肤 MUST 只需要:① `src/app/core/theme/skins/<name>.ts` token 文件;② `board-skins.css` 追加一个 `[data-board-skin='<name>']` 块;③ `DefaultBoardSkinService` 构造函数一行 `register(...)`;④ `header.board-skin.<name>` i18n key(双语)。MUST NOT 改任何组件、模板或路由。header 的棋盘皮肤切换器从 `availableSkins()` 枚举,新 skin 自动出现。

#### Scenario: midnight 本身验证扩展点
- **WHEN** review 本 change 中 midnight 的 diff
- **THEN** 触碰的文件 = 上述 4 类 + 测试;`Board` 组件、`header.html` 的菜单结构均无 diff

