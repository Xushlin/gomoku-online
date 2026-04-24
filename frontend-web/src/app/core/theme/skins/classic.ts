/*
 * Classic skin — clean flat aesthetic that follows the active app theme
 * (bg/surface/border/muted tokens cascade from theme tokens.css). Mirrors
 * src/styles/board-skins.css for `[data-board-skin='classic']`.
 */
import type { BoardSkinTokens } from '../board-skin.tokens';

export const classicSkin: BoardSkinTokens = {
  board: {
    bg: 'var(--color-surface)',
    line: 'var(--color-border)',
    star: 'var(--color-muted)',
    radius: 'var(--radius-card)',
    shadow: '0 2px 8px rgb(0 0 0 / 0.08)',
  },
  stones: {
    blackFill:
      'radial-gradient(circle at 30% 25%, color-mix(in srgb, var(--color-text) 65%, white), var(--color-text) 70%)',
    blackShadow: '0 1px 2px rgb(0 0 0 / 0.35), inset -1px -1px 2px rgb(0 0 0 / 0.25)',
    whiteFill:
      'radial-gradient(circle at 30% 25%, var(--color-bg), color-mix(in srgb, var(--color-bg) 60%, var(--color-muted)))',
    whiteRim: 'var(--color-muted)',
    whiteShadow: '0 2px 4px rgb(0 0 0 / 0.35), inset 0 0 0 1.5px var(--color-muted)',
  },
  lastMove: {
    ring: 'var(--color-primary)',
  },
};
