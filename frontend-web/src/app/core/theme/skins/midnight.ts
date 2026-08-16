/*
 * Midnight skin — tokens mirror src/styles/board-skins.css for the
 * `[data-board-skin='midnight']` selector. The CSS is what paints; these
 * literals exist only for BoardSkinService's completeness check and for
 * enumeration in future preview/switcher UIs.
 *
 * Aesthetic reference: dark slate stone slab — cool near-black surface
 * (never pure #000), pale cool-grey grid, black stones with a deliberately
 * bright specular highlight + light rim so they stay legible on the dark
 * slab, cyan last-move ring (saturated but not red — red reads as "danger").
 */
import type { BoardSkinTokens } from '../board-skin.tokens';

export const midnightSkin: BoardSkinTokens = {
  board: {
    bg: 'radial-gradient(ellipse at 28% 22%, #2a3342 0%, #20272f 55%, #161b22 100%)',
    line: 'rgba(148, 163, 184, 0.4)',
    star: 'rgba(148, 163, 184, 0.65)',
    radius: '10px',
    shadow: '0 10px 28px rgb(0 0 0 / 0.5)',
  },
  stones: {
    blackFill: 'radial-gradient(circle at 32% 26%, #9aa7b8, #0c0f14 80%)',
    blackShadow: '0 2px 4px rgb(0 0 0 / 0.6), inset 0 0 0 1px rgb(148 163 184 / 0.35)',
    whiteFill: 'radial-gradient(circle at 32% 26%, #ffffff, #aeb9c8 90%)',
    whiteRim: 'rgb(90 105 125 / 0.55)',
    whiteShadow: '0 2px 4px rgb(0 0 0 / 0.5), inset 0 0 0 0.5px rgb(90 105 125 / 0.55)',
  },
  pieces: {
    // A slate disc, not ivory — an ivory piece on this surface reads as a hole.
    // The black side goes light for the same reason it does in `stones`: a dark
    // glyph on a dark disc on a dark slab disappears.
    bg: '#2b3442',
    red: '#ff7a6b',
    black: '#ccd6e4',
  },
  lastMove: {
    ring: '#22d3ee',
  },
};
