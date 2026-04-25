## 1. SoundService + tokens

- [ ] 1.1 Create `src/app/core/sound/sound.tokens.ts` with `SoundEventName` union and `SoundPack` interface (per spec).
- [ ] 1.2 Create `src/app/core/sound/sound.service.ts` — abstract `SoundService` + `DefaultSoundService` impl. Mirror `ThemeService` register/activate shape. Lazy `AudioContext` on first non-muted `play()`. Master `GainNode` connected to `ctx.destination`. Mute / pack name persist to `localStorage` via keys `gomoku:sound-muted` / `gomoku:sound-pack`. AudioContext construction failure → silent no-op.
- [ ] 1.3 Register in `app.config.ts` providers + add `inject(SoundService)` to the existing `provideAppInitializer` callback.
- [ ] 1.4 Spec at `sound.service.spec.ts`: defaults (un-muted, pack=wood); mute persists; muted-play is no-op; activate of unregistered pack falls back; AudioContext failure is silent.

## 2. Built-in `wood` pack

- [ ] 2.1 Create `src/app/core/sound/packs/wood.ts` exporting `woodPack: SoundPack`. `play(event, ctx, masterGain)` switches on event and synthesises:
  - `move-place` — short noise burst (~60 ms, lowpass-filtered) via `BufferSourceNode` with random buffer + `BiquadFilterNode`.
  - `urge` — sine sweep 220→520 Hz over 120 ms.
  - `game-win` — three-note ascending arpeggio C5/E5/G5 (sine + short attack/release).
  - `game-lose` — sine sweep 600→180 Hz over 600 ms with linear gain decay.
  - `game-draw` — two soft 400 Hz pulses.
- [ ] 2.2 Each event's nodes auto-`stop(when)` so no resource leaks.
- [ ] 2.3 `DefaultSoundService` constructor calls `register('wood', woodPack)` + `activate('wood')` (or whatever localStorage says).

## 3. Header mute toggle

- [ ] 3.1 Update `src/app/shell/header/header.ts` — inject `SoundService`; expose `sound: SoundService`, `soundStateKey = computed(() => sound.muted() ? 'header.sound.off' : 'header.sound.on')`, and `toggleSound()`.
- [ ] 3.2 Update `src/app/shell/header/header.html` — add a third toggle button next to dark-mode, exact same Tailwind classes + structure. Order: language → theme → board-skin → sound → dark → user.

## 4. RoomPage hooks

- [ ] 4.1 Update `src/app/pages/rooms/room-page/room-page.ts` — inject `SoundService`. Add private field `previousMoveCount = -1`.
- [ ] 4.2 Add `effect(() => { ... })` watching `state()?.game?.moves.length`: on first observation set previousMoveCount; if `n > previousMoveCount`, call `sound.play('move-place')`; update tracker.
- [ ] 4.3 In the existing `effect(() => hub.gameEnded())` block, add the win/lose/draw dispatch per design D9.
- [ ] 4.4 In the existing `urged$` subscription callback, add `sound.play('urge')` next to the toast set.

## 5. i18n

- [ ] 5.1 Add `header.sound.{label, on, off}` to `public/i18n/en.json`.
- [ ] 5.2 Mirror in `zh-CN.json`.
- [ ] 5.3 Run parity flattener; confirm zero drift.

## 6. Tests

- [ ] 6.1 `sound.service.spec.ts` (per task 1.4).
- [ ] 6.2 Update `room-page.spec.ts` — add `SoundService` stub provider in `mount()` (`{ play: vi.fn(), muted: signal(false), packName: signal('wood'), setMuted: vi.fn(), register: vi.fn(), activate: vi.fn(), availablePacks: () => ['wood'] }`); existing tests still pass.
- [ ] 6.3 Update `header.spec.ts` (or create one if missing) — sound toggle flips muted state.

## 7. Final verification

- [ ] 7.1 No new `bg-gray-*`, hex, rgb in new files (grep).
- [ ] 7.2 No CJK in new templates.
- [ ] 7.3 `npm run lint` passes.
- [ ] 7.4 `npm run test:ci` passes.
- [ ] 7.5 `npm run build` passes; bundle delta < 5 KB gzip.
- [ ] 7.6 `openspec validate add-web-sound-fx` is clean.
