import type { GameManifest } from '../game-manifest';

/** 华容道 — sliding-block puzzle. Reuses the puzzle context; state is the block layout and scoring is moves plus elapsed time. */
export const klotskiManifest: GameManifest = {
  key: 'klotski',
  category: 'puzzle',
  status: 'planned',
  titleKey: 'games.klotski.title',
  descriptionKey: 'games.klotski.description',
  icon: '华',
  contentLocales: ['zh-CN', 'en'],
};
