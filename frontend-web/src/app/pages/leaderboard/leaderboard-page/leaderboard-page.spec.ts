import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { of, throwError } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { LeaderboardApiService } from '../../../core/api/leaderboard-api.service';
import type { LeaderboardEntry } from '../../../core/api/models/leaderboard.model';
import { GameCatalogService } from '../../../games/game-catalog.service';
import { LeaderboardPage } from './leaderboard-page';

const langs = {
  en: {
    leaderboard: {
      title: 'Leaderboard',
      'title-for': '{{game}} leaderboard',
      subtitle: 'sub',
      rank: 'Rank',
      player: 'Player',
      rating: 'Rating',
      record: 'W/L/D',
      'empty-title': 'Nobody has played this game yet.',
      'empty-hint': 'The board fills up as games finish.',
      error: "Couldn't load the leaderboard.",
      retry: 'Try again',
      pagination: 'Leaderboard pages',
      'page-indicator': 'Page {{page}} of {{total}}',
      'prev-page': 'Previous',
      'next-page': 'Next',
      'back-to-games': 'All games',
    },
    games: { gomoku: { title: 'Gomoku' } },
  },
};

function entry(rank: number, username: string, rating: number): LeaderboardEntry {
  return {
    rank,
    userId: `u-${rank}`,
    username,
    rating,
    gamesPlayed: 10,
    wins: 6,
    losses: 3,
    draws: 1,
  };
}

function mount(opts: {
  gameKey?: string;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  getPage?: any;
} = {}) {
  const gameKey = opts.gameKey ?? 'gomoku';
  const api = {
    top: vi.fn(),
    getPage:
      opts.getPage ??
      vi.fn(() => of({ items: [entry(1, 'alice', 1500)], total: 1, page: 1, pageSize: 20 })),
  };

  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      LeaderboardPage,
      TranslocoTestingModule.forRoot({
        langs,
        translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [
      provideRouter([]),
      { provide: LeaderboardApiService, useValue: api },
      {
        provide: GameCatalogService,
        useValue: {
          all: () => [],
          available: () => [],
          planned: () => [],
          byKey: (k: string) =>
            k === 'gomoku' ? { key: k, titleKey: 'games.gomoku.title' } : undefined,
        },
      },
      {
        provide: ActivatedRoute,
        useValue: {
          snapshot: { paramMap: { get: (k: string) => (k === 'gameKey' ? gameKey : null) } },
        },
      },
    ],
  });

  const fixture = TestBed.createComponent(LeaderboardPage);
  fixture.detectChanges();
  return { fixture, api, el: fixture.nativeElement as HTMLElement };
}

describe('LeaderboardPage', () => {
  it('fetches the ladder for the route\'s game key', () => {
    const { api } = mount({ gameKey: 'xiangqi' });
    expect(api.getPage).toHaveBeenCalledWith('xiangqi', 1, 20);
  });

  it('renders the rows the server returned', () => {
    const { el } = mount();
    expect(el.textContent).toContain('alice');
    expect(el.textContent).toContain('1500');
  });

  it('uses the server rank verbatim, not the row index', () => {
    // Page 2 starts at rank 21. Recomputing from the index would show 1.
    const getPage = vi.fn(() =>
      of({ items: [entry(21, 'ulf', 1300), entry(22, 'vera', 1290)], total: 40, page: 2, pageSize: 20 }),
    );
    const { el } = mount({ getPage });
    expect(el.textContent).toContain('21');
    expect(el.textContent).toContain('22');
  });

  it('an empty ladder says nobody has played, not "no data"', () => {
    // A new game's board is empty because it is new, not because anything
    // broke. Generic "no data" copy reads as the latter.
    const getPage = vi.fn(() => of({ items: [], total: 0, page: 1, pageSize: 20 }));
    const { el } = mount({ getPage });

    expect(el.textContent).toContain('Nobody has played this game yet.');
    expect(el.textContent).not.toContain("Couldn't load");
  });

  it('an unrated game key renders the empty state, not an error', () => {
    // The server answers 200 + empty for unrated and unregistered keys alike.
    const getPage = vi.fn(() => of({ items: [], total: 0, page: 1, pageSize: 20 }));
    const { el } = mount({ gameKey: 'tictactoe', getPage });

    expect(el.textContent).toContain('Nobody has played this game yet.');
    expect(el.textContent).not.toContain("Couldn't load");
  });

  it('an unregistered game key still renders, with a fallback heading', () => {
    const getPage = vi.fn(() => of({ items: [], total: 0, page: 1, pageSize: 20 }));
    const { el } = mount({ gameKey: 'a-game-nobody-registered', getPage });

    expect(el.textContent).toContain('Leaderboard');
    expect(el.textContent).toContain('Nobody has played this game yet.');
  });

  it('only a failed request is the error state', () => {
    const getPage = vi.fn(() => throwError(() => new Error('boom')));
    const { el } = mount({ getPage });

    expect(el.textContent).toContain("Couldn't load the leaderboard.");
    expect(el.textContent).not.toContain('Nobody has played');
  });

  it('retry re-requests the current page', () => {
    const getPage = vi.fn(() => throwError(() => new Error('boom')));
    const { el, api } = mount({ getPage });
    const button = el.querySelector('button') as HTMLButtonElement;
    button.click();
    expect(api.getPage).toHaveBeenCalledTimes(2);
  });

  it('next() asks for the following page when there is one', () => {
    const getPage = vi.fn(() =>
      of({ items: [entry(1, 'alice', 1500)], total: 40, page: 1, pageSize: 20 }),
    );
    const { fixture, api } = mount({ getPage });
    const next = [...fixture.nativeElement.querySelectorAll('button')].find(
      (b: HTMLButtonElement) => b.textContent?.includes('Next'),
    ) as HTMLButtonElement;

    next.click();

    expect(api.getPage).toHaveBeenLastCalledWith('gomoku', 2, 20);
  });

  it('the heading names the game when the manifest knows it', () => {
    const { el } = mount({ gameKey: 'gomoku' });
    expect(el.textContent).toContain('Gomoku leaderboard');
  });
});
