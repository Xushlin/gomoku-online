# tasks — extend-theme-tokens

## 1. 先立守门的东西(在改任何画面之前)

- [x] 记下现有三套主题 × 明暗 = 6 种组合下,关键面的**计算样式基线**(`background-color` / `background-image` / `border-radius` / `box-shadow` / `border-top`),存成测试里的期望值。
- [x] 那份基线 MUST 由**注册表**产生(`themeService.availableThemes()`),不是手写的三项清单 —— 这个仓库修过五次同一个形状。
- [x] **正面控制:** 故意把一个 token 的中性值写错,确认基线测试变红。基线测不出错的话,后面整个「零变化」的说法就是空的。

## 2. 扩契约

- [x] `theme.tokens.ts`:`ThemeTokenSet` 增 `surfaces` / `controls` / `accents` / `grounds` 四组,`radii` 增 `control`,`shadows` 增 `raised` / `inset`,`colors` 增 `onPrimary`。**`pill` 没加、`grounds` 与 `onPrimary` 是计划外加的 —— 见 §9。**
- [x] `ThemeService.validateTokens` 覆盖新组 —— 缺字段要 warn,并有一条断言钉住 warn 的**内容**(哪个主题缺哪个键),不只是「warn 了」。
- [x] `tailwind.css` 的 `@theme` 声明新 token 名(不声明就没有 utility)。
- [x] 新 token 的**占位值**与 `material` 的中性值一致 —— 一个忘了写 `[data-theme]` 值的主题应该退化成今天的样子,而不是退化成空白。

## 3. 给现有三套补中性值

- [x] `tokens.css`:`material` / `system` / `ink` 各在明暗两份里补齐新 token,取值为「画出来和今天一模一样」。
- [x] `themes/material.ts` / `system.ts` / `ink.ts` 同步(TS 那份是校验用的镜像)。
- [x] 跑第 1 步的基线测试 —— **此刻必须全绿**。这是唯一能证明「补的是中性值」的时刻。

## 4. 角色 utility

- [x] `tailwind.css` 增**七**个 `@utility`:`panel` / `panel-flat` / `well` / `cell` / `control-primary` / `bar` / `ground`。**和计划里那六个名字对不上,两个方向都错了 —— 见 §9。**
- [x] `check-styles.mjs` 增一条:每个角色 utility MUST 只引用 `var(--…)`,MUST NOT 出现色值字面量。
- [x] 每个角色的**语义**写在 utility 上方的注释里(什么时候用 `panel` 什么时候用 `panel-flat`),否则下一个人靠猜。

## 5. 组件换成角色名

- [x] 逐个把 `bg-surface rounded-card shadow-elevated` 一类的串换成角色名。
- [x] 换完后 `grep -rn "bg-surface\|rounded-card\|shadow-elevated" src/app --include=*.html` 的剩余项 MUST 逐个有理由(比如确实只要背景色不要边和影)。**不是清零,是清到每一项都解释得通** —— 清零会逼出一个假的角色。
- [x] 顺手收掉那 7 处 `text-white`(它们跟着 `bg-primary`,应该是 `control-primary` 的一部分)。
- [x] 再跑基线测试 —— **仍然必须全绿**。

## 6. 校验对齐(这一步是机制,不是检查)

- [x] `check-styles.mjs` 增:**每个注册主题都声明每一个 token,明暗两份都要**。清单从 `theme.tokens.ts` 的类型 / `tokens.css` 的选择器**推导**,MUST NOT 手写。
- [x] 变异:给某一套主题删掉一个 token 的暗色值 → 必须红,且错误信息点名**哪套主题、哪个 token、哪个模式**。
- [x] 它跑在 `npm run lint` 下(和 board skin 那条一样),不是 vitest。

## 7. 规格漂移

- [x] `web-theming`:`RENAMED` 那条标题写死数量的需求 → 新标题不含数量;`MODIFIED` 正文说清现在有哪三套,以及扩展点约束管的是「加主题」这个动作。
- [x] `CLAUDE.md` 第 207 行「Two themes ship」→ 改成指目录,与 board skin 那句同一处理。

## 8. 验收

- [x] 6 种组合的计算样式与基线比对:**450 个属性,5 处差异,全部是 `box-shadow`** —— 而那是一个死 token 被修活,见 §9。其余逐条相同。
- [x] 375 px 无横向滚动 —— 由既有的 375px 测试覆盖(本变更不改布局与 DOM,只改上色)。**没有单独量最长内容**,因为没有任何盒模型尺寸改变:七个角色设的都是颜色、圆角与阴影,唯一涉及尺寸的 `--control-edge-width` 中性值是 `0px`,与改动前的「无边框」逐条相同。
- [x] `npm run lint` 绿(含两条新 check)、`npm run test:ci` 绿、`dotnet build` 不受影响(零后端改动)。
- [x] bundle:**473.62 kB → 477.18 kB(+3.56),预算 480 kB,余量从 6.38 掉到 2.82 kB。**
      增量全在全局 CSS(`styles` 45.29 kB):七个角色 utility + 三套主题各 11 个装饰 token。
      **这是下一个变更的硬约束,不是一句备注** —— 再加一套 `[data-theme='qq-game']`
      的明暗两块大约 2–3 kB,余量可能不够。按本仓库记的做法,那时该问的是
      **「什么是眼前载入而不必眼前载入的」**,并且靠打桩去量而不是推理;一个现成的候选是
      非默认主题的 token 块能否懒加载。**先量再动手。**
