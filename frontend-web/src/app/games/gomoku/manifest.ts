import type { GameManifest } from '../game-manifest';

/**
 * 五子棋 — the platform's first game, and the one that established the match
 * kernel (rooms, seats, spectators, chat, ELO, replay) every later `match`
 * game reuses.
 */
export const gomokuManifest: GameManifest = {
  key: 'gomoku',
  category: 'match',
  status: 'available',
  titleKey: 'games.gomoku.title',
  descriptionKey: 'games.gomoku.description',
  icon: '⬤',
  contentLocales: ['zh-CN', 'en'],
  launchRoute: '/g/gomoku/lobby',
};
