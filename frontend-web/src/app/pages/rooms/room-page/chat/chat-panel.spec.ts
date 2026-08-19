import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { beforeEach, describe, expect, it } from 'vitest';
import type { ChatMessage, RoomState } from '../../../../core/api/models/room.model';
import { WRAPPING_UTILITIES, wrapsLongWords } from '../../../../testing/wrapping';
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
    game: null,
    chatMessages: [],
    createdAt: 'x',
  };
}

/** A message at the server-side cap, with no break opportunity anywhere in it. */
function longMessage(): ChatMessage {
  return {
    id: 'm-1',
    senderUserId: 'u-1',
    senderUsername: 'alice',
    channel: 'Room',
    content: 'A'.repeat(500),
    sentAt: '2026-08-18T10:00:00Z',
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
    // Rendering a message needs a router: the sender's name is a `routerLink` to the
    // profile page. Nothing here needed it before, because **no test in this file
    // had ever rendered a message** — they covered the tabs and the input only.
    providers: [provideRouter([])],
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

  it('breaks a long unbroken message instead of widening the room page', () => {
    // Chat content is capped at 500 characters server-side, and a long unbroken run
    // of them only stays inside a 375 px panel because the paragraph wraps. Nothing
    // asserted that until now: the class was load-bearing and unguarded, so a style
    // rewrite would have shipped a horizontally-scrolling room page that no unit test
    // could see.
    //
    // What this proves and does not: it catches the utility being removed from the
    // markup. It cannot catch the stylesheet ceasing to define it — jsdom has no
    // layout — and that half is a browser check, i.e. evidence rather than a guard.
    const fixture = mount();
    const host = fixture.componentInstance;
    host.state.set({ ...baseState(), chatMessages: [longMessage()] });
    fixture.detectChanges();

    const paragraph = fixture.nativeElement.querySelector(
      '[role="tabpanel"] p, p.break-words',
    ) as HTMLElement | null;

    expect(paragraph, 'the message paragraph should render').toBeTruthy();
    expect(
      wrapsLongWords(paragraph),
      `the message paragraph needs one of: ${WRAPPING_UTILITIES.join(', ')}`,
    ).toBe(true);
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
