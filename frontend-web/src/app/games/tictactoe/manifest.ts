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
  icon: '井',
  contentLocales: ['zh-CN', 'en'],
  launchRoute: '/g/tictactoe',
  board: { rows: 3, cols: 3 },
};
