## MODIFIED Requirements

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
