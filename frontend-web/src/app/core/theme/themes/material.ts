/*
 * Material theme — token values mirror src/styles/tokens.css for the
 * `[data-theme='material']` selectors. Used only by DefaultThemeService's
 * register() completeness check and by any future preview/enumeration UI —
 * these literals are NEVER applied as styles by TypeScript; CSS does the
 * painting via tokens.css.
 */
import type { ThemeTokens } from '../theme.tokens';

export const materialTokens: ThemeTokens = {
  light: {
    colors: {
      bg: '#fafafa',
      surface: '#ffffff',
      primary: '#1565c0',
      text: '#1a1a1a',
      muted: '#616161',
      border: '#e0e0e0',
      danger: '#c62828',
      success: '#2e7d32',
      warning: '#e65100',
    },
    radii: { card: '12px' },
    shadows: { elevated: '0 4px 12px rgb(0 0 0 / 0.12)' },
  },
  dark: {
    colors: {
      bg: '#121212',
      surface: '#1e1e1e',
      primary: '#90caf9',
      text: '#e8e8e8',
      muted: '#9e9e9e',
      border: '#2a2a2a',
      danger: '#ef9a9a',
      success: '#a5d6a7',
      warning: '#ffb74d',
    },
    radii: { card: '12px' },
    shadows: { elevated: '0 4px 12px rgb(0 0 0 / 0.6)' },
  },
};
