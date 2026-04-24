import { DOCUMENT, inject, Injectable, OnDestroy, signal, type Signal } from '@angular/core';
import type { Observable, Subscription } from 'rxjs';
import { LeaderboardApiService } from '../api/leaderboard-api.service';
import type { LeaderboardEntry } from '../api/models/leaderboard.model';
import type { RoomSummary } from '../api/models/room.model';
import { PresenceApiService } from '../api/presence-api.service';
import { RoomsApiService } from '../api/rooms-api.service';
import { LOBBY_POLLING_CONFIG } from './lobby-polling.config';

const LEADERBOARD_SIZE = 10;

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

export abstract class LobbyDataService {
  abstract readonly onlineCount: LobbySlice<number>;
  abstract readonly rooms: LobbySlice<readonly RoomSummary[]>;
  abstract readonly myRooms: LobbySlice<readonly RoomSummary[]>;
  abstract readonly leaderboard: LobbySlice<readonly LeaderboardEntry[]>;
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

@Injectable()
export class DefaultLobbyDataService extends LobbyDataService implements OnDestroy {
  private readonly doc = inject(DOCUMENT);
  private readonly presenceApi = inject(PresenceApiService);
  private readonly roomsApi = inject(RoomsApiService);
  private readonly leaderboardApi = inject(LeaderboardApiService);
  private readonly config = inject(LOBBY_POLLING_CONFIG);

  private readonly sliceStates: Record<string, SliceState<unknown>>;
  private readonly intervalIds = new Map<string, ReturnType<typeof setInterval>>();
  private readonly onVisibilityChange = (): void => this.handleVisibilityChange();

  readonly onlineCount: LobbySlice<number>;
  readonly rooms: LobbySlice<readonly RoomSummary[]>;
  readonly myRooms: LobbySlice<readonly RoomSummary[]>;
  readonly leaderboard: LobbySlice<readonly LeaderboardEntry[]>;

  constructor() {
    super();

    this.onlineCount = this.buildSlice<number>(
      'onlineCount',
      () => this.presenceApi.getOnlineCount(),
      this.config.onlineCountMs,
    );
    this.rooms = this.buildSlice<readonly RoomSummary[]>(
      'rooms',
      () => this.roomsApi.list(),
      this.config.roomsMs,
    );
    this.myRooms = this.buildSlice<readonly RoomSummary[]>(
      'myRooms',
      () => this.roomsApi.myActiveRooms(),
      this.config.myRoomsMs,
    );
    this.leaderboard = this.buildSlice<readonly LeaderboardEntry[]>(
      'leaderboard',
      () => this.leaderboardApi.top(LEADERBOARD_SIZE),
      null,
    );

    this.sliceStates = {
      onlineCount: this.onlineCount as unknown as SliceState<unknown>,
      rooms: this.rooms as unknown as SliceState<unknown>,
      myRooms: this.myRooms as unknown as SliceState<unknown>,
      leaderboard: this.leaderboard as unknown as SliceState<unknown>,
    };

    // Kick off the initial fetch for every slice + start intervals for those
    // that have one. Leaderboard has no interval — fetched once on mount.
    for (const key of Object.keys(this.sliceStates)) {
      const state = this.sliceStates[key];
      this.performFetch(state);
      if (state.intervalMs !== null && state.intervalMs > 0) {
        const id = setInterval(() => this.onTick(state), state.intervalMs);
        this.intervalIds.set(key, id);
      }
    }

    this.doc.defaultView?.addEventListener('visibilitychange', this.onVisibilityChange);
  }

  ngOnDestroy(): void {
    for (const id of this.intervalIds.values()) {
      clearInterval(id);
    }
    this.intervalIds.clear();
    this.doc.defaultView?.removeEventListener('visibilitychange', this.onVisibilityChange);
    for (const state of Object.values(this.sliceStates)) {
      state.inFlight?.unsubscribe();
      state.inFlight = null;
    }
  }

  private buildSlice<T>(
    key: string,
    fetch: () => Observable<T>,
    intervalMs: number | null,
  ): LobbySlice<T> & SliceState<T> {
    const data = signal<T | null>(null);
    const loading = signal<boolean>(false);
    const error = signal<unknown | null>(null);
    const slice: LobbySlice<T> & SliceState<T> = {
      data,
      loading,
      error,
      lastSuccessAt: null,
      inFlight: null,
      fetch,
      intervalMs,
      refresh: () => this.performFetch(slice as unknown as SliceState<unknown>),
    };
    return slice;
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
    for (const state of Object.values(this.sliceStates)) {
      if (state.intervalMs === null) continue; // unpolled slices aren't "stale"
      const halfInterval = state.intervalMs / 2;
      const stale = state.lastSuccessAt === null || now - state.lastSuccessAt > halfInterval;
      if (stale) {
        this.performFetch(state);
      }
    }
  }
}
