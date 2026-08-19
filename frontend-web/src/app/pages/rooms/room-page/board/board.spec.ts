import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { beforeEach, describe, expect, it } from 'vitest';
import type { RoomState } from '../../../../core/api/models/room.model';
import { Board } from './board';

@Component({
  selector: 'app-board-host',
  standalone: true,
  imports: [Board],
  template: `
    <app-board
      [state]="state()"
      [mySide]="mySide()"
      [submitting]="submitting()"
      [readonly]="readonly()"
      [rows]="rows()"
      [cols]="cols()"
      (cellClick)="last = $event"
    />
  `,
})
class Host {
  readonly state = signal<RoomState | null>(null);
  readonly mySide = signal<'black' | 'white' | 'spectator'>('spectator');
  readonly submitting = signal(false);
  readonly readonly = signal(false);
  readonly rows = signal(15);
  readonly cols = signal(15);
  last: { row: number; col: number } | null = null;
}

function makeState(overrides: Partial<RoomState> = {}): RoomState {
  return {
    id: 'r-1',
    name: 'r',
    gameKey: 'gomoku',
    status: 'Playing',
    host: { id: 'u-1', username: 'alice' },
    black: { id: 'u-1', username: 'alice' },
    white: { id: 'u-2', username: 'bob' },
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
    ...overrides,
  };
}

function mount() {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      Host,
      TranslocoTestingModule.forRoot({
        langs: { en: {} },
        translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
  });
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  return fixture;
}

function allButtons(fixture: ReturnType<typeof mount>): HTMLButtonElement[] {
  return Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[];
}

