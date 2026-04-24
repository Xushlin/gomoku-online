## Context

Backend is MVP-complete. `frontend-web/` is an empty directory. CLAUDE.md already made the heavy framework decisions (Angular 21 standalone, Tailwind, Material + CDK, Transloco, Signals-first, Vitest), so this design focuses on *how* we wire them together so the four follow-up changes can move without re-litigating architecture.

The sharp edges are three:

1. **Theming** has to support a 2×2 matrix (`material` | `system`) × (light | dark) on day one, with the open-closed property that adding a new theme is "one token file + one registry line" — not a refactor.
2. **Dark mode** must work from the first commit. If we let any component commit hardcoded colors (`bg-gray-900`, `#fff`), the dark-mode bug surface grows linearly with every follow-up PR.
3. **i18n** has to be runtime-switchable, and templates MUST NOT contain raw Chinese or English strings — otherwise `add-web-*` PRs will silently reintroduce untranslatable UI.

None of these are hard individually; the discipline is in making them *impossible to bypass accidentally* in later PRs. That's what this design is buying.

## Goals / Non-Goals

**Goals:**
- Bootstrap `frontend-web/` as an Angular 21 workspace that builds, runs, tests, and lints out of the box.
- Ship a working app shell (header + placeholder home route) that demonstrates all four cross-cutting features: routing, theming, dark-mode toggle, language switching.
- Establish a CSS-variable token contract such that every downstream component can style itself correctly in any theme/mode without extra work.
- Establish extension points: a new theme = one token file + one `register()` call; a new locale = one JSON + one line in `LanguageService.supported`.
- Provide starter Vitest tests for the two cross-cutting services so the test pipeline is proven end-to-end.

**Non-Goals:**
- Any business UI (login, register, lobby, board, replay, profile). Those are separate changes.
- SignalR client wiring — connection lifecycle belongs to `add-web-game-board`.
- HTTP interceptors for auth tokens — belongs to `add-web-auth-pages`.
- NgRx or any global store — per CLAUDE.md, Signals first; NgRx only if a future flow genuinely needs it.
- CI pipeline configuration — out of scope, tracked as a follow-up chore.
- Desktop (Electron) and Mobile (Flutter) — phases 2 and 3.

## Decisions

### D1. Workspace layout and initial routing

**Decision:** Single Angular project (`frontend-web/`), standalone components only, no NgModules. Folder shape:

```
frontend-web/
  src/
    app/
      app.config.ts              # ApplicationConfig, provideRouter, provideTransloco, etc.
      app.routes.ts              # one eager 'home' route + catch-all; all future routes lazy
      app.ts                     # root standalone component <router-outlet/>
      core/
        theme/
          theme.service.ts       # ThemeService (Signal-backed registry)
          theme.tokens.ts        # typed token shape (TypeScript interface)
          themes/
            material.light.ts
            material.dark.ts
            system.light.ts
            system.dark.ts
        i18n/
          language.service.ts    # LanguageService (Signal-backed)
          transloco-root.config.ts
      shell/
        shell.ts                 # layout: <header/> + <router-outlet/>
        header/
          header.ts              # language switcher, theme switcher, dark toggle
      pages/
        home/
          home.ts                # placeholder showing i18n + theme working
    assets/
      i18n/
        zh-CN.json
        en.json
    styles/
      tokens.css                 # [data-theme="material"] { --color-primary: ... } etc.
      tailwind.css               # @tailwind base/components/utilities + @apply utilities
      global.css                 # focus-visible, prefers-reduced-motion, etc.
```

**Rationale:**
- Flat `core/theme/` + `core/i18n/` keeps cross-cutting concerns out of `shell/` and `pages/`, which should stay thin.
- `pages/home/` exists only to prove the pipeline. `add-web-auth-pages` will add `pages/login/` etc., all lazy-loaded.
- `theme.tokens.ts` defines the TypeScript shape; `themes/*.ts` are plain objects implementing that shape. This gives compile-time safety when we add a new theme.

**Alternatives considered:**
- Feature-folder layout (`features/home/`, `features/login/`). Rejected for now: business features don't exist yet; once `add-web-auth-pages` lands we'll likely move `home` under `features/` or `pages/` — either naming is fine, just consistent. We pick `pages/` because it's shorter and matches "route-level" intent; if the team prefers `features/` in review, rename is trivial.

### D2. Theme architecture

**Decision:** Theming is a two-axis system expressed via CSS variables on `<html>`.

- Axis A — theme name: `data-theme="material" | "system"`. Different radius, shadow, font-family tokens per theme.
- Axis B — light/dark: presence of `.dark` class on `<html>`. Swaps color tokens within the selected theme.

Tokens live in `src/styles/tokens.css`, authored as:

