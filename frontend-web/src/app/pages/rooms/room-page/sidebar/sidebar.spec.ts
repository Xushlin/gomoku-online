import { Dialog } from '@angular/cdk/dialog';
import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { GameCapabilitiesService } from '../../../../games/game-capabilities.service';
import { StubGameCapabilities } from '../../../../games/game-capabilities.stub';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { beforeEach, describe, expect, it } from 'vitest';
import type { RoomState } from '../../../../core/api/models/room.model';
import { RoomSidebar } from './sidebar';

@Component({
  selector: 'app-sidebar-host',
  standalone: true,
  imports: [RoomSidebar],
  template: `
    <app-room-sidebar [state]="state()" />
  `,
})
class Host {
  readonly state = signal<RoomState | null>(null);
}

function baseState(): RoomState {
  return {
    id: 'r-1',
    name: 'Alice room',
    gameKey: 'gomoku',
    status: 'Playing',
    host: { id: 'u-1', username: 'alice' },
    black: { id: 'u-1', username: 'alice' },
    white: { id: 'u-2', username: 'bob' },
    seats: [
      { index: 0, player: { id: 'u-1', username: 'alice' } },
      { index: 1, player: { id: 'u-2', username: 'bob' } },
    ],
    spectators: [],
    game: {
      id: 'g-1',
      currentSeat: 0,
      startedAt: 'x',
      endedAt: null,
      result: null,
      winnerUserId: null,
      endReason: null,
      turnStartedAt: 'x',
      turnTimeoutSeconds: 60,
      moves: [],
    },
    chatMessages: [],
    createdAt: 'x',
  };
}

function mount(seatCount = 2) {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      Host,
      TranslocoTestingModule.forRoot({
        langs: {
          en: {
            game: {
              room: {
                'host-label': 'Host',
                'seat-black': 'Black',
                'seat-white': 'White',
                'seat-empty': 'Open',
                'spectators-label': 'Spectators',
                'status-waiting': 'Waiting',
                'status-playing': 'Playing',
                'status-finished': 'Finished',
              },
              turn: {
                'your-turn': 'Your turn',
                'black-turn': 'Black turn',
                'white-turn': 'White turn',
                'countdown-label': 'Time left',
              },
              actions: {
                resign: 'Resign',
                leave: 'Leave',
                urge: 'Urge',
              },
            },
          },
        },
        translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [
      { provide: Dialog, useValue: { open: () => ({ closed: { subscribe: () => ({}) } }) } },
      provideRouter([]),
      // **座位数来自描述符,不来自「坐了几个人」。** 见 sidebar.ts 上那段说明。
      {
        provide: GameCapabilitiesService,
        useValue: new StubGameCapabilities([
          {
            gameKey: 'gomoku',
            isRated: true,
            supportsHumanVsHuman: true,
            supportsAi: true,
            seatCount,
            rows: 15,
            cols: 15,
          },
        ]),
      },
    ],
  });
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  return fixture;
}

describe('RoomSidebar', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('lists all three players when the room has three seats', () => {
    // **也是在浏览器里发现的,而且是同一个缺陷的第二处。** `add-web-doudizhu` 把「轮到谁」
    // 改成了座位号,却没看旁边这份名单:它只列黑白两个,于是**2 号座位上的人在自己的房间里
    // 根本不出现**。上面那条测试当时是绿的 —— 它问的是另一个问题。
    const fixture = mount(3);
    fixture.componentInstance.state.set({
      ...baseState(),
      seats: [
        { index: 0, player: { id: 'u-1', username: 'alice' } },
        { index: 1, player: { id: 'u-2', username: 'bob' } },
        { index: 2, player: { id: 'u-3', username: 'carol' } },
      ],
    });
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    for (const name of ['alice', 'bob', 'carol']) expect(text).toContain(name);
    // 三座位时「黑方 / 白方」那两个标签 MUST NOT 再出现 —— 那一桌上没有黑白。
    expect(text).not.toContain('Black');
    expect(text).not.toContain('White');
  });

  it('still says black and white for a two-seat room', () => {
    // **颜色留着,而这不是遗留。** 你正看着一张摆着黑白子的棋盘,而「谁是黑方」是座位号
    // 给不出的信息。大厅行的答案相反(`fix-lobby-seats`),因为它是跨棋种的列表、不是棋盘 ——
    // 同一个问题,两个层次,两个答案。
    const fixture = mount(2);
    fixture.componentInstance.state.set(baseState());
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Black');
    expect(text).toContain('White');
  });

  it('a WAITING three-seat room does not fall back to black and white', () => {
    // **这是这个变更修的那一格,而它此前红不了** —— 判据是 `seats.length`,
    // 而一个还差一个人的三座位房间那个数是 2,于是它落进了颜色分支。
    // 在浏览器里量到的原文:`Black: Baa11… White: Caa11…`。
    const fixture = mount(3);
    fixture.componentInstance.state.set({
      ...baseState(),
      status: 'Waiting',
      game: null,
      seats: [
        { index: 0, player: { id: 'u-1', username: 'alice' } },
        { index: 1, player: { id: 'u-2', username: 'bob' } },
      ],
    });
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).not.toContain('Black');
    expect(text).not.toContain('White');
    expect(text).toContain('alice');
    expect(text).toContain('bob');
  });

  it('a waiting three-seat room shows the empty seat', () => {
    // 座位数已知之后才画得出空座位。在它之前,泛化那一支只画得出在座的人 ——
    // 于是一个还差一个人的房间看不出自己还差一个。
    const fixture = mount(3);
    fixture.componentInstance.state.set({
      ...baseState(),
      status: 'Waiting',
      game: null,
      seats: [{ index: 0, player: { id: 'u-1', username: 'alice' } }],
    });
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Open');
    // 两个空座位,两处「Open」—— 一处的话说明只画了在座的人加一个占位。
    expect(text.match(/Open/g)).toHaveLength(2);
  });

  it('claims nothing while the descriptor has not arrived', () => {
    // 描述符没到时 `seatCount` 是 null,于是它 MUST NOT 猜三座位。
    // RoomPage 的 loading 状态本来就含 `!capabilities.loaded()`,所以整页是骨架屏;
    // 这一条钉的是即便被渲染,它也不会画出一份凭空的座位名单。
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [Host, TranslocoTestingModule.forRoot({
        langs: { en: {} },
        translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
        preloadLangs: true,
      })],
      providers: [
        { provide: Dialog, useValue: { open: () => ({ closed: { subscribe: () => ({}) } }) } },
        provideRouter([]),
        { provide: GameCapabilitiesService, useValue: new StubGameCapabilities([]) },
      ],
    });
    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.state.set(baseState());
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    // 没有描述符 → 不是「多于两个座位」→ 走两座位那一支,而它读的是 black / white,
    // 那两个字段在快照里本来就有。**它不编造座位。**
    expect(text).toContain('alice');
    expect(text).toContain('bob');
  });
});
