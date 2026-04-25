import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { DefaultSoundService, SoundService } from './sound.service';
import type { SoundPack } from './sound.tokens';

const STORAGE_MUTED = 'gomoku:sound-muted';
const STORAGE_PACK = 'gomoku:sound-pack';

class FakeAudioContextSpy {
  createGain = vi.fn(() => ({
    gain: { value: 1 },
    connect: vi.fn(),
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
    svc.register('minimal', stubPack);
    expect(svc.availablePacks()).toContain('minimal');
    svc.activate('minimal');
    expect(svc.packName()).toBe('minimal');
    expect(localStorage.getItem(STORAGE_PACK)).toBe('minimal');
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
});
