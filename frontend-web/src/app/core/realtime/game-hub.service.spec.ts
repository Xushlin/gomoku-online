import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { describe, expect, it, vi } from 'vitest';
import type {
  ChatMessage,
  GameEndedDto,
  MoveDto,
  RoomState,
  UrgeDto,
} from '../api/models/room.model';
import { AuthService } from '../auth/auth.service';
import {
  DefaultGameHubService,
  GameHubService,
  SIGNALR_LOADER,
} from './game-hub.service';

type HandlerMap = Record<string, (arg: unknown) => void>;

class StubConnection {
  state = 'Disconnected';
  readonly handlers: HandlerMap = {};
  reconnectingCb: (() => void) | null = null;
  reconnectedCb: (() => void) | null = null;
  closeCb: (() => void) | null = null;
  readonly invoke = vi.fn(async () => undefined);
  readonly start = vi.fn(async () => {
    this.state = 'Connected';
  });
  readonly stop = vi.fn(async () => {
    this.state = 'Disconnected';
  });
  on(event: string, handler: (arg: unknown) => void): void {
    this.handlers[event] = handler;
  }
  onreconnecting(cb: () => void): void {
    this.reconnectingCb = cb;
  }
  onreconnected(cb: () => void): void {
    this.reconnectedCb = cb;
  }
  onclose(cb: () => void): void {
    this.closeCb = cb;
  }
}

function makeSnapshot(overrides: Partial<RoomState> = {}): RoomState {
  return {
    id: 'r-1',
    name: 'Alice room',
    status: 'Playing',
    host: { id: 'u-1', username: 'alice' },
    black: { id: 'u-1', username: 'alice' },
    white: { id: 'u-2', username: 'bob' },
    spectators: [],
    game: {
      id: 'g-1',
      currentTurn: 'Black',
      startedAt: '2026-04-24T00:00:00Z',
      endedAt: null,
      result: null,
      winnerUserId: null,
      endReason: null,
      turnStartedAt: '2026-04-24T00:00:00Z',
      turnTimeoutSeconds: 60,
      moves: [],
    },
    chatMessages: [],
    createdAt: '2026-04-24T00:00:00Z',
    ...overrides,
  };
}

function setup() {
  const conn = new StubConnection();
  const stubModule = {
    HubConnectionBuilder: class {
      withUrl() {
        return this;
      }
      withAutomaticReconnect() {
        return this;
      }
      configureLogging() {
        return this;
      }
      build() {
        return conn;
      }
    },
    HubConnectionState: { Connected: 'Connected', Disconnected: 'Disconnected' },
    LogLevel: { Warning: 3 },
  };
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      { provide: GameHubService, useClass: DefaultGameHubService },
      { provide: SIGNALR_LOADER, useValue: () => Promise.resolve(stubModule) },
      {
        provide: AuthService,
        useValue: {
          accessToken: signal('jwt-token'),
          user: signal({ id: 'u-1', username: 'alice', email: 'a@a' }),
          isAuthenticated: signal(true),
        },
      },
    ],
  });

  const svc = TestBed.inject(GameHubService);
  return { svc, conn, stubModule };
}

