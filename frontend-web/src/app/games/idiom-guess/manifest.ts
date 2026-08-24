import type { GameManifest } from '../game-manifest';

/** 猜成语 — guess the 成语 from its explanation. Built from the same dictionary import as the other idiom games. */
export const idiomGuessManifest: GameManifest = {
  key: 'idiom-guess',
  category: 'puzzle',
  status: 'planned',
  titleKey: 'games.idiom-guess.title',
  descriptionKey: 'games.idiom-guess.description',
  // 三个格子一个空 —— 猜的就是那一个。
  emblem: [
    { k: 'r', a: 3, b: 9, c: 6, d: 6, r: 1 },
    { k: 'r', a: 9, b: 9, c: 6, d: 6, r: 1 },
    { k: 'r', a: 15, b: 9, c: 6, d: 6, r: 1 },
    { k: 't', a: 12, b: 12, c: 4.5, s: '?' },
  ],
  contentLocales: ['zh-CN'],
};
