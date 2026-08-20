import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { RoomSeat, RoomSummary } from '../../../../core/api/models/room.model';
import { AuthService } from '../../../../core/auth/auth.service';
import { HomeDataService } from '../../../../core/lobby/home-data.service';
import { MyActiveRoomsCard } from './my-active-rooms';

/**
 * 「我在这个房间里是什么身份」。
 *
 * **这个文件此前不存在。** `sideKey()` 只比 `black` / `white`,而那是 0 号与 1 号座位的派生
 * 读法 —— 于是三座位房间里 2 号座位上的人被标成「你是观战」,**在他自己的对局里**。
 * 没有任何测试问过第三个座位,所以那件事一直是绿的。
 *
 * 核心断言:**「不在座位上」与「在第三个座位上」MUST NOT 得到同一个答案。**
 */
function seats(n: number): readonly RoomSeat[] {
  return Array.from({ length: n }, (_, i) => ({
    index: i,
    player: { id: `u-${i}`, username: `player${i}` },
  }));
}

function room(seated: readonly RoomSeat[]): RoomSummary {
  return {
    id: 'r-1',
    name: 'Sample',
    gameKey: 'doudizhu',
    status: 'Playing',
    host: { id: 'u-0', username: 'player0' },
    black: seated[0]?.player ?? null,
    white: seated[1]?.player ?? null,
    seats: seated,
    spectatorCount: 0,
    createdAt: '2026-08-20T00:00:00Z',
  };
}

function mount(myId: string | null, rooms: readonly RoomSummary[]) {
  const data = {
    myRooms: {
      data: signal<readonly RoomSummary[] | null>(rooms),
      loading: signal(false),
      error: signal<unknown | null>(null),
      refresh: vi.fn(),
    },
  };
  const auth = {
    user: signal(myId === null ? null : { id: myId, username: 'me', email: 'm@m' }),
    accessToken: signal(null),
    isAuthenticated: signal(true),
  };
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      MyActiveRoomsCard,
      TranslocoTestingModule.forRoot({
        langs: { en: {} },
        translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [
      provideRouter([]),
      { provide: HomeDataService, useValue: data },
      { provide: AuthService, useValue: auth },
    ],
  });
  const fixture = TestBed.createComponent(MyActiveRoomsCard);
  fixture.detectChanges();
  return fixture.nativeElement.textContent as string;
}

describe('MyActiveRoomsCard', () => {
  beforeEach(() => TestBed.resetTestingModule());

  // 遍历三个座位:每一个占着座位的人都 MUST 被说成在座,而 MUST NOT 被说成观战。
  it.each([0, 1, 2])('a player holding seat %i is seated, not spectating', (seat) => {
    const text = mount(`u-${seat}`, [room(seats(3))]);

    expect(text).toContain('lobby.my-rooms.you-are-seated');
    expect(text).not.toContain('lobby.my-rooms.you-are-spectator');
  });

  it('seat 2 was the one this fix is about', () => {
    // 单独点名第三个座位。上面那条遍历守不住它被特殊对待 —— 一条断言「所有座位都在座」
    // 在一个只认 0/1 的实现下会红,但它红在哪个座位上不会被记下来。
    const text = mount('u-2', [room(seats(3))]);

    expect(text).toContain('lobby.my-rooms.you-are-seated');
  });

  it('someone with no seat is a spectator', () => {
    // 负控制:少了它,一个恒说「在座」的实现会让上面全部变绿。
    const text = mount('u-99', [room(seats(3))]);

    expect(text).toContain('lobby.my-rooms.you-are-spectator');
    expect(text).not.toContain('lobby.my-rooms.you-are-seated');
  });

  it('a logged-out reader is not seated either', () => {
    const text = mount(null, [room(seats(3))]);

    expect(text).toContain('lobby.my-rooms.you-are-spectator');
  });

  it('two-seat rooms still answer for both seats', () => {
    expect(mount('u-0', [room(seats(2))])).toContain('lobby.my-rooms.you-are-seated');
    expect(mount('u-1', [room(seats(2))])).toContain('lobby.my-rooms.you-are-seated');
  });
});
