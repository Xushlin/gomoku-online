import { cellsOf, heightOf, widthOf, type TetrominoKind } from './tetromino';

/** Field width, in columns. */
export const COLUMNS = 10;

/** Field height, in rows. */
export const ROWS = 20;

/**
 * A 10×20 field, and what happens when a piece lands on it.
 *
 * A port of the backend's `TetrisField`. That class is public on the server for
 * exactly this reason — the client has to compute the landing row to draw a
 * hard-drop preview — so this port has an item-by-item counterpart rather than
 * being reverse-engineered from the replay loop.
 */
export class TetrisField {
  private readonly occupied: boolean[][];

  constructor(rows?: readonly (readonly boolean[])[]) {
    this.occupied = rows
      ? rows.map((r) => [...r])
      : Array.from({ length: ROWS }, () => Array.from({ length: COLUMNS }, () => false));
  }

  /** Is this cell taken? */
  isOccupied(row: number, col: number): boolean {
    return this.occupied[row][col];
  }

  /** A copy of the grid, for rendering. */
  snapshot(): readonly (readonly boolean[])[] {
    return this.occupied.map((r) => [...r]);
  }

  /** An independent copy — used to try a placement without committing to it. */
  clone(): TetrisField {
    return new TetrisField(this.occupied);
  }

  /**
   * Where this piece stops if dropped straight down this column: the row of its
   * topmost cell, or `null` if the stack leaves no room.
   *
   * Out-of-range columns throw rather than returning `null`, matching the server:
   * "does not fit the field" and "the stack is too high" are different faults and
   * collapsing them makes the second one unreadable.
   */
  landingRow(kind: TetrominoKind, rotation: number, column: number): number | null {
    const cells = cellsOf(kind, rotation);
    const width = widthOf(kind, rotation);

    if (column < 0 || column + width > COLUMNS) {
      throw new RangeError(
        `${kind} rotation ${rotation} at column ${column} does not fit the field.`,
      );
    }

    const height = heightOf(kind, rotation);
    let landing: number | null = null;
    for (let top = 0; top + height <= ROWS; top++) {
      if (this.collides(cells, top, column)) break;
      landing = top;
    }
    return landing;
  }

  /** Would the piece overlap anything at this position? */
  collides(
    cells: readonly { readonly row: number; readonly col: number }[],
    top: number,
    column: number,
  ): boolean {
    for (const cell of cells) {
      const r = top + cell.row;
      const cl = column + cell.col;
      if (r < 0 || r >= ROWS || cl < 0 || cl >= COLUMNS) return true;
      if (this.occupied[r][cl]) return true;
    }
    return false;
  }

  /**
   * Could the piece have reached this position by falling straight down its
   * column? True when no occupied cell sits above any of its cells.
   *
   * **This is what keeps every recorded placement replayable.** The server's
   * model is a straight drop: it computes the landing row itself from
   * `(rotation, column)` and never learns how the player got there. So a piece
   * tucked under an overhang replays to a different row — and the symptom is the
   * whole run refused at the very end, which is the failure mode `add-tetris`
   * rejected keystroke replay to avoid.
   */
  reachableFromAbove(kind: TetrominoKind, rotation: number, top: number, column: number): boolean {
    for (const cell of cellsOf(kind, rotation)) {
      const col = column + cell.col;
      for (let r = top + cell.row - 1; r >= 0; r--) {
        if (this.occupied[r][col]) return false;
      }
    }
    return true;
  }

  /** Write the piece in at this position. Assumes it has been checked. */
  lock(kind: TetrominoKind, rotation: number, top: number, column: number): void {
    for (const cell of cellsOf(kind, rotation)) {
      this.occupied[top + cell.row][column + cell.col] = true;
    }
  }

  /** Remove full rows, shifting everything above down. Returns how many went. */
  clearFullLines(): number {
    let cleared = 0;
    let row = ROWS - 1;
    while (row >= 0) {
      if (this.occupied[row].every((x) => x)) {
        cleared++;
        for (let r = row; r > 0; r--) {
          this.occupied[r] = [...this.occupied[r - 1]];
        }
        this.occupied[0] = Array.from({ length: COLUMNS }, () => false);
        // The same row index now holds what used to be above it — re-check it.
      } else {
        row--;
      }
    }
    return cleared;
  }

  /** Drop the piece down this column and clear lines. Returns rows cleared. */
  placeAndClear(kind: TetrominoKind, rotation: number, column: number): number {
    const landing = this.landingRow(kind, rotation, column);
    if (landing === null) {
      throw new RangeError(`${kind} cannot be placed at column ${column}; the stack is too high.`);
    }
    this.lock(kind, rotation, landing, column);
    return this.clearFullLines();
  }
}
