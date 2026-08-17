import { HttpErrorResponse } from '@angular/common/http';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { PresenceApiService } from '../../../core/api/presence-api.service';
import { UsersApiService } from '../../../core/api/users-api.service';
import { LanguageService } from '../../../core/i18n/language.service';
import { GameCapabilitiesService } from '../../../games/game-capabilities.service';
import { StubGameCapabilities } from '../../../games/game-capabilities.stub';
import { GameCatalogService } from '../../../games/game-catalog.service';
import { ProfilePage } from './profile-page';

class StubUsers {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  getProfile: any = vi.fn(() =>
    of({
      id: 'u-1',
      username: 'alice',
      rating: 1280,
      gamesPlayed: 6,
      wins: 3,
      losses: 2,
      draws: 1,
      createdAt: '2025-12-01T00:00:00Z',
    }),
  );
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  getGames: any = vi.fn(() => of({ items: [], total: 0, page: 1, pageSize: 10 }));
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  search: any = vi.fn();
}

function activatedRoute(id: string | null) {
  return {
    snapshot: { paramMap: { get: (k: string) => (k === 'id' ? id : null) } },
  } as unknown as ActivatedRoute;
}

function routerStub() {
  return {
    navigate: vi.fn(() => Promise.resolve(true)),
    navigateByUrl: vi.fn(() => Promise.resolve(true)),
    createUrlTree: vi.fn(() => ({ toString: () => '/' })),
    serializeUrl: vi.fn(() => '/'),
    events: of(),
  };
}

function mount(opts: {
  id?: string | null;
  getProfile?: ReturnType<typeof vi.fn>;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  getUserOnline?: any;
  capabilities?: StubGameCapabilities;
} = {}) {
  const users = new StubUsers();
  if (opts.getProfile) users.getProfile = opts.getProfile;
  const router = routerStub();
  const presence = {
    getOnlineCount: vi.fn(),
    getUserOnline: opts.getUserOnline ?? vi.fn(() => of(true)),
  };
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      ProfilePage,
      TranslocoTestingModule.forRoot({
        langs: {
          en: {
            profile: {
              'game-switcher': { label: 'Record for' },
              'no-games-in-game': 'No finished games in this one yet.',
              'rating-label': 'Rating',
              'wins-label': 'Wins',
              'losses-label': 'Losses',
              'draws-label': 'Draws',
              'win-rate-label': 'Win rate',
              'joined-label': 'Joined',
            },
            games: {
              gomoku: { title: 'Gomoku' },
              xiangqi: { title: 'Xiangqi' },
            },
          },
        },
        translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: UsersApiService, useValue: users },
      { provide: PresenceApiService, useValue: presence },
      { provide: Router, useValue: router },
      { provide: ActivatedRoute, useValue: activatedRoute(opts.id ?? 'u-1') },
      { provide: LanguageService, useValue: { current: signal('en') } },
      { provide: GameCapabilitiesService, useValue: opts.capabilities ?? new StubGameCapabilities() },
      {
        provide: GameCatalogService,
        useValue: {
          all: () => [],
          available: () => [],
          planned: () => [],
          byKey: (k: string) => ({ key: k, titleKey: `games.${k}.title` }),
        },
      },
    ],
  });
  const fixture = TestBed.createComponent(ProfilePage);
  fixture.detectChanges();
  return { fixture, users, router, presence };
}

