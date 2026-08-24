import type { GameManifest } from '../game-manifest';

/** 成语纵横 — fill a crossword grid with 成语 from a tile tray. First game of the puzzle category; answers are validated server-side so the leaderboard means something. */
export const idiomCrosswordManifest: GameManifest = {
  key: 'idiom-crossword',
  category: 'puzzle',
  status: 'available',
  titleKey: 'games.idiom-crossword.title',
  descriptionKey: 'games.idiom-crossword.description',
  // 一横一纵在中间那格相交 —— 「纵横」就是字面意思。
  emblem: [
    { k: 'r', a: 4, b: 10, c: 4, d: 4 },
    { k: 'r', a: 8, b: 10, c: 4, d: 4 },
    { k: 'r', a: 12, b: 10, c: 4, d: 4, f: 1 },
    { k: 'r', a: 16, b: 10, c: 4, d: 4 },
    { k: 'r', a: 12, b: 2, c: 4, d: 4 },
    { k: 'r', a: 12, b: 6, c: 4, d: 4 },
    { k: 'r', a: 12, b: 14, c: 4, d: 4 },
    { k: 'r', a: 12, b: 18, c: 4, d: 4 },
  ],
  contentLocales: ['zh-CN'],
  launchRoute: '/g/idiom-crossword',
};
