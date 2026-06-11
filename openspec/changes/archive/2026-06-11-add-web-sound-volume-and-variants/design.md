# Design: add-web-sound-volume-and-variants

## Context

`DefaultSoundService` already owns a lazy `AudioContext` + a single master `GainNode` fixed at `gain.value = 1` (`core/sound/sound.service.ts`). Mute is implemented as an early return in `play()` — the gain node is never used as the silencing mechanism. Packs (`wood`, `chiptune`) receive `(event, ctx, masterGain)` and connect their short-lived graphs to the master gain, so a volume applied there reaches every pack with zero pack changes.

`DefaultBoardSkinService` (`core/theme/board-skin.service.ts`) registers `wood` + `classic`; painting is pure CSS keyed off `<html data-board-skin>` (`src/styles/board-skins.css`). The TS token files exist only for the completeness check and future preview UIs.

The header (`shell/header/header.{ts,html}`) already renders one CDK menu per registry, enumerating `available*()` — new registry entries appear automatically, only needing an i18n label key.

## Goals / Non-Goals

**Goals:**

- Continuous volume control (0–100) that affects every pack uniformly, persisted, defaulting to today's behaviour (100).
- One new dark-friendly board skin (`midnight`) and one new quiet sound pack (`minimal`), each added as drop-one-file changes.
- A live spec home for the board-skin registry (`web-board-skins` capability) — closing pre-existing drift while we're touching the area.

**Non-Goals:**

- Per-event or per-pack volume balance tuning.
- Replacing the mute toggle (it stays an independent boolean).
- Volume UI anywhere other than the header sound menu (no in-game slider).
- Backend or persistence-format changes beyond one new localStorage key.

## Decisions

### D1 — Volume rides the existing master `GainNode`; mute stays an early return

`setVolume(v)` clamps to `[0, 100]`, stores the raw integer, and (if the context exists) sets `masterGain.gain.value` to the mapped gain. `ensureContext()` initialises the gain from the stored volume instead of the literal `1`. Mute keeps its current early-return-in-`play()` semantics — the two states never write to the same field, so unmuting trivially restores the previous volume and there is no "saved previous volume" bookkeeping.

*Alternative considered:* modelling mute as `volume = 0` with a remembered restore value — rejected: two flags pretending to be one state breeds edge cases (mute, change volume, unmute → which value wins?), and it would change the persisted meaning of the existing `gomoku:sound-muted` key.

### D2 — Perceptual (squared) gain curve

`gain = (volume / 100)²`. Human loudness perception is roughly logarithmic; a linear slider mapped linearly to amplitude crams all audible change into the top quarter of the slider. The squared curve is the standard cheap approximation — one expression, no tables. The *stored* value stays the raw 0–100 integer so the curve can be revisited without a migration.

### D3 — `volume === 0` early-returns in `play()` like mute

At gain 0 the graph would be inaudible anyway; skipping construction avoids pointless `AudioContext` creation on a fresh page whose stored volume is 0 (same rationale as the existing mute early return, and it keeps "slider at zero" honest as a de-facto mute).

### D4 — Slider lives inside the existing sound-pack CDK menu

A plain (non-`cdkMenuItem`) row containing a native `input[type=range]` is appended below the pack options. CDK only auto-closes the menu on `cdkMenuItem` activation, so dragging the slider keeps the menu open; native range inputs are keyboard-operable (arrow keys) and screen-reader-announced for free. On release (`change` event, not `input`) a `move-place` sample plays so the user hears the new level — same audition pattern `selectSoundPack` already uses.

*Alternative considered:* a separate header popover for volume — rejected: the header is already seven controls wide at 375 px; co-locating volume with the rest of the sound settings is also where users will look for it.

### D5 — `midnight` skin is self-contained dark, not theme-reactive

Like `wood` (and unlike `classic`), `midnight` uses literal colour values in its CSS block: near-black slate surface, faint cool-grey grid, glossy stones with stronger rim contrast so black stones stay legible on a dark board (brighter specular highlight + lighter outer rim are mandatory there). It looks the same under both light and dark app themes — it is *for* people who want a dark board, not a mirror of the theme axis. A `.dark` override block is therefore unnecessary; one block suffices.

*Alternative considered:* a theme-reactive skin via `var(--color-*)` like `classic` — rejected: that's what `classic` already is; the gap in the lineup is a deliberately dark standalone skin.

### D6 — `minimal` pack: sine-only, short, peak gain ≈ 50% of wood's

All five events synthesise from plain sine oscillators with fast envelopes (≤ 400 ms total, most ≤ 80 ms) at roughly half the peak gain of the wood pack — "quiet" is the pack's identity, distinct from wood (noise/timbre) and chiptune (square/triangle). Win/lose/draw stay recognisable but understated (two-note rises/falls instead of arpeggios). Same hard rules as existing packs: synchronous, no external resources, every node `stop(when)`-scheduled, never throws.

### D7 — New `web-board-skins` capability instead of stretching `web-theming`

The registry contract and built-in skins get their own spec because (a) `web-theming` is about the app-wide token/theme system, board skins are a parallel registry with a different consumer surface, and (b) the header switcher requirement already in `web-shell` stays where it is — `web-board-skins` owns the service + skins, `web-shell` owns the menu. The new spec documents shipped behaviour (registry, wood, classic) as plain requirements and `midnight` arrives as part of the same spec — being a new capability file, the whole spec is ADDED in this change's delta.

## Risks / Trade-offs

- [Squared curve makes low slider values very quiet] → Acceptable and intended; 0 is explicit silence, and the curve matches perceived loudness. Stored raw value keeps the curve swappable later.
- [Native `input[type=range]` styling varies across browsers] → Style track/thumb via the existing token variables (`accent-color: var(--color-primary)` covers modern browsers cheaply); functional behaviour is identical everywhere.
- [A slider inside a CDK menu is unusual] → CDK menus tolerate non-menu-item content; the row is skipped by typeahead/arrow item navigation but the input itself is in the tab order. Verified pattern in CDK docs (menu content is not restricted to `cdkMenuItem`).
- [Retro-speccing board skins inflates the change] → The registry spec is short (one service contract + three skin requirements) and prevents a second `fix-spec-*-drift` change later; net cheaper.
- [Black stones on a dark board can lose legibility] → Explicit requirement in the spec: midnight's black-stone fill must carry a visibly brighter specular highlight and lighter rim than wood's; manual check at 375 px in both app themes is a task item.

## Migration Plan

Pure additive frontend change. Fresh clients default to volume 100 (today's loudness); absent/garbage localStorage values fall back to 100. No data migration, no rollback steps beyond reverting the commit.

## Open Questions

None — all decisions above are settled unless review says otherwise.
