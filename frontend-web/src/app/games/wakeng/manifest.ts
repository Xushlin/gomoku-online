import type { GameManifest } from '../game-manifest';

/** 挖坑 — three seats, 52 cards, no bombs, and the first game whose first mover comes from the deal. */
export const wakengManifest: GameManifest = {
  key: 'wakeng',
  category: 'match',
  status: 'available',
  titleKey: 'games.wakeng.title',
  descriptionKey: 'games.wakeng.description',
  icon: '挖',
  contentLocales: ['zh-CN'],
  launchRoute: '/g/wakeng/lobby',
};
