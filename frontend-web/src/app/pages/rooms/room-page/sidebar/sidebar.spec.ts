import { Dialog } from '@angular/cdk/dialog';
import {
  DefaultGameCatalogService,
  GameCatalogService,
} from '../../../../games/game-catalog.service';
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

function mount(seatCount = 2, gameKey = 'gomoku') {
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
                'seat-empty': 'Open',
                'seat-label': 'Seat {{seat}}',
                'spectators-label': 'Spectators',
                'status-waiting': 'Waiting',
                'status-playing': 'Playing',
                'status-finished': 'Finished',
              },
              turn: {
                'your-turn': 'Your turn',
                'countdown-label': 'Time left',
              },
              // 席位名 —— 由 manifest 指到,所以这里要有真实的几个。
              seat: {
                black: 'Black',
                white: 'White',
                red: 'Red',
                first: 'First player',
                second: 'Second player',
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
      // **真的目录服务**,不是桩:席位名的数据源就是 manifest,而一个桩会让这一组
      // 测试在一个「象棋没有席位名」的世界里跑。这个坑本仓库付过四次账。
      { provide: GameCatalogService, useClass: DefaultGameCatalogService },
      { provide: Dialog, useValue: { open: () => ({ closed: { subscribe: () => ({}) } }) } },
      provideRouter([]),
      // **座位数来自描述符,不来自「坐了几个人」。** 见 sidebar.ts 上那段说明。
      {
        provide: GameCapabilitiesService,
        useValue: new StubGameCapabilities([
          {
            gameKey,
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

  /**
   * **象棋房说红黑,而这正是这个变更存在的理由。**
   *
   * 0 号座位在象棋里画的是 帥。此前侧栏写死读 `game.room.seat-black`,于是它管红方叫
   * 黑方、管黑方叫白方 —— 在浏览器里量到的原文是「黑方:eg1 / 白方:eg2」。
   */
  it('says red and black in a xiangqi room, and never white', () => {
    const fixture = mount(2, 'xiangqi');
    fixture.componentInstance.state.set({ ...baseState(), gameKey: 'xiangqi' });
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Red');
    expect(text).toContain('Black');
    expect(text).not.toContain('White');
  });

  /** 象棋残局是**伴生键** —— 它没有自己的清单,但要用象棋的席位名。 */
  it('says red and black in an endgame room too', () => {
    const fixture = mount(2, 'xiangqi-endgame');
    fixture.componentInstance.state.set({ ...baseState(), gameKey: 'xiangqi-endgame' });
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Red');
    expect(text).not.toContain('White');
  });

  /** 成语接龙没有棋盘,也没有颜色 —— 它说先手 / 后手。 */
  it('invents no colours for a game that has none', () => {
    const fixture = mount(2, 'idiom-chain');
    fixture.componentInstance.state.set({ ...baseState(), gameKey: 'idiom-chain' });
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('First player');
    expect(text).toContain('Second player');
    expect(text).not.toContain('Black');
    expect(text).not.toContain('White');
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

  /**
   * 描述符没到时,**一个座位都不画** —— 而这是本变更**改掉的**行为,不是它保住的。
   *
   * 从前:没有描述符 → 不算「多于两个座位」→ 落进两座位那一支 → 读 `room.black` /
   * `room.white`,于是画出两个人。那一支现在没了,而它是最后两处读这两个派生字段的地方。
   *
   * 现在:座位数未知 → **不知道该画几行,于是不画** —— 那是一句诚实的话,而不是退化。
   * 从前那个答案在一个三座位房间里是错的(它只画得出两行),而它看起来完全正常。
   *
   * 生产里看不到这一帧:`RoomPage.loading()` 里本来就含 `!capabilities.loaded()`,
   * 整页那时是骨架屏。这一条钉的是**即便被渲染,它也不编造任何东西**。
   */
  it('draws no seats at all while the descriptor has not arrived', () => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [Host, TranslocoTestingModule.forRoot({
        langs: { en: {} },
        translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
        preloadLangs: true,
      })],
      providers: [
      // **真的目录服务**,不是桩:席位名的数据源就是 manifest,而一个桩会让这一组
      // 测试在一个「象棋没有席位名」的世界里跑。这个坑本仓库付过四次账。
      { provide: GameCatalogService, useClass: DefaultGameCatalogService },
        { provide: Dialog, useValue: { open: () => ({ closed: { subscribe: () => ({}) } }) } },
        provideRouter([]),
        { provide: GameCapabilitiesService, useValue: new StubGameCapabilities([]) },
      ],
    });
    const fixture = TestBed.createComponent(Host);
    fixture.componentInstance.state.set(baseState());
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    // 房主那一行还在(它不是座位),而**座位名单是空的**。
    expect(text).toContain('alice');
    expect(text).not.toContain('bob');
    // 正面对照:既没有编号,也没有任何席位名 —— 否则「不画」可能只是文案没渲染出来。
    expect(text).not.toMatch(/Seat \d/);
    for (const label of ['Black', 'White', 'Red', 'First player', 'Second player']) {
      expect(text).not.toContain(label);
    }
  });
});
