import { DOCUMENT, inject, Injectable, signal, type Signal } from '@angular/core';
import { BUILT_IN_PACKS } from './packs';
import type { SoundEventName, SoundPack } from './sound.tokens';

const MUTED_STORAGE_KEY = 'gewu:sound-muted';
const PACK_STORAGE_KEY = 'gewu:sound-pack';
const VOLUME_STORAGE_KEY = 'gewu:sound-volume';
const DEFAULT_PACK = 'wood';
const DEFAULT_VOLUME = 100;

/**
 * Cross-cutting audio API. Three orthogonal pieces of state:
 *   - muted:    whether all sounds are silenced (early return in play()).
 *   - volume:   master loudness 0–100; rides the shared GainNode. Kept
 *               separate from `muted` so un-muting restores the previous
 *               level without any "remembered volume" bookkeeping.
 *   - packName: which registered pack is currently active.
 *
 * The actual audio graph is lazy: `AudioContext` and the master `GainNode`
 * are constructed on the first audible `play()` call (browser autoplay
 * policy demands a user-gesture, and any `play()` invocation is necessarily
 * downstream of one). Construction failure (jsdom, --no-audio flag, locked-
 * down browser) is silently absorbed; subsequent `play()`s are no-ops.
 *
 * Adding a new sound pack = a new TS file under `packs/` + one entry in
 * `BUILT_IN_PACKS`. Components stay untouched — they emit the same closed set of
 * `SoundEventName`s, packs decide what those events sound like.
 */
export abstract class SoundService {
  abstract readonly muted: Signal<boolean>;
  abstract readonly volume: Signal<number>;
  abstract readonly packName: Signal<string>;
  abstract play(event: SoundEventName): void;
  abstract setMuted(muted: boolean): void;
  abstract setVolume(volume: number): void;
  abstract register(name: string, pack: SoundPack): void;
  abstract activate(name: string): void;
  abstract availablePacks(): readonly string[];
}

type AudioContextCtor = new () => AudioContext;

@Injectable()
export class DefaultSoundService extends SoundService {
  private readonly doc = inject(DOCUMENT);

  private readonly _muted = signal<boolean>(false);
  private readonly _volume = signal<number>(DEFAULT_VOLUME);
  private readonly _packName = signal<string>(DEFAULT_PACK);
  private readonly packs = new Map<string, SoundPack>();

  readonly muted: Signal<boolean> = this._muted.asReadonly();
  readonly volume: Signal<number> = this._volume.asReadonly();
  readonly packName: Signal<string> = this._packName.asReadonly();

  private ctx: AudioContext | null = null;
  private masterGain: GainNode | null = null;
  private contextFailed = false;

  constructor() {
    super();
    // One list, walked — not three calls that a test fixture then re-writes by hand.
    for (const [name, pack] of Object.entries(BUILT_IN_PACKS)) {
      this.register(name, pack);
    }

    this._muted.set(this.readMuted());
    this._volume.set(this.resolveInitialVolume());
    const initialPack = this.resolveInitialPack();
    this._packName.set(initialPack);
  }

  play(event: SoundEventName): void {
    // Volume 0 is a de-facto mute — skip graph construction just like muted.
    if (this._muted() || this._volume() === 0) return;
    const ctx = this.ensureContext();
    if (!ctx || !this.masterGain) return;
    const pack = this.packs.get(this._packName());
    if (!pack) return;
    try {
      pack.play(event, ctx, this.masterGain);
    } catch {
      // Broken pack should not crash the app.
    }
  }

  setMuted(muted: boolean): void {
    this._muted.set(muted);
    this.persist(MUTED_STORAGE_KEY, muted ? '1' : '0');
  }

  setVolume(volume: number): void {
    const clamped = Math.round(Math.min(100, Math.max(0, volume)));
    if (!Number.isFinite(clamped)) return;
    this._volume.set(clamped);
    this.persist(VOLUME_STORAGE_KEY, String(clamped));
    if (this.masterGain) {
      this.masterGain.gain.value = this.gainFor(clamped);
    }
  }

  register(name: string, pack: SoundPack): void {
    if (!pack || typeof pack.play !== 'function') {
      this.warn(`register('${name}'): pack missing required play() method.`);
      return;
    }
    this.packs.set(name, pack);
  }

  activate(name: string): void {
    if (!this.packs.has(name)) {
      this.warn(`activate('${name}'): pack not registered; ignoring.`);
      return;
    }
    this._packName.set(name);
    this.persist(PACK_STORAGE_KEY, name);
  }

  availablePacks(): readonly string[] {
    return Array.from(this.packs.keys());
  }

  private ensureContext(): AudioContext | null {
    if (this.contextFailed) return null;
    if (this.ctx) return this.ctx;
    const win = this.doc.defaultView as (Window & { AudioContext?: AudioContextCtor }) | null;
    const Ctor = win?.AudioContext;
    if (!Ctor) {
      this.contextFailed = true;
      return null;
    }
    try {
      const ctx = new Ctor();
      const gain = ctx.createGain();
      gain.gain.value = this.gainFor(this._volume());
      gain.connect(ctx.destination);
      this.ctx = ctx;
      this.masterGain = gain;
      // Autoplay-policy resume; fire-and-forget. The first sound after a
      // muted→unmuted toggle without a fresh user gesture may be silent —
      // accepted v1 limitation.
      void ctx.resume?.();
      return ctx;
    } catch {
      this.contextFailed = true;
      return null;
    }
  }

  private resolveInitialPack(): string {
    const stored = this.read(PACK_STORAGE_KEY);
    if (stored && this.packs.has(stored)) return stored;
    if (stored) this.persist(PACK_STORAGE_KEY, DEFAULT_PACK);
    return DEFAULT_PACK;
  }

  private resolveInitialVolume(): number {
    const stored = this.read(VOLUME_STORAGE_KEY);
    if (stored === null) return DEFAULT_VOLUME;
    const parsed = Number(stored);
    if (!Number.isInteger(parsed) || parsed < 0 || parsed > 100) return DEFAULT_VOLUME;
    return parsed;
  }

  /**
   * Perceptual loudness curve: human hearing is roughly logarithmic, so a
   * linear slider→amplitude map crams all audible change into the top of the
   * slider. The squared curve is the standard cheap approximation; 100 → 1
   * keeps the historical loudness for untouched sliders.
   */
  private gainFor(volume: number): number {
    return (volume / 100) ** 2;
  }

  private readMuted(): boolean {
    return this.read(MUTED_STORAGE_KEY) === '1';
  }

  private read(key: string): string | null {
    try {
      return this.doc.defaultView?.localStorage.getItem(key) ?? null;
    } catch {
      return null;
    }
  }

  private persist(key: string, value: string): void {
    try {
      this.doc.defaultView?.localStorage.setItem(key, value);
    } catch {
      // best-effort
    }
  }

  private warn(message: string): void {
    if (typeof console !== 'undefined' && typeof console.warn === 'function') {
      console.warn(`[SoundService] ${message}`);
    }
  }
}
