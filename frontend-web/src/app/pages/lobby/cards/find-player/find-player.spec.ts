import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { of } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { UsersApiService } from '../../../../core/api/users-api.service';
import { FindPlayerCard } from './find-player';

class StubUsers {
  getProfile = vi.fn();
  getGames = vi.fn();
  search = vi.fn(() =>
    of({
      items: [
        { id: 'u-1', username: 'alice', rating: 1280, gamesPlayed: 5, wins: 3, losses: 1, draws: 1, createdAt: 'x' },
      ],
      total: 1,
      page: 1,
      pageSize: 5,
    }),
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
      FindPlayerCard,
      TranslocoTestingModule.forRoot({
        langs: { en: {} },
        translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [
      { provide: UsersApiService, useValue: users },
      { provide: Router, useValue: router },
    ],
  });
  const fixture = TestBed.createComponent(FindPlayerCard);
  fixture.detectChanges();
  return { fixture, users, router };
}

describe('FindPlayerCard', () => {
  beforeEach(() => TestBed.resetTestingModule());

  afterEach(() => vi.useRealTimers());

  it('does not call search for queries shorter than 3 chars', async () => {
    vi.useFakeTimers();
    const { fixture, users } = mount();
    const comp = fixture.componentInstance as unknown as {
      inputCtrl: { setValue: (v: string) => void };
    };
    comp.inputCtrl.setValue('al');
    vi.advanceTimersByTime(300);
    await Promise.resolve();
    expect(users.search).not.toHaveBeenCalled();
  });

  it('calls search after debounce when ≥3 chars', async () => {
    vi.useFakeTimers();
    const { fixture, users } = mount();
    const comp = fixture.componentInstance as unknown as {
      inputCtrl: { setValue: (v: string) => void };
    };
    comp.inputCtrl.setValue('alice');
    vi.advanceTimersByTime(300);
    await Promise.resolve();
    expect(users.search).toHaveBeenCalledWith('alice', 1, 5);
  });

  it('pick() navigates to /users/:id and clears input', () => {
    const { fixture, router } = mount();
    const comp = fixture.componentInstance as unknown as {
      pick: (u: { id: string; username: string }) => void;
      inputCtrl: { value: string };
    };
    comp.pick({ id: 'u-7', username: 'alice' });
    expect(router.navigateByUrl).toHaveBeenCalledWith('/users/u-7');
    expect(comp.inputCtrl.value).toBe('');
  });
});
