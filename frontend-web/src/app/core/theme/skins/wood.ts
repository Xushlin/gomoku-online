/*
 * Wood skin — tokens mirror src/styles/board-skins.css for the
 * `[data-board-skin='wood']` selector. The CSS is what paints; these
 * literals exist only for BoardSkinService's completeness check and for
 * enumeration in future preview/switcher UIs.
 *
 * Aesthetic reference: Kaya/Shin-Kaya go boards — warm tan surface, dark
 * walnut grid lines, glossy black stones, ivory-cream white stones.
 */
import type { BoardSkinTokens } from '../board-skin.tokens';

export const woodSkin: BoardSkinTokens = {
  board: {
    bg: 'radial-gradient(ellipse at 30% 20%, #e9b57d 0%, #c88a4e 100%)',
    line: 'rgba(50, 25, 8, 0.65)',
    star: 'rgba(50, 25, 8, 0.85)',
    radius: '8px',
    shadow: '0 6px 18px rgb(0 0 0 / 0.22)',
  },
  stones: {
    blackFill: 'radial-gradient(circle at 30% 25%, #4a4a4a, #080808 70%)',
    blackShadow: '0 2px 3px rgb(0 0 0 / 0.5), inset -1px -1px 2px rgb(0 0 0 / 0.4)',
    whiteFill: 'radial-gradient(circle at 30% 25%, #fffdf5, #d7c9a8 90%)',
    whiteRim: 'rgb(120 100 70 / 0.5)',
    whiteShadow: '0 2px 3px rgb(0 0 0 / 0.3), inset 0 0 0 1px rgb(120 100 70 / 0.5)',
  },
  pieces: {
    bg: '#f3e3c0',
    red: '#b3261e',
    black: '#241d16',
  },
  cards: {
    face: 'linear-gradient(160deg, #fffdf6 0%, #f3ead6 100%)',
    faceEdge: 'rgb(120 85 40 / 0.35)',
    red: '#c62828',
    black: '#2b2b2b',
    back: 'linear-gradient(160deg, #8a2420 0%, #5a1512 100%)',
    backEdge: '#e8d3a8',
  },
  felt: {
    bg: 'radial-gradient(ellipse at 50% 35%, #2f7a4a 0%, #1d5333 60%, #123a24 100%)',
    edge: '#6b4423',
    radius: '18px',
    shadow: '0 10px 28px rgb(0 0 0 / 0.28)',
    text: '#f7f2e4',
    textMuted: 'rgb(247 242 228 / 0.72)',
  },
  lastMove: {
    ring: '#ffb000',
  },
};
