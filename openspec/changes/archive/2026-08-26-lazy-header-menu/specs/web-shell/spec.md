## MODIFIED Requirements

### Requirement: Header 外观控件在窄视口折叠进单一 Settings 菜单

`src/app/shell/header/header.html` SHALL 把六个**外观**控件 —— 语言、主题、棋盘皮肤、音效皮肤、音效开关、深色开关 —— 在 `lg`(1024px)以下折叠进**一个** CDK menu 触发器,文案走 `header.settings.label`。导航与身份类元素(品牌链接、`/games` 链接、登录 / 登出)MUST 留在 header 行内,不进 Settings 菜单。

三段式布局,断点全部取 Tailwind 预设:

| 视口 | header 行内容 |
| --- | --- |
| `< lg` | 品牌 · Games · **Settings ▾** · 登出(用户名隐藏) |
| `lg` … `< xl` | 品牌 · Games · 六个内联控件(只显示当前值) · 用户名 · 登出 |
| `≥ xl` | 同上,控件额外显示 `标签:` 前缀 |

控件顺序在两种排布下 MUST 一致:语言 → 主题 → 棋盘 → 音效皮肤 → 音效开关 → 深色。

六个控件的定义 MUST 只存在一份:`Header` SHALL 暴露两个列表 —— `pickers`(语言 / 主题 / 棋盘 / 音效皮肤)与 `toggles`(音效 / 深色),两种排布都以 `@for` 遍历同一份列表渲染。新增一个外观控件因此是加一个数组条目,而非改两处模板。

`PickerControl` 每项 SHALL 携带:`prefix`(该控件全部文案的 i18n 命名空间 —— 标签 `<prefix>.label`,当前值与每个选项 `<prefix>.<option>`)、`options`(直接取自所属服务的注册表)、`value`、`hasVolume`、`apply`。四个选项列表 MUST 共用**一份** `<ng-template>`,由触发器经 `cdkMenuTriggerData` 传入自身的 `PickerControl`。

**这一组控件连同它们的菜单 SHALL 位于一个 `@defer` 块里,而 header 本身 MUST NOT 静态引用
`@angular/cdk`。** 理由是量出来的:`@angular/cdk` 在首屏占 **77.13 kB**,而**我们自己全部的代码
是 52.12 kB** —— 一组下拉菜单比整个应用大 1.5 倍。而它是 cdk 唯一的 eager 导入者(其余 12 处
`@angular/cdk/dialog` 全在懒加载路径上)。打桩量到 **477.83 → 396.42 kB**,而**实际落地是
402.62 kB** —— 打桩是**下限,不是目标**,差的 6.2 kB 是 defer 机制加占位那份标记。所以这里
MUST NOT 写一个由打桩数推出的尺寸门槛:管尺寸的是 `angular.json` 里那条会让构建报警的
480 kB 预算,而管这条要求的是下面那个归因判据。

- 占位(`@placeholder`)SHALL 渲染视觉一致的同一组按钮,但**不带任何 cdk 指令**;
- 占位按钮被点击后 SHALL 请求加载,而加载完成后 MUST 把**刚才点的那一个**菜单打开 ——
  那一次点击 MUST NOT 被吃掉。可接受的代价是「等一个 chunk」,不是「白点一次」;
- SHALL 配 `@prefetch (on idle)`,让正常网络下那个等待是 0;
- 加载完成后 `open()` **SHALL 在下一个宏任务里调**,而 MUST NOT 同步调:量到过同步版本
  「回调跑了、菜单没开」—— 发起的那次点击还在冒泡,而 CDK 的「点到外面就关」接住了同一次
  事件的尾巴。**这一条在 jsdom 里同步版本是绿的**,所以理由必须写在代码注释里;
- 占位与真身是两份按钮标记,所以 SHALL 有一条测试**逐项比对两种状态下按钮的可见文案与
  `aria-label`**,而清单 MUST 从 `pickers` / `toggles` 两个生产数组推导 —— 一份手写的副本清单
  会在加第七个控件那天悄悄漏掉一项,而漏掉的表现是「占位少一个按钮」,首屏一闪而过。

