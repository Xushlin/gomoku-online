# add-web-wakeng — tasks

## 1. 把共享的那部分搬出 `games/doudizhu/`

- [x] `cards.ts` / `card-art.ts` / `table-layout.ts` / `trick.ts` / `card-table/` → `games/cards/`。
      与 `hoist-card-model` 在服务端做的是同一件事、同一个理由:**一个棋种的目录不该成为
      另一个棋种的承重结构。**
- [x] 类型名去掉游戏名:`DoudizhuCard` → **`PlayingCard`**、`DoudizhuAction` → `CardAction`。
      (提案里写的是 `Card`;在一个 UI 代码库里那个名字太容易撞上别的东西,所以取了更窄的。)
- [x] **改掉 `rank` 那句「数值就是大小顺序」** —— 它只对斗地主成立,而这是同一个巧合的第三次。
- [x] 行为零改动:斗地主既有的牌桌测试**一条都不许改断言**(只改 import 路径)。
      那是「搬家没有改行为」的可执行形式。

## 2. 参数化牌桌

- [x] `CardTableConfig`:`kittySize` / `bidLabelKeys` / `roleLabelKey` / `showsFirstBidder` /
      `compareForDisplay` / `parseSeatView`。
- [x] 斗地主与挖坑各一份配置;`room-page` 的分支**不增加**(同一个组件,不同配置)。
- [x] 首叫者与他亮的那张 ♣ 在桌上标出来(仅挖坑)。

## 3. 挖坑自己的东西

- [x] `games/wakeng/`:`game-key.ts` / `manifest.ts` / `seat-view.ts` / `strength.ts` / `config.ts`。
- [x] `strength.ts`:`3 > 2 > A > … > 4`,与服务端 `WakengRank.Strength` 对齐。
- [x] manifest:`status: 'available'`、`launchRoute: '/g/wakeng/lobby'`、`category: 'match'`。
- [x] i18n 两份 JSON:`games.wakeng.*` + 挖坑的房间文案(挖 / 不挖 / 挖坑者 / 首叫)。

## 4. 测试

- [x] 手牌顺序:挖坑按挖坑的强弱排,**用一手含 3 或 2 的牌**;斗地主不受影响。
- [x] 底牌 4 张(叫分阶段桌心画四张背面)。
- [x] 叫分按钮出「挖 N 分」而不是「叫 N 分」。
- [x] 首叫者标记只在挖坑出现。
- [x] 变异:`compareForDisplay` 换成斗地主那份 MUST 红;`kittySize` 写死 3 MUST 红;
      `showsFirstBidder` 恒 true MUST 红。每处变异都要**真的跑起来**。

## 5. 浏览器

- [x] 起临时 API + 临时 dev server(**不碰 4200 / 5145**,改完还原 `proxy.conf.json`
      与 `.claude/launch.json`)。
- [x] 三个账号坐满,真叫一次分、真出一次牌;核对手牌 16 → 20、底牌公开、桌面那一手。
- [x] 375 px **带满屏内容**;暗色看一眼。

## 6. 收尾

- [x] 前后端全绿、lint 干净、bundle 预算不红(牌桌 CSS 是组件级的,别让它进首屏)。
- [x] PR;合并后 `openspec archive add-web-wakeng`。
- [x] CLAUDE.md:**九个棋种 ship**,以及这次「共享的是事实还是形状」逐件量的结果。

## 7. 计划之外

- [x] **我自己建的那个 lint 期检查抓住了这次搬家。** `check-styles.mjs` 里
      `CARD_CSS = 'src/app/games/doudizhu/card-table/card-table.css'` 是按**文件名**钉的,
      于是 `npm run lint` 当场炸。**它按文件名钉正是它有用的原因**:一次搬家如果让那些
      不变量静静失效,谁都不会发现。
- [x] **首叫者那个标记第一版让组件样式预算红了 90 字节。** 我给它加了个 `.ddz-card--mini`
      类,写了 `width` / `height` / `font-size` —— 而 `.ddz-card` 本来就从 `--ddz-w` 推出
      这三样。于是那个类**整个删掉**,改成内联绑一个 `--ddz-w`(底牌那一支本来就在内联绑
      `--ddz-gaps`),**新增 CSS 零行**。`add-doudizhu-table-visuals` 记过:为了绕开这个预算
      去用 Tailwind arbitrary utility,会把字节挪进**首屏**的样式表,更糟。
- [x] **一条断言我先写错了,而错法值得记。** 「首叫者标记显示 3 号座位」—— 而测试用的
      transloco **没有翻译**,所以 `{{seat}}` 根本不插值,渲染出来的是键本身。
      改成断言**哪一张牌**在:服务端点名 ♣4,而手里另有一张 3 —— 于是它证明的是
      「画的是被点名的那张」,而不是「从手牌里随便挑了一张」。**比原来那条更强。**
- [x] **暗色我第一次量错了属性。** `--felt-bg` / `--card-face` 是**渐变**,所以它们落在
      `background-image` 上,而我读的是 `backgroundColor` —— 读到 `rgba(0,0,0,0)`,
      看起来像「暗色下没有底色」。改读 `backgroundImage` 之后两个模式逐值不同
      (felt `#2f7a4a` → `#1f5334`,牌面 `#fffdf6` → `#f6f1e2`)。
      **一个量错了属性的测量,和一个真的缺陷,长得一模一样。**
- [x] **点牌出牌这条交互在这个 pane 里验不了。** Browser pane 不显示时页面不合成帧,
      zoneless 的变更检测不同步跑,所以点完一张牌再读 DOM 读到的是 `Play (0)` 与
      `disabled` —— 那不是缺陷,是这个环境的已知限制(本仓库记过两次)。
      那条路径的权威是单测(`card-table.spec.ts` 里 `emits the selected cards in ascending order`),
      而载荷本身在 `add-wakeng` 的真 SignalR 探针里走通过。
- [x] **一局挖坑会自己往前走**,因为超时兜底 60 秒一手:我第一次打开房间时叫分已经结束、
      底牌已经公开。所以「叫分阶段长什么样」要**新开一局立刻看** ——
      与 `fix-lobby-seats` 记的「Playing 的三座位房间约一分钟自己消失」同源。
