/*
 * System theme — flatter, smaller radius, lighter shadow. Apple/Fluent-ish.
 * See material.ts for the rationale on why token literals live here.
 */
import type { ThemeTokens } from '../theme.tokens';

export const systemTokens: ThemeTokens = {
  light: {
    colors: {
      bg: '#ffffff',
      surface: '#f7f7f8',
      primary: '#0a66d6',
      text: '#111111',
      muted: '#555555',
      border: '#e5e5e5',
      danger: '#b42318',
      success: '#1f7a34',
      warning: '#b45309',
    },
    radii: { card: '6px' },
    shadows: {
      elevated: '0 1px 2px rgb(0 0 0 / 0.06), 0 1px 3px rgb(0 0 0 / 0.08)',
    },
  },
  dark: {
    colors: {
      bg: '#0b0b0d',
      surface: '#151518',
      primary: '#4ea3ff',
      text: '#eaeaea',
      muted: '#9a9a9a',
      border: '#2a2a2e',
      danger: '#fca5a5',
      success: '#86efac',
      warning: '#fbbf24',
    },
    radii: { card: '6px' },
    shadows: {
      elevated: '0 1px 2px rgb(0 0 0 / 0.5), 0 1px 3px rgb(0 0 0 / 0.6)',
    },
  },
};
