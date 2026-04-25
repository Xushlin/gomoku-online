import { DOCUMENT, inject, Injectable, signal, type Signal } from '@angular/core';
import { woodPack } from './packs/wood';
import type { SoundEventName, SoundPack } from './sound.tokens';

const MUTED_STORAGE_KEY = 'gomoku:sound-muted';
const PACK_STORAGE_KEY = 'gomoku:sound-pack';
const DEFAULT_PACK = 'wood';

/**
 * Cross-cutting audio API. Two orthogonal pieces of state:
 *   - muted:    whether all sounds are silenced (master gain → 0).
 *   - packName: which registered pack is currently active.
 *
 * The actual audio graph is lazy: `AudioContext` and the master `GainNode`
 * are constructed on the first non-muted `play()` call (browser autoplay
 * policy demands a user-gesture, and any `play()` invocation is necessarily
 * downstream of one). Construction failure (jsdom, --no-audio flag, locked-
 * down browser) is silently absorbed; subsequent `play()`s are no-ops.
 *
 * Adding a new sound pack = a new TS file under `packs/` + a `register(...)`
 * call here. Components stay untouched — they emit the same closed set of
 * `SoundEventName`s, packs decide what those events sound like.
 */
export abstract class SoundService {
  abstract readonly muted: Signal<boolean>;
  abstract readonly packName: Signal<string>;
  abstract play(event: SoundEventName): void;
  abstract setMuted(muted: boolean): void;
  abstract register(name: string, pack: SoundPack): void;
  abstract activate(name: string): void;
  abstract availablePacks(): readonly string[];
}

type AudioContextCtor = new () => AudioContext;

@Injectable()
export class DefaultSoundService extends SoundService {
  private readonly doc = inject(DOCUMENT);

  private readonly _muted = signal<boolean>(false);
  private readonly _packName = signal<string>(DEFAULT_PACK);
  private readonly packs = new Map<string, SoundPack>();

  readonly muted: Signal<boolean> = this._muted.asReadonly();
  readonly packName: Signal<string> = this._packName.asReadonly();

  private ctx: AudioContext | null = null;
  private masterGain: GainNode | null = null;
  private contextFailed = false;

  constructor() {
    super();
    this.register(DEFAULT_PACK, woodPack);

    this._muted.set(this.readMuted());
    const initialPack = this.resolveInitialPack();
    this._packName.set(initialPack);
  }

  play(event: SoundEventName): void {
    if (this._muted()) return;
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
      gain.gain.value = 1;
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
