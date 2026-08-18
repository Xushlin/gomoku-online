import { describe, expect, it } from 'vitest';
import { envelopePeaks, makeFakeGraph, stopTimes } from '../../../testing/audio-graph';
import { SOUND_EVENTS } from '../sound.tokens';
import { minimalPack } from './minimal';

/**
 * `minimal`'s **identity** only — quiet and short. The contract every pack owes
 * (a source per event, everything stopped, everything terminating at the master
 * gain, no two events alike) lives in `pack-contract.spec.ts`, which walks all
 * three packs; duplicating it here covered one pack and implied it covered them
 * all.
 *
 * The event list used to be hand-written in this file as `ALL_EVENTS`. It is now
 * `SOUND_EVENTS`, the array `SoundEventName` is derived from — so it cannot
 * silently fall behind the union, which is what a hand-written copy always
 * eventually does.
 */
describe('minimalPack identity', () => {
  it('stays quiet — no envelope peak above half of wood loudness', () => {
    // Wood's loudest event peaks at 0.35; the "quiet" identity caps minimal ≈ 50%.
    const graph = makeFakeGraph();
    for (const event of SOUND_EVENTS) {
      minimalPack.play(event, graph.ctx, graph.masterGain);
    }

    const peaks = envelopePeaks(graph);
    expect(peaks.length).toBeGreaterThan(0);
    expect(Math.max(...peaks)).toBeLessThanOrEqual(0.18);
  });

  it('stays short — move-place ends within 80 ms, every event within 400 ms', () => {
    for (const event of SOUND_EVENTS) {
      const graph = makeFakeGraph();
      minimalPack.play(event, graph.ctx, graph.masterGain);

      const limit = event === 'move-place' ? 0.08 : 0.4;
      expect(Math.max(...stopTimes(graph)), event).toBeLessThanOrEqual(limit);
    }
  });
});
