import type { CardSuit } from './cards';

/**
 * 四个花色的形状 —— **自绘的 SVG path,没有素材依赖**。
 *
 * 上一版用的是素材包里的四张 96×96 PNG。换成 path 有三件事一起变好,而它们都不是审美问题:
 *
 *   1. **颜色回到 token。** `fill="currentColor"` 让花色跟着牌面的 `color`,也就是
 *      `--card-red` / `--card-black` —— 于是皮肤重新拿回了「深浅」那一半,而
 *      `add-web-xiangqi` 定的约束(**皮肤挑的是深浅,不是色相**)仍然成立:红的仍然是红的。
 *      位图给不了这个:一张定死的图在每个皮肤下都一样。
 *   2. **那套 loader 绕路整段消失。** 上一版因为测试构建没有 `.png` 的 loader,路径只能由组件
 *      绑成 `--ddz-pip`,还要一条「惰性 glob 只取键名」的测试去证明文件在磁盘上,以及一条
 *      「绑定没被清洗掉」的断言 —— 三样东西现在都不需要了。**能被删掉的机制才是最好的机制。**
 *   3. 任意尺寸都清晰,而且不占字节(四条 path 加起来不到 700 字符)。
 *
 * 顺带,`♥` 在部分平台会被渲染成彩色 emoji 的老问题,path 一样没有。
 *
 * viewBox 统一 `0 0 100 100`,四条 path 都是闭合的(`M…Z`),尺寸由 CSS 决定。
 */
const SUIT_PATHS: Record<Exclude<CardSuit, 'none'>, string> = {
  spades:
    'M50 5C50 5 12 34 12 57c0 13 9.5 22 21 22 6.5 0 12-3 15.5-8.6C47 83 41.5 93 34 99h32' +
    'c-7.5-6-13-16-14.5-28.6C55 76 60.5 79 67 79c11.5 0 21-9 21-22C88 34 50 5 50 5Z',
  hearts:
    'M50 92C18 68 8 52 8 37 8 23 19 12 32 12c8 0 15 4 18 11 3-7 10-11 18-11 13 0 24 11 24 25 0 15-10 31-42 55Z',
  clubs:
    'M50 6a19 19 0 0 1 14 31.7A19 19 0 1 1 78 70a19 19 0 0 1-24-8.4c1.5 12 6.8 22 14 28H32' +
    'c7.2-6 12.5-16 14-28A19 19 0 0 1 22 70 19 19 0 1 1 36 37.7 19 19 0 0 1 50 6Z',
  diamonds: 'M50 5 92 50 50 95 8 50Z',
};

/** 花色的形状;王没有花色,返回 `null`。 */
export function pipPath(suit: CardSuit): string | null {
  return suit === 'none' ? null : (SUIT_PATHS[suit] ?? null);
}

/** 四个花色的键 —— 测试用它走一遍,而它是从形状表推出来的,不是另抄一份。 */
export const SUITS_WITH_ART = Object.keys(SUIT_PATHS) as readonly Exclude<CardSuit, 'none'>[];
