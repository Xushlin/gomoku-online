import { vi } from 'vitest';

/**
 * A recording stand-in for the Web Audio API.
 *
 * jsdom has no `AudioContext`, so a pack cannot be exercised against the real
 * thing here. This fake records what a pack *built* — which sources it created,
 * what it connected them to, and when it scheduled them to stop — so the pack
 * contract can be asserted without any audio.
 *
 * It was extracted from `minimal.spec.ts`, where it only had to model oscillators
 * and gains, because `minimal` is sine-only. `wood` also creates buffers, buffer
 * sources and biquad filters, and it had **no test at all** — so did `chiptune`.
 * One fake that covers all three is what lets a single walking spec cover every
 * built-in pack.
 *
 * What it does not prove: that a real browser builds the graph and makes a noise.
 * Nothing in jsdom can prove that, so it is checked in a browser instead.
 */
export interface FakeOscillator {
  type: string;
  frequency: FakeAudioParam;
  connect: ReturnType<typeof vi.fn>;
  start: ReturnType<typeof vi.fn>;
  stop: ReturnType<typeof vi.fn>;
}

export interface FakeBufferSource {
  buffer: unknown;
  connect: ReturnType<typeof vi.fn>;
  start: ReturnType<typeof vi.fn>;
  stop: ReturnType<typeof vi.fn>;
}

export interface FakeAudioParam {
  value: number;
  setValueAtTime: ReturnType<typeof vi.fn>;
  linearRampToValueAtTime: ReturnType<typeof vi.fn>;
  exponentialRampToValueAtTime: ReturnType<typeof vi.fn>;
}

export interface FakeGainNode {
  gain: FakeAudioParam;
  connect: ReturnType<typeof vi.fn>;
}

export interface FakeFilterNode {
  type: string;
  frequency: FakeAudioParam;
  Q: FakeAudioParam;
  connect: ReturnType<typeof vi.fn>;
}

/** Anything that emits sound and therefore has to be stopped. */
export type FakeSource = FakeOscillator | FakeBufferSource;

export interface FakeGraph {
  readonly ctx: AudioContext;
  readonly masterGain: GainNode;
  /** `ctx.destination`. Nothing a pack builds may connect straight to it. */
  readonly destination: object;
  readonly oscillators: FakeOscillator[];
  readonly bufferSources: FakeBufferSource[];
  readonly gains: FakeGainNode[];
  readonly filters: FakeFilterNode[];
  /** Oscillators and buffer sources together — every node with a `start`/`stop`. */
  readonly sources: FakeSource[];
}

function param(initial = 0): FakeAudioParam {
  return {
    value: initial,
    setValueAtTime: vi.fn(),
    linearRampToValueAtTime: vi.fn(),
    exponentialRampToValueAtTime: vi.fn(),
  };
}

export function makeFakeGraph(): FakeGraph {
  const oscillators: FakeOscillator[] = [];
  const bufferSources: FakeBufferSource[] = [];
  const gains: FakeGainNode[] = [];
  const filters: FakeFilterNode[] = [];
  const sources: FakeSource[] = [];

  // A pack that connected here instead of to the master gain would still make a
  // noise, and would silently ignore the volume slider. Named so a spec can assert
  // nothing ever reaches it.
  const destination = { __destination: true };

  const ctx = {
    currentTime: 0,
    sampleRate: 48000,
    destination,
    createOscillator: () => {
      const osc: FakeOscillator = {
        type: '',
        frequency: param(),
        // Returning the target is what makes `a.connect(b).connect(c)` work, and
        // wood chains three deep.
        connect: vi.fn((target: unknown) => target),
        start: vi.fn(),
        stop: vi.fn(),
      };
      oscillators.push(osc);
      sources.push(osc);
      return osc;
    },
    createBufferSource: () => {
      const source: FakeBufferSource = {
        buffer: null,
        connect: vi.fn((target: unknown) => target),
        start: vi.fn(),
        stop: vi.fn(),
      };
      bufferSources.push(source);
      sources.push(source);
      return source;
    },
    createBuffer: (channels: number, length: number) => ({
      length,
      numberOfChannels: channels,
      getChannelData: () => new Float32Array(length),
    }),
    createGain: () => {
      const gain: FakeGainNode = {
        gain: param(1),
        connect: vi.fn((target: unknown) => target),
      };
      gains.push(gain);
      return gain;
    },
    createBiquadFilter: () => {
      const filter: FakeFilterNode = {
        type: '',
        frequency: param(),
        Q: param(),
        connect: vi.fn((target: unknown) => target),
      };
      filters.push(filter);
      return filter;
    },
  } as unknown as AudioContext;

  const masterGain = { __master: true } as unknown as GainNode;
  return { ctx, masterGain, destination, oscillators, bufferSources, gains, filters, sources };
}

/** Every envelope peak a pack asked for, across all its gain nodes. */
export function envelopePeaks(graph: FakeGraph): number[] {
  return graph.gains.flatMap((g) => [
    ...g.gain.linearRampToValueAtTime.mock.calls.map(([value]) => value as number),
    ...(g.gain.value !== 1 ? [g.gain.value] : []),
  ]);
}

/** When each source was told to stop. */
export function stopTimes(graph: FakeGraph): number[] {
  return graph.sources.map((s) => s.stop.mock.calls[0]?.[0] as number);
}
