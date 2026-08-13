import type { GameManifest } from '../game-manifest';

/** 一字棋 — three in a row on 3×3. Shares its rules engine with gomoku via a parameterised n-in-a-row implementation, so it is the proof that a new board game costs one registration. */
export const ticTacToeManifest: GameManifest = {
  key: 'tictactoe',
  category: 'match',
  status: 'planned',
  titleKey: 'games.tictactoe.title',
  descriptionKey: 'games.tictactoe.description',
  icon: '井',
  contentLocales: ['zh-CN', 'en'],
};
