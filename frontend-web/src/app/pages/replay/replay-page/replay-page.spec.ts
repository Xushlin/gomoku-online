import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
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
    black: { id: 'u-1', username: 'alice' },
    white: { id: 'u-2', username: 'bob' },
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
