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
    seats: [
      { index: 0, player: { id: 'u-1', username: 'alice' } },
      { index: 1, player: { id: 'u-2', username: 'bob' } },
    ],
    startedAt: '2026-04-23T00:00:00Z',
    endedAt: '2026-04-23T00:05:00Z',
    result: 'Decided' as const,
    winnerUserId: 'u-1',
    endReason: 'Decided' as const,
    moveCount: 17,
  },
];

/** 一局三人牌局。profile 的主人是 `u-1`,而赢家是 `u-2`(地主)。 */
const threeSeatGame = {
  roomId: 'r-ddz',
  name: '斗地主',
  seats: [
    { index: 0, player: { id: 'u-2', username: 'bob' } },
    { index: 1, player: { id: 'u-1', username: 'alice' } },
    { index: 2, player: { id: 'u-3', username: 'carol' } },
  ],
  startedAt: '2026-04-26T00:00:00Z',
  endedAt: '2026-04-26T00:05:00Z',
  result: 'Decided' as const,
  winnerUserId: 'u-2',
  endReason: 'Decided' as const,
  moveCount: 40,
};

class StubUsers {
  getProfile = vi.fn();
  search = vi.fn();
  // `any` 与 my-recent-games.spec.ts 里那份同一个约定:mount() 要能换掉它,
  // 而 vi.fn 的推断类型会把 fixture 的字面量形状焊死。
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  getGames: any = vi.fn(() =>
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

function mount(opts: { getGames?: ReturnType<typeof vi.fn> } = {}) {
  const users = new StubUsers();
  if (opts.getGames) users.getGames = opts.getGames;
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

describe('GamesList — 三人局', () => {
  function mountThreeSeat() {
    return mount({
      getGames: vi.fn(() => of({ items: [threeSeatGame], total: 1, page: 1, pageSize: 10 })),
    });
  }

  it('列出两个对手,而不是一个', () => {
    const { fixture } = mountThreeSeat();
    const links: HTMLAnchorElement[] = Array.from(
      fixture.nativeElement.querySelectorAll('li a.username-link'),
    );

    expect(links).toHaveLength(2);
    expect(links.map((a) => a.textContent?.trim()).sort()).toEqual(['bob', 'carol']);
    expect(links.map((a) => a.textContent?.trim())).not.toContain('alice');
  });

  it('两人局仍然恰好一个对手 —— 反面控制', () => {
    // 少了这一条,「列出每一个对手」在一个只有三座位的样本上说明不了两座位没坏。
    const { fixture } = mount();
    const links = fixture.nativeElement.querySelectorAll('li a.username-link');

    expect(links).toHaveLength(1);
  });

  it('赢家不是我时说「说不出」,而不是「负」', () => {
    const { fixture } = mountThreeSeat();
    const text = fixture.nativeElement.textContent ?? '';

    expect(text).toContain('profile.result-unrecorded');
    expect(text).not.toContain('profile.result-loss');
  });
});
