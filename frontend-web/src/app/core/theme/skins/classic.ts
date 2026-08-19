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
  pieces: {
    // Follows the theme for the disc and the black side; the red side does not,
    // because 红 is the game's identity rather than a palette choice.
    // --color-bg rather than --color-surface: this skin paints the board in
    // --color-surface, so matching it would make the discs invisible.
    bg: 'var(--color-bg)',
    red: '#c0392b',
    black: 'var(--color-text)',
  },
  cards: {
    face: 'var(--color-bg)',
    faceEdge: 'var(--color-border)',
    red: '#c0392b',
    black: 'var(--color-text)',
    backEdge: 'var(--color-border)',
    back: 'linear-gradient(160deg, color-mix(in srgb, var(--color-primary) 70%, black), color-mix(in srgb, var(--color-primary) 35%, black))',
  },
  felt: {
    bg: 'color-mix(in srgb, var(--color-surface) 88%, var(--color-text))',
    edge: 'var(--color-border)',
    radius: 'var(--radius-card)',
    shadow: '0 2px 8px rgb(0 0 0 / 0.08)',
    text: 'var(--color-text)',
    textMuted: 'var(--color-muted)',
  },
  lastMove: {
    ring: 'var(--color-primary)',
  },
};
