import { describe, expect, it } from 'vitest';
import { makeFakeGraph, stopTimes, type FakeGraph } from '../../../testing/audio-graph';
import { SOUND_EVENTS, type SoundEventName, type SoundPack } from '../sound.tokens';
import { BUILT_IN_PACKS } from './index';

/**
 * The contract every pack owes, walked over every built-in pack × every event.
 *
 * Both lists are derived, not written here: `BUILT_IN_PACKS` is the same object
 * `DefaultSoundService` registers from, and `SOUND_EVENTS` is the array
 * `SoundEventName` is derived from. A hand-written copy of either is the defect
 * this repo has paid for three times — and `minimal.spec.ts`'s `ALL_EVENTS` was a
 * fourth copy, which this spec's existence lets it drop.
 *
 * Before this file, **`wood.ts` and `chiptune.ts` had no tests at all**. That was
 * survivable while their five voices never changed; adding four events each made
 * eight of the twelve new voices land in files nothing looked at.
 */

const packs = Object.entries(BUILT_IN_PACKS);

/**
 * Which oscillator timbres each pack is allowed, from its own spec requirement:
 * `minimal` is sine-only, `chiptune` is square/triangle and never sawtooth, and
 * `wood` uses sine for its tonal events (its taps are filtered noise instead).
 *
 * Keys are asserted equal to `BUILT_IN_PACKS` below, so a new pack cannot slip in
 * without declaring an identity — silently skipping it is exactly how a walking
 * test ends up covering less than it claims.
 */
const ALLOWED_TYPES: Readonly<Record<string, readonly string[]>> = {
  wood: ['sine'],
  chiptune: ['square', 'triangle'],
  minimal: ['sine'],
};

function play(pack: SoundPack, event: SoundEventName): FakeGraph {
  const graph = makeFakeGraph();
  pack.play(event, graph.ctx, graph.masterGain);
  return graph;
}

/**
 * What a player would hear, reduced to something comparable.
 *
 * Node counts alone are not enough: `chiptune`'s tap and its capture are both a
 * single square oscillator, and they differ only in how the frequency moves. So
 * this folds in the pitches, the number of frequency ramps and the stop times.
 */
function fingerprint(graph: FakeGraph): string {
  return JSON.stringify({
    osc: graph.oscillators.map((o) => ({
      type: o.type,
      freq: o.frequency.value,
      ramps:
        o.frequency.setValueAtTime.mock.calls.length +
        o.frequency.exponentialRampToValueAtTime.mock.calls.length,
    })),
    buffers: graph.bufferSources.length,
    filters: graph.filters.map((f) => ({ type: f.type, cutoff: f.frequency.value })),
    stops: stopTimes(graph).map((t) => Math.round(t * 1000)),
  });
}

describe('sound pack contract', () => {
  it('declares an identity for every built-in pack', () => {
    expect(Object.keys(ALLOWED_TYPES).sort()).toEqual(Object.keys(BUILT_IN_PACKS).sort());
  });

  it('covers every event the type allows', () => {
    // SOUND_EVENTS is what SoundEventName is derived from, so this walk cannot be
    // narrower than the union. Pinned so a future refactor cannot quietly
    // reintroduce a hand-written list.
    expect(SOUND_EVENTS.length).toBeGreaterThan(0);
    expect(new Set(SOUND_EVENTS).size).toBe(SOUND_EVENTS.length);
  });

  describe.each(packs)('%s', (name, pack) => {
    it.each(SOUND_EVENTS)('%s makes at least one source, and never throws', (event) => {
      const graph = play(pack, event);

      expect(graph.sources.length).toBeGreaterThanOrEqual(1);
    });

    it.each(SOUND_EVENTS)('%s starts and stops every source exactly once', (event) => {
      const graph = play(pack, event);

      for (const source of graph.sources) {
        expect(source.start).toHaveBeenCalledTimes(1);
        expect(source.stop).toHaveBeenCalledTimes(1);
        const startedAt = source.start.mock.calls[0][0] as number;
        const stoppedAt = source.stop.mock.calls[0][0] as number;
        expect(Number.isFinite(stoppedAt)).toBe(true);
        // A node stopped at or before its start makes no sound at all.
        expect(stoppedAt).toBeGreaterThan(startedAt);
      }
    });

    it.each(SOUND_EVENTS)('%s terminates every gain at the master gain', (event) => {
      const graph = play(pack, event);

      expect(graph.gains.length).toBeGreaterThanOrEqual(1);
      for (const gain of graph.gains) {
        expect(gain.connect).toHaveBeenCalledWith(graph.masterGain);
      }
    });

    it.each(SOUND_EVENTS)('%s never connects past the master gain', (event) => {
      const graph = play(pack, event);

      // Connecting to ctx.destination would still make a noise while ignoring the
      // volume slider entirely — audible, and invisible to every other assertion.
      const connects = [...graph.oscillators, ...graph.bufferSources, ...graph.gains, ...graph.filters];
      for (const node of connects) {
        expect(node.connect).not.toHaveBeenCalledWith(graph.destination);
      }
    });

    it.each(SOUND_EVENTS)('%s only uses timbres this pack is allowed', (event) => {
      const graph = play(pack, event);

      for (const osc of graph.oscillators) {
        expect(ALLOWED_TYPES[name]).toContain(osc.type);
      }
    });

    it('gives no two events the same audio graph', () => {
      // Two events that build the same graph are the same sound to the player.
      // The capture/move-place pair is the one that matters most — 象棋 needs them
      // distinguishable — but a copy-pasted arm anywhere is the same defect.
      const seen = new Map<string, SoundEventName>();
      for (const event of SOUND_EVENTS) {
        const print = fingerprint(play(pack, event));
        const clash = seen.get(print);
        expect(clash, `${event} sounds exactly like ${clash}`).toBeUndefined();
        seen.set(print, event);
      }
    });
  });
});
