import { describe, expect, it } from 'vitest';
import { COLUMNS, ROWS, TetrisField } from './field';
import { replay, TetrisGame, type Placement } from './game';
import { pieceSequence } from './piece-sequence';
import { levelFor, scoreForClear } from './scoring';
import { cellsOf, ROTATIONS, TETROMINO_KINDS, widthOf } from './tetromino';

/** The seed `add-tetris` aligned C# and Python on. */
const PINNED_SEED = 20260818;

/** Measured: C# `TetrisPieceSequence.Take(20260818, 21)`, matched by a Python port. */
const PINNED_PIECES = [
  'S', 'L', 'O', 'J', 'T', 'Z', 'I',
  'S', 'I', 'L', 'T', 'O', 'Z', 'J',
  'Z', 'J', 'T', 'O', 'S', 'L', 'I',
];

/** A deterministic driver — tests must not depend on `Math.random`. */
function lcg(seed: number): () => number {
  let s = seed >>> 0;
  return () => {
    s = (s * 1664525 + 1013904223) >>> 0;
    return s / 0x100000000;
  };
}

describe('piece sequence', () => {
  it('matches the sequence measured against C# and Python', () => {
    // The one thing in this game with two implementations. It is allowed
    // *because* a test can align it item by item — so this is that test, and it
    // uses numbers produced by the other two implementations rather than by this
    // one. Three whole bags: a per-bag bug would survive a shorter sample.
    expect(pieceSequence(PINNED_SEED, 21)).toEqual(PINNED_PIECES);
  });

  it('is deterministic', () => {
    expect(pieceSequence(7, 50)).toEqual(pieceSequence(7, 50));
  });

  it('gives different seeds different sequences', () => {
    expect(pieceSequence(7, 50)).not.toEqual(pieceSequence(8, 50));
  });

  it('emits every kind', () => {
    expect(new Set(pieceSequence(PINNED_SEED, 70)).size).toBe(TETROMINO_KINDS.length);
  });

  it('emits each kind exactly once per bag', () => {
    const seq = pieceSequence(PINNED_SEED, 70);
    for (let bag = 0; bag < 10; bag++) {
      expect(new Set(seq.slice(bag * 7, bag * 7 + 7)).size).toBe(7);
    }
  });

  it('survives seed 0 instead of degenerating to one kind', () => {
    // State 0 leaves xorshift stuck at 0 forever, which would emit the unshuffled
    // bag every time. Both implementations substitute the same constant.
    expect(new Set(pieceSequence(0, 70)).size).toBe(7);
  });

  it('never indexes outside the bag on a negative-looking seed', () => {
    // JavaScript's `<<` is signed, so a missing `>>> 0` makes `state % n`
    // negative and the shuffle silently swaps with `undefined`.
    for (const seed of [-1, 0x7fffffff, -2147483648, 0xffffffff]) {
      const seq = pieceSequence(seed, 70);
      expect(seq).toHaveLength(70);
      expect(seq.every((k) => TETROMINO_KINDS.includes(k))).toBe(true);
    }
  });
});

describe('tetromino shapes', () => {
  it('normalises every rotation to the origin', () => {
    // `column` means "the column of the leftmost cell of this rotation". Without
    // the same normalisation the server uses, that meaning shifts by a
    // rotation-dependent constant and every placement is wrong — while the screen
    // looks perfectly normal.
    for (const kind of TETROMINO_KINDS) {
      for (let r = 0; r < ROTATIONS; r++) {
        const cells = cellsOf(kind, r);
        expect(Math.min(...cells.map((c) => c.row)), `${kind}/${r} rows`).toBe(0);
        expect(Math.min(...cells.map((c) => c.col)), `${kind}/${r} cols`).toBe(0);
      }
    }
  });

  it('keeps four cells in every rotation', () => {
    for (const kind of TETROMINO_KINDS) {
      for (let r = 0; r < ROTATIONS; r++) {
        expect(new Set(cellsOf(kind, r).map((c) => `${c.row},${c.col}`)).size).toBe(4);
      }
    }
  });

  it('treats rotation as modulo 4, including negatives', () => {
    expect(cellsOf('T', 4)).toEqual(cellsOf('T', 0));
    expect(cellsOf('T', -1)).toEqual(cellsOf('T', 3));
  });
});

describe('scoring', () => {
  it('does not make four rows worth four single rows', () => {
    // That difference is the whole "save up for a tetris" decision.
    expect(scoreForClear(1, 0)).toBe(100);
    expect(scoreForClear(4, 0)).toBe(800);
    expect(scoreForClear(4, 0)).not.toBe(4 * scoreForClear(1, 0));
  });

  it('scales by the level the clear was made on', () => {
    expect(scoreForClear(1, 10)).toBe(200);
    expect(scoreForClear(1, 20)).toBe(300);
  });

  it('levels up every ten lines, from one', () => {
    expect(levelFor(0)).toBe(1);
    expect(levelFor(9)).toBe(1);
    expect(levelFor(10)).toBe(2);
  });

  it('refuses an impossible clear count', () => {
    expect(() => scoreForClear(0, 0)).toThrow();
    expect(() => scoreForClear(5, 0)).toThrow();
  });
});

