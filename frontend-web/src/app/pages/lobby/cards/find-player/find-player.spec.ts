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

  /**
   * Type into the real `<input>` rather than poking an internal.
   *
   * These tests used to call `inputCtrl.setValue(...)`, which stopped existing when
   * the card dropped `@angular/forms` — it was the only eagerly-loaded consumer of
   * it, worth 34 kB of initial bundle. Driving the DOM is the better test anyway:
   * it exercises the `[value]` / `(input)` binding that replaced the `FormControl`,
   * which an internal-poking test could not see at all.
   */
  function type(fixture: ReturnType<typeof mount>['fixture'], value: string): void {
    const input = fixture.nativeElement.querySelector('input') as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  it('does not call search for queries shorter than 3 chars', async () => {
    vi.useFakeTimers();
    const { fixture, users } = mount();

    type(fixture, 'al');
    vi.advanceTimersByTime(300);
    await Promise.resolve();
    fixture.detectChanges();

    expect(users.search).not.toHaveBeenCalled();
  });

  it('calls search after debounce when ≥3 chars', async () => {
    vi.useFakeTimers();
    const { fixture, users } = mount();

    type(fixture, 'alice');
    vi.advanceTimersByTime(300);
    await Promise.resolve();
    fixture.detectChanges();

    expect(users.search).toHaveBeenCalledWith('alice', 1, 5);
  });

  it('does not call search again for the same query', async () => {
    // `distinctUntilChanged` — retyping the same name must not refetch. It was in
    // the pipeline before and is still there; nothing asserted it until now.
    vi.useFakeTimers();
    const { fixture, users } = mount();

    type(fixture, 'alice');
    vi.advanceTimersByTime(300);
    await Promise.resolve();
    fixture.detectChanges();
    type(fixture, 'alice');
    vi.advanceTimersByTime(300);
    await Promise.resolve();
    fixture.detectChanges();

    expect(users.search).toHaveBeenCalledTimes(1);
  });

  it('debounces — a query typed a character at a time fires once', async () => {
    vi.useFakeTimers();
    const { fixture, users } = mount();

    for (const v of ['a', 'al', 'ali', 'alic', 'alice']) {
      type(fixture, v);
      vi.advanceTimersByTime(50);
    }
    vi.advanceTimersByTime(300);
    await Promise.resolve();
    fixture.detectChanges();

    expect(users.search).toHaveBeenCalledTimes(1);
    expect(users.search).toHaveBeenCalledWith('alice', 1, 5);
  });

  it('pick() navigates to /users/:id and clears the input', async () => {
    vi.useFakeTimers();
    const { fixture, router } = mount();
    type(fixture, 'alice');
    vi.advanceTimersByTime(300);
    await Promise.resolve();
    fixture.detectChanges();

    const comp = fixture.componentInstance as unknown as {
      pick: (u: { id: string; username: string }) => void;
    };
    comp.pick({ id: 'u-7', username: 'alice' });
    fixture.detectChanges();

    expect(router.navigateByUrl).toHaveBeenCalledWith('/users/u-7');
    expect((fixture.nativeElement.querySelector('input') as HTMLInputElement).value).toBe('');
  });
});
