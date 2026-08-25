import { Dialog } from '@angular/cdk/dialog';
import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { GameCapabilitiesService } from '../../../../games/game-capabilities.service';
import { StubGameCapabilities } from '../../../../games/game-capabilities.stub';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { of } from 'rxjs';
import { beforeEach, describe, expect, it } from 'vitest';
import type { RoomState } from '../../../../core/api/models/room.model';
import { RoomActionBar } from './action-bar';

@Component({
  selector: 'app-action-bar-host',
  standalone: true,
  imports: [RoomActionBar],
  template: `
    <app-room-action-bar
      [state]="state()"
      [mySeat]="mySeat()"
      [turnRemainingMs]="remaining()"
      [canUrge]="canUrge()"
      (resign)="resigned = resigned + 1"
      (leave)="left = left + 1"
      (urge)="urged = urged + 1"
    />
  `,
})
class Host {
  readonly state = signal<RoomState | null>(null);
  readonly mySeat = signal<number | null>(0);
  readonly remaining = signal<number>(60_000);
  readonly canUrge = signal(false);
  resigned = 0;
  left = 0;
  urged = 0;
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

/** 打开的 dialog 与它返回的结果 —— 辞局那条要断言「先弹框,确认之后才发」。 */
const dialogCalls: unknown[] = [];
let dialogResult: boolean | undefined = true;

function mount(seatCount = 2) {
  dialogCalls.length = 0;
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      Host,
      TranslocoTestingModule.forRoot({
        langs: {
          en: {
            game: {
              turn: {
                'your-turn': 'Your turn',
                'black-turn': 'Black turn',
                'white-turn': 'White turn',
                'seat-turn': 'Seat {{seat}} to play',
                'countdown-label': 'Time left',
              },
              actions: { resign: 'Resign', leave: 'Leave', urge: 'Urge' },
            },
          },
        },
        translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [
      {
        provide: Dialog,
        useValue: {
          open: (...args: unknown[]) => {
            dialogCalls.push(args);
            return { closed: of(dialogResult) };
          },
        },
      },
      // 座位数来自描述符,不来自「坐了几个人」—— 见 action-bar.ts。
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

const bar = (fixture: { nativeElement: HTMLElement }) =>
  fixture.nativeElement.querySelector('[data-testid="action-bar"]') as HTMLElement | null;

describe('RoomActionBar', () => {
  beforeEach(() => {
    dialogResult = true;
    TestBed.resetTestingModule();
  });

  it('formats remaining time as M:SS', () => {
    const fixture = mount();
    fixture.componentInstance.state.set(baseState());
    fixture.componentInstance.remaining.set(65_000);
    fixture.detectChanges();
    expect(bar(fixture)?.textContent).toContain('1:05');
  });

  it('adds text-danger when <=10s, and not before', () => {
    const fixture = mount();
    fixture.componentInstance.state.set(baseState());
    fixture.componentInstance.remaining.set(30_000);
    fixture.detectChanges();
    const countdown = () => bar(fixture)?.querySelector('span.font-mono') as HTMLElement;
    // 两头都要有样本:只断言「低于阈值变红」的话,一个恒红的实现同样是绿的。
    expect(countdown().classList.contains('text-danger')).toBe(false);

    fixture.componentInstance.remaining.set(5_000);
    fixture.detectChanges();
    expect(countdown().classList.contains('text-danger')).toBe(true);
  });

  it('gives a player the three actions, and a spectator none of them', () => {
    const fixture = mount();
    fixture.componentInstance.state.set(baseState());
    fixture.detectChanges();
    // 前置条件:玩家视角下这三个按钮确实画出来了。少了这一条,下面那三个
    // not.toContain 在「什么都没画」时同样是绿的。
    const asPlayer = bar(fixture)?.textContent ?? '';
    expect(asPlayer).toContain('Resign');
    expect(asPlayer).toContain('Leave');
    expect(asPlayer).toContain('Urge');

    fixture.componentInstance.mySeat.set(null);
    fixture.detectChanges();
    const asSpectator = bar(fixture)?.textContent ?? '';
    expect(asSpectator).not.toContain('Resign');
    expect(asSpectator).not.toContain('Leave');
    expect(asSpectator).not.toContain('Urge');
    // 而围观者仍然看得到「现在怎么样」。
    expect(asSpectator).toContain('Black turn');
  });

  it('says which seat is to play when there are more than two', () => {
    const fixture = mount(3);
    const state = baseState();
    fixture.componentInstance.state.set({ ...state, gameKey: 'gomoku', game: { ...state.game!, currentSeat: 2 } });
    fixture.detectChanges();
    const text = bar(fixture)?.textContent ?? '';
    expect(text).toContain('Seat 3 to play');
    expect(text).not.toContain('White turn');
  });

  it('still says black and white for a two-seat game', () => {
    // 上一条的正面对照。没有它,一个「永远说座位号」的实现在上一条下也是绿的。
    const fixture = mount(2);
    fixture.componentInstance.state.set(baseState());
    fixture.detectChanges();
    const text = bar(fixture)?.textContent ?? '';
    expect(text).toContain('Black turn');
    expect(text).not.toContain('Seat 1 to play');
  });

  it('offers resign at two seats and never at three', () => {
    /*
     * 领域层**故意**拒绝三座位的认输(`Room.Resign` 要指出唯一的赢家),而在浏览器里
     * 点那个按钮拿到的是 **500**。所以按钮本身不该出现。
     *
     * 两头都在这一条里:少了「两座位下它确实在」,一个把按钮整个删掉的实现同样是绿的。
     */
    const twoSeats = mount(2);
    twoSeats.componentInstance.state.set(baseState());
    twoSeats.detectChanges();
    expect(bar(twoSeats)?.querySelector('[data-testid="resign"]')).toBeTruthy();

    const threeSeats = mount(3);
    threeSeats.componentInstance.state.set(baseState());
    threeSeats.detectChanges();
    expect(bar(threeSeats)?.querySelector('[data-testid="resign"]')).toBeNull();
    // 而离开与催促照旧 —— 拒绝的是认输,不是整组按钮。
    expect(bar(threeSeats)?.querySelector('[data-testid="leave"]')).toBeTruthy();
    expect(bar(threeSeats)?.querySelector('[data-testid="urge"]')).toBeTruthy();
  });

  it('offers no resign while the descriptor has not arrived', () => {
    /*
     * 判据是 `seatCount === 2`,不是 `!moreThanTwoSeats()` —— 后者在描述符缺席时会说
     * 「可以认输」(`!(0 > 2)`)。**这条断言就是那句注释的执行版**:变异证明了少了它,
     * 两种写法在既有样本下完全等价。
     */
    const fixture = mount(2);
    const state = baseState();
    // 这个棋种不在描述符表里 —— `capabilities.of()` 返回 undefined。
    fixture.componentInstance.state.set({ ...state, gameKey: 'a-game-nobody-registered' });
    fixture.detectChanges();

    // 前置条件:操作条本身画出来了,不是整块没渲染。
    expect(bar(fixture)).toBeTruthy();
    expect(bar(fixture)?.querySelector('[data-testid="leave"]')).toBeTruthy();
    expect(bar(fixture)?.querySelector('[data-testid="resign"]')).toBeNull();
  });

  it('resign opens a CDK dialog first, and only emits after confirmation', () => {
    const fixture = mount();
    fixture.componentInstance.state.set(baseState());
    fixture.detectChanges();
    (bar(fixture)?.querySelector('[data-testid="resign"]') as HTMLButtonElement).click();
    expect(dialogCalls.length).toBe(1);
    expect(fixture.componentInstance.resigned).toBe(1);
  });

  it('resign emits nothing when the dialog is dismissed', () => {
    dialogResult = undefined;
    const fixture = mount();
    fixture.componentInstance.state.set(baseState());
    fixture.detectChanges();
    (bar(fixture)?.querySelector('[data-testid="resign"]') as HTMLButtonElement).click();
    expect(dialogCalls.length).toBe(1);
    expect(fixture.componentInstance.resigned).toBe(0);
  });

  it('urge is disabled until it is allowed', () => {
    const fixture = mount();
    fixture.componentInstance.state.set(baseState());
    fixture.detectChanges();
    const urge = () => bar(fixture)?.querySelector('[data-testid="urge"]') as HTMLButtonElement;
    expect(urge().disabled).toBe(true);

    fixture.componentInstance.canUrge.set(true);
    fixture.detectChanges();
    expect(urge().disabled).toBe(false);
    urge().click();
    expect(fixture.componentInstance.urged).toBe(1);
  });

  it('leave emits', () => {
    const fixture = mount();
    fixture.componentInstance.state.set(baseState());
    fixture.detectChanges();
    (bar(fixture)?.querySelector('[data-testid="leave"]') as HTMLButtonElement).click();
    expect(fixture.componentInstance.left).toBe(1);
  });

  it('every button in the bar carries the 44px minimum', () => {
    const fixture = mount();
    fixture.componentInstance.state.set(baseState());
    fixture.detectChanges();
    const buttons = [...(bar(fixture)?.querySelectorAll('button') ?? [])];
    // 前置条件:样本非空。「每一个都 …」对空集合恒真。
    expect(buttons.length).toBeGreaterThanOrEqual(3);
    // jsdom 没有布局,所以这里只能证明**写了**这个类,不能证明它有 44 px 高。
    // 高度是在浏览器里量的 —— 见 tasks.md §6。
    for (const b of buttons) expect(b.classList.contains('min-h-11')).toBe(true);
  });

  it('a spectator of a room that is not playing gets no bar at all', () => {
    const fixture = mount();
    const state = baseState();
    fixture.componentInstance.state.set({ ...state, status: 'Waiting', game: null });
    fixture.componentInstance.mySeat.set(null);
    fixture.detectChanges();
    expect(bar(fixture)).toBeNull();
  });

  it('a player in a waiting room still gets leave, but no resign or urge', () => {
    const fixture = mount();
    const state = baseState();
    fixture.componentInstance.state.set({ ...state, status: 'Waiting', game: null });
    fixture.detectChanges();
    const text = bar(fixture)?.textContent ?? '';
    expect(text).toContain('Leave');
    expect(text).not.toContain('Resign');
    expect(text).not.toContain('Urge');
  });
});
