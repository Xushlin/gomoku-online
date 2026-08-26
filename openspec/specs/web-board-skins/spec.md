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
- 初始 skin 解析顺序:`localStorage('gewu:board-skin')` → 已注册 → 否则默认 `'wood'`;读到未注册的脏值时重写为默认值。
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

### Requirement: 皮肤绘制走 CSS 变量,组件零感知

棋盘绘制 SHALL 完全由 CSS 驱动:`src/styles/board-skins.css` 中每个 `[data-board-skin='<name>']` 块 MUST 为 `BoardSkinTokens` 的**每一段**提供变量 —— 棋盘面、棋子、象棋棋子、扑克牌与桌面、华容道棋子、最后一手。消费这些变量的是各棋盘自己的类;组件及其模板 MUST NOT 含任何 skin 名或 skin 条件分支。

**这条 requirement MUST NOT 把变量名逐个抄在这里,也 MUST NOT 把消费者的类名逐个抄在这里。** 前者原来抄了 11 个,而象棋棋子的 `--xq-piece-bg` / `--xq-red` / `--xq-black` 自 `add-web-xiangqi` 起就在每个 skin 块里、却从来不在那份名单上。后者原来抄的是 `.board-grid` / `.board-stone` / `.xq-*` / `.ddz-*` —— **而华容道的 `.kt-*` 从来不在里面,那不是疏漏,是它当时确实没接进来**:三个皮肤下它的计算背景逐字节相同。**一份抄进规格的名单会在每一次源码变化时静静过期,而它过期的样子和正确的样子一模一样。**

名单的位置是**一个检查**:`frontend-web/scripts/check-styles.mjs` 解析 `board-skins.css`,取默认 skin 块定义的变量集作为基准,断言**每个** skin 块(以及它的 `.dark` override)定义的是同一个集合。漏一个变量与多一个拼错的变量都会红,而不需要任何人记得来改这段话。

**它跑在 `npm run lint` 里而不是 vitest 里,而这是量出来的:** 这个仓库的测试构建读不到 CSS 文本 —— `import css from '...css?raw'` 的默认导出是 `[]`(Angular 的 CSS-in-JS 壳),`import.meta.glob(..., { query: '?raw' })` 同上,而 `node:fs` 在 spec 的 tsconfig 里没有类型。样式表里若还引用了图片,那个 import 甚至会让整个测试构建失败(`No loader is configured for ".png"`)。CI 已经跑 lint,所以位置换了、覆盖没变。

**CSS 是唯一权威,没有 TypeScript 镜像。** 曾经每个 skin 还要一个 `src/app/core/theme/skins/<name>.ts`,而 `drop-board-skin-mirrors` 把那个目录整个删掉了(它只是 CSS 的一份副本,3.45 kB 的首屏,且校验的是自己)。**这段话在那之后仍然要求那些文件存在,和代码矛盾了整整一个变更周期,而 `openspec validate --strict` 一直是绿的** —— 它验形状,不验真伪。

#### Scenario: 切 skin 不动组件
- **WHEN** `data-board-skin` 从 `'wood'` 切到任意已注册 skin
- **THEN** 棋盘外观变化完全来自 CSS 级联;`Board` 组件无重新编译、无输入变化

#### Scenario: 变量集完整,且由检查而不是由名单保证
- **WHEN** 任一 skin 块少定义(或多定义)一个变量
- **THEN** `npm run lint` MUST 失败,并 MUST 报出是哪个 skin 少了哪个变量

#### Scenario: 检查本身对注释视而不见
- **WHEN** 某个 skin 块的注释里出现形如 `--name:` 的散文
- **THEN** 检查 MUST NOT 把它当成一条声明(第一版就红在这种散文上)

#### Scenario: 没有 TypeScript 镜像可以过期
- **WHEN** 检索 `src/app/core/theme/`
- **THEN** MUST NOT 存在按皮肤逐个列出 token 的 TS 文件

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

新增一个棋盘皮肤 MUST 只需要:① `board-skins.css` 追加一个 `[data-board-skin='<name>']` 块;② `DefaultBoardSkinService` 构造函数一行 `register('<name>')`;③ `header.board-skin.<name>` i18n key(双语)。MUST NOT 改任何组件、模板或路由,也 **MUST NOT** 需要任何 TypeScript token 对象。

**曾经还需要第四类:`skins/<name>.ts` 里的一份 token 镜像。** 它被删掉了,而理由与主题那边逐字相同:镜像的值**从来没被读过** —— 注册表只用到 `has()` 与 `keys()`,而它要每个用户在首屏付约 3.45 kB。

**这是一次有取舍的交换,不是纯粹的清理,而取舍必须写下来:** 镜像买到过一个**真的**编译期保证,并且它真的响过两次 —— `pieces`(`add-web-xiangqi`)与 `cards` / `felt` 加进契约的那两刻,测试里那份假皮肤 fixture 编译不过。但它响在**一份测试假皮肤加三份 TS 副本**上;真正画画的是 `board-skins.css`,而一份 TS 副本齐全、CSS 块缺一项的皮肤**照样编译通过、照样画错**。

所以保证从「TS 副本必须完整」换成「**画画的那份**必须完整」:位置更对,时机更晚(lint 而非编译)。

#### Scenario: 加一个皮肤的仪式
- **WHEN** 假想新增一个 `bamboo` 皮肤
- **THEN** 触碰的文件 = 一段 CSS 块 + 一行 register + 两个 i18n key;`git diff --name-only` 里 MUST NOT 出现任何组件文件,也 MUST NOT 出现任何 `skins/*.ts`

#### Scenario: 删掉镜像不改任何一处长相
- **WHEN** 在每个皮肤下取棋盘与牌桌的计算样式
- **THEN** 与删之前**逐条相同**;镜像从不参与绘制,所以允许的差异是 0

### Requirement: 皮肤集合的完整性由 CSS 侧的走查保证,而它必须双向会红

`scripts/check-styles.mjs` SHALL 以**默认皮肤**在 `board-skins.css` 里的变量集作基准,并要求其它每个 `[data-board-skin]` 块声明**完全相同**的集合。皮肤名单 SHALL 从 `DefaultBoardSkinService` 的 `register('…')` 调用推导,MUST NOT 手写。

**它 SHALL 双向会红**,而这两个方向对应两种不同的错误:

- 某个皮肤**漏**一个变量 → 失败并点名皮肤与变量;
- 某个名字被 `register` 了而 `board-skins.css` 里**没有对应块** → 失败并点名那个名字。

第二个方向在镜像存在时**编译期拦不住**(注册一个不存在的皮肤名一样编译通过),所以它不是被替换的保证,是新增的那一半。

**一次删除编译期保证的变更 SHALL 在删除之后重跑同一个变异**,证明剩下的保证仍然会红。只在删除之前跑过,证明的是被删掉的那一道。

#### Scenario: 漏一个变量
- **WHEN** 从某个非默认皮肤块里删掉一个变量
- **THEN** `npm run lint` 失败,并点名该皮肤与该变量

#### Scenario: 注册了一个没有块的皮肤
- **WHEN** `register('nonexistent')` 而 `board-skins.css` 里没有它的块
- **THEN** `npm run lint` 失败并点名 `nonexistent`

