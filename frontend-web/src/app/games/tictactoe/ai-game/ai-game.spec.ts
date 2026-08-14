import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { NEVER, of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { BotDifficulty, BotSide, RoomState } from '../../../core/api/models/room.model';
import { RoomsApiService } from '../../../core/api/rooms-api.service';
import { TicTacToeAiGame } from './ai-game';

interface CreateCall {
  readonly name: string;
  readonly difficulty: BotDifficulty;
  readonly humanSide: BotSide | undefined;
  readonly gameKey: string | undefined;
}

type Outcome = 'ok' | 'fail' | 'pending';

function setup(outcome: Outcome = 'ok') {
  const calls: CreateCall[] = [];

  const rooms = {
    createAiRoom(
      name: string,
      difficulty: BotDifficulty,
      humanSide?: BotSide,
      gameKey?: string,
    ) {
      calls.push({ name, difficulty, humanSide, gameKey });
      if (outcome === 'fail') return throwError(() => new Error('boom'));
      // NEVER leaves the component stuck in its submitting state, which is what
      // the double-click guard has to be observed against.
      if (outcome === 'pending') return NEVER;
      return of({ id: 'room-42' } as RoomState);
    },
  };

  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      TicTacToeAiGame,
      TranslocoTestingModule.forRoot({
        langs: { en: {} },
        translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    // A real router rather than a stub: the template uses routerLink, which needs
    // ActivatedRoute, and stubbing Router alone does not provide it.
    providers: [provideRouter([]), { provide: RoomsApiService, useValue: rooms }],
  });

  const navigate = vi
    .spyOn(TestBed.inject(Router), 'navigateByUrl')
    .mockResolvedValue(true);

  const fixture = TestBed.createComponent(TicTacToeAiGame);
  fixture.detectChanges();
  return { fixture, calls, navigate };
}

type Fixture = ReturnType<typeof setup>['fixture'];

function buttons(fixture: Fixture): HTMLButtonElement[] {
  return Array.from(fixture.nativeElement.querySelectorAll('button')) as HTMLButtonElement[];
}

function click(fixture: Fixture, text: string): void {
  const btn = buttons(fixture).find((b) => b.textContent?.includes(text));
  expect(btn, `no button containing "${text}"`).toBeTruthy();
  btn!.click();
  fixture.detectChanges();
}

describe('TicTacToeAiGame', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('defaults to Medium and Black', () => {
    const { fixture } = setup();

    const pressed = buttons(fixture)
      .filter((b) => b.getAttribute('aria-pressed') === 'true')
      .map((b) => b.textContent?.trim());

    expect(pressed).toEqual(['tictactoe.difficulty-medium', 'tictactoe.side-black']);
  });

  it('sends the four arguments the backend needs, tictactoe included', () => {
    const { fixture, calls, navigate } = setup();

    click(fixture, 'tictactoe.difficulty-hard');
    click(fixture, 'tictactoe.side-white');
    click(fixture, 'tictactoe.start');

    expect(calls).toHaveLength(1);
    expect(calls[0].difficulty).toBe('Hard');
    expect(calls[0].humanSide).toBe('White');
    expect(calls[0].gameKey).toBe('tictactoe');
    expect(navigate).toHaveBeenCalledWith('/rooms/room-42');
  });

  it('generates a room name inside the server 3-50 character rule', () => {
    // The player is never asked for one: an AI room appears in no lobby list, so
    // the name is visible to nobody. The server still validates it.
    const { fixture, calls } = setup();

    click(fixture, 'tictactoe.start');

    expect(calls[0].name.trim().length).toBeGreaterThanOrEqual(3);
    expect(calls[0].name.trim().length).toBeLessThanOrEqual(50);
  });

  it('has no room-name input at all', () => {
    const { fixture } = setup();

    expect(fixture.nativeElement.querySelectorAll('input').length).toBe(0);
  });

  it('states up front that Hard is unbeatable and the game is unrated', () => {
    // Both facts belong before the first game, not after three losses.
    const { fixture } = setup();
    const text = fixture.nativeElement.textContent as string;

    expect(text).toContain('tictactoe.notice-unbeatable');
    expect(text).toContain('tictactoe.notice-unrated');
  });

  it('shows a retryable error and does not navigate when creation fails', () => {
    const { fixture, navigate } = setup('fail');

    click(fixture, 'tictactoe.start');

    expect(navigate).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('tictactoe.error-create-failed');

    const start = buttons(fixture).find((b) => b.textContent?.includes('tictactoe.start'));
    expect(start?.disabled).toBe(false);
  });

  it('does not fire a second request while one is in flight', () => {
    const { fixture, calls } = setup('pending');

    click(fixture, 'tictactoe.start');
    click(fixture, 'tictactoe.starting');

    expect(calls).toHaveLength(1);
  });
});
