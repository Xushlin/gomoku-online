import { inject, Injectable, InjectionToken, signal, type Signal } from '@angular/core';
import { Observable, Subject } from 'rxjs';
import type {
  ChatChannel,
  ChatMessage,
  GameEndedDto,
  GameSnapshot,
  MoveDto,
  RoomState,
  UrgeDto,
  UserSummary,
} from '../api/models/room.model';
import { AuthService } from '../auth/auth.service';

export type ConnectionStatus = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

export interface RoomDissolvedDto {
  readonly roomId: string;
}

// A tiny subset of the `@microsoft/signalr` surface that we actually use. The
// real types are pulled in dynamically by `loadSignalR()` so the SignalR
// package is not in the main bundle — only the room-page lazy chunk that
// calls into this service triggers its download.
interface SignalRHubConnection {
  readonly state: string;
  start(): Promise<void>;
  stop(): Promise<void>;
  invoke(method: string, ...args: unknown[]): Promise<unknown>;
  on(event: string, handler: (...args: never[]) => void): void;
  onreconnecting(cb: (err?: Error) => void): void;
  onreconnected(cb: (connectionId?: string) => void): void;
  onclose(cb: (err?: Error) => void): void;
}

interface SignalRModule {
  readonly HubConnectionBuilder: new () => SignalRHubConnectionBuilder;
  readonly HubConnectionState: Record<string, string>;
  readonly LogLevel: Record<string, number>;
}

interface SignalRHubConnectionBuilder {
  withUrl(url: string, options: { accessTokenFactory: () => string }): SignalRHubConnectionBuilder;
  withAutomaticReconnect(retryDelays: readonly number[]): SignalRHubConnectionBuilder;
  configureLogging(level: number): SignalRHubConnectionBuilder;
  build(): SignalRHubConnection;
}

/**
 * Abstract DI token for the real-time game hub. Consumers MUST `inject(GameHubService)`
 * and never the default implementation directly, so tests can swap a stub.
 */
export abstract class GameHubService {
  abstract readonly state: Signal<RoomState | null>;
  abstract readonly connectionStatus: Signal<ConnectionStatus>;
  abstract readonly gameEnded: Signal<GameEndedDto | null>;
  abstract readonly urged$: Observable<UrgeDto>;
  abstract readonly roomDissolved$: Observable<RoomDissolvedDto>;

  abstract joinRoom(roomId: string): Promise<void>;
  abstract leaveRoom(roomId: string): Promise<void>;
  abstract joinSpectatorGroup(roomId: string): Promise<void>;
  abstract makeMove(roomId: string, row: number, col: number): Promise<void>;
  abstract sendChat(roomId: string, content: string, channel: ChatChannel): Promise<void>;
  abstract urge(roomId: string): Promise<void>;

  abstract applySnapshot(state: RoomState): void;
  abstract reconnect(): Promise<void>;
}

/**
 * Test seam — specs override this DI token to return a stub SignalR module
 * so they don't need the real WebSocket stack.
 */
export type SignalRLoader = () => Promise<SignalRModule>;

export const SIGNALR_LOADER = new InjectionToken<SignalRLoader>('SIGNALR_LOADER', {
  providedIn: 'root',
  factory: () => async (): Promise<SignalRModule> => {
    const mod = await import('@microsoft/signalr');
    return mod as unknown as SignalRModule;
  },
});

export const GAME_HUB_URL = '/hubs/gomoku';

@Injectable()
export class DefaultGameHubService extends GameHubService {
  private readonly auth = inject(AuthService);
  private readonly loader = inject(SIGNALR_LOADER);

  private readonly _state = signal<RoomState | null>(null);
  private readonly _connectionStatus = signal<ConnectionStatus>('disconnected');
  private readonly _gameEnded = signal<GameEndedDto | null>(null);
  private readonly _urged = new Subject<UrgeDto>();
  private readonly _roomDissolved = new Subject<RoomDissolvedDto>();

