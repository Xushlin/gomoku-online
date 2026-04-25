import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { UsersApiService } from '../../../../core/api/users-api.service';
import { AuthService } from '../../../../core/auth/auth.service';
import { LanguageService } from '../../../../core/i18n/language.service';
import { MyRecentGamesCard } from './my-recent-games';

const sampleGames = [
  {
    roomId: 'r-1',
    name: 'Match 1',
    black: { id: 'u-me', username: 'alice' },
    white: { id: 'u-2', username: 'bob' },
    startedAt: '2026-04-25T00:00:00Z',
    endedAt: '2026-04-25T00:05:00Z',
    result: 'BlackWin' as const,
    winnerUserId: 'u-me',
    endReason: 'Connected5' as const,
    moveCount: 17,
  },
  {
    roomId: 'r-2',
    name: 'Match 2',
    black: { id: 'u-3', username: 'carol' },
    white: { id: 'u-me', username: 'alice' },
    startedAt: '2026-04-24T00:00:00Z',
    endedAt: '2026-04-24T00:05:00Z',
    result: 'BlackWin' as const,
    winnerUserId: 'u-3',
    endReason: 'Resigned' as const,
    moveCount: 8,
  },
];

class StubUsers {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  getProfile: any = vi.fn();
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  search: any = vi.fn();
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  getGames: any = vi.fn(() =>
    of({ items: sampleGames, total: 2, page: 1, pageSize: 5 }),
  );
}

function mount(opts: { userId?: string | null; getGames?: ReturnType<typeof vi.fn> } = {}) {
  const users = new StubUsers();
  if (opts.getGames) users.getGames = opts.getGames;
  const auth = {
    user: signal(opts.userId === null ? null : { id: opts.userId ?? 'u-me', username: 'alice', email: 'a@a' }),
    accessToken: signal(null),
    isAuthenticated: signal(true),
  };
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      MyRecentGamesCard,
      TranslocoTestingModule.forRoot({
        langs: { en: {} },
        translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [
      provideRouter([]),
      { provide: UsersApiService, useValue: users },
      { provide: AuthService, useValue: auth },
      { provide: LanguageService, useValue: { current: signal('en') } },
    ],
  });
  const router = TestBed.inject(Router);
  const navSpy = vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
  const fixture = TestBed.createComponent(MyRecentGamesCard);
  fixture.detectChanges();
  return { fixture, users, router, navSpy };
}

describe('MyRecentGamesCard', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('on mount: fetches getGames(meId, 1, 5)', () => {
    const { users } = mount();
    expect(users.getGames).toHaveBeenCalledWith('u-me', 1, 5);
  });

  it('row click navigates to /replay/:roomId', () => {
    const { fixture, navSpy } = mount();
    const rowButton = fixture.nativeElement.querySelector(
      'ul li button',
    ) as HTMLButtonElement;
    rowButton.click();
    expect(navSpy).toHaveBeenCalledWith('/replay/r-1');
  });

  it('empty state shows the empty translation key', () => {
    const { fixture } = mount({
      getGames: vi.fn(() => of({ items: [], total: 0, page: 1, pageSize: 5 })),
    });
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text.toLowerCase()).toContain('empty');
  });

  it('error state surfaces retry button', () => {
    const { fixture } = mount({
      getGames: vi.fn(() => throwError(() => new Error('boom'))),
    });
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text.toLowerCase()).toContain('error');
    const retryBtn = fixture.nativeElement.querySelector(
      'button[type="button"]',
    ) as HTMLButtonElement;
    expect(retryBtn).toBeTruthy();
  });

  it('retry refires the request', () => {
    let calls = 0;
    const getGames = vi.fn(() => {
      calls += 1;
      if (calls === 1) return throwError(() => new Error('boom'));
      return of({ items: sampleGames, total: 2, page: 1, pageSize: 5 });
    });
    const { fixture, users } = mount({ getGames });
    expect(users.getGames).toHaveBeenCalledTimes(1);
    const retryBtn = fixture.nativeElement.querySelector(
      'button[type="button"]',
    ) as HTMLButtonElement;
    retryBtn.click();
    fixture.detectChanges();
    expect(users.getGames).toHaveBeenCalledTimes(2);
  });

  it('"View all" link points to /users/:meId', () => {
    const { fixture } = mount();
    const links: HTMLAnchorElement[] = Array.from(
      fixture.nativeElement.querySelectorAll('a'),
    );
    const viewAll = links.find((a) => a.textContent?.toLowerCase().includes('view-all'));
    expect(viewAll?.getAttribute('href')).toBe('/users/u-me');
  });
});
