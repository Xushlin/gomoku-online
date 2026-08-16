import { describe, expect, it } from 'vitest';
import { boardSizeFor, DEFAULT_BOARD } from './board-size';
import { StubGameCapabilities } from './game-capabilities.stub';
import { GAME_REGISTRY } from './index';

/**
 * Board dimensions come from the server (`GET /api/games`), not from the
 * front-end manifest.
 *
 * They used to come from `GameManifest.board` — a client-side copy accepted on
 * the grounds that a wrong copy is visible (wrong number of cells) and harmless
 * (the server rejects out-of-range moves). `add-web-xiangqi` broke that: it added
 * a copy for 象棋, whose board component hardcodes its own 10×9, so nothing read
 * it and a wrong value would have been noticed by nobody.
 */
const SERVER = StubGameCapabilities.sized({
  gomoku: { rows: 15, cols: 15 },
  tictactoe: { rows: 3, cols: 3 },
  xiangqi: { rows: 10, cols: 9 },
});

describe('boardSizeFor', () => {
  it('resolves gomoku to 15x15', () => {
    expect(boardSizeFor(SERVER, 'gomoku')).toEqual({ rows: 15, cols: 15 });
  });

  it('resolves tictactoe to 3x3', () => {
    expect(boardSizeFor(SERVER, 'tictactoe')).toEqual({ rows: 3, cols: 3 });
  });

  it('falls back for a game key the server has not described', () => {
    // A client that has not been redeployed will meet keys it does not know.
    // A possibly-wrong board beats a blank page, and the server rejects
    // out-of-range moves either way, so the guess cannot corrupt a game.
    expect(boardSizeFor(SERVER, 'a-game-nobody-registered')).toEqual(DEFAULT_BOARD);
  });

  it('falls back while room state is still loading', () => {
    expect(boardSizeFor(SERVER, null)).toEqual(DEFAULT_BOARD);
    expect(boardSizeFor(SERVER, undefined)).toEqual(DEFAULT_BOARD);
    expect(boardSizeFor(SERVER, '')).toEqual(DEFAULT_BOARD);
  });

  it('falls back for a game with no IGameRules at all', () => {
    // Puzzle games have no descriptor — "not applicable", not "zero".
    expect(boardSizeFor(SERVER, 'idiom-crossword')).toEqual(DEFAULT_BOARD);
  });

  it('falls back rather than trusting a nonsensical descriptor', () => {
    const broken = StubGameCapabilities.sized({ weird: { rows: 0, cols: -1 } });

    expect(boardSizeFor(broken, 'weird')).toEqual(DEFAULT_BOARD);
  });

  it('falls back before the descriptors arrive', () => {
    // Callers must not paint this — they hold their loading state until
    // `loaded()` — but the function still has to answer something.
    expect(boardSizeFor(StubGameCapabilities.pending(), 'tictactoe')).toEqual(DEFAULT_BOARD);
  });
});

describe('match manifests', () => {
  it('declares no board dimensions of its own', () => {
    // The invariant this replaces required every playable match game to declare
    // a `board`. That is what kept 象棋's unread copy alive. The field and the
    // invariant died together.
    for (const game of GAME_REGISTRY) {
      expect(game, `${game.key} must not carry a board`).not.toHaveProperty('board');
    }
  });

  it('has tictactoe available at its own route', () => {
    const ttt = GAME_REGISTRY.find((g) => g.key === 'tictactoe');

    expect(ttt?.status).toBe('available');
    expect(ttt?.category).toBe('match');
    expect(ttt?.launchRoute).toBe('/g/tictactoe');
  });
});
