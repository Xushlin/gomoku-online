import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { of, throwError } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { signal } from '@angular/core';
import type { GameReplayDto } from '../../../core/api/models/room.model';
import { RoomsApiService } from '../../../core/api/rooms-api.service';
import { LanguageService } from '../../../core/i18n/language.service';
import { ReplayPage } from './replay-page';

function makeReplay(overrides: Partial<GameReplayDto> = {}): GameReplayDto {
  return {
    roomId: 'r-1',
    name: 'Replay',
    host: { id: 'u-1', username: 'alice' },
    black: { id: 'u-1', username: 'alice' },
    white: { id: 'u-2', username: 'bob' },
    startedAt: '2026-04-24T00:00:00Z',
    endedAt: '2026-04-24T00:05:00Z',
    result: 'BlackWin',
    winnerUserId: 'u-1',
    endReason: 'Connected5',
    moves: [
      { ply: 1, row: 7, col: 7, stone: 'Black', playedAt: '2026-04-24T00:01:00Z' },
      { ply: 2, row: 7, col: 8, stone: 'White', playedAt: '2026-04-24T00:02:00Z' },
      { ply: 3, row: 8, col: 7, stone: 'Black', playedAt: '2026-04-24T00:03:00Z' },
    ],
    ...overrides,
  };
}

class StubRooms {
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  getReplay: any = vi.fn(() => of(makeReplay()));
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

function mount(opts: { id?: string | null; getReplay?: ReturnType<typeof vi.fn> } = {}) {
  const id = opts.id ?? 'r-1';
  const rooms = new StubRooms();
  if (opts.getReplay) rooms.getReplay = opts.getReplay;
  const router = routerStub();
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [
      ReplayPage,
      TranslocoTestingModule.forRoot({
        langs: { en: {} },
        translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
        preloadLangs: true,
      }),
    ],
    providers: [
      { provide: RoomsApiService, useValue: rooms },
      { provide: Router, useValue: router },
      { provide: ActivatedRoute, useValue: activatedRoute(id) },
      { provide: LanguageService, useValue: { current: signal('en') } },
    ],
  });
  const fixture = TestBed.createComponent(ReplayPage);
  fixture.detectChanges();
  return { fixture, rooms, router };
}

describe('ReplayPage', () => {
  beforeEach(() => TestBed.resetTestingModule());

  it('on init: fetches replay by route id', () => {
    const { rooms } = mount();
    expect(rooms.getReplay).toHaveBeenCalledWith('r-1');
  });

  it('404 sets notFound state', () => {
    const { fixture } = mount({
      getReplay: vi.fn(() =>
        throwError(() => new HttpErrorResponse({ status: 404, statusText: 'Not Found' })),
      ),
    });
    const comp = fixture.componentInstance as unknown as { notFound: () => boolean };
    expect(comp.notFound()).toBe(true);
  });

  it('409 sets notFinished state', () => {
    const { fixture } = mount({
      getReplay: vi.fn(() =>
        throwError(() => new HttpErrorResponse({ status: 409, statusText: 'Conflict' })),
      ),
    });
    const comp = fixture.componentInstance as unknown as { notFinished: () => boolean };
    expect(comp.notFinished()).toBe(true);
  });

  it('step(+1) advances currentPly and clamps at end', () => {
    const { fixture } = mount();
    const comp = fixture.componentInstance as unknown as {
      step: (d: number) => void;
      currentPly: () => number;
      atEnd: () => boolean;
    };
    expect(comp.currentPly()).toBe(0);
    comp.step(+1);
    expect(comp.currentPly()).toBe(1);
    comp.step(+1);
    comp.step(+1);
    comp.step(+1); // beyond end
    expect(comp.currentPly()).toBe(3);
    expect(comp.atEnd()).toBe(true);
  });

  it('step(-1) cannot go below 0', () => {
    const { fixture } = mount();
    const comp = fixture.componentInstance as unknown as {
      step: (d: number) => void;
      currentPly: () => number;
    };
    comp.step(-1);
    expect(comp.currentPly()).toBe(0);
  });

  it('togglePlay at end resets to 0 and plays', () => {
    const { fixture } = mount();
    const comp = fixture.componentInstance as unknown as {
      step: (d: number) => void;
      togglePlay: () => void;
      currentPly: () => number;
      playing: () => boolean;
    };
    comp.step(+1);
    comp.step(+1);
    comp.step(+1); // at end
    comp.togglePlay();
    expect(comp.currentPly()).toBe(0);
    expect(comp.playing()).toBe(true);
  });

  it('auto-play advances currentPly on interval', async () => {
    vi.useFakeTimers();
    try {
      const { fixture } = mount();
      const comp = fixture.componentInstance as unknown as {
        togglePlay: () => void;
        currentPly: () => number;
      };
      comp.togglePlay();
      vi.advanceTimersByTime(700);
      expect(comp.currentPly()).toBe(1);
      vi.advanceTimersByTime(700);
      expect(comp.currentPly()).toBe(2);
    } finally {
      vi.useRealTimers();
    }
  });

  afterEach(() => {
    vi.useRealTimers();
  });
});
