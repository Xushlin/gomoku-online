import { Dialog } from '@angular/cdk/dialog';
import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { beforeEach, describe, expect, it } from 'vitest';
import type { RoomState } from '../../../../core/api/models/room.model';
import { RoomSidebar } from './sidebar';

@Component({
  selector: 'app-sidebar-host',
  standalone: true,
  imports: [RoomSidebar],
  template: `
    <app-room-sidebar
      [state]="state()"
      [mySide]="mySide()"
      [turnRemainingMs]="remaining()"
      [canUrge]="canUrge()"
    />
  `,
})
class Host {
  readonly state = signal<RoomState | null>(null);
  readonly mySide = signal<'black' | 'white' | 'spectator'>('black');
  readonly remaining = signal<number>(60_000);
  readonly canUrge = signal(false);
}

function baseState(): RoomState {
  return {
    id: 'r-1',
    name: 'Alice room',
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
  };
}

function mount() {
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
    ],
  });
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  return fixture;
}

describe('RoomSidebar', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('formats remaining time as M:SS', () => {
    const fixture = mount();
    fixture.componentInstance.state.set(baseState());
    fixture.componentInstance.remaining.set(65_000);
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('1:05');
  });

  it('adds text-danger when <=10s', () => {
    const fixture = mount();
    fixture.componentInstance.state.set(baseState());
    fixture.componentInstance.remaining.set(5_000);
    fixture.detectChanges();
    const countdownNode = fixture.nativeElement.querySelector('span.font-mono') as HTMLElement;
    expect(countdownNode.classList.contains('text-danger')).toBe(true);
  });

  it('spectator sees no player-only buttons', () => {
    const fixture = mount();
    fixture.componentInstance.state.set(baseState());
    fixture.componentInstance.mySide.set('spectator');
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).not.toContain('Resign');
    expect(text).not.toContain('Leave');
    expect(text).not.toContain('Urge');
  });
});
