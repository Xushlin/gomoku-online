import type { GameManifest } from '../game-manifest';

/** 成语纵横 — fill a crossword grid with 成语 from a tile tray. First game of the puzzle category; answers are validated server-side so the leaderboard means something. */
export const idiomCrosswordManifest: GameManifest = {
  key: 'idiom-crossword',
  category: 'puzzle',
  status: 'planned',
  titleKey: 'games.idiom-crossword.title',
  descriptionKey: 'games.idiom-crossword.description',
  icon: '田',
  contentLocales: ['zh-CN'],
};
