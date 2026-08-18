import { COLUMNS, ROWS, TetrisField } from './field';
import { pieceSequence } from './piece-sequence';
import { levelFor, scoreForClear } from './scoring';
import { cellsOf, ROTATIONS, widthOf, type CellOffset, type TetrominoKind } from './tetromino';

/** One recorded placement — exactly what gets submitted. */
export interface Placement {
  readonly rotation: number;
  readonly column: number;
}

/** The falling piece. */
export interface ActivePiece {
  readonly kind: TetrominoKind;
  readonly rotation: number;
  readonly top: number;
  readonly column: number;
}

/** How many pieces to pre-generate. Beyond this the run ends by itself. */
const SEQUENCE_LENGTH = 4000;

/** Gravity interval by level, in milliseconds. */
export function gravityIntervalMs(level: number): number {
  return Math.max(80, 800 - (level - 1) * 70);
}

/**
 * The client-side game loop, as a pure state machine — no Angular, no timers.
 *
 * ### Why the client runs the whole rule set
 *
 * `add-web-klotski` set the test: not *should the client know the rules* but
 * *would knowing them produce a second truth that can diverge*. A 60 fps falling
 * block cannot round-trip to the server, so there is no version of this game
 * where the client does not simulate everything. What makes that acceptable is
 * that the server **replays** the placements: if this engine's field model drifts,
 * the submission is refused rather than silently accepted.
 *
 * ### The invariant that keeps every placement replayable
 *
 * The server computes each piece's resting row itself, by dropping it straight
 * down the submitted column. It never learns how the player got there. So this
 * engine refuses any move that would leave the piece somewhere a straight drop
 * could not reach — i.e. tucking under an overhang. See
 * {@link TetrisField.reachableFromAbove}.
 *
 * With that invariant held, `top === landingRow(rotation, column)` at lock time
 * *follows*, and does not need checking: if nothing is above any of the piece's
 * cells, then no higher position collides either, so the landing row is exactly
 * the lowest non-colliding row — which is where gravity stopped it.
 *
 * ### No lock delay, and that is a consequence rather than a shortcut
 *
 * A landed piece locks on the next gravity step. The usual argument for a grace
 * period is "let the player slide it in at the last moment" — but sliding at
 * floor level is almost always a tuck, which this engine refuses anyway. A grace
 * period would mostly buy moves that get rejected.
 */
export class TetrisGame {
  private readonly sequence: readonly TetrominoKind[];
  private field = new TetrisField();
  private index = 0;
  private piece: ActivePiece | null = null;
  private readonly recorded: Placement[] = [];

  private _score = 0;
  private _lines = 0;
  private _over = false;

  constructor(readonly seed: number) {
    this.sequence = pieceSequence(seed, SEQUENCE_LENGTH);
    this.spawn();
  }

  /** Running score. A preview — the recorded score is the server's. */
  get score(): number {
    return this._score;
  }

  /** Rows cleared so far. */
  get lines(): number {
    return this._lines;
  }

  /** Current level. */
  get level(): number {
    return levelFor(this._lines);
  }

  /** Has the run ended? */
  get over(): boolean {
    return this._over;
  }

  /** The falling piece, or `null` once the run is over. */
  get active(): ActivePiece | null {
    return this.piece;
  }

  /** The next piece, for the preview panel. */
  get next(): TetrominoKind | null {
    return this.sequence[this.index] ?? null;
  }

  /** The locked-in grid. */
  get grid(): readonly (readonly boolean[])[] {
    return this.field.snapshot();
  }

  /** Placements recorded so far — the submission payload. */
  get placements(): readonly Placement[] {
    return this.recorded;
  }

  /** Cells the falling piece occupies right now. */
  activeCells(): readonly CellOffset[] {
    if (!this.piece) return [];
    const { kind, rotation, top, column } = this.piece;
    return cellsOf(kind, rotation).map((cell) => ({
      row: top + cell.row,
      col: column + cell.col,
    }));
  }

  /**
   * Cells the hard-drop preview occupies — from the same `landingRow` the lock
   * uses, so the ghost and the real landing cannot disagree.
   */
  ghostCells(): readonly CellOffset[] {
    if (!this.piece) return [];
    const { kind, rotation, column } = this.piece;
    const landing = this.field.landingRow(kind, rotation, column);
    if (landing === null) return [];
    return cellsOf(kind, rotation).map((cell) => ({
      row: landing + cell.row,
      col: column + cell.col,
    }));
  }

  /** Move one column left. Returns whether it happened. */
  moveLeft(): boolean {
    return this.tryMove(-1);
  }

  /** Move one column right. Returns whether it happened. */
  moveRight(): boolean {
    return this.tryMove(1);
  }

