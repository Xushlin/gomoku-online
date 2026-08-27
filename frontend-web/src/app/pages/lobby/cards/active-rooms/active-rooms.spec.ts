import { Dialog } from '@angular/cdk/dialog';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { RoomSeat, RoomSummary } from '../../../../core/api/models/room.model';
import {
  DefaultGameCapabilitiesService,
  GameCapabilitiesService,
} from '../../../../games/game-capabilities.service';
import { GamesApiService } from '../../../../core/api/games-api.service';
import { RoomsApiService } from '../../../../core/api/rooms-api.service';
import { LobbyDataService } from '../../../../core/lobby/lobby-data.service';
import { ActiveRoomsCard } from './active-rooms';

/**
 * 大厅的房间行。
 *
 * **这个文件此前不存在** —— `active-rooms` 一条测试都没有,而它的每一行都写死了
 * 「黑方 / 白方」两个座位。那是这个缺陷能活到今天的直接原因:没有任何东西问过
 * 「一桌三个人时这一行画了几个人」。
 *
 * 所以核心断言是一条**遍历**:2 个座位与 3 个座位各走一遍,渲染出的玩家链接数
 * MUST 等于 `seats.length`。写成「斗地主房间画三个人」的话,一个把第三个人硬编码进去的
 * 实现同样是绿的。
 */
function seats(n: number): readonly RoomSeat[] {
  return Array.from({ length: n }, (_, i) => ({
    index: i,
    player: { id: `u-${i}`, username: `player${i}` },
  }));
}

function room(overrides: Partial<RoomSummary> = {}): RoomSummary {
  const filled = overrides.seats ?? seats(2);
  return {
    id: 'r-1',
    name: 'Sample',
    gameKey: 'gomoku',
    status: 'Waiting',
    host: { id: 'u-0', username: 'player0' },
    black: filled[0]?.player ?? null,
    white: filled[1]?.player ?? null,
    seats: filled,
    spectatorCount: 0,
    createdAt: '2026-08-20T00:00:00Z',
    ...overrides,
  };
}

class StubRooms {
  /* eslint-disable @typescript-eslint/no-explicit-any */
  list: any = vi.fn(() => of([]));
  myActiveRooms: any = vi.fn(() => of([]));
  get: any = vi.fn();
  create: any = vi.fn();
  createAiRoom: any = vi.fn();
  join: any = vi.fn(() => of(void 0));
  spectate: any = vi.fn(() => of(void 0));
  leave: any = vi.fn();
  resign: any = vi.fn();
  dissolve: any = vi.fn();
  /* eslint-enable @typescript-eslint/no-explicit-any */
}

/**
 * 座位总数的桩。
 *
 * **它必须能表达「还没到达」**,而不只是「几个座位」—— `seatCount` 来自
 * `GET /api/games`,是异步的,而大厅列表没有整页 loading 门。传 `undefined`
 * 就是那个状态,而它在界面上画的是占位而不是「满座」。
 */
function stubCapabilities(seatCounts: Readonly<Record<string, number | undefined>>) {
  return {
    ensureLoaded: vi.fn(),
    of: (key: string) =>
      seatCounts[key] === undefined ? undefined : ({ seatCount: seatCounts[key] } as never),
    ratedKeys: () => [],
    loaded: () => true,
  };
}

function mount(
  rooms: readonly RoomSummary[],
  seatCounts: Readonly<Record<string, number | undefined>> = { gomoku: 2, doudizhu: 3, wakeng: 3 },
) {
  const data = {
    rooms: {
      data: signal<readonly RoomSummary[] | null>(rooms),
      loading: signal(false),
      error: signal<unknown | null>(null),
      refresh: vi.fn(),
    },
  };
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      ActiveRoomsCard,
      TranslocoTestingModule.forRoot({
        langs: { en: {} },
        translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [
      provideRouter([]),
      { provide: LobbyDataService, useValue: data },
      { provide: RoomsApiService, useValue: new StubRooms() },
      { provide: Dialog, useValue: { open: vi.fn() } },
      { provide: GameCapabilitiesService, useValue: stubCapabilities(seatCounts) },
    ],
  });
  const fixture = TestBed.createComponent(ActiveRoomsCard);
  fixture.detectChanges();
  return { fixture };
}

function playerNames(fixture: ReturnType<typeof mount>['fixture']): string[] {
  return [...fixture.nativeElement.querySelectorAll('[data-testid="room-player"]')].map(
    (a) => (a as HTMLElement).textContent?.trim() ?? '',
  );
}

