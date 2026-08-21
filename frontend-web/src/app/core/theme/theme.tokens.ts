/**
 * The neutral values a theme uses to say "paint no decoration", and the record
 * of why two of them are not what you would guess.
 *
 * **This file is a production source, not documentation.** `scripts/check-styles.mjs`
 * parses `NEUTRAL_DECORATION` out of it and asserts that the themes which
 * predate the decoration layer still use exactly these values. Renaming a field
 * here changes what that check looks for.
 *
 * It used to also hold a `ThemeTokens` interface and a mirror of every theme's
 * values in TypeScript, checked for completeness at `register()` time. Those
 * were deleted by `drop-theme-token-mirrors`: they cost 4.88 kB of the initial
 * bundle (measured by stubbing) and guarded the **copy** rather than the source
 * — a theme whose TS mirror was complete while its CSS block was missing a
 * token compiled fine and painted wrong. Completeness now lives where the
 * values live: `check-styles.mjs` derives the required token list from the
 * `@theme` block in `tailwind.css` and the theme list from the `[data-theme]`
 * selectors in `tokens.css`, so neither list can fall behind the thing it
 * describes, and a gap fails CI rather than warning at runtime.
 *
 * **Adding a theme is therefore two edits:** a `[data-theme='<name>']` pair in
 * `tokens.css`, and one `this.register('<name>')` line in `DefaultThemeService`.
 * No TypeScript object, and no component may be touched.
 */
export const NEUTRAL_DECORATION = {
  surfaces: { image: 'none', edge: 'var(--color-border)', edgeWidth: '1px' },
  controls: { image: 'none', edge: 'var(--color-primary)', edgeWidth: '0px' },
  grounds: { image: 'none' },
  shadows: { raised: '0 0 #0000', inset: '0 0 #0000' },
} as const;
/*
 * Two of these look wrong at a glance and are deliberate. Both were bugs first,
 * and both would have shipped a visible change under a claim of zero change.
 *
 * `surfaces.edge` is `var(--color-border)`, **not** `transparent`. The role
 * utilities set a surface's top border from it so a theme can put a lit bevel
 * there; neutral therefore has to mean "the same border as the other three
 * sides", and `transparent` would make every panel's top edge *disappear*.
 *
 * The shadows are `0 0 #0000`, **not** `none`. `panel` composes
 * `box-shadow: var(--shadow-elevated), var(--shadow-raised)`, and `none` is not
 * a legal member of a shadow list — it invalidates the whole declaration, so
 * every panel would lose its existing shadow too. A zero-size transparent
 * shadow is a legal member that paints nothing.
 *
 * `--radius-control` has no entry here because it has no absolute neutral: it
 * is neutral when it **equals `--radius-card`**, which is what every control
 * used before the token existed. `check-styles.mjs` checks that as a relation.
 *
 * There is no `accents` entry any more. `--accent` / `--accent-image` were added
 * by `extend-theme-tokens` and **no role utility ever read them** — the third
 * zero-call-site token in this area, after `--radius-pill` (proposed, declined)
 * and `controlRadiusIsNeutral` (written, never called). A token every theme must
 * fill and nothing consumes is pure cost, and the rule that killed `pill` has to
 * apply to the ones I wrote too.
 */
