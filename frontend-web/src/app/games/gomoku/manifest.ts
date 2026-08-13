import type { GameManifest } from '../game-manifest';

/**
 * 五子棋 — the platform's first game, and the one that established the match
 * kernel (rooms, seats, spectators, chat, ELO, replay) every later `match`
 * game reuses.
 *
 * `launchRoute` is `/home` rather than `/g/gomoku` because gomoku's lobby is
 * still the legacy root route; `generalize-match-contract` moves it, since
 * that change already rewrites the specs pinning `/home`.
 */
export const gomokuManifest: GameManifest = {
  key: 'gomoku',
  category: 'match',
  status: 'available',
  titleKey: 'games.gomoku.title',
  descriptionKey: 'games.gomoku.description',
  icon: '⬤',
  contentLocales: ['zh-CN', 'en'],
  launchRoute: '/home',
};
