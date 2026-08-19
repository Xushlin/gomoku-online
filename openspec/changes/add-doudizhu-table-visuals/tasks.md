# tasks — add-doudizhu-table-visuals

## 1. 素材

- [x] 1.1 从用户提供的素材包取四个花色图。**已验明对应关系**:按不透明像素均色与最宽行的
      纵向位置判定 —— `_60` / `_61` 是深蓝灰(♠ / ♣),`_63` 最宽行在 y≈82(上三分之一,♥),
      `_64` 最宽行在 y=143(正中,♦)。**用形状而不是文件名判花色**:名字里没有花色。
- [x] 1.2 降到 96×96 PNG(原图 288×288),放 `frontend-web/public/cards/{spade,club,heart,diamond}.png`。
- [x] 1.3 `card-art.ts`:`pipUrl(suit)` / `pipStyle(suit)`;一个测试走**全部 54 个编码**,断言每个的
      花色图文件在磁盘上,并断言两张王**没有**花色。
      **计划里写「Vitest 跑在 node 上,可以读 public/」—— 那是错的。** 这个测试构建里没有 .png 的
      loader,eager 的 glob 会让整个构建失败(`No loader is configured for ".png"`);读 CSS 文本也不行
      (`?raw` 的默认导出是 `[]`,`node:fs` 没有类型)。最后用的是**惰性** `import.meta.glob`:
      键名由 Vite 在构建期按真实目录展开,所以它证明的正是「文件在磁盘上」,而一次模块加载都没发生。

## 2. 皮肤 token

- [x] 2.1 `BoardSkinTokens` 加 `cards`(纸面 / 边框 / 角标红黑 / 牌背)与 `felt`(桌面 / 边 / 暗角)两段。
- [x] 2.2 三个内置皮肤各补一份:wood 暖木、classic 跟随主题 token、midnight 自含暗色。
- [x] 2.3 `board-skins.css` 三个块(含 wood 的 `.dark`)各补对应 CSS 变量。
- [x] 2.4 **新检查**:`scripts/check-styles.mjs` 解析 `board-skins.css`,以默认 skin 块的变量集为基准,
      断言每个 skin 块(并上 `.dark` override)定义同一集合;挂在 `npm run lint` 上,所以 CI 照样会红。
      这条以前**根本不存在** —— `--xq-*` 三个变量自 `add-web-xiangqi` 起就在 CSS 里而不在规格的名单上。
      **它不在 vitest 里,是因为这份构建读不到 CSS 文本(见 1.3)。**
      第一版还红在**我自己写的注释**上(classic 块里有一句「NOT `--color-surface`: this skin…」)——
      检查要对注释视而不见,`generalize-match-seats` 的源码级断言记的是同一条。

## 3. 牌桌

- [x] 3.1 牌桌的样式表**在组件里**(`card-table.css` + `styleUrl`),而**不是** `src/styles/` 下的全局
      文件 —— 计划写的是后者。量出来:全局样式首屏就要下载,初始包 474.16 → **484.83 kB**,480 kB 的
      预算当场报警;搬进 `room-page` 那个 lazy chunk 之后是 **479.66 kB**。
      随后 `anyComponentStyle: 4kB` 那条也响了(5.34 kB),于是这里只留 Tailwind 表达不了的东西。
      **中间还走错一步:**把 token 颜色写成 `text-[color:var(--x)]` 之类的 arbitrary utility,
      那些 utility 会进**首屏**的 Tailwind 样式表 —— 479.53 → 480.38 kB,预算又红。
      最后的分工是:尺寸间距用现成 utility,皮肤 token 留在组件 CSS 里。
- [x] 3.2 felt 桌面 + 三个环绕座位(我在下,下家在右,上家在左)+ 头像 / 名字 / 张数 / 地主标 /
      该谁走的高亮。
- [x] 3.3 真牌面:圆角纸面、角标(点数 + 小花色)、一个大花色;红黑由花色定,色相 MUST NOT 由皮肤改。
- [x] 3.4 手牌扇形重叠:`--n` 绑张数,`--step` 取固定比例与"容器装得下"的较小者;选中的牌抬起。
- [x] 3.5 对家画牌背叠,张数可见、牌面不可见。
- [x] 3.6 底牌:叫分阶段三张**牌背**(服务端此时给 `kitty: null`),定下地主后翻成牌面。
- [x] 3.7 桌心那手要压的牌 + 「自由出牌」空态。

## 4. 动画

- [x] 4.1 发牌:`ddz-deal` keyframes,起点由 `--ddz-i` / `--ddz-n` / **`--ddz-spread`** 算出。
      **不能用 `--ddz-step`** —— 计划里就是它,而量出来横向位移是 **0**:那个变量里有 `100%`,
      而百分比是在**用它的地方**解算的,在 `transform: translate()` 里它对着元素自己
      (`(34px - 34px) / 16 = 0`)。牌只往下掉不散开,而动画照样在放,所以看起来像「设计成这样」。
