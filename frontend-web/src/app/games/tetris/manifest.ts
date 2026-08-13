import type { GameManifest } from '../game-manifest';

/** 俄罗斯方块 — the only score-attack game. Client-side game loop, one run submitted at the end, periodic leaderboards. */
export const tetrisManifest: GameManifest = {
  key: 'tetris',
  category: 'score',
  status: 'planned',
  titleKey: 'games.tetris.title',
  descriptionKey: 'games.tetris.description',
  icon: '块',
  contentLocales: ['zh-CN', 'en'],
};
