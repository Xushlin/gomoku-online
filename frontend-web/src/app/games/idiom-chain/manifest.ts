import type { GameManifest } from '../game-manifest';

/** 成语接龙 — two players alternate 成语 whose first character matches the previous last one. The case that proves the match rules engine is not board-shaped. */
export const idiomChainManifest: GameManifest = {
  key: 'idiom-chain',
  category: 'match',
  status: 'available',
  titleKey: 'games.idiom-chain.title',
  descriptionKey: 'games.idiom-chain.description',
  icon: '链',
  contentLocales: ['zh-CN'],
  launchRoute: '/g/idiom-chain/lobby',
};
