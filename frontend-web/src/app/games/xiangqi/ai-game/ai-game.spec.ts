import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { NEVER, of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import en from '../../../../../public/i18n/en.json';
import zhCN from '../../../../../public/i18n/zh-CN.json';
import type { BotDifficulty, BotSide, RoomState } from '../../../core/api/models/room.model';
import { RoomsApiService } from '../../../core/api/rooms-api.service';
import { XiangqiAiGame } from './ai-game';

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
    createAiRoom(name: string, difficulty: BotDifficulty, humanSide?: BotSide, gameKey?: string) {
      calls.push({ name, difficulty, humanSide, gameKey });
      if (outcome === 'fail') return throwError(() => new Error('boom'));
      if (outcome === 'pending') return NEVER;
      return of({ id: 'room-7' } as RoomState);
    },
  };

  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      XiangqiAiGame,
      TranslocoTestingModule.forRoot({
        langs: { en: {} },
        translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [provideRouter([]), { provide: RoomsApiService, useValue: rooms }],
  });

  const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);

  const fixture = TestBed.createComponent(XiangqiAiGame);
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

describe('XiangqiAiGame', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('defaults to Medium and red (the first mover)', () => {
    const { fixture } = setup();

    const pressed = buttons(fixture)
      .filter((b) => b.getAttribute('aria-pressed') === 'true')
      .map((b) => b.textContent?.trim());

    expect(pressed).toEqual(['xiangqi.difficulty-medium', 'xiangqi.side-black']);
  });

  it('sends the four arguments the backend needs, xiangqi included', () => {
    const { fixture, calls, navigate } = setup();

    click(fixture, 'xiangqi.difficulty-hard');
    click(fixture, 'xiangqi.side-white');
    click(fixture, 'xiangqi.start');

    expect(calls).toHaveLength(1);
    expect(calls[0].difficulty).toBe('Hard');
    expect(calls[0].humanSide).toBe('White');
    expect(calls[0].gameKey).toBe('xiangqi');
    expect(navigate).toHaveBeenCalledWith('/rooms/room-7');
  });

  it('generates a room name inside the server 3-50 character rule', () => {
    const { fixture, calls } = setup();

    click(fixture, 'xiangqi.start');

    expect(calls[0].name.trim().length).toBeGreaterThanOrEqual(3);
    expect(calls[0].name.trim().length).toBeLessThanOrEqual(50);
  });

  it('has no room-name input at all', () => {
    const { fixture } = setup();

    expect(fixture.nativeElement.querySelectorAll('input').length).toBe(0);
  });

  it('says what the AI is and that the game is unrated', () => {
    const { fixture } = setup();
    const text = fixture.nativeElement.textContent as string;

    expect(text).toContain('xiangqi.notice-ai');
    expect(text).toContain('xiangqi.notice-unrated');
  });

  it('promises nothing about how strong any difficulty is', () => {
    // 一字棋's page says "you cannot beat Hard" because there it is provable by
    // exhaustive search. Xiangqi cannot be searched exhaustively, so the same
    // sentence here would be an unverifiable claim — and an unverifiable claim is
    // worse than none. This reads the shipped copy, not the template, because the
    // temptation to write it lives in the locale files.
    const forbidden = [
      /不可战胜/,
      /打不赢/,
      /必和/,
      /unbeatable/i,
      /cannot be beaten/i,
      /cannot beat/i,
    ];

    for (const [locale, tree] of Object.entries({ 'zh-CN': zhCN, en })) {
      const copy = JSON.stringify((tree as Record<string, unknown>)['xiangqi']);
      for (const pattern of forbidden) {
        expect(copy, `${locale} must not promise ${pattern}`).not.toMatch(pattern);
      }
    }
  });

  it('shows a retryable error and does not navigate when creation fails', () => {
    const { fixture, navigate } = setup('fail');

    click(fixture, 'xiangqi.start');

    expect(navigate).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('xiangqi.error-create-failed');

    const start = buttons(fixture).find((b) => b.textContent?.includes('xiangqi.start'));
    expect(start?.disabled).toBe(false);
  });

  it('does not fire a second request while one is in flight', () => {
    const { fixture, calls } = setup('pending');

    click(fixture, 'xiangqi.start');
    click(fixture, 'xiangqi.starting');

    expect(calls).toHaveLength(1);
  });
});
