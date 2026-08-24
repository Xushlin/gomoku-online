import type { GameManifest } from '../game-manifest';

/** 挖坑 — three seats, 52 cards, no bombs, and the first game whose first mover comes from the deal. */
export const wakengManifest: GameManifest = {
  key: 'wakeng',
  category: 'match',
  status: 'available',
  titleKey: 'games.wakeng.title',
  descriptionKey: 'games.wakeng.description',
  // 一张牌配 ♣ —— 52 张无王，首叫权归**最小的梅花**。
  emblem: [
    { k: 'r', a: 7, b: 4, c: 10, d: 16, r: 1.6 },
    { k: 'c', a: 12, b: 9, c: 2.1, f: 1 },
    { k: 'c', a: 9.6, b: 12.6, c: 2.1, f: 1 },
    { k: 'c', a: 14.4, b: 12.6, c: 2.1, f: 1 },
    { k: 'l', a: 12, b: 13, c: 12, d: 17 },
  ],
  contentLocales: ['zh-CN'],
  launchRoute: '/g/wakeng/lobby',
};
