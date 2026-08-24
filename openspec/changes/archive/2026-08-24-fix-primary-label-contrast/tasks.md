# tasks — fix-primary-label-contrast

## 1. token

- [x] `material.dark` / `system.dark` / `ink.dark` 的 `--color-on-primary` 从 `#ffffff` 换成 `#0d253f` / `#062033` / `#2a0f08`(8.87 / 6.34 / 6.11)。
- [x] **三套浅色一处不改**,`qq-game` 两套一处不改 —— 它们已经合格,而 `qq-game` 是唯一量过才定的。
- [x] `--color-on-primary: #ffffff;` 在 `tokens.css` 里有 **6** 处,所以替换**按块限定**并逐块断言 `count == 1`;改完剩 **3** 处(三套浅色),这一条也断言了。

## 2. 组件

- [x] 25 处静态 `control-primary … text-bg` → `text-on-primary`。编辑单位是**整个 class 属性**(配对就是按属性量的),不是全文替换。
- [x] 走查剩下的 `text-bg`,发现它们是**另一种形状**:5 处 `[class.text-bg]="expr"` 条件绑定 —— 而第一版走查只看 `class="…"`,**看不见它们**。全部改成 `[class.text-on-primary]`。
- [x] 其中回放页那一处还多一件事,见 §3 的规则 2。
- [x] 最后剩一处 `bg-danger text-bg`(辞局确认框)。**不动** —— 它不在 `control-primary` 上,而新校验把 `bg-<token>` 也当填充量过:8 种主题 × 明暗全部 ≥ 4.5。留着不是因为「看起来没问题」。

## 3. 校验(本变更真正的产物)

- [x] 配对从 `class` 属性推导:角色 utility 的填充**读它自己的 `@utility` 定义**(`background-color` / `background-image`),`bg-<token>` 同样算填充,前景是 `text-<token>` 且 `<token>` 出自 `@theme` —— 这条是用来把 `text-danger` 和 `text-sm` 分开的。
- [x] 状态模型:静态 class ∪ 某一个 guard 的 class。**两个不同 guard 同时为真的组合没有建模** —— 已知空档,写在代码注释里。
- [x] 渐变取每一档,方向按前景明暗取最差那一头。
- [x] **规则 2:一个状态里不许有两个前景色 utility。** 它们同特异性,谁画取决于样式表顺序。
- [x] 失败信息点名主题、模式、配对、档位与第一个出现的文件。
- [x] `npm run lint` 打印 **1196 fg/fill contrast readings**,并点出 **5 处 `[class]="…"` 整串绑定没有建模**。

## 4. 变异(每一条都先看到红)

- [x] `qq-game.dark` 的 `--color-on-primary` 设成填充色本身 → 红,点名 `qq-game.dark`。**正面控制。**
- [x] 一个按钮退回 `text-bg` → 红,点名 `text-bg on control-primary`。**本变更的存在理由,所以它必须红。**
- [x] 一个状态里塞两个前景色 utility → 红,点名 `text-text + text-on-primary`。
- [x] 让校验不走渐变色标 → **绿**。所以补一对:把 `qq-game` 浅色最亮那一档改成 `#e07a55`(平色回退值仍然合格)→ 红,`2.75:1 at stop #e07a55`;再关掉色标走查 → 绿。**证明那段代码是它抓到的**,而单独那一条绿说明的是:今天的 token 值里没有东西依赖它,它是保险。

## 5. 收尾

- [x] `npm run lint` 0 / `test:ci` **886 绿** / `tsconfig.app.json` 0 / `tsconfig.spec.json` 0 / `npm run build` 0。
- [x] 初始包 476.74 → **476.75 kB(+0.01)**。
- [x] 浏览器里逐一读计算样式:四套主题 × 明暗 **8/8** 的 `color` 都等于该主题的 `--color-on-primary`。这一步不是为了好看 —— `shadow-elevated` 那次教的是「一个 utility 可以看起来对、而实际被构建期占位值冻住」。
- [x] 没有测试引用过这两个 class 名(grep 过),所以没有测试需要跟着改。
