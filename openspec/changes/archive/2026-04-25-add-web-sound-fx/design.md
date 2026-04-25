## Context

The web app's UX is silent — no auditory cue for moves, urges, or game-end. Adding sound is a clear quality bump, but introducing audio assets has weight: licensing, file format mismatches, slow first-load, mute lifecycle, AudioContext autoplay policy. We sidestep most of that by **synthesising sounds with Web Audio API** rather than shipping audio files.

The architecture mirrors `ThemeService` / `BoardSkinService`:

- An abstract DI token (`SoundService`) with a `Default*` impl.
- A `SoundPack` interface declaring the playback contract.
- A registry on the service so future packs can land without touching components.
- Persistence of user choice via `localStorage`.
- A `Signal`-backed mute state.

Sharp edges:

1. **Browser autoplay policy.** `AudioContext` can only be resumed after a user gesture. We lazy-create it on first non-muted `play()` (which is always behind a click). `ctx.resume()` is called defensively in case the context starts in `suspended` state.
2. **`vi.useFakeTimers()` doesn't replace AudioContext.** Tests must stub `window.AudioContext` to a no-op constructor, otherwise jsdom throws.
3. **Idempotency.** `play()` is fire-and-forget — multiple consecutive moves should not stack indefinitely. We let each event create its own short-lived `OscillatorNode` / `BufferSourceNode` that auto-stops via `node.stop(when)`. Garbage collection cleans the node graph.
4. **Mute is a master gain knob, not a hard "skip play".** Setting `masterGain.value = 0` means even attempted plays are silent; we still go through the motions so timing / event order isn't affected. (Alternative: short-circuit `play` when muted to save CPU. We do that *too* — early-return — purely to avoid creating short-lived nodes.)

## Goals / Non-Goals

**Goals:**

- Audible feedback for the 5 events users care about (move, win, lose, draw, urge).
- Zero audio assets in the bundle.
- Pluggable: future "soft" / "Japanese" / "8-bit" packs land via 1 file + 1 register call, no component edits.
- Default to ON (sound playing); persist user's mute choice.
- Tolerant of browsers without Web Audio (silent failure).

**Non-Goals:**

- Volume slider — mute toggle is enough for v1.
- Sound on every UI event (chat message, hover, scrubber). Likely annoying; defer.
- Spatialised audio, reverb, or fancy synthesis. Pure sine + noise for v1.
- Sound editor / per-event opt-out granularity.
- Replay-scrubber sound (would fire on every step in auto-play, irritating).

## Decisions

### D1. Synthesise via Web Audio API; don't ship audio files

**Decision:** Each pack provides a function `play(event, ctx, masterGain)` that constructs a fresh `OscillatorNode` / `BufferSourceNode` graph for each event firing. No `<audio>` elements, no `fetch('xxx.mp3')`.

**Rationale:**

- No new HTTP requests, no asset bundle, no licensing concerns, no file-format probing.
- Sounds are theme-able in TS (a future "Japanese" pack just programs different envelopes / pitches).
- Tiny code footprint (~50–100 LOC per pack).

**Alternatives considered:**

- Bundle pre-recorded short audio files. Rejected: licensing pain, codec issues across browsers, larger bundle, harder to theme.
- HTML5 `<audio>` element with `preload="auto"`. Rejected: same as above.

### D2. SoundEventName union, not free-form strings

**Decision:** Closed string-literal union `'move-place' | 'game-win' | 'game-lose' | 'game-draw' | 'urge'`. Adding a 6th event requires editing the type, which forces every pack to provide a render path (or fall through silently).

**Rationale:**

- TS exhaustiveness check catches packs that miss new events.
- Constrains the API surface — RoomPage can't emit arbitrary new events that no pack handles.

### D3. Mute as `localStorage` boolean; default ON (un-muted)

**Decision:** `gomoku:sound-muted` stores `'1'` (muted) or `'0'` (un-muted). Default on first run is un-muted. Toggle persists immediately on click.

**Rationale:**

- Mirrors `gomoku:dark` and `gomoku:theme` storage style.
- Default-ON is correct for a sound-effects feature (vs default-OFF which would mean nobody discovers the feature).

**Alternatives considered:**

- Default-OFF, opt-in. Rejected — feature hides itself; users who'd appreciate it never enable.

### D4. Eager-construct in `provideAppInitializer`

**Decision:** Add `inject(SoundService)` to the existing `provideAppInitializer` callback so the service's constructor runs at app boot, populating the pack registry and reading stored mute state. Same pattern we used for `ThemeService` / `BoardSkinService`.

**Rationale:**

