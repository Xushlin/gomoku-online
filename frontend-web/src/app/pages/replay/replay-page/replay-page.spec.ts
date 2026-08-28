import { HttpErrorResponse } from '@angular/common/http';
import {
  DefaultGameCatalogService,
  GameCatalogService,
} from '../../../games/game-catalog.service';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter, Router } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { of, throwError } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { signal } from '@angular/core';
import type { GameReplayDto } from '../../../core/api/models/room.model';
import { RoomsApiService } from '../../../core/api/rooms-api.service';
import { LanguageService } from '../../../core/i18n/language.service';
import { ReplayPage } from './replay-page';
import { GameCapabilitiesService } from '../../../games/game-capabilities.service';
import { StubGameCapabilities } from '../../../games/game-capabilities.stub';

function makeReplay(overrides: Partial<GameReplayDto> = {}): GameReplayDto {
  return {
    roomId: 'r-1',
    name: 'Replay',
    gameKey: 'gomoku',
    host: { id: 'u-1', username: 'alice' },
    seats: [
      { index: 0, player: { id: 'u-1', username: 'alice' } },
      { index: 1, player: { id: 'u-2', username: 'bob' } },
    ],
    startedAt: '2026-04-24T00:00:00Z',
    endedAt: '2026-04-24T00:05:00Z',
    result: 'Decided',
    winnerUserId: 'u-1',
    endReason: 'Decided',
    moves: [
      { ply: 1, row: 7, col: 7, seat: 0, playedAt: '2026-04-24T00:01:00Z' },
      { ply: 2, row: 7, col: 8, seat: 1, playedAt: '2026-04-24T00:02:00Z' },
      { ply: 3, row: 8, col: 7, seat: 0, playedAt: '2026-04-24T00:03:00Z' },
    ],
    ...overrides,
  };
}

class StubRooms {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  getReplay: any = vi.fn(() => of(makeReplay()));
}

