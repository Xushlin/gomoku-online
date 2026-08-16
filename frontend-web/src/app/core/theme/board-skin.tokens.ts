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
  readonly lastMove: Readonly<{
    /** Ring color around the most recent move. */
    ring: string;
  }>;
}
