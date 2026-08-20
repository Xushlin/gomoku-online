import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { PACK_NAMES } from './packs';
import { DefaultSoundService, SoundService } from './sound.service';
import type { SoundPack } from './sound.tokens';

const STORAGE_MUTED = 'gewu:sound-muted';
const STORAGE_PACK = 'gewu:sound-pack';
const STORAGE_VOLUME = 'gewu:sound-volume';

class FakeAudioContextSpy {
  createGain = vi.fn(() => ({
    gain: {
      value: 1,
      setValueAtTime: vi.fn(),
      linearRampToValueAtTime: vi.fn(),
      exponentialRampToValueAtTime: vi.fn(),
    },
    connect: vi.fn(() => ({ connect: vi.fn() })),
  }));
  createBuffer = vi.fn(() => ({ getChannelData: () => new Float32Array(64) }));
  createBufferSource = vi.fn(() => ({
    buffer: null,
    connect: vi.fn(() => ({ connect: vi.fn(() => ({ connect: vi.fn() })) })),
    start: vi.fn(),
    stop: vi.fn(),
  }));
  createBiquadFilter = vi.fn(() => ({
    type: '',
    frequency: { value: 0, setValueAtTime: vi.fn(), exponentialRampToValueAtTime: vi.fn() },
    connect: vi.fn(() => ({ connect: vi.fn(() => ({ connect: vi.fn() })) })),
  }));
  createOscillator = vi.fn(() => ({
    type: '',
    frequency: { value: 0, setValueAtTime: vi.fn(), exponentialRampToValueAtTime: vi.fn() },
    connect: vi.fn(() => ({ connect: vi.fn() })),
    start: vi.fn(),
    stop: vi.fn(),
  }));
  destination = {};
  resume = vi.fn();
  currentTime = 0;
  sampleRate = 48000;
}

function setup(opts: { audioCtor?: unknown } = {}) {
  const win = window as unknown as { AudioContext?: unknown };
  if ('audioCtor' in opts) {
    win.AudioContext = opts.audioCtor;
  } else {
    win.AudioContext = FakeAudioContextSpy;
  }
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [{ provide: SoundService, useClass: DefaultSoundService }],
  });
  return TestBed.inject(SoundService);
}