function activatedRoute(id: string | null) {
  return {
    snapshot: { paramMap: { get: (k: string) => (k === 'id' ? id : null) } },
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

function mount(opts: { id?: string | null; getReplay?: ReturnType<typeof vi.fn> } = {}) {
  const id = opts.id ?? 'r-1';
  const rooms = new StubRooms();
  if (opts.getReplay) rooms.getReplay = opts.getReplay;
  const router = routerStub();
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      ReplayPage,
      TranslocoTestingModule.forRoot({
        langs: { en: {} },
        translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [
      // **真的目录服务**,不是桩:席位名的数据源就是 manifest,而一个桩会让这一组
      // 测试在一个「象棋没有席位名」的世界里跑。这个坑本仓库付过四次账。
      { provide: GameCatalogService, useClass: DefaultGameCatalogService },
      {
        provide: GameCapabilitiesService,
        useValue: StubGameCapabilities.sized({
          gomoku: { rows: 15, cols: 15 },
          tictactoe: { rows: 3, cols: 3 },
          xiangqi: { rows: 10, cols: 9 },
        }),
      },
      { provide: RoomsApiService, useValue: rooms },
      { provide: Router, useValue: router },
      { provide: ActivatedRoute, useValue: activatedRoute(id) },
      { provide: LanguageService, useValue: { current: signal('en') } },
    ],
  });
  const fixture = TestBed.createComponent(ReplayPage);
  fixture.detectChanges();
  return { fixture, rooms, router };
}

describe('ReplayPage', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('on init: fetches replay by route id', () => {
    const { rooms } = mount();
    expect(rooms.getReplay).toHaveBeenCalledWith('r-1');
  });

  it('404 sets notFound state', () => {
    const { fixture } = mount({
      getReplay: vi.fn(() =>
        throwError(() => new HttpErrorResponse({ status: 404, statusText: 'Not Found' })),
      ),
    });
    const comp = fixture.componentInstance as unknown as { notFound: () => boolean };
    expect(comp.notFound()).toBe(true);
  });

  it('409 sets notFinished state', () => {
    const { fixture } = mount({
      getReplay: vi.fn(() =>
        throwError(() => new HttpErrorResponse({ status: 409, statusText: 'Conflict' })),
      ),
    });
    const comp = fixture.componentInstance as unknown as { notFinished: () => boolean };
    expect(comp.notFinished()).toBe(true);
  });

  /*
   * scrubber 的**行为**断言搬到了 `platform/move-scrubber/move-scrubber.spec.ts` ——
   * 它们原来摸的是本页的私有 `step` / `togglePlay` / `playing`,而那些正是搬走的东西。
   * 留在这里就只能测一个不再存在的实现。
   *
   * 这里留下的是**接线**:点真实的按钮,看棋盘那一帧真的变了。抽取真正可能弄坏的
   * 就是这一段 —— 组件自己全绿而页面没接上,两者长得一模一样。
   */
  const scrubberButton = (
    fixture: ReturnType<typeof mount>['fixture'],
    label: string,
  ): HTMLButtonElement => {
    const el = (fixture.nativeElement as HTMLElement).querySelector<HTMLButtonElement>(
      `app-move-scrubber button[aria-label="replay.scrubber.${label}"]`,
    );
    expect(el).not.toBeNull();
    return el as HTMLButtonElement;
  };

  const stonesOnBoard = (fixture: ReturnType<typeof mount>['fixture']): number =>
    (fixture.nativeElement as HTMLElement).querySelectorAll('.board-stone').length;

  it('renders the shared scrubber rather than its own controls', () => {
    const { fixture } = mount();
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelectorAll('app-move-scrubber').length).toBe(1);
    // 页面自己 MUST NOT 再画进度条 —— 那会是第二份 scrubber。
    expect(host.querySelectorAll(':scope > section > input[type="range"]').length).toBe(0);
  });

  it('a click on the scrubber moves the board forward, and back again', () => {
    const { fixture } = mount();
    expect(stonesOnBoard(fixture)).toBe(0);

    scrubberButton(fixture, 'next').click();
    fixture.detectChanges();
    expect(stonesOnBoard(fixture)).toBe(1);

    scrubberButton(fixture, 'last').click();
    fixture.detectChanges();
    const atEnd = stonesOnBoard(fixture);
    expect(atEnd).toBeGreaterThan(1);

    scrubberButton(fixture, 'first').click();
    fixture.detectChanges();
    expect(stonesOnBoard(fixture)).toBe(0);
  });

  it('clamps a seek the scrubber asks for beyond the end', () => {
    const { fixture } = mount();
    const comp = fixture.componentInstance as unknown as {
      onScrub: (n: number) => void;
      currentPly: () => number;
      totalMoves: () => number;
    };
    comp.onScrub(999);
    expect(comp.currentPly()).toBe(comp.totalMoves());
    comp.onScrub(-5);
    expect(comp.currentPly()).toBe(0);
  });

  afterEach(() => {
    vi.useRealTimers();
  });
});

/**
 * The replay page renders whichever shared board the game needs.
 *
 * It still writes no rendering code of its own — the original requirement said "do
 * not introduce a second board renderer", which was written when the platform had
 * one board shape. Xiangqi's is not a parameterisation of gomoku's, so the rule now
 * reads "pick between the shared components", and the intent it protected is intact.
 */
