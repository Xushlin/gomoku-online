import { Dialog } from '@angular/cdk/dialog';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { of, Subject } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { RoomsApiService } from '../../../core/api/rooms-api.service';
import { AuthService } from '../../../core/auth/auth.service';
import { GameHubService } from '../../../core/realtime/game-hub.service';
import { SoundService } from '../../../core/sound/sound.service';
import { RoomPage } from './room-page';

function makeRoomState() {
  return {
    id: 'r-1',
    name: 'Alice room',
    status: 'Playing' as const,
    host: { id: 'u-1', username: 'alice' },
    black: { id: 'u-1', username: 'alice' },
    white: { id: 'u-2', username: 'bob' },
    spectators: [],
    game: {
      id: 'g-1',
      currentTurn: 'Black' as const,
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
  };
}

class StubHub {
  readonly state = signal(null as ReturnType<typeof makeRoomState> | null);
  readonly connectionStatus = signal<'connected' | 'reconnecting' | 'disconnected' | 'connecting'>(
    'connected',
  );
  readonly gameEnded = signal(null);
  readonly urged$ = new Subject();
  readonly roomDissolved$ = new Subject();
  applySnapshot = vi.fn((s: ReturnType<typeof makeRoomState>) => this.state.set(s));
  joinRoom = vi.fn(async () => undefined);
  joinSpectatorGroup = vi.fn(async () => undefined);
  leaveRoom = vi.fn(async () => undefined);
  makeMove = vi.fn(async () => undefined);
  sendChat = vi.fn(async () => undefined);
  urge = vi.fn(async () => undefined);
  reconnect = vi.fn(async () => undefined);
}

class StubRoomsApi {
  getById = vi.fn(() => of(makeRoomState()));
  leave = vi.fn(() => of(undefined));
  resign = vi.fn(() =>
    of({
      result: 'BlackWin' as const,
      winnerUserId: 'u-1',
      endedAt: 'x',
      endReason: 'Resigned' as const,
    }),
  );
}

function activatedRoute(id: string | null): ActivatedRoute {
  const paramMap = { get: (k: string) => (k === 'id' ? id : null) };
  return {
    snapshot: { paramMap },
  } as unknown as ActivatedRoute;
}

function routerStub() {
  return {
    navigate: vi.fn(() => Promise.resolve(true)),
    navigateByUrl: vi.fn(() => Promise.resolve(true)),
    createUrlTree: vi.fn(() => ({ toString: () => '/' })),
    serializeUrl: vi.fn(() => '/'),
    events: of(),
  };
}

function mount(id = 'r-1') {
  const hub = new StubHub();
  const rooms = new StubRoomsApi();
  const router = routerStub();
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      RoomPage,
      TranslocoTestingModule.forRoot({
        langs: { en: {} },
        translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: GameHubService, useValue: hub },
      {
        provide: SoundService,
        useValue: {
          play: vi.fn(),
          muted: signal(false),
          packName: signal('wood'),
          setMuted: vi.fn(),
          register: vi.fn(),
          activate: vi.fn(),
          availablePacks: () => ['wood'],
        },
      },
      { provide: RoomsApiService, useValue: rooms },
      { provide: Router, useValue: router },
      { provide: ActivatedRoute, useValue: activatedRoute(id) },
      { provide: Dialog, useValue: { open: () => ({ closed: of() }) } },
      {
        provide: AuthService,
        useValue: {
          user: signal({ id: 'u-1', username: 'alice', email: 'a@a' }),
          accessToken: signal('jwt'),
          isAuthenticated: signal(true),
        },
      },
    ],
  });
  const fixture = TestBed.createComponent(RoomPage);
  fixture.detectChanges();
  return { fixture, hub, rooms, router };
}

describe('RoomPage', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('on init: fetches room and joins hub', async () => {
    const { hub, rooms } = mount();
    // allow microtasks to flush
    await Promise.resolve();
    await Promise.resolve();
    expect(rooms.getById).toHaveBeenCalledWith('r-1');
    expect(hub.applySnapshot).toHaveBeenCalled();
    expect(hub.joinRoom).toHaveBeenCalledWith('r-1');
  });

  it('on destroy: calls leaveRoom', async () => {
    const { fixture, hub } = mount();
    await Promise.resolve();
    fixture.destroy();
    expect(hub.leaveRoom).toHaveBeenCalledWith('r-1');
  });

  it('reconnecting banner visible when status is reconnecting', async () => {
    const { fixture, hub } = mount();
    await Promise.resolve();
    hub.connectionStatus.set('reconnecting');
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    // banner renders translated key fallback (empty langs → the key itself)
    expect(text.toLowerCase()).toContain('reconnecting');
  });

  it('roomDissolved$ emission navigates home', async () => {
    const { hub, router } = mount();
    await Promise.resolve();
    hub.roomDissolved$.next({ roomId: 'r-1' });
    expect(router.navigateByUrl).toHaveBeenCalledWith('/home');
  });
});
