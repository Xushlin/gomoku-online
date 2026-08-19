/// <reference types="vite/client" />
import { describe, expect, it } from 'vitest';

import { pipStyle, pipUrl, SUIT_ASSETS } from './card-art';
import { decodeCard } from './cards';

/**
 * 每一张牌都指着一个真存在的花色图,而两张王一张都不指。
 *
 * **走遍全部 54 个编码,而不是抽查四个花色。** 抽查证明的是「这个映射对某一张牌是对的」,
 * 而屏幕上要画的是一副牌:一个漏掉的点数(比如 10 的四张)在抽查下完全看不见。
 *
 * 文件存在性用**惰性** `import.meta.glob` 只取键名。这不是省事:测试用的那份构建没有 .png 的
 * loader,eager 版本会让整个构建失败(`No loader is configured for ".png"`)—— 惰性 glob 的键
 * 由 Vite 在构建期按真实目录展开,所以它证明的正是「文件在磁盘上」,而一次模块加载都没有发生。
 */

// 必须是字面量 —— `import.meta.glob` 是 Vite 的编译期变换。
const ART = import.meta.glob('../../../../public/cards/*.png');

const ON_DISK = new Set(
  Object.keys(ART).map((path) => path.slice(path.lastIndexOf('/') + 1).replace('.png', '')),
);

/** 服务端字母表的全部 54 个编码。 */
const ALL_CODES = [...'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz@#'];

describe('card art', () => {
  it('finds the four suit images on disk', () => {
    // glob 一个都没匹配到时,下面每条断言都会空过 —— 所以先给它一个底。
    expect(ON_DISK.size, 'public/cards/*.png matched nothing').toBe(4);
    for (const suit of SUIT_ASSETS) {
      expect(ON_DISK, `public/cards/${suit}.png is missing`).toContain(suit);
    }
  });

  it('maps all 52 suited cards to an image that exists, and both jokers to none', () => {
    expect(ALL_CODES).toHaveLength(54);
    const jokers: string[] = [];
    for (const code of ALL_CODES) {
      const card = decodeCard(code);
      expect(card, `alphabet character ${code} does not decode`).not.toBeNull();
      const url = pipUrl(card!.suit);
      if (card!.suit === 'none') {
        jokers.push(code);
        expect(url, `${code} is a joker and must have no pip`).toBeNull();
        continue;
      }
      expect(url, `${code} (${card!.label}) has no pip url`).not.toBeNull();
      const file = url!.slice(url!.lastIndexOf('/') + 1).replace('.png', '');
      expect(ON_DISK, `${code} points at ${url} which is not on disk`).toContain(file);
    }
    // 两张王,不是零张也不是三张 —— 否则上面那个 continue 可能吞掉了整副牌。
    expect(jokers).toEqual(['@', '#']);
  });

  it('wraps the path in url() for the custom property', () => {
    expect(pipStyle('spades')).toBe('url("/cards/spade.png")');
    expect(pipStyle('none')).toBeNull();
  });

  // 「样式表里 MUST NOT 再写一份路径」那一半在 `scripts/check-board-skins.mjs` 里守着:
  // 这份构建读不到 CSS 文本 —— `?raw` 的默认导出是 `[]`,而 `node:fs` 在 spec 的 tsconfig
  // 里没有类型。那个脚本挂在 `npm run lint` 上,所以 CI 照样会红。
});