describe('ReplayPage board selection', () => {
  beforeEach(() => TestBed.resetTestingModule());

  const XIANGQI_MOVES: GameReplayDto['moves'] = [
    { ply: 1, row: 5, col: 0, seat: 0, playedAt: 'x', fromRow: 6, fromCol: 0 },
    { ply: 2, row: 4, col: 0, seat: 1, playedAt: 'x', fromRow: 3, fromCol: 0 },
    // 炮打马: the cannon on (7,1) uses the black cannon on (2,1) as its screen and
    // takes the horse on (0,1).
    { ply: 3, row: 0, col: 1, seat: 0, playedAt: 'x', fromRow: 7, fromCol: 1 },
  ];

  function mountXiangqi() {
    return mount({
      getReplay: vi.fn(() =>
        of(makeReplay({ gameKey: 'xiangqi', moves: XIANGQI_MOVES, endReason: 'Resigned' })),
      ),
    });
  }

  it('draws a read-only xiangqi board for a xiangqi replay', () => {
    const { fixture } = mountXiangqi();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('app-xiangqi-board')).toBeTruthy();
    expect(el.querySelector('app-board')).toBeNull();

    const points = Array.from(el.querySelectorAll('.xq-point')) as HTMLButtonElement[];
    expect(points).toHaveLength(90);
    expect(points.every((b) => b.disabled)).toBe(true);
  });

  it('brings captured pieces back when the scrubber goes backwards', () => {
    // Ply 3 is 炮打马 — the black horse on (0,1) comes off. Stepping back must
    // put it there again, which only works because the position is derived from
    // the opening setup each frame rather than mutated in place.
    const { fixture } = mountXiangqi();
    const comp = fixture.componentInstance as unknown as { onScrub: (n: number) => void };
    const pieces = () => (fixture.nativeElement as HTMLElement).querySelectorAll('.xq-piece');

    comp.onScrub(3);
    fixture.detectChanges();
    expect(pieces()).toHaveLength(31);

    comp.onScrub(2);
    fixture.detectChanges();
    expect(pieces()).toHaveLength(32);
  });

  it('leaves gomoku replays exactly as they were', () => {
    const { fixture } = mount();
    const el = fixture.nativeElement as HTMLElement;

    expect(el.querySelector('app-board')).toBeTruthy();
    expect(el.querySelector('app-xiangqi-board')).toBeNull();
  });
});