- [x] 4.2 出牌:`ddz-play` keyframes,起点由 `relativeSeat()` 给的 `self` / `left` / `right` 三组变量决定。
- [x] 4.3 `prefers-reduced-motion: reduce` 下全关。
- [x] 4.4 **不加计时器、不加信号**:靠 `track card.code` —— 节点只在牌第一次出现时创建,动画就只放一次。

## 5. 纯函数

- [x] 5.1 `table-layout.ts`:`seatRing(mySeat, total)`(环绕顺序)、
      `relativeSeat(seat, mySeat, total)`、`currentTrick(moves)`。
- [x] 5.2 `currentTrick`:从最后一手 `play:` 到末尾就是当前一轮;叫分阶段取全部 `bid:`。
      **不需要规则知识**,并且 MUST NOT 用于判断。

## 6. i18n

- [x] 6.1 两份 locale 补键(座位、牌背 alt、不要 / 叫分气泡、底牌)。键集合 MUST 一致。

## 7. 验证

- [x] 7.1 `npx ng test --no-watch`(现 761 绿)、`npm run lint`、`npm run build`。
- [x] 7.2 **bundle 预算**:现 474.16 kB / 480 kB。牌桌在 lazy chunk 里,但 `add-web-tetris` 教过
      「lazy 路由不代表 lazy 服务」—— 所以是量,不是推。
- [x] 7.3 变异检查每条新断言:忽略 `--n`、`relativeSeat` 恒返回 `self`、`currentTrick` 不切轮、
      少一个花色图、皮肤漏一个变量。
- [x] 7.4 浏览器:三个账号一桌真斗地主。量 375 px **满手牌**下 `overflow: 0`(空手牌下这个检查
      是白测的)、暗色、三个皮肤、发牌与出牌确实动、`prefers-reduced-motion` 下确实不动。

## 8. 量到的东西(而不是看着像对的东西)

- **测试与构建**:前端 **790** 绿(此前 761,新增 29)、`npm run lint` 通过(含
  `check:styles`:3 个皮肤 × 26 个变量)、`npm run build` **零警告**,初始包 **479.66 kB / 480 kB**。
- **变异检查**:七处全红 —— 手牌不再告诉 CSS 一共几张、`relativeSeat` 把下家画到左边、
  `currentTrick` 不在最后一手出牌处切轮、给王凑一个花色、对家的牌画成正面、头像按 UTF-16 单元取首字、
  侧栏在三座位时仍走黑白那一支;另有三处针对 `check-styles.mjs`(皮肤漏一个变量 / 多一个拼错的 /
  keyframe 引用带百分比的变量)。
- **一次变异什么也没证明,而它长得像证明了。** 侧栏那条第一次用 `@if (false)` 变异,结果是
  **模板编译错误**(exit 1,但没有一条测试跑起来)—— 改成 `seats.length > 5` 才真正让 2 条测试变红。
  **一个构建失败的变异不是红测试。**
- **真浏览器**:三个真账号一桌真斗地主。叫分阶段 → bob 叫 3 分成为地主(20 张)→ bob 首出 ♣3 →
  carol 不要。屏幕上:绿呢桌面、三个座位环绕(下家 bob 在右、上家 carol 在左)、头像与张数、
  地主标、carol 身边一个「不要」气泡、桌心翻开的底牌(3♥ K♠ 2♥)与要压的那手牌、17 张真牌面的扇形手牌。
- **发牌动画是量到的,不是看着像的**:`document.timeline` 走到 t=432ms 时 17 个动画全在 `running`、
  第一张牌在 `x=216, y=-108, opacity 0`;t=882ms 时 15 个还在跑、第一张已就位;t=2482ms 时**一个都不剩**。
  中途那一帧的截图上,前 8 张已落位、后 9 张还挤在中间往外飞。
  `--force-prefers-reduced-motion` 下同一组采样:**每次都是 0 个动画**,牌从第一帧就在原位。
- **375 px 是在满屏内容下量的**:20 张牌 / 两家各 17 张牌背 / 桌上一手牌,**没有任何元素**
  `scrollWidth > clientWidth`。这一条是必要的:三次溢出里有两次在页面级
  `scrollWidth - clientWidth === 0` 下完全看不见。
- **三个皮肤各截了一张**:wood 绿呢 + 红牌背、midnight 青灰呢 + 石板牌背 + 青色高亮、
  classic 跟随主题(灰桌 + 蓝牌背)。牌面在三者下都是浅色纸面,而 ♥ / ♦ 在三者下都是红的。
- **Browser pane 隐藏时动画一件也量不到**:`document.timeline.currentTime` **冻在 0**,
  所有动画 `running@0`、`opacity: 0`。CLAUDE.md 记的是「读到的 DOM 属性是旧的」,而更准确的说法是
  **时间线根本不走**。这次的动画证据全部来自 headless CDP(它会合成),而 `chrome --screenshot`
  也不行 —— 它只能靠 `--virtual-time-budget`,而 SignalR 的长轮询一直挂着,虚拟时间到点时页面还
  停在骨架屏上。
