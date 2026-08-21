/*
 * Ink theme — 活字印刷.
 *
 * The palette comes from the 成语纵横 prototype: 墨蓝 ground, 宣纸 tiles, 朱砂
 * seal red, 竹青 for solved. It is registered as a *theme* rather than a board
 * skin because it is a whole-page look, so a player who picks it keeps it in
 * gomoku too.
 *
 * The prototype is dark-only. A theme's contract is a matched pair, so the light
 * set inverts it: 宣纸 ground with ink text, 朱砂 kept as the accent — vermilion
 * on paper is the older half of this look anyway.
 *
 * See material.ts for why token literals live here rather than in CSS alone.
 */
import type { ThemeTokens } from '../theme.tokens';

export const inkTokens: ThemeTokens = {
  light: {
    colors: {
      bg: '#f4ecdb', // 宣纸
      surface: '#fbf6ea', // 稍亮的纸面,让卡片浮起来
      primary: '#a8301f',
      onPrimary: '#ffffff', // 朱砂,压深到能在纸上过 AA
      text: '#26221c', // 墨
      muted: '#6b5f4a',
      border: '#cbbc9d', // 字块底缘
      danger: '#a8301f',
      success: '#2f6b52', // 竹青,压深版
      warning: '#8a5a12',
    },
    radii: { card: '8px', control: '8px' },
    shadows: {
      elevated: '0 2px 6px rgb(38 34 28 / 0.14), 0 1px 2px rgb(38 34 28 / 0.10)',
      raised: '0 0 #0000',
      inset: '0 0 #0000',
    },
    surfaces: { image: 'none', edge: 'var(--color-border)', edgeWidth: '1px' },
    controls: { image: 'none', edge: 'var(--color-primary)', edgeWidth: '0px' },
    accents: { color: 'var(--color-primary)', image: 'none' },
    grounds: { image: 'none' },
  },
  dark: {
    colors: {
      bg: '#171c26', // 墨蓝底
      surface: '#1f2531',
      primary: '#e4785f',
      onPrimary: '#ffffff', // 朱砂在暗底上要提亮才够对比
      text: '#ece3cf', // 宣纸色的字
      muted: '#9aa0ab',
      border: '#39404e',
      danger: '#e4785f',
      success: '#5fbf94', // 竹青提亮版
      warning: '#d9a95c', // 哑金
    },
    radii: { card: '8px', control: '8px' },
    shadows: {
      // 比别的主题重 —— 活字是有厚度的,阴影是那个厚度。
      elevated: '0 3px 10px rgb(0 0 0 / 0.45), 0 1px 3px rgb(0 0 0 / 0.35)',
      raised: '0 0 #0000',
      inset: '0 0 #0000',
    },
    surfaces: { image: 'none', edge: 'var(--color-border)', edgeWidth: '1px' },
    controls: { image: 'none', edge: 'var(--color-primary)', edgeWidth: '0px' },
    accents: { color: 'var(--color-primary)', image: 'none' },
    grounds: { image: 'none' },
  },
};
