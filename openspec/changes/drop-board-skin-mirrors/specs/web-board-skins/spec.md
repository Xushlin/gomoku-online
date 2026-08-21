# web-board-skins 的规格变化

## MODIFIED Requirements

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

## ADDED Requirements

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
