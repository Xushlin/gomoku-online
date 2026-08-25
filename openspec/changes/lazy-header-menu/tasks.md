# tasks — lazy-header-menu

## 1. 先证明它真的会拆出去(在写任何界面细节之前)

- [x] 新建 `shell/header/appearance-menus/`:六个控件的触发按钮**连同**两个 `<ng-template>`(`optionPanel` / `settingsMenu`)整体搬进去 —— **必须整体搬**:`CdkMenu` 用 content query 收集菜单项,而 content query 不进子组件的 view,也不进 `ngTemplateOutlet` 的 embedded view(既有要求里写着,踩过 `NG0201`)。
- [x] header 模板改成 `@defer (when menuRequested())` + `@placeholder` + `@prefetch (on idle)`。
- [x] 归因脚本(从 `index.html` 的脚本出发、只沿 `import-statement` 传递闭包):**`@angular/cdk` 在首屏是 0.00 kB**。这一步过了,后面才有意义。

## 2. 那一次点击不能白点

- [x] 占位按钮点下去 → 置 `menuRequested` 并记下是第几个 → 加载完成后对那个 `CdkMenuTrigger` 调 `open()`。
- [x] 只有第一次需要;之后真身一直在,行为与今天一样。
- [x] **而这一条在浏览器里第一版是坏的** —— 见 §7。

## 3. 占位与真身不许漂

- [x] 占位渲染同一组按钮,视觉一致、**不带任何 cdk 指令**。
- [x] 测试:两种状态下按钮的**可见文案与 `aria-label` 逐项相等**(`DeferBlockState.Placeholder` / `Complete` 各渲染一次),清单就是渲染出来的那些按钮,不手写。
- [x] 配套的反面:**只有加载后的状态带 `aria-haspopup`** —— 两头都断言,否则一个把菜单整个删掉的实现同样是绿的。

## 4. CDK 给的东西不能弄丢

- [x] 既有 header 测试的**断言一条没改**:菜单角色(`menuitem` / `menuitemcheckbox`)、六个控件的顺序、当前值、`aria-checked`、子菜单列出注册表全部选项、音量滑杆不关菜单 —— 全部原样通过。
- [x] 375 px 那四条(无横向溢出、不换行、控件不在行内、登出时也不超预算)原样通过。

## 5. 变异(先看到红)

- [x] 把 `<app-appearance-menus>` 挪出 `@defer` → **lint 红**(新增两条源码规则:header 不许 import cdk;那个组件只许出现在 defer 块里)。
- [x] 去掉加载后的 `open()` → 既有那四条「打开菜单」的测试红。
- [x] 占位少画一个 picker → 逐项比对那条红。

## 6. 量出来的东西

- [x] 初始包 477.83 → **402.62 kB(−75.21)**;**归因里 `@angular/cdk` 是 0.00 kB**。菜单落在 `chunk-3KKU5H6A.js`,而 `index.html` 只加载 `main-*.js`。预算余量 **2.2 → 77.4 kB**。
- [x] 没到打桩量的 396.42 —— 差的 6.2 kB 是 defer 机制加占位那份标记。**打桩是下限,不是预期值**(这条本仓库写过,这次又对了一遍)。
- [x] `npm run lint` 0 / `test:ci` **920 绿** / 两个 tsconfig 0 / `build` 0。
- [x] 浏览器:冷启动后 header 七个按钮**都没有** `aria-haspopup`(占位在);点一次「主题」→ 菜单打开,项目是 Material / System / Ink / Game hall。

## 7. 浏览器里量出来的那一条,单元测试抓不到

**同步调 `open()` 的版本:回调跑了,菜单没开。** 临时探针确认 `ran: true`、索引 1(主题是第二个 picker)、触发器 5 个 —— 全对,而 `cdk-overlay-pane` 数量是 **0**。

原因:**发起这一切的那次点击还在冒泡**,而 CDK 打开菜单时会订阅 document 上的「点到外面就关」,它接住了同一次事件的尾巴 —— 刚开就关。挪到**下一个宏任务**之后:pane 1 个,四个菜单项都在。

**而这一条在 jsdom 里同步版本是绿的。** 所以:

- 修复的**理由**写进了 `appearance-menus.ts` 的注释,而不是只把代码改对;
- 测试的 helper 也跟着等一个宏任务 —— 它等的正是生产真实的时序,不是给测试开的后门;
- 规格里补了一句「`open()` SHALL 在下一个宏任务里调,MUST NOT 同步调」,并写明它在 jsdom 下同步版本也是绿的。

## 8. 既有测试改了什么、没改什么

- **断言一条没改。**
- 挂载与点击的 helper 变成 `async`:`@defer` 让组件需要 `await TestBed.compileComponents()`,而 TestBed 对 defer 块默认是 Manual,所以两份 header spec 都设了 `Playthrough`(比对那两条反过来要 Manual)。
- 我原来在 spec 里写的「既有测试一条都不许改」**说过头了**,已经改成「断言不动,挂载变 async」。