describe('ProfilePage', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('on init: fetches profile by route id', () => {
    const { users } = mount();
    expect(users.getProfile).toHaveBeenCalledWith('u-1', undefined);
  });

  it('404 sets notFound', () => {
    const { fixture } = mount({
      getProfile: vi.fn(() =>
        throwError(() => new HttpErrorResponse({ status: 404, statusText: 'Not Found' })),
      ),
    });
    const comp = fixture.componentInstance as unknown as { notFound: () => boolean };
    expect(comp.notFound()).toBe(true);
  });

  it('renders username + rating in the card', () => {
    const { fixture } = mount();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('alice');
    expect(text).toContain('1280');
  });

  it('win-rate label is computed correctly', () => {
    const { fixture } = mount();
    const comp = fixture.componentInstance as unknown as { winRateLabel: () => string };
    // 3 / (3+2+1) * 100 = 50.0%
    expect(comp.winRateLabel()).toBe('50.0%');
  });

  it('win-rate label is em-dash when no games', () => {
    const { fixture } = mount({
      getProfile: vi.fn(() =>
        of({
          id: 'u-2',
          username: 'newbie',
          rating: 1200,
          gamesPlayed: 0,
          wins: 0,
          losses: 0,
          draws: 0,
          createdAt: '2026-04-20T00:00:00Z',
        }),
      ),
    });
    const comp = fixture.componentInstance as unknown as { winRateLabel: () => string };
    expect(comp.winRateLabel()).toBe('—');
  });

  it('the switcher lists rated games only', () => {
    const { fixture } = mount({
      capabilities: new StubGameCapabilities([
        { gameKey: 'gomoku', isRated: true, supportsHumanVsHuman: true, supportsAi: true, rows: 15, cols: 15 },
        { gameKey: 'tictactoe', isRated: false, supportsHumanVsHuman: false, supportsAi: true, rows: 3, cols: 3 },
      ]),
    });
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Gomoku');
    expect(text).not.toContain('Tic-tac-toe');
  });

  it('renders the switcher even with a single rated game', () => {
    // It is the only thing on screen saying *which* game the rating belongs to.
    // Hide it and a gomoku 1500 reads as "their rating", full stop.
    const { fixture } = mount({ capabilities: StubGameCapabilities.rated('gomoku') });
    const group = fixture.nativeElement.querySelector('[role="group"]');
    expect(group).not.toBeNull();
    expect(group.querySelectorAll('button').length).toBe(1);
  });

  it('the first paint sends no gameKey, letting the server default answer', () => {
    const { users } = mount({ capabilities: StubGameCapabilities.rated('gomoku') });
    expect(users.getProfile).toHaveBeenCalledWith('u-1', undefined);
  });

  it('switching games re-fetches with ?gameKey=', () => {
    const { fixture, users } = mount({
      capabilities: StubGameCapabilities.rated('gomoku', 'xiangqi'),
    });
    const buttons = [...fixture.nativeElement.querySelectorAll('[role="group"] button')];
    const xiangqi = buttons.find((b) =>
      (b as HTMLElement).textContent?.includes('Xiangqi'),
    ) as HTMLButtonElement;

    xiangqi.click();

    expect(users.getProfile).toHaveBeenLastCalledWith('u-1', 'xiangqi');
  });

  it('clicking the already-active game does not re-fetch', () => {
    const { fixture, users } = mount({ capabilities: StubGameCapabilities.rated('gomoku') });
    const button = fixture.nativeElement.querySelector('[role="group"] button') as HTMLButtonElement;

    button.click();

    expect(users.getProfile).toHaveBeenCalledTimes(1);
  });

  it('switching games does NOT re-fetch presence', () => {
    // Presence is a property of the person, not of the game being viewed.
    const { fixture, presence } = mount({
      capabilities: StubGameCapabilities.rated('gomoku', 'xiangqi'),
    });
    const buttons = [...fixture.nativeElement.querySelectorAll('[role="group"] button')];
    (buttons.find((b) => (b as HTMLElement).textContent?.includes('Xiangqi')) as HTMLButtonElement).click();

    expect(presence.getUserOnline).toHaveBeenCalledTimes(1);
  });

  it('zero games in this one shows an empty state instead of 1200', () => {
    // The server answers 200 + initial values rather than 404, because "exists
    // but has not played this game" is a normal answer. Rendering that payload
    // verbatim would read as a beginner who HAS played. When a new game ships
    // that is true of nearly every user, so it is not an edge case.
    const { fixture } = mount({
      capabilities: StubGameCapabilities.rated('gomoku'),
      getProfile: vi.fn(() =>
        of({
          id: 'u-1',
          username: 'alice',
          rating: 1200,
          gamesPlayed: 0,
          wins: 0,
          losses: 0,
          draws: 0,
          createdAt: '2025-12-01T00:00:00Z',
        }),
      ),
    });
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';

    expect(text).toContain('No finished games in this one yet.');
    expect(text).not.toContain('1200');
  });

  it('a played game still shows the numbers', () => {
    const { fixture } = mount({ capabilities: StubGameCapabilities.rated('gomoku') });
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('1280');
    expect(text).not.toContain('No finished games in this one yet.');
  });

  it('no switcher at all when capabilities never loaded', () => {
    const { fixture } = mount({ capabilities: new StubGameCapabilities() });
    expect(fixture.nativeElement.querySelector('[role="group"]')).toBeNull();
  });

  it('presence dot has bg-success when online', () => {
    const { fixture } = mount({ getUserOnline: vi.fn(() => of(true)) });
    const dot = fixture.nativeElement.querySelector('h1 span.rounded-full') as HTMLElement | null;
    expect(dot?.classList.contains('bg-success')).toBe(true);
  });

  it('presence dot has bg-muted when offline', () => {
    const { fixture } = mount({ getUserOnline: vi.fn(() => of(false)) });
    const dot = fixture.nativeElement.querySelector('h1 span.rounded-full') as HTMLElement | null;
    expect(dot?.classList.contains('bg-muted')).toBe(true);
  });

  it('presence dot is omitted on getUserOnline failure', () => {
    const { fixture } = mount({
      getUserOnline: vi.fn(() => throwError(() => new Error('boom'))),
    });
    const dot = fixture.nativeElement.querySelector('h1 span.rounded-full');
    expect(dot).toBeNull();
  });
});
