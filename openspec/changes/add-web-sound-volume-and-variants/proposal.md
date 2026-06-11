# Proposal: add-web-sound-volume-and-variants

## Why

The sound layer is binary today — on or off. Players who want quieter feedback (office, late night) have no middle ground, and the README already promises a volume slider as a next step. At the same time the skin/pack registries exist precisely so new variants are cheap; shipping one new board skin and one new sound pack exercises that open/closed promise end-to-end and gives players meaningful choice (especially a dark-friendly board skin — both current skins are light-surface oriented).

## What Changes

- **Volume control** — `SoundService` gains a `volume: Signal<number>` (0–100, default 100) and `setVolume(volume: number)`, persisted to `localStorage` (`gomoku:sound-volume`). The master `GainNode` applies a perceptual (squared) curve; volume 0 is fully silent and `play()` early-returns just like mute. Mute stays an independent toggle — unmuting restores the previous volume.
- **Volume slider UI** — the existing sound-pack CDK menu in the header gains a volume slider row (native `input[type=range]`, doesn't close the menu, keyboard-operable). New `header.sound.volume` i18n key in both locales.
- **New board skin `midnight`** — dark slate/stone-slab surface designed to sit comfortably next to dark mode (both shipped skins are light-surface). One CSS block in `board-skins.css` + one token file + one `register` call; no component edits, proving the drop-one-file rule.
- **New sound pack `minimal`** — soft, quiet sine-only clicks for all 5 events; unobtrusive alternative to wood/chiptune. One pack file + one `register` call.
- **Retroactive spec home for board skins** — `BoardSkinService` and the wood/classic skins shipped without a live capability spec (only the header switcher is specced in `web-shell`). This change creates the `web-board-skins` capability documenting the registry contract and built-in skins, then adds `midnight` to it.

No breaking changes. No backend changes.

## Capabilities

### New Capabilities

- `web-board-skins`: Board-skin registry contract (`BoardSkinService` abstract DI token, `register`/`activate`/`availableSkins`, `<html data-board-skin>` application, localStorage persistence) and the built-in skins: `wood` (default), `classic`, and the new `midnight`. Documents already-shipped registry behaviour that previously had no spec home, plus the new skin.

### Modified Capabilities

- `web-sound`: `SoundService` API contract grows `volume` signal + `setVolume`; volume persistence/clamping/perceptual-gain requirements; default registered packs list grows `minimal`; new requirement for the built-in `minimal` pack's synthesis.
- `web-shell`: header sound-pack menu gains a volume slider row; new `header.sound.volume` i18n key with zh-CN/en parity.

## Impact

- **Code** (all `frontend-web/`):
  - `core/sound/sound.service.ts` — volume signal, gain application, persistence.
  - `core/sound/packs/minimal.ts` — new file.
  - `core/theme/skins/midnight.ts` — new file; `core/theme/board-skin.service.ts` — one `register` line.
  - `src/styles/board-skins.css` — one `[data-board-skin='midnight']` block.
  - `shell/header/header.{ts,html}` — slider row in the existing sound menu.
  - `public/i18n/{en,zh-CN}.json` — `header.sound.volume`, `header.board-skin.midnight`, `header.sound-pack.minimal`.
- **Tests** — Vitest: volume clamp/persist/gain logic, early-return at volume 0, registry additions, header slider interaction. Existing specs must stay green (default behaviour unchanged: volume defaults to 100).
- **Dependencies** — none added.
- **Backend / API** — untouched.