describe('ActiveRoomsCard', () => {
  beforeEach(() => TestBed.resetTestingModule());

  // **遍历,而不是举例。** 一个把第三个人硬编码进去的实现在「斗地主画三个人」下也是绿的。
  it.each([1, 2, 3])('renders exactly one player link per occupied seat (%i seats)', (n) => {
    const { fixture } = mount([room({ seats: seats(n) })]);

    expect(playerNames(fixture)).toEqual(
      Array.from({ length: n }, (_, i) => `player${i}`),
    );
  });

  it('the third seat of a three-seat room shows up', () => {
    // 这个缺陷的可执行形式:在它之前,`player2` 在大厅里根本不出现。
    const { fixture } = mount([room({ gameKey: 'doudizhu', seats: seats(3) })]);

    expect(playerNames(fixture)).toContain('player2');
  });

  it('every player name is a link to their profile', () => {
    const { fixture } = mount([room({ seats: seats(3) })]);

    const hrefs = [...fixture.nativeElement.querySelectorAll('[data-testid="room-player"]')].map(
      (a) => (a as HTMLAnchorElement).getAttribute('href'),
    );
    expect(hrefs).toEqual(['/users/u-0', '/users/u-1', '/users/u-2']);
  });

  it('the row says nothing about colours', () => {
    // `board-seats.ts` 自己的文档写着那套「座位号 → 颜色」的读法只有棋盘家族可以调用,
    // 而一个座位数大于二的棋种没有颜色可映。大厅不是棋盘。
    //
    // **fixture 必须真的画出一个空位,否则这条负向断言什么都没测。** 上一版用
    // 3 个在座配一个 2 座位的棋种(一个不可能的局面),于是空位数是 -1、一个
    // 圆片都不画 —— 把退役键名放回 `aria-label` 的变异照样绿。负向断言在
    // 「什么都没发生」时恒真,所以下面先断言局面搭起来了。
    const { fixture } = mount([room({ gameKey: 'doudizhu', seats: seats(2) })], { doudizhu: 3 });

    expect(
      fixture.nativeElement.querySelectorAll('[data-testid="seat-vacant"]').length,
      'no empty seat rendered — the assertion below would pass vacuously',
    ).toBe(1);

    // **`textContent` 看不见属性**,而一个退役的键名完全可以从 `aria-label` 或
    // `title` 里溜回来。查整段 outerHTML。
    const html: string = fixture.nativeElement.outerHTML ?? '';
    for (const key of ['seat-black', 'seat-white', 'seat-empty']) {
      expect(html, `retired key ${key} came back`).not.toContain(key);
    }
  });

  const seatEls = (f: ReturnType<typeof mount>['fixture'], kind: string) =>
    [...f.nativeElement.querySelectorAll(`[data-testid="seat-${kind}"]`)];

  it('draws one chip per seat the game has, taken and empty', () => {
    // **这是 `seats.length` 不等于座位总数的可执行形式。** 一个等待中的三人房
    // `seats` 里只有两项,而它该画三个位子 —— 少了这条,一个只画在座者的实现
    // 会让每个等待中的房间看起来都满座,而那正是这一整条线要修掉的症状。
    const { fixture } = mount([room({ gameKey: 'doudizhu', seats: seats(2) })], { doudizhu: 3 });

    expect(seatEls(fixture, 'taken')).toHaveLength(2);
    expect(seatEls(fixture, 'vacant')).toHaveLength(1);
    expect(seatEls(fixture, 'unknown')).toHaveLength(0);
  });

  it('a full room has no empty chip', () => {
    // 正面对照 —— 少了它,一个恒画一个空位的实现在上一条下也是绿的。
    const { fixture } = mount([room({ gameKey: 'doudizhu', seats: seats(3) })], { doudizhu: 3 });

    expect(seatEls(fixture, 'taken')).toHaveLength(3);
    expect(seatEls(fixture, 'vacant')).toHaveLength(0);
  });

  it('before the seat count arrives it draws a placeholder, not a full table', () => {
    // `seatCount` 是异步的,而大厅列表没有整页 loading 门。**退化成 `seats.length`
    // 会画出一个「满座」的等待房间** —— 一个看起来不能加入、实际能加入的房间。
    const { fixture } = mount([room({ gameKey: 'doudizhu', seats: seats(2) })], { doudizhu: undefined });

    expect(seatEls(fixture, 'taken')).toHaveLength(2);
    expect(seatEls(fixture, 'unknown')).toHaveLength(1);
    expect(seatEls(fixture, 'vacant')).toHaveLength(0);
  });

  it('the placeholder is not announced as a seat', () => {
    // 一个还不知道数量的占位被朗读成「空位」比不朗读更糟 —— 它在说一件没被确认的事。
    const { fixture } = mount([room({ gameKey: 'doudizhu', seats: seats(2) })], { doudizhu: undefined });

    expect(seatEls(fixture, 'unknown')[0].getAttribute('aria-hidden')).toBe('true');
  });

  it('a taken chip shows one character but keeps the whole name reachable', () => {
    // 375 px 下名字那行收起来,只剩圆片。**视觉上省掉的不能在语义上也省掉**,
    // 否则屏幕阅读器只会读到一个字。
    const { fixture } = mount([room({ seats: seats(1) })]);
    const [chip] = seatEls(fixture, 'taken');

    expect(chip.textContent?.trim()).toBe('p');
    expect(chip.getAttribute('aria-label')).toBe('player0');
    expect(chip.getAttribute('title')).toBe('player0');
    expect(chip.getAttribute('href')).toBe('/users/u-0');
  });

  it('the row shows which game it is', () => {
    // 房间行今天看不出是哪个棋种。纹章是既有组件,所以这一处不新增资源。
    const { fixture } = mount([room({ gameKey: 'doudizhu', seats: seats(1) })], { doudizhu: 3 });

    const svg = fixture.nativeElement.querySelector('app-game-emblem svg');
    expect(svg).not.toBeNull();
    expect(svg.querySelectorAll('line, circle, rect, text, path').length).toBeGreaterThan(0);
  });

  /**
   * **伴生键的行画的是它主人的纹章。**
   *
   * 象棋残局在服务端是另一个键,manifest 里没有它自己的条目 —— 表里不加的话
   * `emblemOf` 返回空数组,画出来是一块什么都没有的空白。**它不抛、不报、不红,只是不见。**
   *
   * 判据是**两行画出同样多的图元**,而不是「残局那行有图元」:后者在两行都退化成
   * 一个空 `<svg>` 时同样是绿的。
   */
  it('an endgame room borrows the xiangqi emblem', () => {
    const { fixture } = mount(
      [
        room({ id: 'r-x', gameKey: 'xiangqi', seats: seats(1) }),
        room({ id: 'r-e', gameKey: 'xiangqi-endgame', seats: seats(1) }),
      ],
      { xiangqi: 2, 'xiangqi-endgame': 2 },
    );

    const shapes = [...fixture.nativeElement.querySelectorAll('app-game-emblem svg')].map(
      (svg: SVGElement) => svg.querySelectorAll('line, circle, rect, text, path').length,
    );

    expect(shapes).toHaveLength(2);
    expect(shapes[0]).toBeGreaterThan(0);
    expect(shapes[1]).toBe(shapes[0]);
  });

  it('wires to the real capabilities service, not just the stub', () => {
    /*
     * 上面那些用的是 `GameCapabilitiesService` 的桩,所以它们证明的是**模板逻辑**,
     * 不是**接线**。这一条用真的 `DefaultGameCapabilitiesService`,只在 HTTP 边界打桩 ——
     * 它会因为 `ensureLoaded()` 没被调用、或者读错字段名而红,而那两种错误在桩下都是绿的。
     *
     * 这一条不是浏览器验证的替代品,是它的**近似**:它证明得到「描述符到达后圆片补齐」,
     * 证明不了「真实屏幕上好看」。后者由试画页的人眼复核负责。
     */
    const descriptors = [
      { gameKey: 'doudizhu', isRated: false, supportsHumanVsHuman: true, supportsAi: false, seatCount: 3, rows: null, cols: null },
    ] as const;
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [
        ActiveRoomsCard,
        TranslocoTestingModule.forRoot({
          langs: { en: {} },
          translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
          preloadLangs: true,
        }),
      ],
      providers: [
        provideRouter([]),
        {
          provide: LobbyDataService,
          useValue: {
            rooms: {
              data: signal<readonly RoomSummary[] | null>([
                room({ gameKey: 'doudizhu', seats: seats(2) }),
              ]),
              loading: signal(false),
              error: signal<unknown | null>(null),
              refresh: vi.fn(),
            },
          },
        },
        { provide: RoomsApiService, useValue: new StubRooms() },
        { provide: Dialog, useValue: { open: vi.fn() } },
        { provide: GamesApiService, useValue: { list: () => of(descriptors) } },
        { provide: GameCapabilitiesService, useClass: DefaultGameCapabilitiesService },
      ],
    });
    const fixture = TestBed.createComponent(ActiveRoomsCard);
    fixture.detectChanges();

    // 描述符已经到了(of() 是同步的),所以第三个位子必须是空位而不是占位
    expect(fixture.nativeElement.querySelectorAll('[data-testid="seat-taken"]').length).toBe(2);
    expect(fixture.nativeElement.querySelectorAll('[data-testid="seat-vacant"]').length).toBe(1);
    expect(fixture.nativeElement.querySelectorAll('[data-testid="seat-unknown"]').length).toBe(0);
  });

  it('a room nobody has joined yet still lists its host seat', () => {
    const { fixture } = mount([room({ seats: seats(1) })]);

    expect(playerNames(fixture)).toEqual(['player0']);
  });
});