**判据是构建产物的归因,MUST NOT 是「它在 `@defer` 里所以是懒的」这一推理** —— 与本规范
「eager 与懒加载的判断只认实测」同一条理由:从 `index.html` 加载的那个脚本出发、只沿
`import-statement` 传递闭包地走,`@angular/cdk` MUST NOT 出现在那个集合里。

CDK 约束,MUST 遵守:`CdkMenu` 以 `@ContentChildren(CdkMenuItem, { descendants: true })` 收集菜单项,而 content query 既不进入子组件的 **view**,也不进入 `ngTemplateOutlet` 实例化的embedded view(后者的 DI 与查询都解析到模板的**声明**位置)。因此菜单项 MUST 与其 `cdkMenu` 声明在同一个模板中 —— MUST NOT 把菜单行抽成 `<app-header-picker>` 之类的子组件,也 MUST NOT 用 `ngTemplateOutlet` 把它们放进菜单:前者静默破坏 roving tabindex / 方向键 / type-ahead,后者直接抛 `NG0201: No provider found for InjectionToken cdk-menu-stack`。

两种排布下同一个控件的渲染差异:内联为带边框按钮(picker 用 `[cdkMenuTriggerFor]`,toggle 用 `role="switch"`);Settings 菜单内 picker 为 `cdkMenuItem` + `cdkMenuTriggerFor`(CDK 子菜单),toggle 为 `cdkMenuItemCheckbox`(`role="menuitemcheckbox"` + `aria-checked`,而非 menu 语境下非法的 `role="switch"`)。

本折叠 MUST NOT 改变任何服务 API、i18n 键(除新增 `header.settings.label`)、或控件自身的行为契约 —— 音效皮肤菜单的音量滑杆在两种排布下都 MUST NOT 标记 `cdkMenuItem`,拖动仍不关闭菜单。

#### Scenario: 375px 下只暴露导航与身份控件
- **WHEN** 视口 375px,shell 渲染完成
- **THEN** header 行内可见的交互元素为品牌链接、`/games` 链接、Settings 触发器、登出(或未登录时的登录 CTA);六个外观控件都 MUST NOT 直接可见

#### Scenario: Settings 菜单列全六个外观控件
- **WHEN** 视口 375px,点击 Settings 触发器
- **THEN** 菜单按序列出语言、主题、棋盘、音效皮肤、音效开关、深色六行;前四行展开子菜单后的选项集合与 `lg` 以上内联控件的选项集合完全一致

#### Scenario: `lg` 以上恢复内联
- **WHEN** 视口 ≥ 1024px
- **THEN** 六个外观控件内联显示在 header 行中;Settings 触发器 MUST NOT 可见

#### Scenario: 标签在 `xl` 显示
- **WHEN** 视口从 1024px 增大到 1280px
- **THEN** 每个内联控件前出现 `标签:` 前缀;1024–1279px 之间只显示当前值

#### Scenario: 新增皮肤仍不改模板
- **WHEN** 按 `web-board-skins` 的开放/封闭规则注册一个新棋盘皮肤
- **THEN** 它同时出现在 `lg` 以上的内联棋盘菜单和 Settings 子菜单中,`header.html` 无 diff

#### Scenario: 音量滑杆行为不变
- **WHEN** 在任一排布下打开音效皮肤菜单并拖动音量滑杆
- **THEN** 菜单保持打开;释放时 `sound.setVolume` 被调一次;未静音时播放一次 `move-place` 试听

---

#### Scenario: cdk 不在首屏
- **WHEN** `ng build --stats-json` 之后,从 `index.html` 引用的脚本出发沿静态 import 走遍
- **THEN** 该集合里 MUST NOT 含任何 `node_modules/@angular/cdk/*` 输入,而菜单 MUST 出现在某个 `entryPoint` 标着 `appearance-menus` 的懒加载 chunk 里

#### Scenario: 第一次点击最终会打开菜单
- **WHEN** 冷启动后第一次点某个外观控件
- **THEN** 加载完成后**那个**菜单是打开的 —— MUST NOT 需要点第二次

#### Scenario: 占位与真身的按钮一一对应
- **WHEN** 比对占位状态与加载后状态里的控件按钮
- **THEN** 数量相等,且逐项的可见文案与 `aria-label` 相等;清单从 `pickers` / `toggles` 推导
