import type { GameManifest } from '../game-manifest';

/** 中国象棋 — the heavy rules lift: piece-specific moves, 将帅照面, perpetual-check bans, repetition draws. Also the first game whose move payload is from→to rather than a single cell. */
export const xiangqiManifest: GameManifest = {
  key: 'xiangqi',
  category: 'match',
  status: 'planned',
  titleKey: 'games.xiangqi.title',
  descriptionKey: 'games.xiangqi.description',
  icon: '帥',
  contentLocales: ['zh-CN', 'en'],
};
