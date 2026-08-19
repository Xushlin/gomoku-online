import { Dialog } from '@angular/cdk/dialog';
import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { signal, type WritableSignal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { of, Subject, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { RoomsApiService } from '../../../core/api/rooms-api.service';
import { AuthService } from '../../../core/auth/auth.service';
import type {
  ChatChannel,
  GameEndedDto,
  MoveDto,
  RoomState,
  UrgeDto,
} from '../../../core/api/models/room.model';
import {
  GameHubService,
  type RoomDissolvedDto,
} from '../../../core/realtime/game-hub.service';
import {
  DefaultGameCatalogService,
  GameCatalogService,
} from '../../../games/game-catalog.service';
import { SoundService } from '../../../core/sound/sound.service';
import { playedEvents, stubSoundService } from '../../../testing/sound';
import { RoomPage } from './room-page';
import { GameCapabilitiesService } from '../../../games/game-capabilities.service';
import { StubGameCapabilities } from '../../../games/game-capabilities.stub';

function makeRoomState(): RoomState {
  return {
    id: 'r-1',
    name: 'Alice room',
    // Was missing entirely while this helper was untyped, so every test ran with
    // `gameKey: undefined` and `boardSizeFor` quietly handed back its 15x15 default
    // — which happens to be gomoku's, so nothing looked wrong.
    gameKey: 'gomoku',
    status: 'Playing' as const,
    host: { id: 'u-1', username: 'alice' },
    black: { id: 'u-1', username: 'alice' },
    white: { id: 'u-2', username: 'bob' },
    spectators: [],
    game: {
      id: 'g-1',
      currentSeat: 0,
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

/**
 * The hub double, **bound to the real contract** via `implements GameHubService`.
 *
 * It used to be a bare class, and that cost something concrete: adding `sayWord` to
 * the abstract class left this double silently incomplete, and nothing failed until
 * a test happened to walk the chain path — at runtime, not at compile time. The
 * mechanism holding it together was "every hub method has a test that calls it",
 * which is a habit rather than a check.
 *
 * Binding it was logged as blocked on `makeRoomState` returning a real `RoomState`.
 * Doing that turned up the reason it mattered: the helper had **no `gameKey` at
 * all**, so every room test in this file ran against `undefined` and got
 * `boardSizeFor`'s 15x15 fallback. That fallback is gomoku's own size, which is
 * exactly why it never looked wrong.
 *
 * Now a fifteenth abstract member cannot be added without this file failing to
 * compile.
 */
class StubHub implements GameHubService {
  readonly state: WritableSignal<RoomState | null> = signal<RoomState | null>(null);
  readonly connectionStatus = signal<
    'connected' | 'reconnecting' | 'disconnected' | 'connecting'
  >('connected');
  readonly gameEnded = signal<GameEndedDto | null>(null);
  readonly urged$ = new Subject<UrgeDto>();
  readonly roomDissolved$ = new Subject<RoomDissolvedDto>();
  // `vi.fn<T>` declares the signature without naming parameters the body ignores —
  // the type is still checked against `GameHubService`, and there is nothing for the
  // unused-args rule to complain about.
  applySnapshot = vi.fn((s: RoomState) => this.state.set(s));
  joinRoom = vi.fn<(roomId: string) => Promise<void>>(async () => undefined);
  joinSpectatorGroup = vi.fn<(roomId: string) => Promise<void>>(async () => undefined);
  leaveRoom = vi.fn<(roomId: string) => Promise<void>>(async () => undefined);
  makeMove = vi.fn<(roomId: string, row: number, col: number) => Promise<void>>(
    async () => undefined,
  );
  movePiece = vi.fn<
    (roomId: string, fromRow: number, fromCol: number, row: number, col: number) => Promise<void>
  >(async () => undefined);
  sayWord = vi.fn<(roomId: string, word: string) => Promise<void>>(async () => undefined);
  sendChat = vi.fn<(roomId: string, content: string, channel: ChatChannel) => Promise<void>>(
    async () => undefined,
  );
  urge = vi.fn<(roomId: string) => Promise<void>>(async () => undefined);
  reconnect = vi.fn<() => Promise<void>>(async () => undefined);
}

class StubRoomsApi {
  getById = vi.fn(() => of(makeRoomState()));
  leave = vi.fn(() => of(undefined));
  dissolve = vi.fn(() => of(undefined));
  resign = vi.fn(() =>
    of({
      result: 'Decided' as const,
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

const SERVER_BOARDS = () =>
  StubGameCapabilities.sized({
    gomoku: { rows: 15, cols: 15 },
    tictactoe: { rows: 3, cols: 3 },
    xiangqi: { rows: 10, cols: 9 },
  });

function mount(id = 'r-1', capabilities: GameCapabilitiesService = SERVER_BOARDS()) {
  const hub = new StubHub();
  const rooms = new StubRoomsApi();
  const router = routerStub();
  const sound = stubSoundService();
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
      { provide: GameCapabilitiesService, useValue: capabilities },
      { provide: GameCatalogService, useClass: DefaultGameCatalogService },
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: GameHubService, useValue: hub },
      { provide: SoundService, useValue: sound },
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
  return { fixture, hub, rooms, router, sound };
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

  it('roomDissolved$ emission returns to the game the room belonged to', async () => {
    const { fixture, hub, router } = mount();
    await Promise.resolve();
    hub.state.set({ ...makeRoomState(), gameKey: 'gomoku' });
    fixture.detectChanges();

    hub.roomDissolved$.next({ roomId: 'r-1' });

    expect(router.navigateByUrl).toHaveBeenCalledWith('/g/gomoku/lobby');
  });

  it('a room whose game key the client cannot resolve falls back to the platform home', async () => {
    // A server newer than this build sends a key the registry lacks, and guessing
    // `/g/<key>/lobby` would route to a "no such game" page.
    //
    // This used to lean on `makeRoomState()` having no `gameKey` at all. That was a
    // weaker version of the same test *and* the wrong scenario: the field is not
    // optional on the wire, so `undefined` is not something a server can produce.
    const { fixture, hub, router } = mount();
    await Promise.resolve();
    hub.state.set({ ...makeRoomState(), gameKey: 'a-game-nobody-registered' });
    fixture.detectChanges();

    hub.roomDissolved$.next({ roomId: 'r-1' });

    expect(router.navigateByUrl).toHaveBeenCalledWith('/home');
  });

  it('leaving a room returns to that game, not the platform home', async () => {
    const { fixture, hub, rooms, router } = mount();
    await Promise.resolve();
    hub.state.set({ ...makeRoomState(), gameKey: 'gomoku' });
    fixture.detectChanges();

    (fixture.componentInstance as unknown as { handleLeave: () => void }).handleLeave();

    expect(rooms.leave).toHaveBeenCalled();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/g/gomoku/lobby');
  });

  it('leaving an AI room of a game with no lobby returns to that game', async () => {
    // 一字棋 has no room list, but /g/tictactoe is where you start another one.
    // This case named 象棋 until `enable-xiangqi-human-play` gave it a lobby;
    // 一字棋 is the only game left on this path.
    const { fixture, hub, router } = mount();
    await Promise.resolve();
    hub.state.set({ ...makeRoomState(), gameKey: 'tictactoe' });
    fixture.detectChanges();

    (fixture.componentInstance as unknown as { handleLeave: () => void }).handleLeave();

    expect(router.navigateByUrl).toHaveBeenCalledWith('/g/tictactoe');
  });

  it('leaving a xiangqi room now returns to its lobby', async () => {
    const { fixture, hub, router } = mount();
    await Promise.resolve();
    hub.state.set({ ...makeRoomState(), gameKey: 'xiangqi' });
    fixture.detectChanges();

    (fixture.componentInstance as unknown as { handleLeave: () => void }).handleLeave();

    expect(router.navigateByUrl).toHaveBeenCalledWith('/g/xiangqi/lobby');
  });

  it('a room that vanished during a reconnect goes to the platform home', async () => {
    // This is the only 404 that navigates: `initialLoad` renders the not-found
    // panel instead. On rehydrate the room is gone, so there is no game key left
    // to read — `exitRoute()` would answer from stale state, and `/home` is the
    // honest answer.
    const { fixture, hub, rooms, router } = mount();
    await new Promise((r) => setTimeout(r, 0));
    hub.state.set({ ...makeRoomState(), gameKey: 'gomoku' });
    fixture.detectChanges();

    rooms.getById.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 404 })));
    hub.connectionStatus.set('reconnecting');
    fixture.detectChanges();
    hub.connectionStatus.set('connected');
    fixture.detectChanges();
    await new Promise((r) => setTimeout(r, 0));

    expect(router.navigateByUrl).toHaveBeenCalledWith('/home');
  });
});

/**
 * Which board a room draws.
 *
 * A two-way `@if` rather than a registry — see `RoomPage.isXiangqi` for why. These
 * tests are what makes the branch a contract instead of an implementation detail.
 */
describe('RoomPage board selection', () => {
  beforeEach(() => TestBed.resetTestingModule());

  async function mountWithGame(gameKey: string, capabilities?: GameCapabilitiesService) {
    const mounted = mount('r-1', capabilities);
    await Promise.resolve();
    await Promise.resolve();
    mounted.hub.state.set({ ...makeRoomState(), gameKey });
    mounted.fixture.detectChanges();
    return mounted;
  }

  it('draws the xiangqi board for a xiangqi room', async () => {
    const { fixture } = await mountWithGame('xiangqi');
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('app-xiangqi-board')).toBeTruthy();
    expect(el.querySelector('app-board')).toBeNull();
    expect(el.querySelectorAll('.xq-point')).toHaveLength(90);
  });

  it('sizes a tic-tac-toe room from the server descriptor', async () => {
    // 3×3 comes from GET /api/games now, not from a manifest copy of it.
    const { fixture } = await mountWithGame('tictactoe');

    expect((fixture.nativeElement as HTMLElement).querySelectorAll('.board-cell')).toHaveLength(9);
  });

  it('holds the skeleton until the descriptors arrive', async () => {
    // The client knows the size is coming. Painting 15×15 for a frame and then
    // snapping to 3×3 is worse than showing the skeleton a moment longer.
    const { fixture } = await mountWithGame('tictactoe', StubGameCapabilities.pending());
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('app-board')).toBeNull();
    expect(el.querySelector('app-xiangqi-board')).toBeNull();
    expect(el.querySelector('.animate-pulse')).toBeTruthy();
  });

  it('leaves gomoku on the 15x15 board', async () => {
    const { fixture } = await mountWithGame('gomoku');
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('app-board')).toBeTruthy();
    expect(el.querySelector('app-xiangqi-board')).toBeNull();
    expect(el.querySelectorAll('.board-cell')).toHaveLength(225);
  });

  it('falls back to the default board for a game key it has never heard of', async () => {
    // A client that has not been redeployed will meet keys its registry lacks. A
    // possibly-wrong board beats a blank page.
    const { fixture } = await mountWithGame('a-game-nobody-registered');

    expect((fixture.nativeElement as HTMLElement).querySelector('app-board')).toBeTruthy();
  });

  it('draws the chain board for a 成语接龙 room', async () => {
    const { fixture } = await mountWithGame(
      'idiom-chain',
      StubGameCapabilities.boardless('idiom-chain'),
    );
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('app-chain-board')).toBeTruthy();
    expect(el.querySelector('app-board')).toBeNull();
    expect(el.querySelector('app-xiangqi-board')).toBeNull();
  });

  it('says a word through the hub, never MakeMove or MovePiece', async () => {
    // This is also the only thing checking that `StubHub` implements `sayWord` —
    // the double is not typed against `GameHubService`, so a missing method here
    // surfaces at runtime or not at all. See the note on StubHub.
    const { fixture, hub } = await mountWithGame(
      'idiom-chain',
      StubGameCapabilities.boardless('idiom-chain'),
    );
    const el = fixture.nativeElement as HTMLElement;

    const input = el.querySelector('input[type="text"]') as HTMLInputElement;
    input.value = '一心一意';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    (el.querySelector('button[type="submit"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(hub.sayWord).toHaveBeenCalledWith('r-1', '一心一意');
    expect(hub.makeMove).not.toHaveBeenCalled();
    expect(hub.movePiece).not.toHaveBeenCalled();
  });

  it('does not paint the default 15x15 grid for a boardless game', async () => {
    // "declared boardless" and "unknown key" both leave boardSizeFor() with nothing
    // to return, and only the second should fall through to the default board.
    const { fixture } = await mountWithGame(
      'idiom-chain',
      StubGameCapabilities.boardless('idiom-chain'),
    );

    expect((fixture.nativeElement as HTMLElement).querySelectorAll('.board-cell')).toHaveLength(0);
  });

  it('sends a xiangqi move through MovePiece, never MakeMove', async () => {
    const { fixture, hub } = await mountWithGame('xiangqi');
    const points = Array.from(
      (fixture.nativeElement as HTMLElement).querySelectorAll('.xq-point'),
    ) as HTMLButtonElement[];

    points[9 * 9 + 0].click(); // pick up the red 俥 on (9,0)
    fixture.detectChanges();
    points[8 * 9 + 0].click(); // and slide it to (8,0)
    fixture.detectChanges();

    expect(hub.movePiece).toHaveBeenCalledWith('r-1', 9, 0, 8, 0);
    expect(hub.makeMove).not.toHaveBeenCalled();
  });

  describe('sound', () => {
    // 红 soldier up its file, 黑 soldier down the same one, then 红 takes it. Real
    // 象棋: soldiers capture straight ahead and 红 moves towards row 0.
    const RED_ADVANCE: MoveDto = {
      ply: 1, fromRow: 6, fromCol: 0, row: 5, col: 0, seat: 0, playedAt: 'x',
    };
    const BLACK_ADVANCE: MoveDto = {
      ply: 2, fromRow: 3, fromCol: 0, row: 4, col: 0, seat: 1, playedAt: 'x',
    };
    const RED_CAPTURES: MoveDto = {
      ply: 3, fromRow: 5, fromCol: 0, row: 4, col: 0, seat: 0, playedAt: 'x',
    };

    const roomWith = (gameKey: string, moves: readonly MoveDto[]): RoomState => {
      const base = makeRoomState();
      return { ...base, gameKey, game: { ...base.game!, moves } };
    };

    it('plays move-place when a stone is placed', async () => {
      const { fixture, hub, sound } = mount();
      await Promise.resolve();

      hub.state.set(
        roomWith('gomoku', [{ ply: 1, row: 7, col: 7, seat: 0, playedAt: 'x' }]),
      );
      fixture.detectChanges();

      expect(playedEvents(sound)).toEqual(['move-place']);
    });

    it('plays capture when a 象棋 move takes a piece', async () => {
      const { fixture, hub, sound } = mount();
      await Promise.resolve();

      hub.state.set(roomWith('xiangqi', [RED_ADVANCE, BLACK_ADVANCE, RED_CAPTURES]));
      fixture.detectChanges();

      // "He moved" and "he took my 車" are two different pieces of news.
      expect(playedEvents(sound)).toEqual(['capture']);
    });

    it('plays move-place when a 象棋 move takes nothing', async () => {
      const { fixture, hub, sound } = mount();
      await Promise.resolve();

      hub.state.set(roomWith('xiangqi', [RED_ADVANCE]));
      fixture.detectChanges();

      expect(playedEvents(sound)).toEqual(['move-place']);
    });

    it('never plays capture for a game without captures', async () => {
      // 成语接龙's plies carry no coordinates at all, so a capture check that ran on
      // them would be answering a question the game does not have.
      const { fixture, hub, sound } = mount();
      await Promise.resolve();

      hub.state.set(
        roomWith('idiom-chain', [
          { ply: 1, row: null, col: null, text: '一五一十', seat: 0, playedAt: 'x' },
        ]),
      );
      fixture.detectChanges();

      expect(playedEvents(sound)).toEqual(['move-place']);
    });

    // The three end-of-game sounds had **no test at all**, which mutation testing is
    // what found: swapping `'game-win'` for `'game-lose'` in the dispatch left the whole
    // suite green. The dialog's title had tests; the sound that plays beside it did not,
    // and those two are the pair whose disagreement is audible.
    const ended = (winnerUserId: string | null, result: 'Decided' | 'Draw' = 'Decided') => ({
      result,
      winnerUserId,
      endedAt: 'x',
      endReason: 'Decided' as const,
    });

    it('plays game-win when I am the winner', async () => {
      const { fixture, hub, sound } = mount();
      await Promise.resolve();
      sound.play.mockClear();

      hub.gameEnded.set(ended('u-1')); // mount() signs in as u-1
      fixture.detectChanges();

      expect(playedEvents(sound)).toEqual(['game-win']);
    });

    it('plays game-lose when someone else is the winner', async () => {
      const { fixture, hub, sound } = mount();
      await Promise.resolve();
      sound.play.mockClear();

      hub.gameEnded.set(ended('u-2'));
      fixture.detectChanges();

      expect(playedEvents(sound)).toEqual(['game-lose']);
    });

    it('plays game-draw on a draw', async () => {
      const { fixture, hub, sound } = mount();
      await Promise.resolve();
      sound.play.mockClear();

      hub.gameEnded.set(ended(null, 'Draw'));
      fixture.detectChanges();

      expect(playedEvents(sound)).toEqual(['game-draw']);
    });

    it('says nothing on the first state it ever sees', async () => {
      const { fixture, hub, sound } = mount();
      await Promise.resolve();
      sound.play.mockClear();

      // Same count as the state already observed — a reconnect, not a new move.
      hub.state.set(roomWith('gomoku', []));
      fixture.detectChanges();

      expect(playedEvents(sound)).toEqual([]);
    });
  });
});
