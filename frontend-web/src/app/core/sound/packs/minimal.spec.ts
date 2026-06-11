import { describe, expect, it, vi } from 'vitest';
import type { SoundEventName } from '../sound.tokens';
import { minimalPack } from './minimal';

const ALL_EVENTS: readonly SoundEventName[] = [
  'move-place',
  'game-win',
  'game-lose',
  'game-draw',
  'urge',
];

interface FakeOscillator {
  type: string;
  frequency: { value: number };
  connect: ReturnType<typeof vi.fn>;
  start: ReturnType<typeof vi.fn>;
  stop: ReturnType<typeof vi.fn>;
}

interface FakeGainNode {
  gain: {
    value: number;
    setValueAtTime: ReturnType<typeof vi.fn>;
    linearRampToValueAtTime: ReturnType<typeof vi.fn>;
    exponentialRampToValueAtTime: ReturnType<typeof vi.fn>;
  };
  connect: ReturnType<typeof vi.fn>;
}

function makeFakeGraph() {
  const oscillators: FakeOscillator[] = [];
  const gains: FakeGainNode[] = [];

  const ctx = {
    currentTime: 0,
    sampleRate: 48000,
    createOscillator: () => {
      const osc: FakeOscillator = {
        type: '',
        frequency: { value: 0 },
        connect: vi.fn((target: unknown) => target),
        start: vi.fn(),
        stop: vi.fn(),
      };
      oscillators.push(osc);
      return osc;
    },
    createGain: () => {
      const gain: FakeGainNode = {
        gain: {
          value: 1,
          setValueAtTime: vi.fn(),
          linearRampToValueAtTime: vi.fn(),
          exponentialRampToValueAtTime: vi.fn(),
        },
        connect: vi.fn((target: unknown) => target),
      };
      gains.push(gain);
      return gain;
    },
  } as unknown as AudioContext;

  const masterGain = { __master: true } as unknown as GainNode;
  return { ctx, masterGain, oscillators, gains };
}

describe('minimalPack', () => {
  it.each(ALL_EVENTS)('%s creates at least one sine oscillator wired to masterGain', (event) => {
    const { ctx, masterGain, oscillators, gains } = makeFakeGraph();
    minimalPack.play(event, ctx, masterGain);

    expect(oscillators.length).toBeGreaterThanOrEqual(1);
    for (const osc of oscillators) {
      expect(osc.type).toBe('sine');
      expect(osc.connect).toHaveBeenCalled();
    }
    // Every intermediate gain terminates at the shared master gain.
    for (const gain of gains) {
      expect(gain.connect).toHaveBeenCalledWith(masterGain);
    }
  });

  it.each(ALL_EVENTS)('%s schedules stop() on every oscillator', (event) => {
    const { ctx, masterGain, oscillators } = makeFakeGraph();
    minimalPack.play(event, ctx, masterGain);
    for (const osc of oscillators) {
      expect(osc.stop).toHaveBeenCalledTimes(1);
    }
  });

  it('stays quiet — no envelope peak above half of wood loudness', () => {
    // Wood's loudest event peaks at 0.35; "quiet" identity caps minimal ≈ 50%.
    const { ctx, masterGain, gains } = makeFakeGraph();
    for (const event of ALL_EVENTS) {
      minimalPack.play(event, ctx, masterGain);
    }
    const peaks = gains.flatMap((g) =>
      g.gain.linearRampToValueAtTime.mock.calls.map(([value]) => value as number),
    );
    expect(peaks.length).toBeGreaterThan(0);
    expect(Math.max(...peaks)).toBeLessThanOrEqual(0.18);
  });

  it('stays short — move-place ends within 80 ms, every event within 400 ms', () => {
    for (const event of ALL_EVENTS) {
      const { ctx, masterGain, oscillators } = makeFakeGraph();
      minimalPack.play(event, ctx, masterGain);
      const stopTimes = oscillators.map((o) => o.stop.mock.calls[0][0] as number);
      const limit = event === 'move-place' ? 0.08 : 0.4;
      expect(Math.max(...stopTimes)).toBeLessThanOrEqual(limit);
    }
  });
});
