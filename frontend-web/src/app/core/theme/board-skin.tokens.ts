/**
 * Shape of a registered board skin's token set.
 *
 * Values are CSS value strings (so `var(--color-*)` is fine alongside literal
 * colors / gradients). CSS in `src/styles/board-skins.css` is what browsers
 * actually paint with — this TypeScript shape mirrors those values so
 * `BoardSkinService` can (a) validate at registration time that every skin
 * declares every key, and (b) enumerate skins for the board-skin-switcher UI.
 */
export interface BoardSkinTokens {
  readonly board: Readonly<{
    /** Background value for the playing surface (color, gradient, or image). */
    bg: string;
    /** Color of the 14 horizontal + 14 vertical grid lines. */
    line: string;
    /** Color of the 5 traditional star points (天元 + 四星). */
    star: string;
    /** Outer border-radius. */
    radius: string;
    /** Outer box-shadow to give the board depth. */
    shadow: string;
  }>;
  readonly stones: Readonly<{
    /** `background` shorthand for black stones. */
    blackFill: string;
    /** `box-shadow` for black stones. */
    blackShadow: string;
    /** `background` shorthand for white stones. */
    whiteFill: string;
    /** Rim color used by whiteShadow's inset ring. */
    whiteRim: string;
    /** `box-shadow` for white stones (usually includes the inset ring). */
    whiteShadow: string;
  }>;
  readonly pieces: Readonly<{
    /**
     * Disc background for a 中国象棋 piece.
     *
     * Xiangqi pieces are discs with a character on them, not stones, so they need
     * their own tokens rather than reusing `stones`.
     */
    bg: string;
    /**
     * Glyph colour of the red side (`Stone.Black` — see games/xiangqi/position.ts).
     *
     * A skin may pick any *shade* that reads on its own surface, but not any hue:
     * a xiangqi board whose red side is not red is broken, in every theme. Hue is
     * the game's identity; shade is the skin's call.
     */
    red: string;
    /** Glyph colour of the black side. Same rule: shade is free, hue is not. */
    black: string;
  }>;
  readonly cards: Readonly<{
    /**
     * `background` shorthand for a card's paper face.
     *
     * Playing cards need their own tokens for the same reason xiangqi pieces did:
     * a card is neither a stone nor a disc. And they need to be *tokens* rather than
     * a set of 54 bitmaps, because a bitmap follows neither the app theme nor the
     * board skin — and this repo's hard rule is that components never hard-code colour.
     */
    face: string;
    /** Border colour of the paper face. */
    faceEdge: string;
    /**
     * Colour of the corner index on a red suit.
     *
     * Same constraint the xiangqi pieces carry: **a skin picks the shade, never the
     * hue.** A card table whose hearts are not red is broken, in every theme — the
     * suit's hue is the game's identity. (The pip artwork itself is a fixed image
     * for that reason; only the index takes a token.)
     */
    red: string;
    /** Colour of the corner index on a black suit. Same rule. */
    black: string;
    /** `background` shorthand for the back of a card (the lattice). */
    back: string;
    /** Border colour of a card back. */
    backEdge: string;
  }>;
  readonly felt: Readonly<{
    /** `background` shorthand for the card table's surface. */
    bg: string;
    /** Border colour around the table. */
    edge: string;
    /** Outer border-radius of the table. */
    radius: string;
    /** Box-shadow giving the table depth (usually with an inset vignette). */
    shadow: string;
    /**
     * Text colour on the table surface.
     *
     * A token rather than a literal because the surface itself is a skin's choice:
     * cream reads on wood's baize and disappears on `classic`, whose felt is mixed
     * from `--color-surface` and is therefore light under a light theme.
     */
    text: string;
    /** Secondary text colour on the table (counts, labels). */
    textMuted: string;
  }>;
  readonly lastMove: Readonly<{
    /** Ring color around the most recent move. */
    ring: string;
  }>;
}
