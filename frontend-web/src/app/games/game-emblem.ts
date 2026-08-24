/**
 * A game's emblem, as data rather than markup.
 *
 * Each game declares a handful of primitives on a **24×24 grid**; the renderer
 * owns the grid, the stroke width and the line caps, and no emblem may specify
 * them. That split is the whole point: **it is the mechanism that makes nine
 * emblems read as one set**, because none of them has the vocabulary to drift.
 * Nine hand-authored `<path>` strings would each be free to pick its own weight
 * and its own optical size, and they would look like nine drawings.
 *
 * It is also far cheaper than paths, which was a surprise worth recording:
 * measured, the nine shape tables total **1274 bytes** — 142 on average, where
 * `card-art.ts` averages 575 bytes for a single suit silhouette. The byte
 * saving was not the reason for the design, but it is why every emblem can
 * afford to be drawn from real geometry instead of one clever glyph.
 *
 * Coordinates are on even numbers wherever a stroke lands, so a 1.6-unit stroke
 * scaled into a 30 px box falls on pixel boundaries instead of blurring across
 * two.
 */

/** Line: from (a,b) to (c,d). */
export interface EmblemLine {
  readonly k: 'l';
  readonly a: number;
  readonly b: number;
  readonly c: number;
  readonly d: number;
}

/** Circle centred at (a,b) with radius c. `f` fills it with the ink colour. */
export interface EmblemCircle {
  readonly k: 'c';
  readonly a: number;
  readonly b: number;
  readonly c: number;
  readonly f?: 1;
}

/** Rect at (a,b), size c×d, corner radius r. `f` fills it. */
export interface EmblemRect {
  readonly k: 'r';
  readonly a: number;
  readonly b: number;
  readonly c: number;
  readonly d: number;
  readonly r?: number;
  readonly f?: 1;
}

/**
 * A glyph centred at (a,b) at font-size c.
 *
 * Used by exactly two games, for the two characters that *are* their identity:
 * 象棋's 帥 and 斗地主's 王.
 *
 * **Size it from the measured box, not from the font-size.** Measured with
 * `getBBox()` rather than estimated, a CJK glyph's box is `width == font-size`
 * exactly but `height ≈ 1.45 × font-size` — the line box, not the ink. The first
 * draft sized both glyphs by their width and shipped two emblems whose glyphs
 * burst their containers: 帥 at 9.5 has a half-diagonal of 8.4 inside a circle
 * of radius 7, and 王 at 9 left 0.5 units of clearance in a 10-wide card. The
 * width was fine in both cases, which is exactly why checking only the width
 * missed it.
 *
 * So: for a glyph inside a circle of radius r, `font-size ≤ r / 0.881`; inside a
 * box, leave margin against `1.45 × font-size` vertically.
 *
 * A second trap, from the same family: a glyph drawn in `currentColor` on top of
 * a **filled** shape is invisible. 猜成语's middle cell was filled.
 *
 * This positioning is not the bet the xiangqi board makes — that draws 帥 in an
 * HTML `<span>` centred by flexbox, which self-corrects for any font. Here the
 * glyph sits in a viewBox, so a different font shifts it. If it ever looks
 * off-centre on a real device, replace these two with `p` paths at a cost of a
 * few hundred bytes each.
 */
export interface EmblemText {
  readonly k: 't';
  readonly a: number;
  readonly b: number;
  readonly c: number;
  readonly s: string;
}

/**
 * Escape hatch for a shape the primitives cannot express.
 *
 * **Currently unused, and that is deliberate information:** all nine emblems
 * are circles, rects, lines and two glyphs, which is what made the shape table
 * the right representation in the first place. If this arm starts collecting
 * call sites, the primitives are the wrong set and that is worth noticing.
 */
export interface EmblemPath {
  readonly k: 'p';
  readonly d: string;
  readonly f?: 1;
}

export type EmblemShape = EmblemLine | EmblemCircle | EmblemRect | EmblemText | EmblemPath;

/** The SVG element and attributes one shape renders to. */
export interface EmblemNode {
  readonly tag: 'line' | 'circle' | 'rect' | 'text' | 'path';
  readonly attrs: Readonly<Record<string, string | number>>;
  readonly text?: string;
}

/**
 * Reached only if a shape kind is added to the union without being handled.
 *
 * The parameter is `never`, so **that omission fails to compile, naming the
 * shape** — the same mechanism `unhandledSoundEvent` uses for sound events. A
 * `switch` that silently fell through would render nothing at all, and an
 * emblem missing one of its lines is not something anyone would notice in a
 * 30 px tile.
 */
function unhandledShape(shape: never): never {
  throw new Error(`Unhandled emblem shape: ${JSON.stringify(shape)}`);
}

const FILL = 'currentColor';
const NONE = 'none';

/** Map one shape to the element the template should draw. */
export function emblemNode(shape: EmblemShape): EmblemNode {
  switch (shape.k) {
    case 'l':
      return { tag: 'line', attrs: { x1: shape.a, y1: shape.b, x2: shape.c, y2: shape.d } };
    case 'c':
      return {
        tag: 'circle',
        attrs: { cx: shape.a, cy: shape.b, r: shape.c, fill: shape.f ? FILL : NONE },
      };
    case 'r':
      return {
        tag: 'rect',
        attrs: {
          x: shape.a,
          y: shape.b,
          width: shape.c,
          height: shape.d,
          rx: shape.r ?? 0,
          fill: shape.f ? FILL : NONE,
        },
      };
    case 't':
      return {
        tag: 'text',
        attrs: {
          x: shape.a,
          y: shape.b,
          'font-size': shape.c,
          'text-anchor': 'middle',
          'dominant-baseline': 'central',
          fill: FILL,
          stroke: NONE,
        },
        text: shape.s,
      };
    case 'p':
      return { tag: 'path', attrs: { d: shape.d, fill: shape.f ? FILL : NONE } };
    default:
      return unhandledShape(shape);
  }
}

/*
 * There is deliberately **no exported list of shape kinds here.**
 *
 * The first draft had one, with a comment claiming it was "derived from the
 * mapper" — it was typed by hand, which is the defect this repo has fixed five
 * times wearing a different hat each time. Nothing needs the list: the compiler
 * enforces the mapper's exhaustiveness through `unhandledShape`, and the walking
 * test iterates the shapes that **actually exist** in `GAME_REGISTRY`.
 */
