## Why

The web app is silent — every move, win, loss, urge happens with zero auditory feedback. That's a real gap in feel: users sitting at the board can't tell at a glance whether their opponent just moved (especially while glancing at chat or another tab), and the moment of winning / losing has no payoff. Sound is the cheapest meaningful UX upgrade left.

This change adds a synthesised-on-demand sound layer (no audio files shipped — pure Web Audio API), wired into the five events that matter, plus a header mute toggle. The architecture mirrors the established `ThemeService` / `BoardSkinService` register pattern: sound packs are pluggable, so adding a "Japanese", "minimal", or "8-bit" pack later is "one TS file + one register call" with zero component edits.

## What Changes

- **New `SoundService`** (`src/app/core/sound/sound.service.ts`) — abstract DI token + `DefaultSoundService` impl. Mirrors the `ThemeService` / `BoardSkinService` shape:
  - `readonly muted: Signal<boolean>` — global mute toggle.
  - `readonly packName: Signal<string>` — currently active pack name (for future switcher UI).
  - `play(event: SoundEventName): void` — fire-and-forget; no-op when muted; tolerates unsupported browsers.
  - `setMuted(muted: boolean): void` — persists to `localStorage` key `gomoku:sound-muted`.
  - `register(name: string, pack: SoundPack): void` — validate + add to registry.
  - `activate(name: string): void` — switch packs, persists to `localStorage` key `gomoku:sound-pack`.
  - `availablePacks(): readonly string[]`.
  - Lazy-init `AudioContext` on first non-muted `play()` (browser autoplay policy demands a user gesture; falling-back to noop if creation throws).
- **`SoundEventName` union**: `'move-place' | 'game-win' | 'game-lose' | 'game-draw' | 'urge'`. Five events for v1; chat / scrubber events deferred.
- **Built-in pack `wood`** (`src/app/core/sound/packs/wood.ts`) — synthesised via Web Audio API. Each event renders procedurally:
  - `move-place`: short noise burst with steep attack/decay (≈ 60 ms) — wooden tap.
  - `urge`: rising sine sweep 220 → 520 Hz over 120 ms — attention pop.
  - `game-win`: ascending major arpeggio C5–E5–G5 with overlapping tails.
  - `game-lose`: descending sine 600 → 180 Hz over 600 ms with decreasing gain.
  - `game-draw`: two soft 400 Hz pulses, neutral.
- **`SoundPack` interface** at `src/app/core/sound/sound.tokens.ts` — `{ play(event, ctx, masterGain): void }`. Same shape pattern as `BoardSkinTokens`.
- **Header mute toggle** — third button next to the existing dark-mode toggle, mirrors its visual: `<button role="switch" aria-checked>`, label "Sound: On / Off". Reads/writes `SoundService.muted`.
- **RoomPage hooks** (`src/app/pages/rooms/room-page/room-page.ts`):
  - `effect` on `state()?.game?.moves.length`: when count grows by 1 (and not on initial load) → `sound.play('move-place')`.
  - `effect` on `hub.gameEnded()`: when it goes non-null → branch on `(result, mySide)`: draw → `'game-draw'`; user is winner → `'game-win'`; otherwise → `'game-lose'`.
  - `urged$` subscription (already exists) → also call `sound.play('urge')`.
- **i18n** — new `header.sound.{label, on, off}` subtree. Both en + zh-CN. Parity zero drift.
- **Tests**:
  - `sound.service.spec.ts`: muted state persists; toggling unblocks/blocks `play`; `play` on unknown pack name falls back to active default; `register` validates the pack shape; AudioContext creation failure is silent (jsdom path — verified by stubbing `window.AudioContext` to throw).
  - Update `room-page.spec.ts` to assert `sound.play('move-place')` fires when state's move count increments; existing tests keep passing.
  - Header spec gains a "mute toggle flips muted signal" test.

Out of scope (deferred to follow-ups):
- Pack switcher UI in the header (architecture supports it, UI doesn't expose it yet — adding a 5th header dropdown is overcrowding).
- Volume slider — single mute toggle is enough for v1.
- Sound on chat-message arrival, replay scrubber stepping, and reconnection events — likely annoying; revisit if users ask.
- Bundled audio file assets — Web Audio synth keeps bundle clean and theme-agnostic.

## Capabilities

### New Capabilities

- `web-sound`: `SoundService` API + pack registration + the 5 sound events + how RoomPage hooks them.

### Modified Capabilities

- `web-shell`: header gains a third state toggle (sound on/off) next to dark-mode.
- `web-game-board`: RoomPage emits sound events on move-arrival, game-end, and urge-received. No game-loop semantics change.

## Impact

- **New folders / files**:
  - `src/app/core/sound/sound.service.ts` + spec
  - `src/app/core/sound/sound.tokens.ts`
  - `src/app/core/sound/packs/wood.ts`
- **Modified files**:
  - `src/app/app.config.ts` — provide `SoundService`; eagerly inject in `provideAppInitializer` so the pack registry is populated before first paint.
  - `src/app/shell/header/header.{ts,html}` — sound toggle button.
  - `src/app/pages/rooms/room-page/room-page.{ts,html}` — three new effects / hooks (no template change beyond the existing structure).
  - `public/i18n/{en,zh-CN}.json` — `header.sound.*` keys.
- **Backend impact**: none — purely frontend.
- **Bundle**: < 2 KB gzip added (one tiny service file + one synthesis pack file). No audio assets shipped.
- **Browser support**: all modern browsers have Web Audio API. Fallback path: try/catch around `new AudioContext()` — failure leaves `play()` as a no-op (no console errors).
- **Enables**: a future "sound pack switcher" UI change can ship a second pack file + register call + a header dropdown.
