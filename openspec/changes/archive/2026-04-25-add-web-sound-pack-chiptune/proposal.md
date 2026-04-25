## Why

`add-web-sound-fx` shipped the `SoundService` with register-based pack pluggability — but only one pack (`wood`) ever uses it, and there's no UI to switch even if a second pack existed. The architecture is theoretical until something proves it. This change ships a contrasting **`chiptune`** pack (8-bit square/triangle synthesis) and a **pack switcher dropdown** in the header, mirroring the existing theme / board-skin dropdowns.

Two wins: (a) the visual pattern of "switchable preference" gets one more concrete instance so the architecture is provably "1 file + 1 register call + 1 i18n label = new pack"; (b) users get an actual choice between two characteristic sound styles.

## What Changes

- **New `chiptune` pack** at `src/app/core/sound/packs/chiptune.ts` — same 5 `SoundEventName`s, but synthesised with `OscillatorType: 'square' | 'triangle'` for 8-bit timbre. Sounds:
  - `move-place`: short square click ~50 ms, ~150 Hz with fast attack/decay.
  - `urge`: triangle sweep 300 → 700 Hz over 100 ms — classic 8-bit alert.
  - `game-win`: ascending square-wave arpeggio C5 → E5 → G5 → C6 (one octave higher than wood, with a flourish) — "level up" feel.
  - `game-lose`: square-wave descending 640 → 160 Hz over 700 ms — "game over" feel.
  - `game-draw`: two triangle-wave 440 Hz pulses.
- **`DefaultSoundService` constructor** registers `chiptune` alongside `wood`.
- **Header pack-switcher dropdown** — new CDK menu trigger between the board-skin trigger and the existing sound on/off toggle. Lists `wood` and `chiptune` (both translated). Mirrors the theme + board-skin trigger visuals exactly. Selecting a pack:
  - Calls `sound.activate(name)` (which persists to `localStorage` via the existing service).
  - Plays a one-shot preview sound (`sound.play('move-place')`) so the user immediately hears what they picked. Skipped if `sound.muted()` is true.
- **i18n** — new `header.sound-pack.{label, wood, chiptune}` subtree. en + zh-CN, parity zero drift.
- **Tests**:
  - `chiptune` pack — same coverage as wood: each event constructs at least one OscillatorNode connected to the master gain, no event throws, no resource leaks.
  - `sound.service.spec.ts` already covers register/activate; add one test that confirms `chiptune` is registered by default and listed in `availablePacks()`.
  - Header spec: pack-switcher menu trigger renders, lists both packs, selecting one calls `sound.activate(name)` and plays preview unless muted.

Out of scope:
- A volume slider — still deferred to a future change.
- Per-event opt-out / customisation — too granular.
- Bundled audio assets / sample-based packs — synthesis remains the only mechanism.
- A third pack (Japanese zen / minimal / etc.) — proves the pattern with two; future packs cost the same as this one.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities

- `web-sound`: adds a second pack to the registry; the contract `SoundService.register / activate / availablePacks` is exercised end-to-end. No new requirements on the abstract API; the `wood` requirement is replaced with a more-general "≥ 2 packs registered by default" requirement.
- `web-shell`: header gains a pack-switcher dropdown next to the sound toggle, plus the `header.sound-pack.*` i18n keys.

## Impact

- **New folders / files**:
  - `src/app/core/sound/packs/chiptune.ts` + spec
- **Modified files**:
  - `src/app/core/sound/sound.service.ts` — register `chiptune` in the constructor.
  - `src/app/shell/header/header.{ts,html}` — dropdown trigger + cdkMenu template.
  - `public/i18n/{en,zh-CN}.json` — `header.sound-pack.*` subtree.
- **Backend impact**: none.
- **Bundle**: ~1 KB raw delta (one tiny synthesis module + a few menu lines).
