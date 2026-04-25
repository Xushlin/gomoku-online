import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { UsersApiService } from '../../../../core/api/users-api.service';
import { LanguageService } from '../../../../core/i18n/language.service';
import { GamesList } from './games-list';

@Component({
  selector: 'app-games-list-host',
  standalone: true,
  imports: [GamesList],
  template: `<app-games-list [userId]="userId()" />`,
})
class Host {
  readonly userId = signal('u-1');
}

const sampleGames = [
  {
    roomId: 'r-1',
    name: 'Match 1',
    black: { id: 'u-1', username: 'alice' },
    white: { id: 'u-2', username: 'bob' },
    startedAt: '2026-04-23T00:00:00Z',
    endedAt: '2026-04-23T00:05:00Z',
    result: 'BlackWin' as const,
    winnerUserId: 'u-1',
    endReason: 'Connected5' as const,
    moveCount: 17,
  },
];

class StubUsers {
  getProfile = vi.fn();
  search = vi.fn();
  getGames = vi.fn(() =>
    of({ items: sampleGames, total: 1, page: 1, pageSize: 10 }),
  );
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

function mount() {
  const users = new StubUsers();
  const router = routerStub();
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
    providers: [
      { provide: UsersApiService, useValue: users },
      { provide: Router, useValue: router },
      { provide: ActivatedRoute, useValue: { snapshot: { paramMap: { get: () => null } } } },
      { provide: LanguageService, useValue: { current: signal('en') } },
    ],
  });
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  return { fixture, users, router };
}

describe('GamesList', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('fetches first page on init', () => {
    const { users } = mount();
    expect(users.getGames).toHaveBeenCalledWith('u-1', 1, 10);
  });

  it('row click navigates to /replay/:roomId', () => {
    const { fixture, router } = mount();
    const rowButton = fixture.nativeElement.querySelector(
      'ul li button',
    ) as HTMLButtonElement;
    rowButton.click();
    expect(router.navigateByUrl).toHaveBeenCalledWith('/replay/r-1');
  });
});
