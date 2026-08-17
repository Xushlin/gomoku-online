import { signal, type Signal } from '@angular/core';
import type { Observable, Subscription } from 'rxjs';

/**
 * Read-only view over a single data slice. Components bind to these signals
 * and call `refresh()` when they want an immediate re-fetch (e.g. after the
 * user creates a room).
 */
export interface LobbySlice<T> {
  readonly data: Signal<T | null>;
  readonly loading: Signal<boolean>;
  readonly error: Signal<unknown | null>;
  refresh(): void;
}

interface SliceState<T> {
  readonly data: ReturnType<typeof signal<T | null>>;
  readonly loading: ReturnType<typeof signal<boolean>>;
  readonly error: ReturnType<typeof signal<unknown | null>>;
  lastSuccessAt: number | null;
  inFlight: Subscription | null;
  readonly fetch: () => Observable<T>;
  readonly intervalMs: number | null;
}

/**
 * The polling machinery behind every lobby data service: initial fetch,
 * per-slice intervals, visibility gating, half-interval catch-up, in-flight
 * dedup, and teardown.
 *
 * It lives here rather than in a service because `generalize-lobby` split the
 * lobby's four slices across two pages — `/home` polls account-scoped data,
 * `/g/:gameKey/lobby` polls game-scoped data. What differs between them is the
 * *set of slices*, never the machinery, so the machinery has exactly one
 * implementation and both services drive it.
 *
 * Not an `@Injectable` on purpose: it holds no DI of its own and is owned by
 * whichever service constructed it, which is what makes "the page dies, the
 * timers die" a structural fact rather than a convention.
 */
export class SliceEngine {
  private readonly states = new Map<string, SliceState<unknown>>();
  private readonly intervalIds = new Map<string, ReturnType<typeof setInterval>>();
  private readonly onVisibilityChange = (): void => this.handleVisibilityChange();
  private started = false;

  /** @param doc The document whose `visibilityState` gates polling. */
  constructor(private readonly doc: Document) {}

  /**
   * Register a slice. Must be called before {@link start}.
   *
   * @param key Stable identifier, used only for interval bookkeeping.
   * @param fetch Produces the request. Called once per fetch, never shared.
   * @param intervalMs Poll interval, or `null` for fetch-once slices.
   */
  add<T>(key: string, fetch: () => Observable<T>, intervalMs: number | null): LobbySlice<T> {
    const state: SliceState<T> = {
      data: signal<T | null>(null),
      loading: signal<boolean>(false),
      error: signal<unknown | null>(null),
      lastSuccessAt: null,
      inFlight: null,
      fetch,
      intervalMs,
    };
    this.states.set(key, state as unknown as SliceState<unknown>);
    return {
      data: state.data.asReadonly(),
      loading: state.loading.asReadonly(),
      error: state.error.asReadonly(),
      refresh: () => this.performFetch(state as unknown as SliceState<unknown>),
    };
  }

  /** Fire every slice's first fetch, start the intervals, listen for visibility. */
  start(): void {
    if (this.started) return;
    this.started = true;
    for (const [key, state] of this.states) {
      this.performFetch(state);
      if (state.intervalMs !== null && state.intervalMs > 0) {
        this.intervalIds.set(key, setInterval(() => this.onTick(state), state.intervalMs));
      }
    }
    this.doc.defaultView?.addEventListener('visibilitychange', this.onVisibilityChange);
  }

  /** Stop the clocks and drop any in-flight request. Idempotent. */
  teardown(): void {
    for (const id of this.intervalIds.values()) {
      clearInterval(id);
    }
    this.intervalIds.clear();
    this.doc.defaultView?.removeEventListener('visibilitychange', this.onVisibilityChange);
    for (const state of this.states.values()) {
      state.inFlight?.unsubscribe();
      state.inFlight = null;
    }
  }

  private performFetch(state: SliceState<unknown>): void {
    if (state.inFlight) return;
    state.loading.set(true);
    state.inFlight = state.fetch().subscribe({
      next: (value) => {
        state.data.set(value);
        state.error.set(null);
        state.lastSuccessAt = Date.now();
      },
      error: (err: unknown) => {
        state.error.set(err);
        state.loading.set(false);
        state.inFlight = null;
      },
      complete: () => {
        state.loading.set(false);
        state.inFlight = null;
      },
    });
  }

  private onTick(state: SliceState<unknown>): void {
    if (this.doc.visibilityState !== 'visible') return;
    this.performFetch(state);
  }

  private handleVisibilityChange(): void {
    if (this.doc.visibilityState !== 'visible') return;
    const now = Date.now();
    for (const state of this.states.values()) {
      if (state.intervalMs === null) continue; // unpolled slices aren't "stale"
      const halfInterval = state.intervalMs / 2;
      const stale = state.lastSuccessAt === null || now - state.lastSuccessAt > halfInterval;
      if (stale) {
        this.performFetch(state);
      }
    }
  }
}