describe('field', () => {
  it('drops a piece to the floor of an empty field', () => {
    const field = new TetrisField();
    // I at rotation 0 is one row tall, so its top cell lands on the last row.
    expect(field.landingRow('I', 0, 0)).toBe(ROWS - 1);
  });

  it('refuses a column the rotation does not fit', () => {
    expect(() => new TetrisField().landingRow('I', 0, COLUMNS - 3)).toThrow(RangeError);
  });

  it('reports no room when the stack reaches the top', () => {
    const rows = Array.from({ length: ROWS }, () => Array.from({ length: COLUMNS }, () => true));
    expect(new TetrisField(rows).landingRow('O', 0, 0)).toBeNull();
  });

  it('clears a full row and shifts the rest down', () => {
    const rows = Array.from({ length: ROWS }, () => Array.from({ length: COLUMNS }, () => false));
    rows[ROWS - 1] = rows[ROWS - 1].map(() => true);
    rows[ROWS - 2][0] = true;
    const field = new TetrisField(rows);

    expect(field.clearFullLines()).toBe(1);
    expect(field.isOccupied(ROWS - 1, 0)).toBe(true);
    expect(field.isOccupied(ROWS - 1, 1)).toBe(false);
  });

  it('clears two adjacent full rows in one pass', () => {
    // A naive loop that decrements after a clear skips the row that shifted in.
    const rows = Array.from({ length: ROWS }, () => Array.from({ length: COLUMNS }, () => false));
    rows[ROWS - 1] = rows[ROWS - 1].map(() => true);
    rows[ROWS - 2] = rows[ROWS - 2].map(() => true);

    expect(new TetrisField(rows).clearFullLines()).toBe(2);
  });

  it('calls a spot under an overhang unreachable from above', () => {
    // The mechanism that keeps recorded placements replayable. Build an overhang
    // at column 5 and ask whether a piece could be standing under it.
    const rows = Array.from({ length: ROWS }, () => Array.from({ length: COLUMNS }, () => false));
    rows[ROWS - 2][5] = true;
    const field = new TetrisField(rows);

    // O occupies two rows; placed with its top at the last row it would be out of
    // bounds, so use the row above the floor for a one-row-tall I instead.
    expect(field.reachableFromAbove('I', 0, ROWS - 1, 3)).toBe(false);
    expect(field.reachableFromAbove('I', 0, ROWS - 1, 6)).toBe(true);
  });
});

describe('TetrisGame', () => {
  it('spawns the first piece of the seed sequence', () => {
    const game = new TetrisGame(PINNED_SEED);

    expect(game.active?.kind).toBe('S');
    expect(game.active?.rotation).toBe(0);
    expect(game.over).toBe(false);
    expect(game.placements).toEqual([]);
  });

  it('centres the spawn column', () => {
    const game = new TetrisGame(PINNED_SEED);
    const width = widthOf('S', 0);

    expect(game.active?.column).toBe(Math.floor((COLUMNS - width) / 2));
  });

  it('records one placement per lock', () => {
    const game = new TetrisGame(PINNED_SEED);
    const column = game.active!.column;

    game.hardDrop();

    expect(game.placements).toEqual([{ rotation: 0, column }]);
  });

  it('puts the ghost exactly where a hard drop lands', () => {
    const game = new TetrisGame(PINNED_SEED);
    const ghost = [...game.ghostCells()].map((c) => `${c.row},${c.col}`).sort();

    game.hardDrop();
    const locked = game.grid
      .flatMap((row, r) => row.map((filled, c) => (filled ? `${r},${c}` : null)))
      .filter((x): x is string => x !== null)
      .sort();

    expect(locked).toEqual(ghost);
  });

  it('refuses to move a piece off either edge', () => {
    const game = new TetrisGame(PINNED_SEED);
    for (let i = 0; i < COLUMNS; i++) game.moveLeft();

    expect(game.active?.column).toBe(0);
    expect(game.moveLeft()).toBe(false);

    for (let i = 0; i < COLUMNS; i++) game.moveRight();
    expect(game.active!.column + widthOf(game.active!.kind, game.active!.rotation)).toBe(COLUMNS);
    expect(game.moveRight()).toBe(false);
  });

  it('refuses a rotation that would stick out of the right edge', () => {
    const game = new TetrisGame(PINNED_SEED);
    for (let i = 0; i < COLUMNS; i++) game.moveRight();

    // S is 3 wide at rotation 0 and 2 wide at rotation 1, so rotating at the wall
    // is fine; drive it to a rotation that grows instead.
    game.rotate();
    for (let i = 0; i < COLUMNS; i++) game.moveRight();

    expect(game.rotate()).toBe(false);
  });

  it('ends the run when a new piece has no room', () => {
    const game = new TetrisGame(PINNED_SEED);
    // Stack against the left wall until the spawn area is blocked.
    // `moveLeft()` returns false once the move is blocked — looping on
    // `column > 0` instead would spin forever the moment the stack is in the way.
    for (let i = 0; i < 400 && !game.over; i++) {
      while (game.moveLeft()) {
        /* slide as far left as this position allows */
      }
      game.hardDrop();
    }

    expect(game.over).toBe(true);
    expect(game.active).toBeNull();
  });

  it('ignores input once the run is over', () => {
    const game = new TetrisGame(PINNED_SEED);
    for (let i = 0; i < 400 && !game.over; i++) {
      while (game.moveLeft()) {
        /* slide as far left as this position allows */
      }
      game.hardDrop();
    }
    const before = game.placements.length;

    game.moveLeft();
    game.rotate();
    game.hardDrop();
    game.tick();

    expect(game.placements.length).toBe(before);
  });
});

