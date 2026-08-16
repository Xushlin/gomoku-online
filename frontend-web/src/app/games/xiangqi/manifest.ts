import type { GameManifest } from '../game-manifest';

/**
 * 中国象棋 — the platform's first *slide* game: a move is `from → to`, not a
 * placement.
 *
 * `status: 'available'` ships human-vs-AI only, the same shape as 一字棋 and for
 * sharper reasons: `SupportsHumanVsHuman` is false on the server, so a
 * human-vs-human entry would point at an operation it refuses, and `IsRated` is
 * false, so a ladder would be permanently empty.
 *
 * It is also the game that proved the front end had the same placement-shaped
 * assumption the Domain shed in `generalize-match-domain`: gomoku and 一字棋 are the
 * same family, so the shared `Board` had never been asked by a different one.
 */
export const xiangqiManifest: GameManifest = {
  key: 'xiangqi',
  category: 'match',
  status: 'available',
  titleKey: 'games.xiangqi.title',
  descriptionKey: 'games.xiangqi.description',
  icon: '帥',
  contentLocales: ['zh-CN', 'en'],
  launchRoute: '/g/xiangqi',
};
