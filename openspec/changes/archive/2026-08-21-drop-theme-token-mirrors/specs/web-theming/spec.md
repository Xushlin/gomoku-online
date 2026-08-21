# web-theming 的规格变化

## MODIFIED Requirements

### Requirement: 扩展点 —— 加主题是单文件改动

新增一个主题 MUST 只需要两处编辑:

1. 在 `src/styles/tokens.css` 追加两段 `[data-theme="<name>"]` 与 `[data-theme="<name>"].dark` 规则;
2. 在 `DefaultThemeService` 启动注册序列中新增一行 `this.register('<name>')`。

MUST NOT 需要:任何 TypeScript 的 token 对象、修改任何组件源码、修改任何现有主题的 token、修改 Tailwind config(因为 utility 已经绑定到 CSS 变量)。

**曾经还需要第三处:每套主题在 TypeScript 里的一份 token 镜像。** 它被删掉了,理由有两条,而第二条才是主要的:

1. 它要每个用户在**首屏**付 4.88 kB(打桩量的:把 `register()` 的实参换成空对象再构建)。
2. **它守的是副本,不是真源。** 镜像不画画,CSS 画画 —— 一套主题的 TS 镜像齐全而它的 `tokens.css` 块缺一项,**照样编译通过、照样画错**。而一份被校验过完整性的副本,比一份没人校验的副本更容易让人相信。

`ThemeService.register` 因此 SHALL 只收名字;注册表 SHALL 只存名字。`activate()` 对未注册的名字的拒绝 SHALL 保留 —— 编译期不再拦「注册一个 CSS 里没有对应块的名字」,那道运行时拒绝是仅剩的一道。

#### Scenario: 扩展仪式
- **WHEN** 假想新增一个 `playful` 主题
- **THEN** 从 diff 角度:一段 CSS 规则 + 一行注册调用。`git diff --name-only` 里 MUST NOT 出现任何组件文件,也 MUST NOT 出现任何 `themes/*.ts`

#### Scenario: 删掉镜像不改任何一处长相
- **WHEN** 依次切到每套主题 × 明暗
- **THEN** 关键面的计算样式与删之前**逐条相同,零差异** —— 镜像从不参与绘制,所以这里允许的差异是 0,不是「可解释的若干处」

#### Scenario: activate 仍然拒绝没注册过的名字
- **WHEN** `activate('never-registered')`
- **THEN** `themeName()` 与 `data-theme` 都不变

### Requirement: 主题 token 的对齐校验从注册表推导

`check-styles.mjs` SHALL 断言**每个已注册主题都声明了每一个 token**,而清单 SHALL 从生产源推导 —— token 名单从 `tailwind.css` 的 `@theme` 块,主题名单从 `tokens.css` 的 `[data-theme=…]` 选择器。MUST NOT 手写成一份主题名清单。

它 SHALL 跑在 `npm run lint` 下而不是 vitest,与已有的 board skin 对齐校验同一处。

**镜像删掉之后,这条校验是完整性的唯一保证,所以它的地位从「第二道」变成「仅有的一道」。** 这不是降级:它检查的是**真正画画的那份**,而被删掉的编译期检查看的是副本。两个后果:

- 任何放宽这条校验才能通过的改动 MUST 被当作错误的改动看待;
- 一次删除编译期保证的变更 SHALL 在删除**之后**重跑同一个变异,证明剩下的保证仍然会红。只在删除之前跑过,证明的是被删掉的那一道。

#### Scenario: 清单不是手写的
- **WHEN** 新增一套主题而**不**改 `check-styles.mjs`
- **THEN** 新主题自动进入校验范围;它缺 token 就失败

#### Scenario: 删掉编译期保证之后校验仍然会红
- **WHEN** 给某套主题的 `[data-theme]` 块删掉一个 token,在镜像已经不存在的代码上跑 `npm run lint`
- **THEN** 失败,并点名该主题与该 token
