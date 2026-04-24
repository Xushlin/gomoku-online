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
    text: string;
    muted: string;
    border: string;
    danger: string;
    success: string;
    warning: string;
  }>;
  readonly radii: Readonly<{ card: string }>;
  readonly shadows: Readonly<{ elevated: string }>;
}

/** A theme is always paired: a light and a dark token set with identical keys. */
export interface ThemeTokens {
  readonly light: ThemeTokenSet;
  readonly dark: ThemeTokenSet;
}
