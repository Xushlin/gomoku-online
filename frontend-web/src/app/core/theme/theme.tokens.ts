/**
 * Shape of a registered theme's token set.
 *
 * The actual CSS values live in `src/styles/tokens.css` and are what browsers
 * paint with. This TypeScript shape mirrors those values so `ThemeService`
 * can (a) validate at registration time that every theme declares every key,
 * and (b) enumerate themes for the theme-switcher UI.
 */
export interface ThemeTokenSet {
  readonly colors: Readonly<{
    bg: string;
    surface: string;
    primary: string;
    /**
     * Foreground for anything filled with `primary`.
     *
     * Neutral is the literal `#ffffff` the templates used before this token, in
     * **both** modes — including dark, where `--color-primary` is a pale blue and
     * white on it measures about 1.9:1. That is a real pre-existing contrast bug,
     * and it is preserved here on purpose: this change's whole acceptance
     * criterion is that nothing paints differently, so fixing it belongs to a
     * change that is allowed to alter appearance.
     */
    onPrimary: string;
    text: string;
    muted: string;
    border: string;
    danger: string;
    success: string;
    warning: string;
  }>;
  readonly radii: Readonly<{ card: string; control: string }>;
  readonly shadows: Readonly<{ elevated: string; raised: string; inset: string }>;

  /**
   * The decoration layer — what makes a surface look like a physical plate
   * rather than a flat rectangle.
   *
   * These exist because **a colour cannot be a gradient.** `--color-surface`
   * can hold `#ffffff`; it cannot hold `linear-gradient(…)`. Every theme that
   * wants depth, a bevel or a texture needs somewhere else to put it, and
   * `board-skins.css` already proved the shape works — `--board-bg-image` and
   * `--felt-edge` have been doing exactly this inside the board for three
   * skins. This is that vocabulary, applied outside the board.
   *
   * **Required, not optional.** Every theme declares every field in both
   * modes. An optional decoration layer would let a theme half-implement, and
   * a half-implemented theme paints a visible hole with nothing going red —
   * the same reason a board skin that omits a token fails to compile.
   *
   * A theme that wants no decoration says so explicitly, using
   * `NEUTRAL_DECORATION` below — and `check-styles.mjs` asserts that the three
   * themes which predate this layer use exactly those values. Do not guess the
   * neutrals from their names; two of them are not what you would expect, and
   * the comment on that constant says why.
   */
  readonly surfaces: Readonly<{ image: string; edge: string; edgeWidth: string }>;
  readonly controls: Readonly<{ image: string; edge: string; edgeWidth: string }>;
  readonly accents: Readonly<{ color: string; image: string }>;
  readonly grounds: Readonly<{ image: string }>;
}

/**
 * The values that mean "paint nothing extra".
 *
 * A theme using all of these renders identically to how it rendered before the
 * decoration layer existed — which is what makes "we added, we did not change"
 * a checkable claim rather than an assurance.
 */
export const NEUTRAL_DECORATION = {
  surfaces: { image: 'none', edge: 'var(--color-border)', edgeWidth: '1px' },
  controls: { image: 'none', edge: 'var(--color-primary)', edgeWidth: '0px' },
  accents: { color: 'var(--color-primary)', image: 'none' },
  grounds: { image: 'none' },
  shadows: { raised: '0 0 #0000', inset: '0 0 #0000' },
} as const;
/*
 * Two of these look wrong at a glance and are deliberate. Both were bugs first.
 *
 * `surfaces.edge` is `var(--color-border)`, **not** `transparent`. The role
 * utilities set the top border from it so a theme can put a lit bevel there;
 * neutral therefore has to mean "the same border as the other three sides",
 * and `transparent` would make every panel's top edge *disappear* — a visible
 * change shipped under a claim of zero visual change.
 *
 * The shadows are `0 0 #0000`, **not** `none`. `panel` composes
 * `box-shadow: var(--shadow-elevated), var(--shadow-raised)`, and `none` is not
 * a legal member of a shadow list — it invalidates the whole declaration, so
 * every panel would lose its existing shadow too. A zero-size transparent
 * shadow is a legal member that paints nothing.
 */

/**
 * `radii.control` has no absolute neutral — it is neutral when it **equals
 * `radii.card`**, because that is what every control used before there was a
 * separate token. So it is checked as a relation, not against a literal.
 */
export const controlRadiusIsNeutral = (set: ThemeTokenSet): boolean =>
  set.radii.control === set.radii.card;

/** A theme is always paired: a light and a dark token set with identical keys. */
export interface ThemeTokens {
  readonly light: ThemeTokenSet;
  readonly dark: ThemeTokenSet;
}
