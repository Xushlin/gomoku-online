# tasks — drop-board-skin-mirrors

## 1. 删之前先证明剩下的保证是活的

- [x] 记下 `check-styles.mjs` 打印的那一行(`3 skins x 26 skin variables`)。
- [x] **变异:** 从 `midnight` 块删掉一个变量 → 必须红并点名。**在删镜像之前跑。**

## 2. 删

- [x] 删 `skins/{wood,classic,midnight}.ts` 与 `board-skin.tokens.ts`。
- [x] `register(name)` 去掉第二个参数;删 `validate`;`Map` → `Set`。
- [x] spec 里那条 fixture 测试改成 `register('bamboo')`,并把被删掉那个保证的历史写进注释。

## 3. 删之后再证明一次(成败判据)

- [x] **同一个变异重跑 → 仍然红并点名。** 只在删除之前跑过,证明的是被删掉的那一道。
- [x] **另一个方向:** 注册一个 CSS 里没有块的皮肤名 → 红并点名。编译期原来拦不住这个。

## 4. 验收

- [x] `npm run lint` 绿,仍然打印 `3 skins x 26 skin variables`。
- [x] `tsconfig.app.json` 与 `tsconfig.spec.json` 两个都绿(`tsconfig.json` 是 `files: []`,编译零个文件,不算)。
- [x] `npm run test:ci` 绿。
- [x] `npm run build`:记下确切数字。

## 5. 计划之外

- [x] **省了 3.45 kB(476.12 → 472.67),余量 3.88 → 7.33 kB。** 与归因表估的 3.2 kB 接近。

- [x] **这条变更是测量找出来的,不是推理找出来的。** 我上一个变更刚删掉主题镜像,而 CLAUDE.md
      的第一条坑就写着「修了一个就去 grep 兄弟」—— 我没 grep。真正指出它的是为了腾纹章空间
      做的初始包归因表,`skins/*.ts` 三行就排在上面。**一条我读过、写过、还刚引用过的规则,
      在该用的时候没有想起来。**

- [x] **第一次的归因脚本量错了一个量:** 我用 esbuild metafile 的 `entryPoint` 当首屏入口,
      得到 742.99 kB(构建报 476.12),因为 **metafile 把每个懒加载块也标成 entryPoint** ——
      动态 import 就是入口。表格看起来整整齐齐,而它把 `card-table.ts`、`room-page.ts` 这些
      明显懒加载的东西算进了首屏。**一个错的推导也能产出一张漂亮的表。** 改成从
      `index.html` 真正加载的那一个脚本出发,数字立刻自洽(418 kB JS + 47.66 kB CSS ≈ 476)。

- [x] **最大的一块 eager 是我们能控制的:** `@angular/cdk` **77.13 kB**,唯一的 eager 导入者是
      `header.ts` 的 `@angular/cdk/menu`(18.69 kB),它把 overlay(34.17)、portal、
      focus-monitor、list-key-manager、scrolling 一起拽进首屏。**一个下拉菜单占首屏 16%。**
      不在这次动它:懒加载菜单意味着首次点击要等一个 chunk,而手搓下拉是 CLAUDE.md 明令
      禁止的(focus trap / ESC / ARIA)。留成延期项,预算下次触发时答案在那儿。

- [x] **`@microsoft/signalr`(54 kB)确认不在首屏** —— 那条「只在首次订阅时连接」的规则是真的
      在生效,不是一句话。
