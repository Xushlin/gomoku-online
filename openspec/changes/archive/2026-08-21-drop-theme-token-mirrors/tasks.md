# tasks — drop-theme-token-mirrors

## 1. 先确认删之前的保证是什么

- [x] 记下 `check-styles.mjs` 当前打印的那一行(`N themes x M tokens`),它是删完之后唯一的完整性保证。
- [x] **变异确认它现在就会红**:给某套主题的 CSS 块删掉一个 token → 必须失败并点名。删掉编译期保证之前,先证明剩下那个保证是活的。

## 2. 删

- [x] 删 `themes/{material,system,ink}.ts`。
- [x] `ThemeService.register(name)` 去掉第二个参数;删 `validateTokens` 与它的私有 helper。
- [x] `theme.tokens.ts`:删 `ThemeTokenSet` / `ThemeTokens` / `controlRadiusIsNeutral`,保留 `NEUTRAL_DECORATION`(lint 解析它)并把文件顶部的说明改成「这里只剩中性值,完整性由 check-styles.mjs 从 CSS 推导」。
- [x] `theme.service.spec.ts`:去掉引用 token 对象的断言。

## 3. 证明什么都没坏

- [x] `npm run lint` 绿,且**那一行仍然打印 4 themes x N tokens** —— 主题清单来自 CSS,与镜像无关,所以数字不该变。
- [x] `npm run test:ci` 绿。
- [x] 6 种(主题 × 明暗)组合的计算样式与删之前**逐条相同** —— 镜像不画画,所以这必须是零差异,一处都不许有。
- [x] `npm run build`:初始包 **必须回到 480 kB 以下**,并记下确切数字。

## 4. 变异

- [x] 删完之后再做一次第 1 步那个变异 —— 必须仍然红。**这是整个变更的成败判据**:如果它在删掉编译期保证之后不红了,那就是把保证删干净了而不是换了个地方。
- [x] 注册一个 `tokens.css` 里没有对应块的主题名 → 观察会发生什么,并把结论写进 §5(编译期不再拦这个了)。

## 5. 计划之外

- [x] **量到的省量比打桩预估的更多:481.29 → 473.54 kB(省 7.75)。** 打桩时只把四份镜像
      从包里去掉(→ 476.41),而真做的时候连带删了 `accents` 组、`ThemeTokens` /
      `ThemeTokenSet` 类型与 `controlRadiusIsNeutral`。**打桩是下限,不是估计值。**

- [x] **`--accent` / `--accent-image` 零消费者,一并删了。** 它们是 `extend-theme-tokens`
      加的,而没有任何角色 utility 读过。这是这一片区域**第三个**零调用点的东西 ——
      前两个是 `--radius-pill`(提了、被否)和 `controlRadiusIsNeutral`(写了、从未调用)。
      **我用「没有调用点的 token 是每套主题都要付账的死条目」否掉了 pill,却在同一个变更里
      造了两个同样的东西。** 一条只对别人用的规则不是规则。

- [x] **`validateTokens` 从来没检查过 `extend-theme-tokens` 新增的那三组。** 它只看
      `colors / radii / shadows`。而更要紧的是:**`extend-theme-tokens` 的 tasks.md 里
      「validateTokens 覆盖新组」是打了勾的,而我根本没做。** 那是一条已经归档的假记录。
      代码这会儿被删掉所以后果为零,**但记录错了比代码错了更值得说** —— 一个打了勾的
      清单项是下一个人唯一的依据。

- [x] **整场我用的类型检查一直在编译零个文件。** `tsc --noEmit -p tsconfig.json` 里
      `"files": []` + `references` 是方案式配置 —— 它退出 0 而什么都没检查。
      换成 `tsconfig.spec.json` 立刻抓到两个 `TS2304`(spec 还在引用删掉的 `inkTokens`)。
      **这条就写在我自己的全局指南里**(「一个 tsc -p 对着 files: [] 的配置编译零个文件并
      退出 0;探针要配正面控制」),而我照样用了它一整场。真正要检查的是
      `tsconfig.app.json` 与 `tsconfig.spec.json` 两个。

- [x] **一条断言不了自己想断言的东西的断言。** 我写了
      `expect(ThemeService.prototype.register.length).toBe(1)` 想钉住「register 不收
      token」—— 而 `register` 是**抽象**方法,原型上没有实现,`.length` 是 undefined,
      测试直接报 TypeError。删了而不是修:参数个数是抽象签名的事,编译器已经管了。

- [x] **验收:450 个属性逐条比对,0 差异。** 镜像不画画,所以这必须是零 —— 而它是零。
      变异(给 ink 的 CSS 块删一个 token)在删掉编译期保证**之后**仍然红,并点名主题与键:
      保证换了位置,没有消失。
