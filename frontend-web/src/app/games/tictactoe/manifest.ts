import type { GameManifest } from '../game-manifest';

/**
 * 一字棋 — three in a row on 3×3.
 *
 * It shares its entire rules engine with gomoku: the backend registers it as
 * `NInARowRules("tictactoe", 3, 3, 3)` and writes no win-detection code of its
 * own. That made it the change that *tested* the rules registry rather than
 * extending it — see the `add-tictactoe` audit for what that measurement found.
 *
 * `status: 'available'` ships human-vs-AI only. There is deliberately no lobby,
 * no human-vs-human entry and no leaderboard: the game is unrated (perfect play
 * always draws, so a rating over it measures nothing), and parameterising the
 * lobby would drag gomoku's shipped `/home` UX into an unrelated change.
 */
export const ticTacToeManifest: GameManifest = {
  key: 'tictactoe',
  category: 'match',
  status: 'available',
  titleKey: 'games.tictactoe.title',
  descriptionKey: 'games.tictactoe.description',
  // 井字格 + 一个 O 一个 X，对角相望。
  emblem: [
    { k: 'l', a: 10, b: 4, c: 10, d: 20 },
    { k: 'l', a: 14, b: 4, c: 14, d: 20 },
    { k: 'l', a: 4, b: 10, c: 20, d: 10 },
    { k: 'l', a: 4, b: 14, c: 20, d: 14 },
    { k: 'c', a: 7, b: 7, c: 2 },
    { k: 'l', a: 15.4, b: 15.4, c: 18.6, d: 18.6 },
    { k: 'l', a: 18.6, b: 15.4, c: 15.4, d: 18.6 },
  ],
  // 席位名 —— 一字棋是缩小的五子棋,同一套读法。
  seatLabelKeys: ['game.seat.black', 'game.seat.white'],
  contentLocales: ['zh-CN', 'en'],
  launchRoute: '/g/tictactoe',
};
