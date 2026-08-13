## Context

After `rename-to-gewu` the codebase is named for a platform but still ships exactly one game, reachable only through gomoku's own lobby at `/home`. The next change is the idiom vertical, which is a *different category of game* (single-player levels, no room, no hub) and therefore cannot be discovered through a lobby built around rooms and ELO.

Existing patterns this design deliberately copies rather than invents:

- `ThemeService`, `BoardSkinService`, and `SoundService` are all "abstract class as DI token + default implementation over a registry", and adding a theme/skin/pack is already a one-file operation. The game registry is the fourth instance of the same shape, which is what the project's open/closed rule asks for.
- Every non-shell route is lazy via `loadComponent`, with a per-chunk budget.
- Display strings never appear in templates; everything goes through Transloco with both locales in lockstep.

The hard constraint discovered while scoping: `/home` is load-bearing in three live specs (`web-lobby` defines it, `web-game-board` names it 9×, `web-auth` 8×). That single fact shapes the whole design — see D1.

## Goals / Non-Goals

**Goals:**

- A user can see the whole platform — what exists, what is coming, and what category each game is.
- Adding a game is one folder plus one array entry, with no edits to the catalogue page.
- The idiom vertical can land next without touching any gomoku code or route.
- Zero change to any existing route, guard, redirect, component, or i18n key.

**Non-Goals:**

- Moving gomoku off `/home`, or changing the post-login landing page.
- Introducing a `/g/:gameKey` route shell before a game needs one.
- Any backend work. The catalogue is static client-side data.
- Per-game stats, leaderboards, or "continue where you left off" on the cards.

## Decisions

### D1: The catalogue is additive at `/games`; `/home` is left alone

The obvious platform design is "catalogue at `/home`, gomoku at `/g/gomoku`". It is rejected *for now* on cost, not on merit: `/home` appears as a normative destination 17 times across `web-lobby`, `web-game-board`, and `web-auth`, and OpenSpec's MODIFIED operation requires reproducing each affected requirement block in full. That is a large amount of copied spec text whose only product benefit is a nicer URL, and it competes for the same review budget as the idiom vertical.

`generalize-match-contract` must rewrite `web-game-board` and `web-lobby` regardless, because `MakeMove` → `SubmitMove` changes the hub contract those specs pin. The route move is close to free there and expensive here, so it goes there.

The honest cost of deferring: two coexisting conventions — gomoku at `/home`, new games at `/g/<key>`. This is written into the proposal, this design, and `CLAUDE.md` so nobody mistakes it for an accident.

*Alternative considered:* put the catalogue at `/games` permanently and never move `/home`. Rejected — a platform whose front door is one game's lobby is the thing this whole effort is trying to fix; deferring is acceptable, abandoning is not.

### D2: Planned games are manifests, not code

All eight games get a manifest immediately; seven carry `status: 'planned'`. Each later change flips one `status` field and adds a `launchRoute`.

This buys three things: the catalogue shows the platform's shape from the first commit (a single-card catalogue would look broken), the i18n keys for all eight titles land in one reviewable pass instead of eight, and the manifest type gets exercised against eight real cases now — so a wrong field shape surfaces here rather than after four games are built on it.

The cost is eight cards where seven do nothing. Mitigated by making planned cards visibly and semantically inert (D4).

*Alternative considered:* register only gomoku and add manifests as games ship. Rejected — a one-item catalogue communicates nothing, and it would let the manifest shape be validated by a single example.

### D3: `contentLocales` on the manifest, not a hardcoded rule

The idiom games are Chinese-content games: their puzzle data is 成语 and their explanations are Chinese prose, and no amount of UI translation changes that. Rather than special-casing them in the catalogue template, the manifest declares `contentLocales`, and the card shows a "Chinese only" badge whenever the active locale is not in that list.

This keeps the policy declarative and puts the platform in a position to answer the harder question later (hide? warn? offer machine translation?) by changing one component instead of auditing games.

Deliberately **not** decided here: whether an `en` user should be blocked from launching a Chinese-content game. Badge only for now; blocking is a product decision with no evidence behind it yet.

### D4: Planned cards are not links

A disabled card renders as a non-interactive element with `aria-disabled="true"`, not an `<a>` with a dead href and not a `<button>` that swallows clicks. Rationale: a focusable control that does nothing is worse for keyboard and screen-reader users than an element that never claims to be interactive. The "coming soon" label carries the state in text, not in colour alone.

### D5: No loading, empty, or error states

The project's UX rules require real UI for loading / empty / error rather than a bare "loading…". Those states cannot occur here: the registry is a static import, so there is no fetch to fail, the array is never empty (it is a source file), and there is no pagination. Stating this explicitly so a reviewer does not read their absence as an oversight.

### D6: `GameCatalogService` wraps the array even though a bare export would work

Components could import the manifest array directly. Injecting a service instead costs one small class and buys the seam the project's dependency-inversion rule asks for: tests inject a two-game stub instead of asserting against the real eight-game registry, so catalogue specs do not have to change every time a game is added.

## Risks / Trade-offs

- **[Two URL conventions until `generalize-match-contract`]** → Documented in three places (proposal, this design, `CLAUDE.md` roadmap) and owned by a named change. The alternative was a large spec-rewrite PR now.
- **[Seven cards that do nothing invite "is this broken?"]** → Planned cards are labelled and inert (D4), and grouping/ordering puts available games first.
- **[Manifest shape guessed before the puzzle and score categories exist]** → Partly mitigated by writing all eight manifests now (D2). Accepted residual risk: `launchRoute` as a single string may not survive a game that needs a parameterised entry point (e.g. "resume level 7"). That would become `launchRoute` plus an optional resolver, which is an additive change to a type only the registry depends on.
- **[Catalogue and lobby will both look like "the place you start"]** → Real, and it is the cost of D1. The header link is labelled so the catalogue is clearly the platform-level surface.

## Migration Plan

Nothing to migrate: additive files, one new lazy route, one header link, new i18n keys. No backend, no DB, no contract change. Rollback is reverting the commit.

## Open Questions

- **Should an `en` user be able to launch a Chinese-content game?** Badge-only for now (D3). Answer it when there is a real `en` user, not before.
- **Icon strategy.** Manifests currently carry an emoji-ish `icon` string, which is the cheapest thing that renders in both themes. If the platform later wants real artwork, `icon` becomes a component or asset reference — again a type-level change behind the registry.
