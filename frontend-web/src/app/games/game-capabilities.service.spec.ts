import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { GamesApiService } from '../core/api/games-api.service';
import type { GameDescriptor } from '../core/api/models/game-descriptor.model';
import {
  DefaultGameCapabilitiesService,
  GameCapabilitiesService,
} from './game-capabilities.service';

const GOMOKU: GameDescriptor = {
  gameKey: 'gomoku',
  isRated: true,
  supportsHumanVsHuman: true,
  rows: 15,
  cols: 15,
};

const TICTACTOE: GameDescriptor = {
  gameKey: 'tictactoe',
  isRated: false,
  supportsHumanVsHuman: false,
  rows: 3,
  cols: 3,
};

function setup(list: () => ReturnType<GamesApiService['list']>) {
  const api = { list: vi.fn(list) };
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    providers: [
      { provide: GamesApiService, useValue: api },
      { provide: GameCapabilitiesService, useClass: DefaultGameCapabilitiesService },
    ],
  });
  return { svc: TestBed.inject(GameCapabilitiesService), api };
}

describe('GameCapabilitiesService', () => {
  it('exposes each descriptor by key once loaded', () => {
    const { svc } = setup(() => of([GOMOKU, TICTACTOE]));
    svc.ensureLoaded();

    expect(svc.of('gomoku')?.isRated).toBe(true);
    expect(svc.of('tictactoe')?.isRated).toBe(false);
    expect(svc.loaded()).toBe(true);
  });

  it('ratedKeys() lists only the rated games', () => {
    const { svc } = setup(() => of([GOMOKU, TICTACTOE]));
    svc.ensureLoaded();

    expect(svc.ratedKeys()).toEqual(['gomoku']);
  });

  it('a key the server never mentioned is "not applicable", not false', () => {
    // Puzzle games have no IGameRules at all. Collapsing that into
    // `isRated: false` would make "tic-tac-toe is unrated" indistinguishable
    // from "idiom crossword isn't a versus game" — two different facts that
    // happen to produce the same UI today and would diverge tomorrow.
    const { svc } = setup(() => of([GOMOKU, TICTACTOE]));
    svc.ensureLoaded();

    expect(svc.of('idiom-crossword')).toBeUndefined();
  });

  it('fetches once however many times ensureLoaded is called', () => {
    const { svc, api } = setup(() => of([GOMOKU]));
    svc.ensureLoaded();
    svc.ensureLoaded();
    svc.ensureLoaded();

    expect(api.list).toHaveBeenCalledTimes(1);
  });

  it('a failed load degrades to "no capabilities", never to wrong ones', () => {
    // Every game becomes "not applicable", so no ladder links and no game
    // switcher render — i.e. the pre-change UI. Failing into a missing
    // affordance is right; failing into a wrong one (a link to an empty
    // ladder, a switcher listing an unrated game) would not be.
    const { svc } = setup(() => throwError(() => new Error('offline')));
    svc.ensureLoaded();

    expect(svc.of('gomoku')).toBeUndefined();
    expect(svc.ratedKeys()).toEqual([]);
    expect(svc.loaded()).toBe(true);
  });

  it('reports nothing before the load is kicked off', () => {
    const { svc, api } = setup(() => of([GOMOKU]));

    expect(svc.of('gomoku')).toBeUndefined();
    expect(svc.loaded()).toBe(false);
    expect(api.list).not.toHaveBeenCalled();
  });
});
