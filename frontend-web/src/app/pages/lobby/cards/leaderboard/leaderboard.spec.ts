import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { LeaderboardEntry } from '../../../../core/api/models/leaderboard.model';
import { LobbyDataService } from '../../../../core/lobby/lobby-data.service';
import { LeaderboardCard } from './leaderboard';

function entry(rank: number, name: string): LeaderboardEntry {
  return {
    rank,
    userId: `u-${rank}`,
    username: name,
    rating: 1500 - rank * 10,
    gamesPlayed: 20,
    wins: 10,
    losses: 5,
    draws: 5,
  };
}

function emptySlice<T>(data: T | null) {
  return {
    data: signal<T | null>(data),
    loading: signal<boolean>(false),
    error: signal<unknown | null>(null),
    refresh: vi.fn(),
  };
}

function mount(entries: readonly LeaderboardEntry[]) {
  const stub = {
    onlineCount: emptySlice<number>(null),
    rooms: emptySlice<readonly unknown[]>(null),
    myRooms: emptySlice<readonly unknown[]>(null),
    leaderboard: emptySlice<readonly LeaderboardEntry[]>(entries),
  } as unknown as LobbyDataService;
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      LeaderboardCard,
      TranslocoTestingModule.forRoot({
        langs: { en: {} },
        translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [{ provide: LobbyDataService, useValue: stub }, provideRouter([])],
  });
  const fixture = TestBed.createComponent(LeaderboardCard);
  fixture.detectChanges();
  return fixture;
}

describe('LeaderboardCard', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('renders medal icons for rank 1/2/3 only', () => {
    const fixture = mount([
      entry(1, 'alice'),
      entry(2, 'bob'),
      entry(3, 'carol'),
      entry(4, 'dave'),
      entry(5, 'eve'),
    ]);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('🥇');
    expect(text).toContain('🥈');
    expect(text).toContain('🥉');
    // rank 4 / 5 should show #4 / #5 (no medal)
    expect(text).toContain('#4');
    expect(text).toContain('#5');
  });

  it('tierKey resolves only for top 3', () => {
    const fixture = mount([]);
    const comp = fixture.componentInstance as unknown as {
      tierKey: (r: number) => string | null;
      tierIcon: (r: number) => string | null;
    };
    expect(comp.tierKey(1)).toBe('lobby.leaderboard.tier-gold');
    expect(comp.tierKey(2)).toBe('lobby.leaderboard.tier-silver');
    expect(comp.tierKey(3)).toBe('lobby.leaderboard.tier-bronze');
    expect(comp.tierKey(4)).toBeNull();
    expect(comp.tierIcon(1)).toBe('🥇');
    expect(comp.tierIcon(10)).toBeNull();
  });
});