describe('DefaultSoundService', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => {
    delete (window as unknown as { AudioContext?: unknown }).AudioContext;
  });

  it('defaults to un-muted, pack=wood', () => {
    const svc = setup();
    expect(svc.muted()).toBe(false);
    expect(svc.packName()).toBe('wood');
  });

  it('registers exactly the built-in pack list, in its order', () => {
    // Item-by-item against `PACK_NAMES` rather than three `toContain`s: the
    // service walks that object, so this is the assertion that the walk is
    // complete. Three `toContain`s pass just as happily when a fourth pack has
    // been added and forgotten — the same shape as the hand-written registry
    // fixtures this repo has had to fix three times.
    //
    // Order matters too: it is the order the header's pack menu renders.
    const svc = setup();

    expect(svc.availablePacks()).toEqual([...PACK_NAMES]);
    expect(svc.availablePacks()).toEqual(['wood', 'chiptune', 'minimal']);
  });

  it('activate(minimal) persists and survives reconstruction', () => {
    const svc = setup();
    svc.activate('minimal');
    expect(localStorage.getItem(STORAGE_PACK)).toBe('minimal');
    const next = setup();
    expect(next.packName()).toBe('minimal');
  });

  it('persists muted state to localStorage', () => {
    const svc = setup();
    svc.setMuted(true);
    expect(localStorage.getItem(STORAGE_MUTED)).toBe('1');
    expect(svc.muted()).toBe(true);
  });

  it('restores muted state on next construction', () => {
    localStorage.setItem(STORAGE_MUTED, '1');
    const svc = setup();
    expect(svc.muted()).toBe(true);
  });

  it('muted play() does not construct AudioContext', () => {
    const ctorSpy = vi.fn(() => new FakeAudioContextSpy());
    const svc = setup({ audioCtor: ctorSpy });
    svc.setMuted(true);
    svc.play('move-place');
    expect(ctorSpy).not.toHaveBeenCalled();
  });

  it('un-muted play() constructs the AudioContext lazily and only once', () => {
    const ctorSpy = vi.fn(() => new FakeAudioContextSpy());
    const svc = setup({ audioCtor: ctorSpy });
    svc.play('move-place');
    svc.play('move-place');
    svc.play('urge');
    expect(ctorSpy).toHaveBeenCalledTimes(1);
  });

  it('falls back silently when AudioContext is undefined', () => {
    const svc = setup({ audioCtor: undefined });
    expect(() => svc.play('move-place')).not.toThrow();
  });

  it('falls back silently when AudioContext construction throws', () => {
    const throwingCtor = vi.fn(() => {
      throw new Error('blocked');
    });
    const svc = setup({ audioCtor: throwingCtor });
    expect(() => svc.play('move-place')).not.toThrow();
    // After failure, subsequent plays must NOT keep retrying construction.
    svc.play('urge');
    expect(throwingCtor).toHaveBeenCalledTimes(1);
  });

  it('register() adds to availablePacks; activate() switches', () => {
    const svc = setup();
    const stubPack: SoundPack = { play: vi.fn() };
    svc.register('custom', stubPack);
    expect(svc.availablePacks()).toContain('custom');
    svc.activate('custom');
    expect(svc.packName()).toBe('custom');
    expect(localStorage.getItem(STORAGE_PACK)).toBe('custom');
  });

  it('activate() on unregistered pack is a no-op', () => {
    const svc = setup();
    svc.activate('nope');
    expect(svc.packName()).toBe('wood');
  });

  it('register() rejects an invalid pack', () => {
    const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    const svc = setup();
    svc.register('broken', {} as SoundPack);
    expect(svc.availablePacks()).not.toContain('broken');
    warnSpy.mockRestore();
  });

  it('un-muted play() routes through the active pack', () => {
    const svc = setup();
    const stubPlay = vi.fn();
    const stubPack: SoundPack = { play: stubPlay };
    svc.register('stub', stubPack);
    svc.activate('stub');
    svc.play('move-place');
    expect(stubPlay).toHaveBeenCalledTimes(1);
    expect(stubPlay.mock.calls[0][0]).toBe('move-place');
  });

  it('queues the first play() until the built-in pack has loaded', async () => {
    // **这是「按需加载」唯一真正新增的行为路径**:第一声可能在 pack 解出来之前就到了。
    // 丢掉它的表现是「这一局的第一手是静的」—— 一个只在会话的第一次出现、之后永远
    // 复现不出来的缺陷。所以它排队。
    const svc = setup();
    svc.play('move-place');

    const ctx = (svc as unknown as { ctx: FakeAudioContextSpy | null }).ctx;
    // AudioContext 是**同步**建的:autoplay 策略要的是用户手势那一帧,而 pack 的加载不在其中。
    expect(ctx, 'play() should construct the AudioContext synchronously').not.toBeNull();

    // 然后那一声要真的响 —— 用 waitFor 而不是数几个 tick:动态 import 花多少轮微任务
    // 是打包器的实现细节,而「最终会响」才是要钉的东西。
    // wood 的 move-place 是一段滤波噪声,所以至少要建一个 buffer 和一个 filter。
    await vi.waitFor(() => {
      expect(ctx!.createBuffer).toHaveBeenCalled();
      expect(ctx!.createBiquadFilter).toHaveBeenCalled();
    });
  });

  it('keeps a persisted pack across construction even before it has loaded', async () => {
    // `resolveInitialPack` 若只查已加载的实现,持久化的选择会在**每次启动**时掉回 wood ——
    // 而内置 pack 在启动那一刻一个都还没加载。
    localStorage.setItem(STORAGE_PACK, 'minimal');
    const svc = setup();
    expect(svc.packName()).toBe('minimal');
  });

  describe('volume', () => {
    it('defaults to 100', () => {
      const svc = setup();
      expect(svc.volume()).toBe(100);
    });

    it('clamps and rounds setVolume input', () => {
      const svc = setup();
      svc.setVolume(150);
      expect(svc.volume()).toBe(100);
      svc.setVolume(-5);
      expect(svc.volume()).toBe(0);
      svc.setVolume(33.7);
      expect(svc.volume()).toBe(34);
      expect(localStorage.getItem(STORAGE_VOLUME)).toBe('34');
    });

    it('restores persisted volume on next construction', () => {
      const svc = setup();
      svc.setVolume(40);
      expect(localStorage.getItem(STORAGE_VOLUME)).toBe('40');
      const next = setup();
      expect(next.volume()).toBe(40);
    });

    it.each(['abc', '-3', '999', '33.7'])(
      'falls back to 100 when localStorage holds %j',
      (garbage) => {
        localStorage.setItem(STORAGE_VOLUME, garbage);
        const svc = setup();
        expect(svc.volume()).toBe(100);
      },
    );

    it('play() at volume 0 does not construct AudioContext', () => {
      const ctorSpy = vi.fn(() => new FakeAudioContextSpy());
      const svc = setup({ audioCtor: ctorSpy });
      svc.setVolume(0);
      svc.play('move-place');
      expect(ctorSpy).not.toHaveBeenCalled();
    });

    it('applies the perceptual (squared) curve to the master gain', () => {
      const instances: FakeAudioContextSpy[] = [];
      class TrackingAudioContext extends FakeAudioContextSpy {
        constructor() {
          super();
          instances.push(this);
        }
      }
      const svc = setup({ audioCtor: TrackingAudioContext });
      svc.setVolume(50);
      svc.play('move-place'); // constructs ctx with current volume
      expect(instances).toHaveLength(1);
      const gainNode = instances[0].createGain.mock.results[0].value;
      expect(gainNode.gain.value).toBeCloseTo(0.25);
      // Live update once the context exists:
      svc.setVolume(100);
      expect(gainNode.gain.value).toBeCloseTo(1);
    });

    it('mute and volume do not interfere', () => {
      const svc = setup();
      svc.setVolume(40);
      svc.setMuted(true);
      svc.setMuted(false);
      expect(svc.volume()).toBe(40);
      svc.setVolume(70);
      expect(svc.muted()).toBe(false);
    });
  });
});
