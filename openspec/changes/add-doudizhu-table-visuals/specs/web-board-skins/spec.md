# web-board-skins 的规格变化

## MODIFIED Requirements

### Requirement: 皮肤绘制走 CSS 变量,组件零感知

棋盘绘制 SHALL 完全由 CSS 驱动:`src/styles/board-skins.css` 中每个 `[data-board-skin='<name>']` 块 MUST 为 `BoardSkinTokens` 的**每一段**提供变量 —— 棋盘面、棋子、象棋棋子、扑克牌与桌面、最后一手。`.board-grid` / `.board-stone` / `.xq-*` / `.ddz-*` 只消费这些变量;组件及其模板 MUST NOT 含任何 skin 名或 skin 条件分支。

**这条 requirement MUST NOT 把变量名逐个抄在这里。** 它原来抄了 11 个,而象棋棋子的 `--xq-piece-bg` / `--xq-red` / `--xq-black` 自 `add-web-xiangqi` 起就在每个 skin 块里、却从来不在那份名单上 —— **一份抄进规格的名单会在每一次源码变化时静静过期**。名单的位置是**一个检查**:`frontend-web/scripts/check-styles.mjs` 解析 `board-skins.css`,取默认 skin 块定义的变量集作为基准,断言**每个** skin 块(以及它的 `.dark` override)定义的是同一个集合。漏一个变量与多一个拼错的变量都会红,而不需要任何人记得来改这段话。

**它跑在 `npm run lint` 里而不是 vitest 里,而这是量出来的:** 这个仓库的测试构建读不到 CSS 文本 —— `import css from '...css?raw'` 的默认导出是 `[]`(Angular 的 CSS-in-JS 壳),`import.meta.glob(..., { query: '?raw' })` 同上,而 `node:fs` 在 spec 的 tsconfig 里没有类型。样式表里若还引用了图片,那个 import 甚至会让整个测试构建失败(`No loader is configured for ".png"`)。CI 已经跑 lint,所以位置换了、覆盖没变。

每个 skin 同时 SHALL 有一个 `src/app/core/theme/skins/<name>.ts` token 文件(`BoardSkinTokens` 形状),供注册表完整性校验与未来预览 UI 枚举;TS 字面量与 CSS 为镜像关系,CSS 是绘制权威。

#### Scenario: 切 skin 不动组件
- **WHEN** `data-board-skin` 从 `'wood'` 切到任意已注册 skin
- **THEN** 棋盘外观变化完全来自 CSS 级联;`Board` 组件无重新编译、无输入变化

#### Scenario: 变量集完整,且由检查而不是由名单保证
- **WHEN** 任一 skin 块少定义(或多定义)一个变量
- **THEN** `npm run lint` MUST 失败,并 MUST 报出是哪个 skin 少了哪个变量

#### Scenario: 检查本身对注释视而不见
- **WHEN** 某个 skin 块的注释里出现形如 `--name:` 的散文
- **THEN** 检查 MUST NOT 把它当成一条声明(第一版就红在这种散文上)
