import { Dialog } from '@angular/cdk/dialog';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { RoomSeat, RoomSummary } from '../../../../core/api/models/room.model';
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

function mount(rooms: readonly RoomSummary[]) {
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
    const { fixture } = mount([room({ seats: seats(3) })]);

    const text: string = fixture.nativeElement.textContent ?? '';
    for (const key of ['seat-black', 'seat-white', 'seat-empty']) {
      expect(text).not.toContain(key);
    }
  });

  it('a room nobody has joined yet still lists its host seat', () => {
    const { fixture } = mount([room({ seats: seats(1) })]);

    expect(playerNames(fixture)).toEqual(['player0']);
  });
});
