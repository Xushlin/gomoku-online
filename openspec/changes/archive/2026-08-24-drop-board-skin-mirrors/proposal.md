# drop-board-skin-mirrors

删掉三份棋盘皮肤的 TypeScript token 镜像。**这是 `drop-theme-token-mirrors` 没去 grep 的那个兄弟。**

## Why

`CLAUDE.md` 的坑清单第一条写着:「一份手写清单冒充注册表…**修了一个就去 grep 兄弟**;『我刚修过这类问题』是该去看的理由,不是放松的理由。」

`drop-theme-token-mirrors` 删掉了 `themes/*.ts`,而**隔壁 `skins/*.ts` 是同一个模式,我没看**。这次是**测量**替我 grep 的:为了给大厅的九个纹章腾首屏空间,把初始包按源文件归因了一遍,`core/theme/skins/{wood,classic,midnight}.ts` 就排在那张表上,三份合计约 3.2 kB,而它们是 eager 的 —— 棋盘皮肤只在棋盘上有用,而棋盘全是懒加载路由。

结构完全同型:`register(name, tokens)` + `validate()`,而 **token 的值从来没被读过** —— 注册表只用到 `has()` 与 `keys()`。CSS 那边 `board-skins.css` 才画画,而 `check-styles.mjs` 已经在钉它(基准集 + 逐皮肤对齐,皮肤名单从 `register` 调用推导)。

## What Changes

- 删 `src/app/core/theme/skins/{wood,classic,midnight}.ts` 与 `board-skin.tokens.ts`。
- `BoardSkinService.register(name)` 不再收 token;`validate` 一并删除;注册表从 `Map` 变 `Set`。
- `board-skin.service.spec.ts` 里那条传整份 fixture 的测试改成 `register('bamboo')`,并把**被删掉的那个保证的历史**写进注释。

## 这次的取舍要写清,因为它不是纯粹的清理

镜像买到过一个**真的**编译期保证,而且它真的响过**两次** —— `pieces`(`add-web-xiangqi`)与 `cards` / `felt` 加进 `BoardSkinTokens` 的那两刻,spec 里那份假皮肤 fixture 编译不过。那条注释是过去的我写的,而它是反对本变更最有力的证据。

但它响的位置是:**一份测试假皮肤,加三份 TS 副本**。真正画画的是 CSS,而一份 TS 副本齐全、CSS 块缺一项的皮肤照样编译通过、照样画错。所以保证换成「画画的那份必须完整」——**位置更对,时机更晚(lint 而非编译)**。

顺带补上一半原来没有的:**注册一个 `board-skins.css` 里没有块的皮肤名**,编译期拦不住,现在 lint 拦得住并点名。

## 不做的事

- 不动 `board-skins.css` 的任何一个值。屏幕上零变化。
- 不动 `check-styles.mjs` 的任何一条断言 —— 它是删完之后唯一的完整性保证,需要放宽才能通过的话,说明这个变更是错的。
- **不动 header 的 CDK。** 测量里最大的一块 eager 是 `@angular/cdk` 77.13 kB,唯一的 eager 导入者是 `header.ts` 的 `@angular/cdk/menu`。换掉它是另一件事,留成带触发条件的延期项。
