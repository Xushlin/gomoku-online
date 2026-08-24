import type { GameManifest } from '../game-manifest';

/** 斗地主 — three seats, hidden hands, settled in points rather than ELO. The game that made the match kernel stop assuming two players. */
export const doudizhuManifest: GameManifest = {
  key: 'doudizhu',
  category: 'match',
  status: 'available',
  titleKey: 'games.doudizhu.title',
  descriptionKey: 'games.doudizhu.description',
  // 一张牌配「王」—— 54 张带双王，而王炸压一切。
  emblem: [
    { k: 'r', a: 7, b: 4, c: 10, d: 16, r: 1.6 },
    { k: 't', a: 12, b: 12, c: 7, s: '王' },
  ],
  contentLocales: ['zh-CN'],
  launchRoute: '/g/doudizhu/lobby',
};
