# draw-card-suits

## Why

`add-doudizhu-table-visuals` 用了用户提供的素材包里四张 96×96 的花色 PNG。用户看过之后的判断是
**「换成自己的更好」** —— 而按下去之后发现这不只是审美或授权:换成自绘的 SVG path,**有三样东西
一起消失**,而它们都是上一次为了迁就位图而搭的脚手架。

## What Changes

- `card-art.ts`:四张 PNG 换成四条自绘 SVG path,`fill="currentColor"`。
- 删掉 `public/cards/*.png`(22 KB)、`--ddz-pip` 绑定、以及那条用惰性 `import.meta.glob`
  证明文件在磁盘上的测试。
- `check-styles.mjs` 多一条:牌桌的样式表里**不许出现 `url(`**。
- 前端以外:**零改动**。

## 三样一起消失的东西

**一、颜色回到 token。** `currentColor` 让花色跟着牌面的 `color`,也就是 `--card-red` /
`--card-black` —— 于是**皮肤重新拿回了「深浅」那一半**,而 `add-web-xiangqi` 定的约束仍然成立:
皮肤挑的是深浅,不是色相,红的仍然是红的。一张定死的位图给不了这个:它在每个皮肤下都一样。

**二、那套 loader 绕路整段作废。** 上一版量到这份测试构建**没有 `.png` 的 loader**,于是路径只能
由组件绑成 `--ddz-pip`;为了证明那条路径指着一个真存在的文件,还要一条**惰性** glob 的测试
(eager 会让整个构建失败);为了防止那个 style 绑定被清洗掉导致花色**静静地不见**,还要一条读
inline style 的断言。**三样东西现在都不需要了。能被删掉的机制才是最好的机制。**

**三、授权问题消失。** 仓库里不再有第三方素材。

## 形状是看过的,不是写完就算

四条 path 是手写的,在 headless Chrome 里按三个尺寸(96 / 40 / 18 px)和四张模拟牌面渲染出来
看过:梅花的第一版没有梗(像一株三叶草),换了带梗的那版;黑桃取了肩部更饱满的那版。
**一条 path 写得对不对,只有画出来才知道。**

## Impact

- Affected specs: `web-doudizhu`(「牌桌画成一张桌子」那条里关于花色来源的段落)
- Affected code: `games/doudizhu/card-art.ts` + spec、`card-table.{ts,html,css}` + spec、
  `scripts/check-styles.mjs`,删除 `public/cards/`
- 后端:**零改动**
