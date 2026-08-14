import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';
import { boardSizeFor, DEFAULT_BOARD } from './board-size';
import { DefaultGameCatalogService, GameCatalogService } from './game-catalog.service';

describe('boardSizeFor', () => {
  let catalog: GameCatalogService;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [{ provide: GameCatalogService, useClass: DefaultGameCatalogService }],
    });
    catalog = TestBed.inject(GameCatalogService);
  });

  it('resolves gomoku to 15x15', () => {
    expect(boardSizeFor(catalog, 'gomoku')).toEqual({ rows: 15, cols: 15 });
  });

  it('resolves tictactoe to 3x3', () => {
    expect(boardSizeFor(catalog, 'tictactoe')).toEqual({ rows: 3, cols: 3 });
  });

  it('falls back for a game key the registry does not know', () => {
    // A client that has not been redeployed will meet keys its registry lacks.
    // A possibly-wrong board beats a blank page, and the server rejects
    // out-of-range moves either way, so the guess cannot corrupt a game.
    expect(boardSizeFor(catalog, 'a-game-nobody-registered')).toEqual(DEFAULT_BOARD);
  });

  it('falls back while room state is still loading', () => {
    expect(boardSizeFor(catalog, null)).toEqual(DEFAULT_BOARD);
    expect(boardSizeFor(catalog, undefined)).toEqual(DEFAULT_BOARD);
    expect(boardSizeFor(catalog, '')).toEqual(DEFAULT_BOARD);
  });

  it('falls back for a game whose manifest declares no board', () => {
    // Puzzle games have no board field; asking for one must not throw.
    expect(boardSizeFor(catalog, 'idiom-crossword')).toEqual(DEFAULT_BOARD);
  });
});

describe('match manifests', () => {
  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [{ provide: GameCatalogService, useClass: DefaultGameCatalogService }],
    });
  });

  it('gives every playable match game a positive board', () => {
    const catalog = TestBed.inject(GameCatalogService);
    const playableMatch = catalog.available().filter((g) => g.category === 'match');

    expect(playableMatch.length).toBeGreaterThanOrEqual(2);
    for (const g of playableMatch) {
      expect(g.board, `${g.key} must declare a board`).toBeDefined();
      expect(g.board!.rows).toBeGreaterThan(0);
      expect(g.board!.cols).toBeGreaterThan(0);
    }
  });

  it('has tictactoe available with its launch route', () => {
    const ttt = TestBed.inject(GameCatalogService).byKey('tictactoe');

    expect(ttt?.status).toBe('available');
    expect(ttt?.category).toBe('match');
    expect(ttt?.launchRoute).toBe('/g/tictactoe');
    expect(ttt?.board).toEqual({ rows: 3, cols: 3 });
  });
});
