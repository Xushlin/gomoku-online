import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { describe, expect, it, vi } from 'vitest';
import { DefaultRoomsApiService, RoomsApiService } from '../../core/api/rooms-api.service';
import { LobbyDataService } from '../../core/lobby/lobby-data.service';
import { LOBBY_GAME_KEY } from '../../core/lobby/lobby-game-key';
import { GameCapabilitiesService } from '../../games/game-capabilities.service';
import { StubGameCapabilities } from '../../games/game-capabilities.stub';
import { GameCatalogService, DefaultGameCatalogService } from '../../games/game-catalog.service';
import { GameLobby } from './game-lobby';

function slice<T>(data: T | null) {
  return {
    data: signal<T | null>(data),
    loading: signal<boolean>(false),
    error: signal<unknown | null>(null),
    refresh: vi.fn(),
  };
}

function mount(gameKey: string, capabilities: StubGameCapabilities) {
  const data = {
    rooms: slice<readonly unknown[]>([]),
    leaderboard: slice<readonly unknown[]>([]),
  } as unknown as LobbyDataService;

  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [GameLobby, TranslocoTestingModule.forRoot({ langs: { en: {} }, translocoConfig: { availableLangs: ['en'], defaultLang: 'en' } })],
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      // The rendered cards inject these; the page itself does not.
      { provide: RoomsApiService, useClass: DefaultRoomsApiService },
      { provide: LOBBY_GAME_KEY, useValue: gameKey },
      { provide: GameCapabilitiesService, useValue: capabilities },
      { provide: GameCatalogService, useClass: DefaultGameCatalogService },
      {
        provide: ActivatedRoute,
        useValue: { snapshot: { paramMap: new Map([['gameKey', gameKey]]) } },
      },
    ],
  });
  // `set`, not `add`: the component's own `useClass: DefaultLobbyDataService`
  // must be *replaced*. Leaving it in place makes Angular register that class's
  // ngOnDestroy against whichever instance the token resolves to — the plain
  // stub — and teardown then dies reading a field the stub never had.
  TestBed.overrideComponent(GameLobby, {
    set: { providers: [{ provide: LobbyDataService, useValue: data }] },
  });
  const fixture = TestBed.createComponent(GameLobby);
  fixture.detectChanges();
  return { fixture, html: () => fixture.nativeElement.innerHTML as string };
}

describe('GameLobby', () => {
  it('renders the game cards for a rated human-vs-human game', () => {
    const { html } = mount('gomoku', StubGameCapabilities.rated('gomoku'));

    expect(html()).toContain('app-active-rooms-card');
    expect(html()).toContain('app-ai-game-card');
    expect(html()).toContain('app-leaderboard-card');
  });

  it('hides the ladder card for an unrated game', () => {
    // A permanently empty board reads as "nobody has played this yet", which is
    // a different claim from "this game has no ladder".
    const capabilities = new StubGameCapabilities([
      { gameKey: 'friendly', isRated: false, supportsHumanVsHuman: true, supportsAi: true, seatCount: 2, rows: 9, cols: 9 },
    ]);
    const { html } = mount('friendly', capabilities);

    expect(html()).toContain('app-active-rooms-card');
    expect(html()).not.toContain('app-leaderboard-card');
  });

  it('hides the AI card for a game with no computer opponent', () => {
    // 成语接龙's shape: human play, no bot. This card used to render for every
    // game, on a written argument that no game could yet contradict it. The
    // argument was about the card; the hole was in POST /api/rooms/ai, which
    // returned 201 and let a turn timeout pay out ELO for a game nobody played.
    const { html } = mount('idiom-chain', StubGameCapabilities.boardless('idiom-chain'));

    expect(html()).toContain('app-active-rooms-card');
    expect(html()).not.toContain('app-ai-game-card');
  });

  it('decides nothing about the AI card before the descriptors arrive', () => {
    const { html } = mount('gomoku', StubGameCapabilities.pending());

    expect(html()).toContain('game-lobby-skeleton');
    expect(html()).not.toContain('app-ai-game-card');
  });

  it('explains an AI-only game instead of listing rooms', () => {
    const { html } = mount('xiangqi', StubGameCapabilities.aiOnly('xiangqi'));

    expect(html()).toContain('game-lobby-ai-only');
    expect(html()).not.toContain('app-active-rooms-card');
  });

  it('explains an unknown game key', () => {
    const { html } = mount('go', StubGameCapabilities.rated('gomoku'));

    expect(html()).toContain('game-lobby-unknown');
    expect(html()).not.toContain('app-active-rooms-card');
  });

  it('holds a skeleton until the descriptors arrive', () => {
    // "The descriptor has not arrived" and "this key is not a game" are
    // different claims. Painting the second over the first tells the player
    // something false one response before the truth lands.
    const { html } = mount('gomoku', StubGameCapabilities.pending());

    expect(html()).toContain('game-lobby-skeleton');
    expect(html()).not.toContain('game-lobby-unknown');
    expect(html()).not.toContain('app-active-rooms-card');
  });

  it('never redirects — a mistyped key stays on its own URL', () => {
    const { fixture } = mount('go', StubGameCapabilities.rated('gomoku'));
    const router = TestBed.inject(ActivatedRoute);

    // No navigation happened: the component holds no Router navigation at all,
    // which is the point — a redirect would disguise the typo as another page.
    expect(router).toBeDefined();
    expect(fixture.nativeElement.innerHTML).toContain('game-lobby-unknown');
  });

  /*
   * 古谱入口由 **manifest** 决定,不是由 `gameKey === 'xiangqi'`。
   *
   * **两个方向都要在样本里**:象棋有 `manualRoute`,五子棋没有 —— 只断言一边,
   * 一个「永远显示」或「永远不显示」的实现同样是绿的。
   */
  it('links to the manual only for games whose manifest declares one', () => {
    const xiangqi = mount('xiangqi', StubGameCapabilities.rated('xiangqi')).html();
    const gomoku = mount('gomoku', StubGameCapabilities.rated('gomoku')).html();

    expect(xiangqi).toContain('/g/xiangqi/manual');
    expect(gomoku).not.toContain('manual');
  });
});
