# tasks — restyle-klotski-board

## 1. 先让"三个皮肤一模一样"这件事有一条会红的测试

- [ ] 写一条断言:遍历 `BoardSkinService` 注册表(**推导,不手写**),对每个皮肤读 `.kt-board` / `.kt-piece` 的计算背景,断言两两不同。
- [ ] **它现在就必须红**,而且红的理由要对(三个读数相同),不是"找不到元素"。先看到这个红,再动 CSS。
- [ ] 反面对照:把断言改成"引用了 `var(--board-…)`",确认它在**今天的坏版本上也是绿的** —— 这就是判据不能选它的原因。

## 2. 接进皮肤层

- [ ] `board-skins.css` 的 `wood` 块加华容道需要的 `--kt-*`(四类棋子的面/边/字、盘面槽、出口、落点)。
- [ ] `classic` / `midnight` 不补 → `npm run lint` 红。**确认这个红出现过**,再补上。
- [ ] `.kt-*` 改为消费 `--board-*` / `--kt-*`,`--color-surface` / `--color-text` 从这几条规则里消失。

## 3. 角色从尺寸推

- [ ] 纯函数 `roleOf(piece)`:`2×2 && target` → 主帅;`1×2` → 竖将;`2×1` → 横将;`1×1` → 兵;其它 → 明确兜底。
- [ ] 单元测试直接测这个函数,含兜底那一支。
- [ ] 组件按角色加类;模板里 **MUST NOT** 出现棋子名。
- [ ] 渲染测试用一个**四类齐全**的盘面,断言四个背景两两不同 —— 并断言样本里四类都在(否则它在两类的盘面上恒真)。

## 4. 让它真的滑

- [ ] 定位从 `grid-area` 换成 `transform: translate()`;grid 只负责格子尺寸。
- [ ] 测试:移动前后 `transform` 变了,而 `grid-row-start` / `grid-column-start` 没变。
- [ ] **变异:把 `transform` 换回 `grid-area`** → 这条必须红。
- [ ] 删掉 `global.css` 里那句「browsers animate as a layout change」——它描述的是浏览器不做的事。
- [ ] `prefers-reduced-motion: reduce` 下 `transition-duration` 为 `0s`。

## 5. 尺寸

- [ ] 去掉写死的 `max-width: 360px`,`sm` 以上跟容器长大;长宽比恒为 `cols / rows`。
- [ ] 375px:用**格数最多的那一关**测无横向溢出(空盘面对任何布局断言都成立)。
- [ ] ≥1024px:渲染宽度 > 360px。

## 6. 对比度

- [ ] 把四类棋子的「字 / 面」配对纳入 `check-styles.mjs` 的读数,四主题 × 明暗 × 每个皮肤。
- [ ] 确认读数**总条数变多**了 —— 数字掉下去意味着覆盖丢了,不是代码变简单了。
- [ ] 变异:把某一类的字调成和面同色 → 红,并报出是哪个皮肤、哪一类。

## 7. 在真浏览器里看

- [ ] 三个皮肤 × 明暗各看一遍,截图或读数留证。
- [ ] 走一次完整的一关:选中 → 落点 → 滑动 → 通关,确认滑动是**看得见的**。
- [ ] 375px 与桌面各一遍。
- [ ] **Browser pane 不合成时 DOM 读数是陈旧的**(本仓库记过),交互后要 `window.ng.applyChanges(...)` 再读。

## 8. 收尾

- [ ] `npm run lint` 0、`test:ci` 全绿、`build` 0。
- [ ] `web-board-skins` 那条漂移(要求已被删除的 `core/theme/skins/*.ts`)一并改掉。
