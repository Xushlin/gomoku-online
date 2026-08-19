import type { GameManifest } from '../game-manifest';

/** 斗地主 — three seats, hidden hands, settled in points rather than ELO. The game that made the match kernel stop assuming two players. */
export const doudizhuManifest: GameManifest = {
  key: 'doudizhu',
  category: 'match',
  status: 'available',
  titleKey: 'games.doudizhu.title',
  descriptionKey: 'games.doudizhu.description',
  icon: '斗',
  contentLocales: ['zh-CN'],
  launchRoute: '/g/doudizhu/lobby',
};