  readonly state = this._state.asReadonly();
  readonly connectionStatus = this._connectionStatus.asReadonly();
  readonly gameEnded = this._gameEnded.asReadonly();
  readonly urged$: Observable<UrgeDto> = this._urged.asObservable();
  readonly roomDissolved$: Observable<RoomDissolvedDto> = this._roomDissolved.asObservable();

  private connection: SignalRHubConnection | null = null;
  private startPromise: Promise<void> | null = null;
  private signalRModule: SignalRModule | null = null;
  private lastAppliedPly: number | null = null;

  private async ensureConnected(): Promise<SignalRHubConnection> {
    const mod = this.signalRModule ?? (this.signalRModule = await this.loader());
    const conn = this.connection ?? this.build(mod);
    if (conn.state === mod.HubConnectionState['Connected']) return conn;
    if (this.startPromise) {
      await this.startPromise;
      return conn;
    }
    this._connectionStatus.set('connecting');
    this.startPromise = conn
      .start()
      .then(() => {
        this._connectionStatus.set('connected');
      })
      .catch((err) => {
        this._connectionStatus.set('disconnected');
        throw err;
      })
      .finally(() => {
        this.startPromise = null;
      });
    await this.startPromise;
    return conn;
  }

  private build(mod: SignalRModule): SignalRHubConnection {
    const conn = new mod.HubConnectionBuilder()
      .withUrl(GAME_HUB_URL, { accessTokenFactory: () => this.auth.accessToken() ?? '' })
      .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 30_000])
      .configureLogging(mod.LogLevel['Warning'])
      .build();
    this.registerHandlers(conn);
    this.connection = conn;
    return conn;
  }

  private registerHandlers(conn: SignalRHubConnection): void {
    conn.on('RoomState', ((state: RoomState) => this.handleRoomState(state)) as never);
    conn.on('PlayerJoined', ((user: UserSummary) => this.handlePlayerJoined(user)) as never);
    conn.on('PlayerLeft', ((user: UserSummary) => this.handlePlayerLeft(user)) as never);
    conn.on('SpectatorJoined', ((user: UserSummary) =>
      this.handleSpectatorJoined(user)) as never);
    conn.on('SpectatorLeft', ((user: UserSummary) => this.handleSpectatorLeft(user)) as never);
    conn.on('MoveMade', ((move: MoveDto) => this.handleMoveMade(move)) as never);
    conn.on('GameEnded', ((payload: GameEndedDto) => this.handleGameEnded(payload)) as never);
    conn.on('ChatMessage', ((message: ChatMessage) => this.handleChatMessage(message)) as never);
    conn.on('UrgeReceived', ((payload: UrgeDto) => this._urged.next(payload)) as never);
    conn.on('RoomDissolved', ((payload: RoomDissolvedDto) =>
      this._roomDissolved.next({ roomId: payload.roomId })) as never);
    conn.onreconnecting(() => this._connectionStatus.set('reconnecting'));
    conn.onreconnected(() => this._connectionStatus.set('connected'));
    conn.onclose(() => this._connectionStatus.set('disconnected'));
  }

  async joinRoom(roomId: string): Promise<void> {
    const conn = await this.ensureConnected();
    await conn.invoke('JoinRoom', roomId);
  }

  async leaveRoom(roomId: string): Promise<void> {
    const connected =
      this.connection !== null &&
      this.signalRModule !== null &&
      this.connection.state === this.signalRModule.HubConnectionState['Connected'];
    if (!connected) {
      this._gameEnded.set(null);
      return;
    }
    try {
      await this.connection!.invoke('LeaveRoom', roomId);
    } finally {
      this._gameEnded.set(null);
    }
  }

  async joinSpectatorGroup(roomId: string): Promise<void> {
    const conn = await this.ensureConnected();
    await conn.invoke('JoinSpectatorGroup', roomId);
  }

  async makeMove(roomId: string, row: number, col: number): Promise<void> {
    const conn = await this.ensureConnected();
    await conn.invoke('MakeMove', roomId, row, col);
  }

  async sendChat(roomId: string, content: string, channel: ChatChannel): Promise<void> {
    const conn = await this.ensureConnected();
    await conn.invoke('SendChat', roomId, content, channel);
  }

  async urge(roomId: string): Promise<void> {
    const conn = await this.ensureConnected();
    await conn.invoke('Urge', roomId);
  }

  applySnapshot(state: RoomState): void {
    this._state.set(state);
    const moves = state.game?.moves ?? [];
    this.lastAppliedPly = moves.length > 0 ? moves[moves.length - 1].ply : null;
  }

  async reconnect(): Promise<void> {
    const connected =
      this.connection !== null &&
      this.signalRModule !== null &&
      this.connection.state === this.signalRModule.HubConnectionState['Connected'];
    if (connected) return;
    await this.ensureConnected();
  }

  // ---- event handlers ----

  private handleRoomState(state: RoomState): void {
    this._state.set(state);
    const moves = state.game?.moves ?? [];
    this.lastAppliedPly = moves.length > 0 ? moves[moves.length - 1].ply : null;
  }

  private handleMoveMade(move: MoveDto): void {
    if (this.lastAppliedPly !== null && move.ply <= this.lastAppliedPly) return;
    const current = this._state();
    if (!current || !current.game) {
      this.lastAppliedPly = move.ply;
      return;
    }
    const moves = [...current.game.moves, move].sort((a, b) => a.ply - b.ply);
    const nextTurn: GameSnapshot['currentTurn'] = move.stone === 'Black' ? 'White' : 'Black';
    const nextGame: GameSnapshot = {
      ...current.game,
      moves,
      currentTurn: nextTurn,
      turnStartedAt: move.playedAt,
    };
    this._state.set({ ...current, game: nextGame });
    this.lastAppliedPly = move.ply;
  }

  private handleGameEnded(payload: GameEndedDto): void {
    this._gameEnded.set(payload);
    const current = this._state();
    if (current) {
      const nextGame: GameSnapshot | null = current.game
        ? {
            ...current.game,
            endedAt: payload.endedAt,
            result: payload.result,
            winnerUserId: payload.winnerUserId,
            endReason: payload.endReason,
          }
        : null;
      this._state.set({ ...current, status: 'Finished', game: nextGame });
    }
  }

  private handleChatMessage(message: ChatMessage): void {
    const current = this._state();
    if (!current) return;
    if (current.chatMessages.some((m) => m.id === message.id)) return;
    this._state.set({
      ...current,
      chatMessages: [...current.chatMessages, message],
    });
  }

  private handlePlayerJoined(user: UserSummary): void {
    const current = this._state();
    if (!current) return;
    if (!current.black) {
      this._state.set({ ...current, black: user });
      return;
    }
    if (!current.white) {
      this._state.set({ ...current, white: user });
    }
  }

  private handlePlayerLeft(user: UserSummary): void {
    const current = this._state();
    if (!current) return;
    const blackLeft = current.black?.id === user.id;
    const whiteLeft = current.white?.id === user.id;
    if (!blackLeft && !whiteLeft) return;
    this._state.set({
      ...current,
      black: blackLeft ? null : current.black,
      white: whiteLeft ? null : current.white,
    });
  }

  private handleSpectatorJoined(user: UserSummary): void {
    const current = this._state();
    if (!current) return;
    if (current.spectators.some((s) => s.id === user.id)) return;
    this._state.set({ ...current, spectators: [...current.spectators, user] });
  }

  private handleSpectatorLeft(user: UserSummary): void {
    const current = this._state();
    if (!current) return;
    const next = current.spectators.filter((s) => s.id !== user.id);
    if (next.length !== current.spectators.length) {
      this._state.set({ ...current, spectators: next });
    }
  }
}
