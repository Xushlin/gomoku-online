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
        langs: { en: {} },
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
    expect(users.getProfile).toHaveBeenCalledWith('u-1');
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