/**
 * The two invariants the whole design rests on, checked over **played** games
 * rather than hand-written placement lists — a hand-written list bypasses the game
 * loop, which is the only thing these assertions exist to test.
 */
describe('TetrisGame invariants', () => {
  /**
   * Lowest-first greedy, driven only through the public API.
   *
   * The first version of this used a random driver, and the "did anything actually
   * clear?" guard below caught it: none of five random games ever completed a row,
   * so every other assertion in this block was passing on `0 === 0`. Greedy really
   * clears lines — the backend's own tests established that.
   *
   * Exploration is free: with no gravity tick between inputs the piece stays at the
   * spawn row, where nothing is above it, so every lateral move is legal.
   */
  function greedyStep(game: TetrisGame): void {
    const start = game.active;
    if (!start) return;

    let best = { rotation: start.rotation, column: start.column, depth: -1 };

    for (let r = 0; r < ROTATIONS; r++) {
      while (game.moveLeft()) {
        /* to the far left */
      }
      for (;;) {
        const ghost = game.ghostCells();
        if (ghost.length > 0) {
          const depth = Math.max(...ghost.map((c) => c.row));
          if (depth > best.depth) {
            best = { rotation: game.active!.rotation, column: game.active!.column, depth };
          }
        }
        if (!game.moveRight()) break;
      }
      // Back to column 0 before rotating: a rotation that grows the piece would be
      // refused at the right wall, and the scan would silently skip that rotation.
      while (game.moveLeft()) {
        /* to the far left */
      }
      game.rotate();
    }

    // Bounded, not `while (rotation !== best.rotation)`: with a high stack a
    // rotation can be refused, and an unbounded loop then spins forever. Landing on
    // a different rotation than planned only makes the move worse, never invalid.
    for (let i = 0; i < ROTATIONS && game.active!.rotation !== best.rotation; i++) {
      if (!game.rotate()) break;
    }
    while (game.active!.column < best.column && game.moveRight()) {
      /* to the chosen column */
    }
    game.hardDrop();
  }

  /** Play a whole game greedily. */
  function playGreedy(seed: number, checkReachable = false): TetrisGame {
    const game = new TetrisGame(seed);
    let guard = 0;
    while (!game.over && guard++ < 500) {
      greedyStep(game);
      if (checkReachable) {
        const bad = unreachableCell(game);
        if (bad) throw new Error(bad);
      }
    }
    return game;
  }

  /** Play with a deterministic pseudo-random driver — messy play stresses the rule. */
  function playRandom(seed: number, driverSeed: number, checkReachable: boolean): TetrisGame {
    const game = new TetrisGame(seed);
    const rand = lcg(driverSeed);
    let guard = 0;

    while (!game.over && guard++ < 3000) {
      const roll = rand();
      if (roll < 0.3) game.moveLeft();
      else if (roll < 0.6) game.moveRight();
      else if (roll < 0.75) game.rotate();
      else if (roll < 0.9) game.softDrop();
      else game.hardDrop();

      if (checkReachable) {
        const bad = unreachableCell(game);
        if (bad) throw new Error(bad);
      }
    }
    return game;
  }

  /**
   * Independent re-check of the reachability rule, computed from the public grid
   * rather than by calling the engine's own predicate — so a broken predicate
   * cannot make this pass.
   *
   * Returns a description instead of asserting: this runs after every single input
   * of every played game, and one `expect` per cell per row would be over a million
   * assertions.
   */
  function unreachableCell(game: TetrisGame): string | null {
    const grid = game.grid;
    for (const cell of game.activeCells()) {
      for (let r = cell.row - 1; r >= 0; r--) {
        if (grid[r][cell.col]) {
          return `active cell ${cell.row},${cell.col} has ${r},${cell.col} above it`;
        }
      }
    }
    return null;
  }

  it('refuses the one move that would tuck a piece under an overhang', () => {
    // The deterministic version, and the only one that actually guards the rule.
    //
    // The played-game assertions below did **not** catch removing the check:
    // mutation-tested, all 33 stayed green, because none of those games happened to
    // attempt a tuck. Random play covers the rule only by luck, so this walks
    // straight to the situation.
    //
    // Seed 20260818 opens with S. Dropped at rotation 0 it leaves exactly one
    // overhang: a filled cell at (18,5) with (19,5) empty underneath.
    const game = new TetrisGame(PINNED_SEED);
    expect(game.active!.kind).toBe('S');
    game.hardDrop();

    expect(game.grid[ROWS - 2][5], 'overhang cell').toBe(true);
    expect(game.grid[ROWS - 1][5], 'hole under it').toBe(false);

    // Next is L. Slide it to columns 6–8 and let it settle on the floor, so its
    // bottom row is the last row — right beside the hole.
    expect(game.active!.kind).toBe('L');
    for (let i = 0; i < 3; i++) game.moveRight();
    expect(game.active!.column).toBe(6);
    while (Math.max(...game.activeCells().map((c) => c.row)) < ROWS - 1) {
      expect(game.softDrop()).toBe(true);
    }

    // Moving right is fine — nothing hangs over columns 7–9.
    expect(game.moveRight(), 'a move with clear sky above must still work').toBe(true);
    expect(game.moveLeft()).toBe(true);

    // Moving left again would put a cell at (19,5), under the overhang. No straight
    // drop reaches that, so the server would replay this piece two rows higher and
    // refuse the run.
    expect(game.moveLeft(), 'the tuck must be refused').toBe(false);
    expect(game.active!.column, 'and the piece must not have moved').toBe(6);
  });

  it('never lets a piece stand somewhere a straight drop could not reach', () => {
    for (const driver of [1, 2, 3, 4, 5]) {
      expect(() => playRandom(PINNED_SEED, driver, true), `driver ${driver}`).not.toThrow();
    }
    expect(() => playGreedy(PINNED_SEED, true)).not.toThrow();
  });

  it('really does clear lines in the games these invariants run on', () => {
    // Without this, everything below could be passing on games where nothing ever
    // scored — every assertion trivially 0 === 0. It caught exactly that once.
    for (const seed of [PINNED_SEED, 1, 999]) {
      expect(playGreedy(seed).lines, `seed ${seed}`).toBeGreaterThan(0);
    }
  });

  it('replays its own recorded placements to the same score', () => {
    // The self-consistency invariant. It catches a wrong scoring formula *and* a
    // recorded placement that does not match where the piece actually landed — and
    // the second one is invisible on screen, because the screen draws the actual
    // landing.
    for (const seed of [PINNED_SEED, 1, 999, 20260101]) {
      const game = playGreedy(seed);
      const again = replay(seed, game.placements);

      expect(again.score, `seed ${seed} score`).toBe(game.score);
      expect(again.lines, `seed ${seed} lines`).toBe(game.lines);
      expect(again.level, `seed ${seed} level`).toBe(game.level);
    }
  });

  it('replays its own placements to the same field', () => {
    // Stronger than the score check: a placement that landed one row off usually
    // scores the same but cannot produce the same grid.
    for (const seed of [PINNED_SEED, 1, 999]) {
      const game = playGreedy(seed);
      const kinds = pieceSequence(seed, game.placements.length);
      const field = new TetrisField();
      game.placements.forEach((p: Placement, i: number) =>
        field.placeAndClear(kinds[i], p.rotation, p.column),
      );

      expect(field.snapshot(), `seed ${seed}`).toEqual(game.grid);
    }
  });

  it('replays a messy random game to the same field too', () => {
    // Greedy play never tries to tuck; random play does, constantly. If the
    // reachability rule were removed, this is the assertion that would break.
    for (const driver of [11, 22, 33]) {
      const game = playRandom(PINNED_SEED, driver, false);
      const kinds = pieceSequence(PINNED_SEED, game.placements.length);
      const field = new TetrisField();
      game.placements.forEach((p: Placement, i: number) =>
        field.placeAndClear(kinds[i], p.rotation, p.column),
      );

      expect(field.snapshot(), `driver ${driver}`).toEqual(game.grid);
    }
  });
});