```css
:root,
[data-theme="material"] { --color-primary: #1976d2; --radius-card: 12px; --shadow-elevated: 0 4px 12px rgb(0 0 0 / 0.12); ... }
[data-theme="material"].dark { --color-bg: #0f1115; --color-surface: #1a1d23; --color-text: #e6e8eb; ... }
[data-theme="system"] { --color-primary: #0a84ff; --radius-card: 6px; --shadow-elevated: 0 1px 2px rgb(0 0 0 / 0.08); ... }
[data-theme="system"].dark { ... }
```

`ThemeService`:

- API: `register(name: string, tokens: ThemeTokens)`, `activate(name: string)`, `setDark(isDark: boolean)`, readonly signals `themeName()` and `isDark()`.
- On `activate`: sets `document.documentElement.dataset.theme = name`, persists to `localStorage['gomoku:theme']`.
- On `setDark`: toggles `.dark` class on `<html>`, persists to `localStorage['gomoku:dark']`.
- Initial resolution order: `localStorage → prefers-color-scheme for dark → 'material' & system pref for dark`.
- TypeScript-side `register()` isn't what paints pixels (CSS does). It exists to (a) validate token completeness at runtime in dev, (b) enumerate available themes for the theme switcher UI. This keeps TypeScript and CSS in sync without generating CSS from TS.

**Rationale:**
- CSS variables survive across Angular Material's Emotion/Sass output, CDK overlays (which render outside the component tree), and Tailwind's `dark:` variant.
- Two-axis model matches the CLAUDE.md mandate exactly: "Dark/Light 是主题的正交维度".
- Keeping tokens in CSS (not TS-generated) means the browser paints them without a JS round-trip and avoids FOUC on first paint.

**Alternatives considered:**
- **Emit CSS from TypeScript token objects at runtime (`document.documentElement.style.setProperty`).** Rejected: paint-blocking on every theme switch, fights Material's Sass, and creates a hot FOUC window on first load.
- **Angular Material's `mat.theme` mixins only.** Rejected: Material themes don't extend cleanly to a non-Material "system" theme, and we'd lose the one-registry-line extension point.
- **CSS `color-scheme` only (no `.dark` class).** Rejected: Tailwind's `dark:` variant needs either the class or a `media` strategy; class strategy wins because users can override OS pref.

### D3. i18n architecture

**Decision:** Transloco, file-based JSON translations, Signal-wrapped language state.

- Translation files at `src/assets/i18n/<locale>.json` — flat objects with dot-path keys (`room.join.button`, `header.language.label`). Transloco's scoped translations are not used at the scaffold stage; we'll revisit if any feature gets a huge translation surface.
- `LanguageService`:
  - API: readonly signal `current()`, method `use(locale: SupportedLocale)`, static `supported: readonly SupportedLocale[]`.
  - Initial value: `localStorage['gomoku:lang'] → navigator.language (if supported) → 'en'`.
  - On `use`: calls `TranslocoService.setActiveLang`, persists, updates signal.
- Transloco fallback language: `en`. Missing-key handler in dev logs a warning; in prod returns the key so it surfaces visibly.
- Lint/PR rule (enforced in review, not automatically): templates MUST NOT contain Han characters or standalone English words outside of `{{ '...' | translate }}` or `[attr]="'...' | translate"`. We don't add a custom ESLint rule in this change — a code-review checklist item is enough initially.

**Rationale:**
- Transloco is already the documented choice in CLAUDE.md.
- Flat keys with dot paths keep the JSON diff-friendly and easy to grep.
- Supported-locale list in one file means `add-locale: ru` is a 2-line PR.

**Alternatives considered:**
- **Angular's built-in `$localize` / `i18n` attribute.** Rejected: swapping locales at runtime requires reloading the app (or complex workarounds), which breaks the "user toggles language in the header" UX we want.
- **ngx-translate.** Transloco is its spiritual successor from the same author, better TypeScript story, and explicitly mentioned in CLAUDE.md.

### D4. Dependency Injection for extensibility

**Decision:** `ThemeService` and `LanguageService` are concrete classes today, but we expose them via `abstract` DI tokens so tests and future replacements can swap implementations without changing callers.

```ts
// core/theme/theme.service.ts
export abstract class ThemeService {
  abstract readonly themeName: Signal<string>;
  abstract readonly isDark: Signal<boolean>;
  abstract register(name: string, tokens: ThemeTokens): void;
  abstract activate(name: string): void;
  abstract setDark(isDark: boolean): void;
}

export class DefaultThemeService extends ThemeService { /* impl */ }

// app.config.ts
providers: [
  { provide: ThemeService, useClass: DefaultThemeService },
]
```

Consumers `inject(ThemeService)`. Tests provide a stub via `{ provide: ThemeService, useValue: stub }`.

