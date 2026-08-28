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
  // 棋盘 + 一排三子，一黑一白一黑 —— 两方在同一条线上。
  emblem: [
    { k: 'l', a: 6, b: 8, c: 18, d: 8 },
    { k: 'l', a: 6, b: 12, c: 18, d: 12 },
    { k: 'l', a: 6, b: 16, c: 18, d: 16 },
    { k: 'l', a: 8, b: 6, c: 8, d: 18 },
    { k: 'l', a: 12, b: 6, c: 12, d: 18 },
    { k: 'l', a: 16, b: 6, c: 16, d: 18 },
    { k: 'c', a: 8, b: 12, c: 2.6, f: 1 },
    { k: 'c', a: 12, b: 12, c: 2.6 },
    { k: 'c', a: 16, b: 12, c: 2.6, f: 1 },
  ],
  // 席位名 —— 五子棋就是黑白子。
  seatLabelKeys: ['game.seat.black', 'game.seat.white'],
  contentLocales: ['zh-CN', 'en'],
  launchRoute: '/g/gomoku/lobby',
};
