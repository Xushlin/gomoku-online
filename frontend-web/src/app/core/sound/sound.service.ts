import { DOCUMENT, inject, Injectable, signal, type Signal } from '@angular/core';
import { PACK_LOADERS } from './packs';
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
 * `PACK_LOADERS`. Components stay untouched — they emit the same closed set of
 * `SoundEventName`s, packs decide what those events sound like.
 *
 * **The built-in packs are loaded on demand** (see `packs/index.ts`: they were
 * 8.69 kB of the initial bundle, for audio that cannot play before the first user
 * gesture). The active one is warmed at construction without awaiting; a `play()`
 * that still arrives first is queued rather than dropped, because a silent first
 * move is a defect and a slightly late one is not. `register()` stays synchronous —
 * an external pack hands over an object, not a loader.
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
  private readonly loaders = new Map<string, () => Promise<SoundPack>>();
  private readonly loading = new Map<string, Promise<SoundPack | null>>();

  readonly muted: Signal<boolean> = this._muted.asReadonly();
  readonly volume: Signal<number> = this._volume.asReadonly();
  readonly packName: Signal<string> = this._packName.asReadonly();

  private ctx: AudioContext | null = null;
  private masterGain: GainNode | null = null;
  private contextFailed = false;

  constructor() {
    super();
    // One list, walked — not three calls that a test fixture then re-writes by hand.
    for (const [name, load] of Object.entries(PACK_LOADERS)) {
      this.loaders.set(name, load);
    }

    this._muted.set(this.readMuted());
    this._volume.set(this.resolveInitialVolume());
    const initialPack = this.resolveInitialPack();
    this._packName.set(initialPack);
    // 预热,不 await:让第一声在事件到达之前就已经就位,而不把它放进启动的关键路径。
    void this.resolvePack(initialPack);
  }

  play(event: SoundEventName): void {
    // Volume 0 is a de-facto mute — skip graph construction just like muted.
    if (this._muted() || this._volume() === 0) return;
    const ctx = this.ensureContext();
    if (!ctx || !this.masterGain) return;
    const name = this._packName();
    const pack = this.packs.get(name);
    if (pack) {
      this.render(pack, event, ctx, this.masterGain);
      return;
    }
    // 还没加载完:排队,而不是丢掉。丢掉的表现是「这一局的第一手是静的」。
    const gain = this.masterGain;
    void this.resolvePack(name).then((loaded) => {
      if (loaded) this.render(loaded, event, ctx, gain);
    });
  }

  private render(pack: SoundPack, event: SoundEventName, ctx: AudioContext, gain: GainNode): void {
    try {
      pack.play(event, ctx, gain);
    } catch {
      // Broken pack should not crash the app.
    }
  }

  /**
   * 解出一个 pack 的实现,并缓存。同一个 pack 并发请求只加载一次。
   *
   * 加载失败(chunk 拉不到)吞掉并返回 `null` —— 与 `AudioContext` 构造失败同一条:
   * 没有声音是可以接受的降级,而白屏不是。
   */
  private async resolvePack(name: string): Promise<SoundPack | null> {
    const cached = this.packs.get(name);
    if (cached) return cached;
    const inFlight = this.loading.get(name);
    if (inFlight) return inFlight;
    const load = this.loaders.get(name);
    if (!load) return null;
    const promise = load()
      .then((pack) => {
        this.packs.set(name, pack);
        return pack;
      })
      .catch(() => {
        this.warn(`pack '${name}' failed to load; staying silent.`);
        return null;
      })
      .finally(() => {
        this.loading.delete(name);
      });
    this.loading.set(name, promise);
    return promise;
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
    // 「已知」= 已注册的实现,或者有一个 loader —— 内置 pack 在第一次响之前是后者。
    if (!this.packs.has(name) && !this.loaders.has(name)) {
      this.warn(`activate('${name}'): pack not registered; ignoring.`);
      return;
    }
    this._packName.set(name);
    this.persist(PACK_STORAGE_KEY, name);
    void this.resolvePack(name);
  }

  availablePacks(): readonly string[] {
    // loader 在前(内置的顺序就是 header 菜单的顺序),外部 register 的接在后面。
    const names = new Set<string>([...this.loaders.keys(), ...this.packs.keys()]);
    return Array.from(names);
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
    // 已知 = 有实现或有 loader。**只查 `packs` 的那一版会让持久化的选择在启动时失效** ——
    // 内置 pack 此时还没加载,于是每次启动都掉回默认的 wood。
    if (stored && (this.packs.has(stored) || this.loaders.has(stored))) return stored;
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