- [x] `git diff --name-only` 里 MUST NOT 出现 `themes/qq-game.ts` —— 皮肤是下一个变更的事。

## 9. 计划之外

- [x] **`--shadow-elevated` 是个死 token,而这是本变更最大的发现。** 6 种(主题 × 明暗)组合声明了
      **6 个不同的**阴影,全部画出同一个 `rgba(0,0,0,0.12) 0 4px 12px` —— material 浅色那份占位值。
      原因:Tailwind v4 的 `shadow-*` utility 走 `@property` 注册的 `--tw-shadow`,并在**构建期
      内联** `@theme` 的值,所以运行时 `[data-theme]` 覆盖到不了它。三套主题各自写的阴影从主题
      系统上线起就没生效过。角色 utility 直接写 `var(--shadow-elevated)`,于是顺手修活了它 ——
      **本变更因此不可能是零视觉变化**,而例外只有这一处,并且是修复。加了一条禁止用这个 class
      的检查,否则它会静默地死回去。

- [x] **验收比对的结果:450 个属性,5 处差异,全部是 `box-shadow`。** 那 5 处正是 6 种组合里
      阴影真的该变的那 5 种(material 浅色那种本来就等于占位值)。其余逐条相同。

- [x] **两个「显然」的中性值都是错的,而两个都会以「零变化」的名义改坏东西。**
      `--surface-edge` 写 `transparent` 会让面板**上边消失**(角色 utility 用它设上边框,而中性
      必须是「和另外三条边同色」);`--shadow-raised` 写 `none` 会让 `box-shadow: A, none`
      **整条声明失效**,连原有阴影一起没了。两处都是写检查之前想出来的,不是被检查抓到的。

- [x] **角色清单拟错了两个方向。** 提案拟六个,而 `rail` 与 `control` 在代码里**一次都没出现**,
      占比最大的那一组(有边框无填充,64 处)根本没被想到。改成按共现统计推导,得到七个 ——
      并且其中 `bar` / `ground` 是走查「剩下 29 处不属于任何角色」时才发现必须加的:
      **一个漏掉的角色不会让本变更变红,它会让下一个变更做不到。**

- [x] **`pill` 没加。** 提案列了它,走查里零调用点。一个没有调用点的 token 是每套主题都要付账的
      死条目;触发条件是第一个真需要全圆角的控件。

- [x] **一个正则的单词边界变成了退格字符,而它在每一种渲染里都是隐形的。** 通过 heredoc 写
      Python 脚本时,双写的反斜杠被壳层收成单个,Python 在普通字符串里把「反斜杠 b」解释成
      `0x08`,于是生成的 JS 正则里嵌了两个退格控制符 —— 永不匹配。而 `String(regex)` 打印时
      退格不可见,`sed`、`grep`、编辑器全都显示得像正常的;只有 `repr()` 让它露了出来。
      **是变异测试逼出来的:检查在变异下判绿,而单独跑同样的逻辑判红。** 教训不是「别用
      heredoc」,是**一条测不出错的检查必须当成坏的去查**,以及**要看字节,不要看渲染**。

      顺带一记:这条记录本身在写进本文件时**又被同一个壳层吃了一遍反斜杠**,得用别的办法重写。
      同一个坑在描述它的那段文字里发生了第三次。

- [x] **两次静默空转的补丁。** 给变异脚本打补丁的 `str.replace` 没匹配上,脚本照样打印
      「fixed」并退出 0;第二次是同一个形状。**一个不断言替换发生过的 `replace` 是一条空操作。**

- [x] **一个空操作的变异不算变异。** 第一版把非中性值**插在**主题块顶部,而同一个块里后面
      那条胜出 —— 于是它在浏览器里也什么都不改。判绿被误读成「检查漏了」。与「构建失败的
      变异不算变异」同族。

- [x] **`EXIT=$?` 在管道后面测的是 `head`。** 又踩了一次,而这条就写在自己的全局指南第一行。

- [x] **pane 不合成时,处于 CSS 过渡下的属性会读到旧值,而没有过渡的属性正常更新。**
      本文件此前记的是「DOM 属性读到的是旧的」,这一条更锐:同一次 `getComputedStyle` 里
      `border-radius` 已经是新主题的、`background-color` 还是旧主题的 —— 因为
      `transition-colors` 在那儿而过渡永不推进。量之前先注入
      `*{transition:none!important}`,并用「六种组合是否互不相同」当正面控制。

- [x] **另一处空测量:后台建的标签页 `clientWidth` 是 0**,于是 `scrollWidth > clientWidth`
      恒真。我据此说过一句「横向溢出了」,而那个测量什么也没测。CLAUDE.md 记的「布局度量仍然
      有效」只在**视口有宽度**时成立。
