## Context

`add-web-sound-fx` set up the registry; this change is the proof-of-concept second consumer. Effort is mostly synthesis-style choices for the new pack and one more dropdown wire-up in the header.

Sharp edges (small):

1. **Square-wave loudness.** Square waves at the same gain as sine waves *sound* much louder because of harmonic content. Each chiptune event uses ~30–50% lower gain than the equivalent wood event to keep perceived volume even.
2. **Preview on activate.** Some users may switch packs while muted; the preview play is gated by `sound.muted()`. AudioContext is already lazy-created on first non-muted play, so the preview *is* the first chance to surface a problem (e.g. blocked by autoplay policy in an exotic browser). Failure remains silent — `play()` short-circuits when ctx fails.
3. **Persistence already works.** `SoundService.activate` writes `gomoku:sound-pack` to localStorage; the constructor reads it back. So picking `chiptune` once survives reload — no extra wiring.

## Goals / Non-Goals

**Goals:**

- A second sound pack with audibly distinct character.
- A header switcher that exercises the registry's `availablePacks()` + `activate()` API.
- Preview-on-pick so the choice feels immediate.

**Non-Goals:**

- Volume control.
- More than two packs (this change ships exactly one new pack).
- Sample-based audio.
- Per-event customisation.

## Decisions

### D1. Square + triangle, no sawtooth

**Decision:** Chiptune events use only `OscillatorType: 'square'` (tonal, classic NES) and `'triangle'` (softer, like NES bass). No `'sawtooth'` (too harsh).

**Rationale:** Square + triangle is the canonical chiptune timbre. Sawtooth adds nothing recognisable for the events we have.

### D2. Lower gain on square waves

**Decision:** All square-wave events use peak gain ~0.18 (vs wood's ~0.28 sine). Triangle events stay around 0.25.

**Rationale:** Equal-loudness compensation. Square waves fundamentally have more energy in upper harmonics; matching by perceived loudness, not numeric gain.

### D3. Pack switcher trigger placement

**Decision:** Dropdown sits between the board-skin trigger and the sound on/off toggle. Order in the header: language → theme → board-skin → **sound-pack** → sound (on/off) → dark → user.

**Rationale:** Group the two sound-related controls (pack picker + on/off) adjacent. Theme + board-skin are visual; sound-pack + on/off are aural.

### D4. Preview plays `move-place` after activate

**Decision:** Right after `sound.activate(name)`, fire `sound.play('move-place')`. If `sound.muted()` is true, skip — switching packs while silent shouldn't unmute or override.

**Rationale:** `move-place` is the most-frequent event in real play, so previewing it gives the most relevant sample of what the new pack sounds like. Single short event keeps the preview unobtrusive.

**Alternatives considered:**

- Play the longer `game-win` arpeggio. Rejected: too celebratory for a minor preference change; sounds odd if user is in a quiet room.
- Skip preview entirely, let the user notice on the next move. Rejected: defeats the immediacy that makes "try a new pack" fun.

## Risks / Trade-offs

- **Risk: chiptune `game-win` arpeggio sounds cheesy in a serious game context.** → Accepted. Users who don't want it pick `wood`. The header dropdown is the escape hatch.
- **Risk: square waves on cheap laptop speakers / earbuds may distort.** → Mitigation: lower gain (D2). If still bad, follow-up tweak.
- **Trade-off: header gets one more dropdown.** → Accepted. Pattern is established; user discoverability outweighs density.

## Migration Plan

- Net-additive. No existing pack changes. Existing tests pass without modification (they pass `wood` paths).
- Rollback = revert.
