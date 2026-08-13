import type { GameManifest } from '../game-manifest';

/** 猜成语 — guess the 成语 from its explanation. Built from the same dictionary import as the other idiom games. */
export const idiomGuessManifest: GameManifest = {
  key: 'idiom-guess',
  category: 'puzzle',
  status: 'planned',
  titleKey: 'games.idiom-guess.title',
  descriptionKey: 'games.idiom-guess.description',
  icon: '谜',
  contentLocales: ['zh-CN'],
};