**Rationale:** This is exactly the CLAUDE.md rule: "Service 有可能有替代实现时,用抽象类作为 DI token, inject by token 不 by concrete." Cost is low (one extra class declaration), payoff is large (clean tests, future-proofing).

### D5. Tailwind × CSS variables interplay

**Decision:** Tailwind is configured so its color utilities map to CSS variables:

```js
// tailwind.config.js
theme: {
  extend: {
    colors: {
      bg:       'var(--color-bg)',
      surface:  'var(--color-surface)',
      primary:  'var(--color-primary)',
      text:     'var(--color-text)',
      muted:    'var(--color-text-muted)',
      border:   'var(--color-border)',
      // danger / success / warning similarly
    },
    borderRadius: {
      card: 'var(--radius-card)',
    },
    boxShadow: {
      elevated: 'var(--shadow-elevated)',
    },
  },
},
darkMode: 'class',
```

So components write `class="bg-bg text-text rounded-card shadow-elevated"` — never `class="bg-gray-900"`. The `dark:` variant is available for the rare cases where a component genuinely needs a different layout in dark mode, not just different colors.

**Rationale:** This makes the "no hardcoded colors" rule *enforceable by greppability* — searching the repo for `bg-gray`, `bg-white`, `text-black`, hex colors in class attributes is trivial, and any hit is a bug.

### D6. Testing framework

**Decision:** **Vitest** via `@analogjs/vitest-angular`. Starter tests cover:
- `ThemeService`: `register` adds a theme; `activate` sets `data-theme` + persists; `setDark` toggles `.dark` class; initial resolution picks `localStorage` before OS pref.
- `LanguageService`: `use` sets Transloco active lang + persists; initial resolution order (`localStorage > navigator.language > 'en'`).
- Snapshot/smoke: `Home` renders the translated title for the active locale.

**Rationale:** CLAUDE.md mandates Vitest. `@analogjs/vitest-angular` is the best-supported integration right now; Angular 21's own experimental Vitest builder is an acceptable fallback if Analog has issues.

### D7. Scripts

`package.json` scripts (exactly these, no aliases):

```json
{
  "start":       "ng serve",
  "build":       "ng build",
  "test":        "vitest",
  "test:ci":     "vitest run",
  "lint":        "ng lint"
}
```

CLAUDE.md's "Common commands" section references `npm test -- --run` — that continues to work via Vitest's CLI flags. `test:ci` is an explicit alias for CI clarity.

### D8. What we deliberately don't build

- No `HttpClient` usage, no `ApiService`, no interceptors. First consumer is `add-web-auth-pages`; building an HTTP skeleton with no routes to call is over-engineering.
- No SignalR wiring. First consumer is `add-web-game-board`.
- No route guards. First consumer is `add-web-auth-pages` (auth guard).
- No NgRx. First business flow will decide if it's needed.

## Risks / Trade-offs

- **Risk: Angular Material's own theming fights our CSS variables.** → Mitigation: use Material's `mat.theme` tokens with CSS variables as inputs where Material supports it (Material 17+ has a CSS-variables API via `mat.theme` in M3); for components that still bake colors, scope those to the `material` theme only and accept that `system` theme uses a narrower Material subset at first. Revisit in `add-web-game-board` if we hit friction.
- **Risk: Transloco JSON files will sprawl as features land.** → Mitigation: if `en.json` exceeds ~300 keys, switch to Transloco scoped translations per feature folder. Not now.
- **Risk: `prefers-color-scheme` detection vs. persisted choice can deadlock (user sets dark, OS flips light, confusion).** → Mitigation: persisted choice always wins. `prefers-color-scheme` is only consulted when no persisted choice exists.
- **Risk: Vitest + Angular integration still has rough edges (SSR-specific APIs, `ResizeObserver`).** → Mitigation: Keep tests at unit-of-logic level for now (services, not component harnesses); if `@analogjs/vitest-angular` proves flaky, fall back to Angular's Vitest builder. The two cross-cutting services we test here are pure logic with minimal DOM.
- **Trade-off: no CI in this change.** → Accepted: wiring CI is a cross-cutting chore; doing it here bloats the PR. Follow-up tracked in proposal "Impact" section.
- **Trade-off: component-library (Material) + utility CSS (Tailwind) together.** → Accepted. Boundary: Material for complex widgets (dialog, menu, form-field), Tailwind for layout and spacing. If a Material component's look clashes with our tokens, we override via CSS variables, not deep Sass.

## Migration Plan

N/A — new workspace, no existing frontend to migrate from. Rollback is `rm -rf frontend-web/` + revert the commit.

## Open Questions

- None blocking implementation. One aesthetic question for review: is `pages/` or `features/` the preferred route-folder name? Either works; default is `pages/` and we rename in `add-web-auth-pages` review if the team disagrees.
