import type { GameManifest } from '../game-manifest';

/** 成语接龙 — two players alternate 成语 whose first character matches the previous last one. The case that proves the match rules engine is not board-shaped. */
export const idiomChainManifest: GameManifest = {
  key: 'idiom-chain',
  category: 'match',
  status: 'available',
  titleKey: 'games.idiom-chain.title',
  descriptionKey: 'games.idiom-chain.description',
  // 两个环扣在一起 —— 上一句的尾字是下一句的头字。
  emblem: [
    { k: 'r', a: 3, b: 8, c: 11, d: 8, r: 4 },
    { k: 'r', a: 10, b: 8, c: 11, d: 8, r: 4 },
  ],
  // 席位名 —— 成语接龙没有棋盘,也没有颜色。
  seatLabelKeys: ['game.seat.first', 'game.seat.second'],
  contentLocales: ['zh-CN'],
  launchRoute: '/g/idiom-chain/lobby',
};