describe('DefaultGameHubService', () => {
  it('joinRoom() lazily starts the connection exactly once', async () => {
    const { svc, conn } = setup();
    await svc.joinRoom('r-1');
    expect(conn.start).toHaveBeenCalledTimes(1);
    await svc.joinRoom('r-2');
    expect(conn.start).toHaveBeenCalledTimes(1);
    expect(conn.invoke).toHaveBeenCalledWith('JoinRoom', 'r-1');
    expect(conn.invoke).toHaveBeenCalledWith('JoinRoom', 'r-2');
  });

  it('RoomState event replaces state wholesale', async () => {
    const { svc, conn } = setup();
    await svc.joinRoom('r-1');
    const snap = makeSnapshot();
    conn.handlers['RoomState']?.(snap);
    expect(svc.state()).toEqual(snap);
  });

  it('MoveMade appends the move and flips the turn', async () => {
    const { svc, conn } = setup();
    await svc.joinRoom('r-1');
    conn.handlers['RoomState']?.(makeSnapshot());
    const move: MoveDto = {
      ply: 1,
      row: 7,
      col: 7,
      stone: 'Black',
      playedAt: '2026-04-24T00:01:00Z',
    };
    conn.handlers['MoveMade']?.(move);
    expect(svc.state()?.game?.moves.length).toBe(1);
    expect(svc.state()?.game?.currentTurn).toBe('White');
    expect(svc.state()?.game?.turnStartedAt).toBe(move.playedAt);
  });

  it('MoveMade with old ply is dropped', async () => {
    const { svc, conn } = setup();
    await svc.joinRoom('r-1');
    conn.handlers['RoomState']?.(
      makeSnapshot({
        game: {
          ...makeSnapshot().game!,
          moves: [
            {
              ply: 5,
              row: 0,
              col: 0,
              stone: 'Black',
              playedAt: '2026-04-24T00:00:00Z',
            },
          ],
          currentTurn: 'White',
        },
      }),
    );
    conn.handlers['MoveMade']?.({
      ply: 3,
      row: 1,
      col: 1,
      stone: 'Black',
      playedAt: '2026-04-24T00:02:00Z',
    } satisfies MoveDto);
    expect(svc.state()?.game?.moves.length).toBe(1);
    expect(svc.state()?.game?.currentTurn).toBe('White');
  });

  it('GameEnded sets gameEnded signal and marks room Finished', async () => {
    const { svc, conn } = setup();
    await svc.joinRoom('r-1');
    conn.handlers['RoomState']?.(makeSnapshot());
    const ended: GameEndedDto = {
      result: 'BlackWin',
      winnerUserId: 'u-1',
      endedAt: '2026-04-24T00:05:00Z',
      endReason: 'Connected5',
    };
    conn.handlers['GameEnded']?.(ended);
    expect(svc.gameEnded()).toEqual(ended);
    expect(svc.state()?.status).toBe('Finished');
  });

  it('leaveRoom clears gameEnded', async () => {
    const { svc, conn } = setup();
    await svc.joinRoom('r-1');
    conn.handlers['GameEnded']?.({
      result: 'Draw',
      winnerUserId: null,
      endedAt: 'x',
      endReason: 'Connected5',
    });
    expect(svc.gameEnded()).not.toBeNull();
    await svc.leaveRoom('r-1');
    expect(svc.gameEnded()).toBeNull();
  });

  it('urged$ emits for UrgeReceived', async () => {
    const { svc, conn } = setup();
    await svc.joinRoom('r-1');
    const seen: UrgeDto[] = [];
    svc.urged$.subscribe((x) => seen.push(x));
    conn.handlers['UrgeReceived']?.({
      roomId: 'r-1',
      urgerUserId: 'u-2',
      urgedUserId: 'u-1',
      sentAt: 'x',
    } satisfies UrgeDto);
    expect(seen.length).toBe(1);
  });

  it('reconnection lifecycle updates connectionStatus', async () => {
    const { svc, conn } = setup();
    await svc.joinRoom('r-1');
    conn.reconnectingCb?.();
    expect(svc.connectionStatus()).toBe('reconnecting');
    conn.reconnectedCb?.();
    expect(svc.connectionStatus()).toBe('connected');
    conn.closeCb?.();
    expect(svc.connectionStatus()).toBe('disconnected');
  });

  it('applySnapshot replaces state and resets lastAppliedPly', async () => {
    const { svc, conn } = setup();
    await svc.joinRoom('r-1');
    const snap = makeSnapshot({
      game: {
        ...makeSnapshot().game!,
        moves: [
          {
            ply: 10,
            row: 0,
            col: 0,
            stone: 'Black',
            playedAt: '2026-04-24T00:00:00Z',
          },
        ],
      },
    });
    svc.applySnapshot(snap);
    expect(svc.state()?.game?.moves.length).toBe(1);
    // A stale MoveMade (ply 8) should be dropped after applySnapshot
    conn.handlers['MoveMade']?.({
      ply: 8,
      row: 1,
      col: 1,
      stone: 'White',
      playedAt: '2026-04-24T00:01:00Z',
    } satisfies MoveDto);
    expect(svc.state()?.game?.moves.length).toBe(1);
  });

  it('ChatMessage appends to chatMessages', async () => {
    const { svc, conn } = setup();
    await svc.joinRoom('r-1');
    conn.handlers['RoomState']?.(makeSnapshot());
    const msg: ChatMessage = {
      id: 'm-1',
      senderUserId: 'u-1',
      senderUsername: 'alice',
      content: 'hi',
      channel: 'Room',
      sentAt: 'x',
    };
    conn.handlers['ChatMessage']?.(msg);
    expect(svc.state()?.chatMessages).toEqual([msg]);
  });
});
