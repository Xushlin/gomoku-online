import type { CardSuit } from './cards';

/**
 * 一张牌的**花色图** —— 只有路径,没有颜色。
 *
 * 素材是用户提供的那份素材包里的四个花色图(♠♣♥♦),降到 96×96 放在 `public/cards/`。
 * 整张牌的**牌面**没有用位图:那 54 张定死的位图既不跟 app 主题、也不跟棋盘皮肤,而这个仓库的
 * 硬规则是组件里不许写死颜色。判据与 `add-web-xiangqi` 给象棋棋子的那条相同,连约束一起继承 ——
 * **皮肤挑的是深浅,不是色相**。纸面、边框、角标、牌背、桌面因此全部走 token,而**花色的色相
 * 是这个游戏的身份**(♥ 必须是红的),所以花色用图。
 *
 * 顺带解决一个老问题:`♥` 在部分平台会被渲染成彩色 emoji,而一张图不会。
 *
 * **路径为什么在 TS 而不在 CSS 里,是被测出来的。** 第一版写在 `card-table.css` 的 `url()` 里
 * (CSS 才是绘制权威),而测试用的那份构建**没有 .png 的 loader**:绝对路径报
 * `Could not resolve`,相对路径报 `No loader is configured for ".png"` —— 两次都是整个测试构建
 * 失败,而样式表的完整性测试要用 `?raw` 读它。于是路径回到这里,图片留在 `public/`(原样拷贝的
 * 静态资源,不进打包器),而「路径指着一个真存在的文件」由一条走遍 54 个编码的测试钉住。
 */

/** 花色图的文件名(不含扩展名)。测试用它去 `public/cards/` 找文件。 */
export const SUIT_ASSETS = ['spade', 'heart', 'club', 'diamond'] as const;

const SUIT_ASSET: Record<Exclude<CardSuit, 'none'>, (typeof SUIT_ASSETS)[number]> = {
  spades: 'spade',
  hearts: 'heart',
  clubs: 'club',
  diamonds: 'diamond',
};

/** 花色图的路径;王没有花色,返回 `null`。 */
export function pipUrl(suit: CardSuit): string | null {
  if (suit === 'none') return null;
  const asset = SUIT_ASSET[suit];
  return asset ? `/cards/${asset}.png` : null;
}

/**
 * 绑给 `--ddz-pip` 的值。
 *
 * 王返回 `null` —— 给它凑一个花色,就是用一个合法值表示「不适用」,而内核那条规则
 * (`MoveIntent` 上加粗的那句)在显示层同样成立。牌面上它画的是一个「王」字。
 */
export function pipStyle(suit: CardSuit): string | null {
  const url = pipUrl(suit);
  return url === null ? null : `url("${url}")`;
}
