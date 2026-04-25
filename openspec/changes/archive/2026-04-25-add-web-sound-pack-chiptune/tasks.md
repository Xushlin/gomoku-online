## 1. Chiptune pack

- [x] 1.1 Create `src/app/core/sound/packs/chiptune.ts` exporting `chiptunePack: SoundPack`. Implement all 5 events using only `OscillatorType: 'square' | 'triangle'`. Match the cadence/timing of wood pack (so `move-place` is short, `game-win` is the longest).
- [x] 1.2 Apply ~30–50 % gain reduction relative to wood's sine-wave equivalents (per design D2).
- [x] 1.3 All nodes auto-`stop(when)` to prevent leaks.

## 2. SoundService registration

- [x] 2.1 Update `src/app/core/sound/sound.service.ts` `DefaultSoundService` constructor to register `chiptune` after `wood`.
- [x] 2.2 Existing `sound.service.spec.ts` passes unchanged. Add one assertion: `availablePacks()` after construction includes both `'wood'` and `'chiptune'`.

## 3. Header pack-switcher

- [x] 3.1 Update `src/app/shell/header/header.ts` — expose `currentSoundPackKey = computed(() => 'header.sound-pack.' + sound.packName())`, `soundPacks = computed(() => sound.availablePacks())`, `soundPackKey(name) => 'header.sound-pack.' + name`, `selectSoundPack(name)` method that calls `sound.activate(name)` then `sound.play('move-place')` only if `!sound.muted()`.
- [x] 3.2 Update `src/app/shell/header/header.html` — add a new CDK menu trigger button + `<ng-template #soundPackMenu>` block, mirroring the existing theme/board-skin patterns. Insert before the existing sound on/off toggle (order: language → theme → board-skin → sound-pack → sound on/off → dark → user).

## 4. i18n

- [x] 4.1 Add `header.sound-pack.{label, wood, chiptune}` to `public/i18n/en.json`.
- [x] 4.2 Mirror in `zh-CN.json`.
- [x] 4.3 Run parity flattener; confirm zero drift.

## 5. Tests

- [x] 5.1 `chiptune.spec.ts` (or extend `sound.service.spec.ts`):
  - For each of the 5 events: `chiptunePack.play(event, ctx, masterGain)` does not throw; calls `ctx.createOscillator()` at least once; the oscillator's `type` is `'square'` or `'triangle'`.
- [x] 5.2 Update header spec — sound-pack menu trigger renders, lists both packs, selecting one calls `sound.activate(name)` and `sound.play('move-place')` (or skips play when muted).

## 6. Final

- [x] 6.1 No `sawtooth` in chiptune.ts (grep).
- [x] 6.2 No hex/rgb/hsl in new files.
- [x] 6.3 No CJK in new templates.
- [x] 6.4 `npm run lint` passes.
- [x] 6.5 `npm run test:ci` passes.
- [x] 6.6 `npm run build` passes.
- [x] 6.7 `openspec validate add-web-sound-pack-chiptune` is clean.
