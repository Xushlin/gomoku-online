# tasks — draw-card-suits

- [x] 1. 手写四条 SVG path(黑桃 / 红桃 / 梅花 / 方块),viewBox `0 0 100 100`,闭合。
- [x] 2. **画出来看**:headless Chrome 渲染 96 / 40 / 18 px 三个尺寸 + 四张模拟牌面。
      梅花第一版没有梗(像一株三叶草)→ 换带梗的;黑桃取肩部更饱满的那版。
      **一条 path 写得对不对,只有画出来才知道。**
- [x] 3. `card-art.ts`:`pipPath(suit)` + `SUITS_WITH_ART`(从形状表推出来,不另抄一份);
      删掉 `pipUrl` / `pipStyle` / `SUIT_ASSETS`。
- [x] 4. 模板:两个 `<svg viewBox="0 0 100 100"><path [attr.d] fill="currentColor">`,同一条 path
      两个尺寸;牌上不再绑 `--ddz-pip`。
- [x] 5. CSS:`.ddz-card__pip` 从背景图变成一个 svg 盒子(要显式 `height`,`aspect-ratio` 对
      `<svg>` 不够)。
- [x] 6. 删 `public/cards/*.png`;`check-styles.mjs` 多一条「这份样式表里不许有 `url(`」。
- [x] 7. 测试改断言:从「inline style 里有 `url(...)`」改成「`<path d>` 以 M 开头、以 Z 结尾、
      四条两两不同、王没有 path」。
- [x] 8. `npx ng test --no-watch` 791 绿、`npm run lint` 通过、`npm run build` 零警告,
      初始包 **479.66 kB**(不变 —— path 在 lazy chunk 里)。

## 量到的东西

- **`currentColor` 真的接到了皮肤 token 上**,而这是这次换路线的全部意义,所以它是量的不是看的:
  同一张 ♥ 的 `<path>` 计算色 —— wood 下 `rgb(198,40,40)`(`#c62828`)、midnight 下
  `rgb(217,59,57)`(`#d93b39`);同一张 ♠ —— wood `rgb(43,43,43)`、midnight `rgb(29,36,48)`。
  **同一个色相,不同的深浅,由皮肤决定。** 一张 PNG 在两个皮肤下只会是同一个值。
- 两个尺寸都在画:底牌那张 38×54 的牌上,小花色 9.9px 在 (3.9, 20)、大花色 19px 在 (15.5, 31.7)
  —— 正好是牌宽的 26% 与 50%,位置分别在左上与右下。
- 791 前端测试绿、lint 通过、build 零警告,初始包 **479.66 kB**(不变 —— path 在 lazy chunk 里)。

## 一个我自己造的回归,只有截图看得见

为了把组件 CSS 压进 4 kB 的预算,我在上一个 change 里删掉了 `:host { width: 100% }`,理由写的是
「宽度由父级 flex 给」。**那是错的**:房间页的容器是 `flex-col items-center`,而 `items-center` 让
子元素 shrink-to-fit —— 整张桌子按内容收窄,felt 从 ~730px 变成 **~430px**,而牌宽是 `8.6vw`
(跟视口,不跟容器),于是牌挤在一张窄桌上。

**这是「shrink-to-fit 咬到这张牌桌」的第四次**(前三次:右侧座位的 `flex-end`、改成 `center`、
桌心的 `items-center`)。所以它现在被 `check-styles.mjs` 钉住了,并变异验过:删掉那一行,
`npm run lint` 报 `:host must set width: 100%`。

**上一个 change 的所有单元测试与断言都是绿的** —— jsdom 没有排版引擎,量不到宽度。
