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
      (cellClick)="last = $event"
    />
  `,
})
class Host {
  readonly state = signal<RoomState | null>(null);
  readonly mySide = signal<'black' | 'white' | 'spectator'>('spectator');
  readonly submitting = signal(false);
  readonly readonly = signal(false);
  last: { row: number; col: number } | null = null;
}

function makeState(overrides: Partial<RoomState> = {}): RoomState {
  return {
    id: 'r-1',
    name: 'r',
    status: 'Playing',
    host: { id: 'u-1', username: 'alice' },
    black: { id: 'u-1', username: 'alice' },
    white: { id: 'u-2', username: 'bob' },
    spectators: [],
    game: {
      id: 'g-1',
      currentTurn: 'Black',
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
      game: { ...makeState().game!, currentTurn: 'White' },
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
              stone: 'Black',
              playedAt: 'x',
            },
          ],
          currentTurn: 'White',
        },
      }),
    );
    fixture.componentInstance.mySide.set('white');
    fixture.detectChanges();
    const buttons = allButtons(fixture);
    const idx = 3 * 15 + 4;
    expect(buttons[idx].classList.contains('ring-2')).toBe(true);
    expect(buttons[idx].getAttribute('aria-describedby')).toBe('board-last-move-label');
  });

  it('finished game: all buttons disabled', () => {
    const fixture = mount();
    fixture.componentInstance.state.set(makeState({ status: 'Finished' }));
    fixture.componentInstance.mySide.set('black');
    fixture.detectChanges();
    expect(allButtons(fixture).every((b) => b.disabled)).toBe(true);
  });
});