  /**
   * Rotate clockwise. No wall kick: a kick would slide the piece sideways, and
   * the resulting position has to satisfy the same reachability invariant, so a
   * kick that helped would be one the server could not reproduce.
   */
  rotate(): boolean {
    if (!this.piece || this._over) return false;
    const { kind, top, column } = this.piece;
    const rotation = (this.piece.rotation + 1) % ROTATIONS;

    if (column + widthOf(kind, rotation) > COLUMNS) return false;
    return this.commitIfLegal({ kind, rotation, top, column });
  }

  /** Fall one row if possible; otherwise lock. Returns whether it fell. */
  softDrop(): boolean {
    if (!this.piece || this._over) return false;
    if (this.canFall()) {
      this.piece = { ...this.piece, top: this.piece.top + 1 };
      return true;
    }
    this.lock();
    return false;
  }

  /** Drop to the landing row and lock immediately. */
  hardDrop(): void {
    if (!this.piece || this._over) return;
    const { kind, rotation, column } = this.piece;
    const landing = this.field.landingRow(kind, rotation, column);
    if (landing !== null) {
      this.piece = { ...this.piece, top: landing };
    }
    this.lock();
  }

  /** One gravity step. */
  tick(): void {
    this.softDrop();
  }

  private tryMove(delta: number): boolean {
    if (!this.piece || this._over) return false;
    const { kind, rotation, top } = this.piece;
    const column = this.piece.column + delta;
    if (column < 0 || column + widthOf(kind, rotation) > COLUMNS) return false;
    return this.commitIfLegal({ kind, rotation, top, column });
  }

  /**
   * Accept a candidate position only if it is both unoccupied *and* reachable by
   * a straight drop. The second condition is the one that keeps the recorded
   * placements replayable; without it the move would look fine on screen and the
   * whole run would be refused at submission.
   */
  private commitIfLegal(candidate: ActivePiece): boolean {
    const cells = cellsOf(candidate.kind, candidate.rotation);
    if (this.field.collides(cells, candidate.top, candidate.column)) return false;
    if (
      !this.field.reachableFromAbove(
        candidate.kind,
        candidate.rotation,
        candidate.top,
        candidate.column,
      )
    ) {
      return false;
    }
    this.piece = candidate;
    return true;
  }

  private canFall(): boolean {
    if (!this.piece) return false;
    const { kind, rotation, top, column } = this.piece;
    return !this.field.collides(cellsOf(kind, rotation), top + 1, column);
  }

  private lock(): void {
    if (!this.piece) return;
    const { kind, rotation, top, column } = this.piece;

    this.field.lock(kind, rotation, top, column);
    this.recorded.push({ rotation, column });

    const cleared = this.field.clearFullLines();
    if (cleared > 0) {
      this._score += scoreForClear(cleared, this._lines);
      this._lines += cleared;
    }

    this.piece = null;
    this.spawn();
  }

  private spawn(): void {
    const kind = this.sequence[this.index];
    if (kind === undefined) {
      this._over = true;
      return;
    }
    this.index++;

    const rotation = 0;
    const column = Math.floor((COLUMNS - widthOf(kind, rotation)) / 2);
    const cells = cellsOf(kind, rotation);

    // No room for the new piece at the top — the classic end condition.
    if (this.field.collides(cells, 0, column)) {
      this._over = true;
      return;
    }

    // ...and a second end condition the straight-drop model creates. A piece whose
    // top row is empty at the spawn column (L, J, T, S, Z all have such rotations)
    // can fit at row 0 while a filled cell sits *above* one of its lower cells —
    // once the stack reaches the ceiling. That position is unoccupied but no
    // straight drop reaches it, so the placement would be unreplayable and the
    // whole run refused at submission.
    //
    // Found by the reachability invariant test, not by reading this function: it
    // only happens in the last plies of a losing game, which is exactly where
    // nobody looks.
    if (!this.field.reachableFromAbove(kind, rotation, 0, column)) {
      this._over = true;
      return;
    }

    this.piece = { kind, rotation, top: 0, column };
  }
}

/**
 * Replay a recorded placement list, exactly as the server does.
 *
 * It exists so a test can assert the engine is **self-consistent**: the score it
 * showed while playing must equal replaying the placements it wrote down. That
 * catches two different bugs at once — a wrong scoring formula, and a recorded
 * placement that does not match where the piece actually landed. The second is
 * invisible on screen, because the screen draws the actual landing.
 */
export function replay(
  seed: number,
  placements: readonly Placement[],
): { score: number; lines: number; level: number } {
  const kinds = pieceSequence(seed, placements.length);
  const field = new TetrisField();
  let score = 0;
  let lines = 0;

  placements.forEach((placement, i) => {
    const cleared = field.placeAndClear(kinds[i], placement.rotation, placement.column);
    if (cleared > 0) {
      score += scoreForClear(cleared, lines);
      lines += cleared;
    }
  });

  return { score, lines, level: levelFor(lines) };
}

/** Total cells on the field — handy for tests and for rendering loops. */
export const FIELD_CELLS = ROWS * COLUMNS;