describe('Board', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('renders 225 cells', () => {
    const fixture = mount();
    fixture.componentInstance.state.set(makeState());
    fixture.detectChanges();
    expect(allButtons(fixture).length).toBe(225);
  });

  it('cell click on my turn emits with correct coords', () => {
    const fixture = mount();
    fixture.componentInstance.state.set(makeState());
    fixture.componentInstance.mySide.set('black');
    fixture.detectChanges();
    const buttons = allButtons(fixture);
    // row 7 col 7 → index 7 * 15 + 7 = 112
    buttons[112].click();
    expect(fixture.componentInstance.last).toEqual({ row: 7, col: 7 });
  });

  it('opponent turn → button disabled, no emit', () => {
    const fixture = mount();
    fixture.componentInstance.state.set(makeState({
      game: { ...makeState().game!, currentSeat: 1 },
    }));
    fixture.componentInstance.mySide.set('black');
    fixture.detectChanges();
    const buttons = allButtons(fixture);
    expect(buttons[0].disabled).toBe(true);
    buttons[0].click();
    expect(fixture.componentInstance.last).toBeNull();
  });

  it('spectator: all cells disabled', () => {
    const fixture = mount();
    fixture.componentInstance.state.set(makeState());
    fixture.componentInstance.mySide.set('spectator');
    fixture.detectChanges();
    const buttons = allButtons(fixture);
    expect(buttons.every((b) => b.disabled)).toBe(true);
  });

  it('readonly mode: all cells disabled', () => {
    const fixture = mount();
    fixture.componentInstance.state.set(makeState());
    fixture.componentInstance.mySide.set('black');
    fixture.componentInstance.readonly.set(true);
    fixture.detectChanges();
    const buttons = allButtons(fixture);
    expect(buttons.every((b) => b.disabled)).toBe(true);
  });

  it('last move gets the highlight class', () => {
    const fixture = mount();
    fixture.componentInstance.state.set(
      makeState({
        game: {
          ...makeState().game!,
          moves: [
            {
              ply: 1,
              row: 3,
              col: 4,
              seat: 0,
              playedAt: 'x',
            },
          ],
          currentSeat: 1,
        },
      }),
    );
    fixture.componentInstance.mySide.set('white');
    fixture.detectChanges();
    const buttons = allButtons(fixture);
    const idx = 3 * 15 + 4;
    expect(buttons[idx].classList.contains('board-cell--last-move')).toBe(true);
    expect(buttons[idx].getAttribute('aria-describedby')).toBe('board-last-move-label');
  });

  it('finished game: all buttons disabled', () => {
    const fixture = mount();
    fixture.componentInstance.state.set(makeState({ status: 'Finished' }));
    fixture.componentInstance.mySide.set('black');
    fixture.detectChanges();
    expect(allButtons(fixture).every((b) => b.disabled)).toBe(true);
  });

  // ---- 尺寸参数化 ----

  it('renders 9 cells at 3x3', () => {
    const fixture = mount();
    fixture.componentInstance.rows.set(3);
    fixture.componentInstance.cols.set(3);
    fixture.componentInstance.state.set(makeState({ gameKey: 'tictactoe' }));
    fixture.detectChanges();
    expect(allButtons(fixture).length).toBe(9);
  });

  it('drives the CSS grid through custom properties, not Tailwind classes', () => {
    // A class like grid-cols-3 is not knowable at compile time, so Tailwind never
    // emits it and the grid silently collapses to one column. The size has to ride
    // on inline custom properties.
    const fixture = mount();
    fixture.componentInstance.rows.set(3);
    fixture.componentInstance.cols.set(3);
    fixture.detectChanges();
    const grid = fixture.nativeElement.querySelector('.board-grid') as HTMLElement;
    expect(grid.style.getPropertyValue('--board-rows')).toBe('3');
    expect(grid.style.getPropertyValue('--board-cols')).toBe('3');
  });

  it('hides the 15x15 star points on other sizes', () => {
    const fixture = mount();
    const grid = () => fixture.nativeElement.querySelector('.board-grid') as HTMLElement;

    fixture.detectChanges();
    expect(grid().classList.contains('board-grid--no-stars')).toBe(false);

    fixture.componentInstance.rows.set(3);
    fixture.componentInstance.cols.set(3);
    fixture.detectChanges();
    expect(grid().classList.contains('board-grid--no-stars')).toBe(true);
  });

  it('ignores plies outside the board instead of throwing', () => {
    // If the client's idea of the size ever disagrees with the server's, the board
    // should look wrong — not blank the page.
    const fixture = mount();
    fixture.componentInstance.rows.set(3);
    fixture.componentInstance.cols.set(3);
    fixture.componentInstance.state.set(
      makeState({
        gameKey: 'tictactoe',
        game: {
          ...makeState().game!,
          moves: [
            { ply: 1, row: 0, col: 0, seat: 0, playedAt: 'x' },
            { ply: 2, row: 7, col: 7, seat: 1, playedAt: 'x' },
          ],
        },
      }),
    );

    expect(() => fixture.detectChanges()).not.toThrow();
    const buttons = allButtons(fixture);
    expect(buttons.length).toBe(9);
    expect(buttons[0].querySelector('.board-stone--black')).not.toBeNull();
  });

  it('paints seat 0 black and seat 1 white', () => {
    // **变异测试逼出来的一条。** 把 `seatStone` 改成永远返回 'Black',744 条测试**全绿** ——
    // 没有任何一条断言过"1 号座位画成白子"。上面那条只查了 0 号,而它本来是在测越界。
    //
    // 这条断言的是这次改动的**全部意义**:线上说座位,显示层把座位读成颜色。
    // 读错了的症状是"两个人下同一种颜色",而那在屏幕上是最明显的一种坏。
    const fixture = mount();
    fixture.componentInstance.state.set(
      makeState({
        game: {
          ...makeState().game!,
          moves: [
            { ply: 1, row: 0, col: 0, seat: 0, playedAt: 'x' },
            { ply: 2, row: 0, col: 1, seat: 1, playedAt: 'x' },
          ],
        },
      }),
    );
    fixture.detectChanges();

    const buttons = allButtons(fixture);
    expect(buttons[0].querySelector('.board-stone--black')).not.toBeNull();
    expect(buttons[0].querySelector('.board-stone--white')).toBeNull();
    expect(buttons[1].querySelector('.board-stone--white')).not.toBeNull();
    expect(buttons[1].querySelector('.board-stone--black')).toBeNull();
  });
});