- Without eager construction, the first call to `sound.play(...)` would defer pack registration; lookup would fail or surprise.
- Constructor is cheap (no AudioContext yet — that's lazy).

### D5. AudioContext creation lazy + try/catch

**Decision:** `private ensureCtx(): AudioContext | null` is called inside `play()` only when not muted. If `new AudioContext()` throws (Safari edge cases, jsdom test environment, `--no-audio` flag in some browsers), return `null` and let `play()` early-return.

**Rationale:**

- Browser autoplay policy: AudioContext can be created at any time but won't *play* anything until resumed via user gesture. We `await ctx.resume()` synchronously (it returns a Promise but we fire-and-forget; subsequent plays will work). The first attempted sound after mute-toggle-on may be silent — accepted.
- jsdom doesn't have AudioContext. Tests that assert on play() shouldn't crash.

### D6. Master gain at the service level

**Decision:** Service holds a single `GainNode` connected `pack-output → masterGain → ctx.destination`. Mute sets `masterGain.gain.value = 0`. Un-mute restores to `1`.

**Rationale:**

- Simpler than each pack tracking its own destination.
- Mute applies uniformly across packs.
- Future "volume slider" change just animates `masterGain.gain.value` between 0 and 1.

### D7. Hook points: RoomPage, not Board

**Decision:** RoomPage's existing `effect`s drive sound — Board stays pure (no audio dependency injected, easier to reuse for replay).

**Rationale:**

- Board is also used in replay context with `[readonly]="true"`; we don't want replay scrubber stepping to fire move sounds (annoying when fast-forwarding).
- Sound events are page-level concerns ("a move just landed in this game I'm watching") — naturally belong with the orchestrator.

**Alternatives considered:**

- Hook into `GameHubService` (where `MoveMade` first arrives). Rejected: service shouldn't depend on `SoundService` (testability + layering); RoomPage is the right level of abstraction.

### D8. Move-count diff via tracked previous-count

**Decision:** RoomPage tracks `private previousMoveCount = -1`. The state-watching effect computes `n = state.game?.moves.length ?? 0`. If `previousMoveCount === -1`, just set it (avoids playing on initial load when state hydrates with N existing moves). On `n > previousMoveCount`, `play('move-place')`. Update `previousMoveCount = n`.

**Rationale:**

- A reconnect rehydration via REST snapshot bumps `moves.length` to the same value it was → no false positives.
- A game-end via `GameEnded` doesn't touch `moves` → no false positives.
- A new move via `MoveMade` increments the count by 1 → trigger fires once per move.

**Alternatives considered:**

- Subscribe to `MoveMade` events directly via a new transient `Subject` on `GameHubService`. Rejected: more plumbing, plus we lose the natural debounce when batched RoomState arrives.

### D9. Game-end: dispatch from `(result, mySide)`

**Decision:** RoomPage's existing `effect(() => hub.gameEnded())` adds a sound dispatch:

```ts
const ended = hub.gameEnded();
if (!ended) return;
if (ended.result === 'Draw') sound.play('game-draw');
else {
  const won = (ended.result === 'BlackWin' && mySide() === 'black')
           || (ended.result === 'WhiteWin' && mySide() === 'white');
  sound.play(won ? 'game-win' : 'game-lose');
}
```

**Rationale:**

- Same logic as the dialog title selection — keeps two truths in sync (the dialog says "you won" if and only if we play game-win).
- Spectators who happen to be on the page when a game ends hear `game-lose` (since `mySide() === 'spectator'` ≠ either color). That's fine — there's no "spectator-neutral" sound; the lose tone for spectators reads as "the match is over".

**Alternatives considered:**

- Spectator gets `game-draw`-style neutral sound. Rejected: not worth the branch; spectators can mute.

## Risks / Trade-offs

- **Risk: synthesised sounds sound cheap or unpleasant.** → Mitigation: short envelopes (no sustained tones), conventional pitch choices (C major arpeggio for win = universally readable as "good"), test on real users. If feedback is bad, ship a follow-up pack with sample-based audio.
- **Risk: AudioContext creation fails in some edge browsers / kiosk modes.** → Mitigation: try/catch + null-return; the rest of the app works silently.
- **Risk: A reconnection rehydration triggers a phantom move-place sound.** → Mitigation: D8's previous-count tracker resets to `n` on initial load (no play). Reconnection triggers another full state arrival; if `n` hasn't changed, no play. If a real move arrived during the disconnection window, `n` did grow — we play once, which is arguably correct ("opponent moved while you were offline").
- **Risk: User has multiple browser tabs of the same game open and hears sounds twice.** → Accepted. Same as toast notifications. Power users open multiple tabs by intent.
- **Risk: `sound.play('urge')` fires alongside the existing `urgeToast.set(true)` — does the timing feel right?** → Both fire from the same `urged$` subscription, so they're simultaneous. Should feel like one alert.
- **Risk: Tests need to stub AudioContext.** → Mitigation: `(window as any).AudioContext = undefined` in `beforeEach` for tests that don't care about audio; tests that *do* care provide a minimal stub.
- **Trade-off: pack registration is plumbed but no UI exposes it in v1.** → Accepted. Reduces UI clutter for now; future change adds a header dropdown next to "Theme" + "Board".
- **Trade-off: no per-event opt-out.** → Accepted. Master mute is enough; granular control adds preference UI we don't want yet.

## Migration Plan

- Net-additive change.
- One existing service injection (`AppInitializer`) gets a new `inject(SoundService)` line.
- Header gets a new button next to dark-mode; existing tests don't assert on header button count, so they pass unchanged.
- RoomPage's existing effects gain side-effect calls to `sound.play(...)`; existing RoomPage spec stubs `SoundService` (or omits it via Angular's `optional: true` style — actually we can't do optional since constructor injects synchronously; the spec adds a stub provider).
- Rollback = revert; no data migration.

## Open Questions

- **Q1: Sound at multiplayer sync points (someone joins / leaves room)?** Likely too noisy. Defer.
- **Q2: Header button order?** Current: language → theme → board-skin → dark → user. Insert sound between board-skin and dark, since both belong to the "preferences" cluster vs the "auth" cluster. Settled.
- **Q3: Future "Japanese" pack — what does "win" sound like?** Out of scope; design when we add the second pack.