describe('ReplayPage — 每一个座位', () => {
  /** 一局三座位的牌局回放:三个人,没有盘面。 */
  function threeSeatReplay(): GameReplayDto {
    return makeReplay({
      gameKey: 'doudizhu',
      seats: [
        { index: 0, player: { id: 'u-1', username: 'alice' } },
        { index: 1, player: { id: 'u-2', username: 'bob' } },
        { index: 2, player: { id: 'u-3', username: 'carol' } },
      ],
      moves: [
        { ply: 1, row: null, col: null, seat: 0, playedAt: '2026-04-24T00:01:00Z', text: 'bid:3' },
        { ply: 2, row: null, col: null, seat: 1, playedAt: '2026-04-24T00:02:00Z', text: 'pass' },
        { ply: 3, row: null, col: null, seat: 2, playedAt: '2026-04-24T00:03:00Z', text: 'pass' },
      ],
    });
  }

  function renderWith(replay: GameReplayDto, caps: StubGameCapabilities) {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [
        ReplayPage,
        TranslocoTestingModule.forRoot({
          langs: { en: {} },
          translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
          preloadLangs: true,
        }),
      ],
      providers: [
        // **真路由器,不是 stub。** routerLink 只有在真路由器下才解析出 href,
        // 而 href 正是这里要证的东西:三个座位各指向**各自**的用户 ——
        // 一个把三个链接都指到同一个人的缺陷,在只数个数的断言下是绿的。
        provideRouter([]),
        // **真的目录服务,不是 stub** —— 席位怎么称呼由清单决定,而 stub 会让
        // 「象棋读作红方」这类断言测的是 stub 自己。与 mount() 同一个选择。
        { provide: GameCatalogService, useClass: DefaultGameCatalogService },
        { provide: GameCapabilitiesService, useValue: caps },
        { provide: RoomsApiService, useValue: { getReplay: vi.fn(() => of(replay)) } },
        { provide: ActivatedRoute, useValue: activatedRoute('r-1') },
        { provide: LanguageService, useValue: { current: signal('en') } },
      ],
    });
    const fixture = TestBed.createComponent(ReplayPage);
    fixture.detectChanges();
    return fixture;
  }

  afterEach(() => TestBed.resetTestingModule());

  it('三座位对局的标题区画三个人', () => {
    const fixture = renderWith(threeSeatReplay(), StubGameCapabilities.boardless('doudizhu'));
    const links: HTMLAnchorElement[] = Array.from(
      fixture.nativeElement.querySelectorAll('header a.username-link'),
    );

    // **恰好三个**,不是「至少两个」—— 后者在丢掉一个座位之后依然是绿的。
    expect(links).toHaveLength(3);
    expect(links.map((a) => a.getAttribute('href'))).toEqual([
      '/users/u-1',
      '/users/u-2',
      '/users/u-3',
    ]);
    expect(new Set(links.map((a) => a.textContent?.trim())).size).toBe(3);
  });

  it('两座位对局的标题区画两个人', () => {
    // 这一条与上一条 MUST 同时存在:「每个座位都画出来了」在一个只有两座位的样本上恒真。
    const fixture = renderWith(makeReplay(), StubGameCapabilities.sized({ gomoku: { rows: 15, cols: 15 } }));
    const links = fixture.nativeElement.querySelectorAll('header a.username-link');

    expect(links).toHaveLength(2);
  });

  it('标题区整句读得通 —— 标签与用户名不重复', () => {
    // 走 DOM 读**整句**,不是 toContain 单个用户名:`add-xiangqi-manual` 在这里踩过 ——
    // 标签与值各带一次前缀,拼出来是重复的,而每一条 toContain 都绿。
    const fixture = renderWith(threeSeatReplay(), StubGameCapabilities.boardless('doudizhu'));
    const header = fixture.nativeElement.querySelector('header')?.textContent ?? '';

    expect(header).toContain('alice');
    expect(header).toContain('carol');
    expect(header.match(/alice/g) ?? []).toHaveLength(1);
    expect(header.match(/carol/g) ?? []).toHaveLength(1);
  });

  it('画不出盘面的棋种给出说明,而不是一片空白', () => {
    const fixture = renderWith(threeSeatReplay(), StubGameCapabilities.boardless('doudizhu'));
    const el: HTMLElement = fixture.nativeElement;

    expect(el.querySelector('app-board')).toBeNull();
    expect(el.querySelector('app-xiangqi-board')).toBeNull();
    expect(el.querySelector('app-chain-board')).toBeNull();
    // 说明文案在场 —— 空白与「有说明」的区别只有这一条断言看得见。
    expect(el.textContent).toContain('replay.no-board');
  });

  it('成语接龙没有 rows/cols,但它有自己的棋盘,所以不走说明分支', () => {
    // 反面控制:判据是「有没有专用渲染组件」,MUST NOT 是 boardSizeFor 是否为 null。
    const fixture = renderWith(makeReplay({ gameKey: 'idiom-chain' }), StubGameCapabilities.boardless('idiom-chain'));
    const el: HTMLElement = fixture.nativeElement;

    expect(el.querySelector('app-chain-board')).not.toBeNull();
    expect(el.textContent).not.toContain('replay.no-board');
  });

  it('标题区能断行 —— 20 字符用户名在 375 px 下不横向溢出', () => {
    // **在浏览器里量到的既有缺陷**(不是这次改出来的):Angular 去掉元素间空白,`mx-1` 是
    // margin 不是断行机会,于是「Black:」+20 字符+「White:」+20 字符连成一个没有断点的长串,
    // 375 px 下 scrollWidth 504 / clientWidth 311。jsdom 量不了布局,所以这里钉的是**那个类**
    // 还在 —— 真正的证据是浏览器里的 scrollWidth 311,写在模板的注释里。
    const fixture = renderWith(makeReplay(), StubGameCapabilities.sized({ gomoku: { rows: 15, cols: 15 } }));
    const p: HTMLElement = fixture.nativeElement.querySelector('header p');

    expect(p.className).toContain('break-words');
  });

  it('合成给棋盘的 RoomState 原样带着三个座位', () => {
    const fixture = renderWith(threeSeatReplay(), StubGameCapabilities.boardless('doudizhu'));
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const state = (fixture.componentInstance as any).boardState();

    expect(state.seats).toHaveLength(3);
    expect(state.seats.map((s: { index: number }) => s.index)).toEqual([0, 1, 2]);
  });
});
