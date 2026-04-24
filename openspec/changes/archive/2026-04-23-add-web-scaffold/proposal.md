## Why

The backend is feature-complete for an MVP (auth, rooms, gameplay, AI, ELO, replay, presence, observability, rate limiting), but `frontend-web/` is still empty. Before we can build any user-facing page we need a solid Angular shell with dark-mode-capable theming, runtime i18n, and the CDK/Material/Tailwind foundation that every later page will lean on. This change lays that scaffold exactly once so business pages (`add-web-auth-pages`, `add-web-lobby`, `add-web-game-board`, `add-web-replay-and-profile`) can plug in without re-solving cross-cutting concerns.

Scaffolding it as its own change — with no business pages — keeps this PR small, keeps the architectural decisions reviewable in isolation, and means each subsequent web change can focus purely on its own vertical.

## What Changes

- Create `frontend-web/` as a fresh Angular 21 workspace (standalone components, strict TypeScript, kebab-case filenames, Signals-first state).
- Add Tailwind CSS with a CSS-variables-based color system (no hardcoded color classes like `bg-gray-900` / `text-white`); Tailwind's `dark:` variant is the toggle.
- Integrate Angular Material + `@angular/cdk` so later work uses CDK overlays/dialogs (not hand-rolled modals).
- Integrate **Transloco** for runtime i18n with initial locales `zh-CN` and `en`; all template text flows through the `translate` pipe (no hardcoded strings in templates).
- Introduce `LanguageService` (Signal-backed) — persists to `localStorage`, initial value resolved via `localStorage → navigator.language → en`. Adding a new locale = add `i18n/<locale>.json` + one line in `LanguageService.supported`.
- Introduce `ThemeService` (Signal-backed) with a theme **registry** pattern: `ThemeService.register(name, tokens)` accepts CSS-variable token sets. Ship two themes out of the box: `material` (Material rounding/shadows) and `system` (Apple/Fluent-ish, flatter). Dark/light is an orthogonal axis — each theme defines both token sets; `themeName` and `isDark` are independent Signals. Theme switching sets `data-theme="<name>"` + `.dark` class on `<html>` and persists to `localStorage`.
- Replace any hardcoded design tokens with CSS variables (`--color-bg`, `--color-primary`, `--color-surface`, `--radius-card`, `--shadow-elevated`, …). Components reference `var(--color-primary)`, never `#2962FF`.
- App shell: header with language switcher, theme switcher, light/dark toggle. No business pages — just a placeholder `home` route confirming routing + theming + i18n work.
- Router: only the shell + home route are eager. Every future route **must** use `loadComponent` / `loadChildren` (documented as a rule in the spec).
- Responsive baseline: shell must render usable at 375px mobile-first and scale up via Tailwind breakpoints; `prefers-reduced-motion` and `focus-visible` styles enabled globally.
- Test tooling: **Vitest** (not Karma/Jasmine) wired via Angular's Vitest builder or `@analogjs/vitest-angular`; starter unit tests for `LanguageService` and `ThemeService`.
- `package.json` scripts: `start`, `build`, `test`, `test -- --run`, `lint`.

Out of scope (explicitly deferred to later changes):
- Login / register / change-password pages — `add-web-auth-pages`
- Lobby / room list / leaderboard — `add-web-lobby`
- Board + SignalR gameplay — `add-web-game-board`
- Replay & profile — `add-web-replay-and-profile`

## Capabilities

### New Capabilities
- `web-shell`: Angular app bootstrap, root routing contract, lazy-loading rule for non-shell routes, global responsive + a11y baseline, HTTP layer convention (all HTTP lives in `services/api/`, not in components), Container vs. Presentational component separation rule.
- `web-theming`: `ThemeService` registry API, CSS-variable token contract that components must honor, shipped `material` + `system` themes each with light/dark token sets, `themeName` + `isDark` orthogonal persistence, extension rule ("add theme = add token file + one registry line, don't edit components").
- `web-i18n`: Transloco configuration, `LanguageService` API and persistence, required initial locales `zh-CN` + `en`, translation-key conventions (dot-path, flat keys), rule that templates MUST NOT hardcode user-visible strings, extension rule ("add locale = add JSON + one registry line").

### Modified Capabilities
<!-- None — no existing frontend specs to modify. -->

## Impact

- **New workspace**: `frontend-web/` — Angular 21 project, Tailwind, Material, CDK, Transloco, Vitest.
- **New services** (under `frontend-web/src/app/core/`): `ThemeService`, `LanguageService`; abstract-class DI tokens where mocking is plausible.
- **New top-level folders**: `src/app/core/` (cross-cutting services), `src/app/shell/` (header + layout), `src/assets/i18n/` (`zh-CN.json`, `en.json`), `src/styles/themes/` (CSS-variable token files per theme × dark/light).
- **No backend impact** — this change is frontend-only; backend specs remain untouched.
- **Dependencies added**: `@angular/material`, `@angular/cdk`, `tailwindcss` + `postcss` + `autoprefixer`, `@jsverse/transloco`, `vitest` + Angular Vitest integration.
- **CI note**: CI will need to `npm ci && npm run lint && npm run test -- --run && npm run build` under `frontend-web/`; wiring CI itself is out of scope for this change but noted so it can be added in a follow-up chore.
- **Future constraint**: every subsequent web change is expected to (a) route-level lazy-load, (b) consume CSS variables not raw colors, (c) `translate` every user-visible string, (d) use CDK/Material for dialogs/overlays. These rules become enforceable via this scaffold.
