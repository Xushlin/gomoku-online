import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { beforeEach, describe, expect, it } from 'vitest';
import type { RoomState } from '../../../../core/api/models/room.model';
import { ChatPanel, type SendChatPayload } from './chat-panel';

@Component({
  selector: 'app-chat-host',
  standalone: true,
  imports: [ChatPanel],
  template: `
    <app-chat-panel
      [state]="state()"
      [mySide]="mySide()"
      [canSend]="canSend()"
      (send)="last = $event"
    />
  `,
})
class Host {
  readonly state = signal<RoomState | null>(null);
  readonly mySide = signal<'black' | 'white' | 'spectator'>('black');
  readonly canSend = signal(true);
  last: SendChatPayload | null = null;
}

function baseState(): RoomState {
  return {
    id: 'r-1',
    name: 'r',
    status: 'Playing',
    host: { id: 'u-1', username: 'alice' },
    black: { id: 'u-1', username: 'alice' },
    white: { id: 'u-2', username: 'bob' },
    spectators: [],
    game: null,
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

function tabs(fixture: ReturnType<typeof mount>): HTMLButtonElement[] {
  return Array.from(
    fixture.nativeElement.querySelectorAll('button[role="tab"]'),
  ) as HTMLButtonElement[];
}

describe('ChatPanel', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('player sees only Room tab', () => {
    const fixture = mount();
    fixture.componentInstance.state.set(baseState());
    fixture.componentInstance.mySide.set('black');
    fixture.detectChanges();
    expect(tabs(fixture).length).toBe(1);
  });

  it('spectator sees Room and Spectator tabs', () => {
    const fixture = mount();
    fixture.componentInstance.state.set(baseState());
    fixture.componentInstance.mySide.set('spectator');
    fixture.detectChanges();
    expect(tabs(fixture).length).toBe(2);
  });

  it('sends with active channel', () => {
    const fixture = mount();
    fixture.componentInstance.state.set(baseState());
    fixture.componentInstance.mySide.set('spectator');
    fixture.detectChanges();
    // switch to spectator tab
    tabs(fixture)[1].click();
    fixture.detectChanges();

    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    input.value = 'hello';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    const form = fixture.nativeElement.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(fixture.componentInstance.last).toEqual({ content: 'hello', channel: 'Spectator' });
  });

  it('empty input does not submit', () => {
    const fixture = mount();
    fixture.componentInstance.state.set(baseState());
    fixture.detectChanges();
    const form = fixture.nativeElement.querySelector('form') as HTMLFormElement;
    form.dispatchEvent(new Event('submit'));
    fixture.detectChanges();
    expect(fixture.componentInstance.last).toBeNull();
  });

  it('whitespace-only input does not submit', () => {
    const fixture = mount();
    fixture.componentInstance.state.set(baseState());
    fixture.detectChanges();
    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    input.value = '   ';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    const send = fixture.nativeElement.querySelector(
      'button[type="submit"]',
    ) as HTMLButtonElement;
    expect(send.disabled).toBe(true);
  });
});
