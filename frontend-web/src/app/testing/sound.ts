import { signal, type WritableSignal } from '@angular/core';
import { vi, type Mock } from 'vitest';
import type { SoundService } from '../core/sound/sound.service';
import type { SoundEventName, SoundPack } from '../core/sound/sound.tokens';

/**
 * A `SoundService` test double, **bound to the contract**.
 *
 * `extends SoundService` is the whole point. Angular's `useValue` is typed `any`,
 * so the three hand-written stubs this replaces were unchecked object literals —
 * and one of them proved it: `room-page.spec.ts` had no `volume` and no
 * `setVolume`, missing ever since the volume slider was added, and nothing
 * complained. Same defect `StubHub` had before it was made to `implements
 * GameHubService`. Now a new abstract member on `SoundService` fails to compile
 * here, once, instead of being silently absent in every spec.
 *
 * The signals are writable so a test can flip mute or volume; `Signal` accepts a
 * `WritableSignal`, so the component still sees exactly the declared type.
 */
export interface StubSoundService extends SoundService {
  readonly muted: WritableSignal<boolean>;
  readonly volume: WritableSignal<number>;
  readonly packName: WritableSignal<string>;
  readonly play: Mock<(event: SoundEventName) => void>;
  readonly setMuted: Mock<(muted: boolean) => void>;
  readonly setVolume: Mock<(volume: number) => void>;
  readonly register: Mock<(name: string, pack: SoundPack) => void>;
  readonly activate: Mock<(name: string) => void>;
}

export function stubSoundService(
  opts: { muted?: boolean; volume?: number; pack?: string; packs?: readonly string[] } = {},
): StubSoundService {
  const packs = opts.packs ?? ['wood', 'chiptune', 'minimal'];
  return {
    muted: signal(opts.muted ?? false),
    volume: signal(opts.volume ?? 100),
    packName: signal(opts.pack ?? 'wood'),
    play: vi.fn<(event: SoundEventName) => void>(),
    setMuted: vi.fn<(muted: boolean) => void>(),
    setVolume: vi.fn<(volume: number) => void>(),
    register: vi.fn<(name: string, pack: SoundPack) => void>(),
    activate: vi.fn<(name: string) => void>(),
    availablePacks: () => packs,
  };
}

/** Every event played so far, in order — the readable form for assertions. */
export function playedEvents(sound: StubSoundService): SoundEventName[] {
  return sound.play.mock.calls.map(([event]) => event);
}
