/**
 * The seven standard tetrominoes and their four rotations.
 *
 * A **port** of the backend's `Tetromino`, not an independent design. The
 * underlying order matters: it is what `pieceSequence` emits and what the server
 * replays, so renaming or reordering these changes every historical run.
 */
export const TETROMINO_KINDS = ['I', 'O', 'T', 'S', 'Z', 'J', 'L'] as const;

/** One of the seven standard pieces. */
export type TetrominoKind = (typeof TETROMINO_KINDS)[number];

/** A cell offset within a rotation, row-down-positive. */
export interface CellOffset {
  readonly row: number;
  readonly col: number;
}

/** How many rotation states a piece has. */
export const ROTATIONS = 4;

/** Rotation 0 of each piece — the only hand-written data. */
const BASIS: Readonly<Record<TetrominoKind, readonly CellOffset[]>> = {
  I: [c(0, 0), c(0, 1), c(0, 2), c(0, 3)],
  O: [c(0, 0), c(0, 1), c(1, 0), c(1, 1)],
  T: [c(0, 1), c(1, 0), c(1, 1), c(1, 2)],
  S: [c(0, 1), c(0, 2), c(1, 0), c(1, 1)],
  Z: [c(0, 0), c(0, 1), c(1, 1), c(1, 2)],
  J: [c(0, 0), c(1, 0), c(1, 1), c(1, 2)],
  L: [c(0, 2), c(1, 0), c(1, 1), c(1, 2)],
};

function c(row: number, col: number): CellOffset {
  return { row, col };
}

/** Clockwise 90°: (row, col) → (col, -row). */
function rotateClockwise(cells: readonly CellOffset[]): readonly CellOffset[] {
  return cells.map((cell) => c(cell.col, -cell.row));
}

/**
 * Translate so the shape's top-left touches the origin, then sort.
 *
 * **This is the load-bearing half of the port.** `column` in a submitted
 * placement means "the column of the leftmost cell of this rotation", and that
 * only has one meaning if every rotation is normalised the same way the server
 * normalises it. Skip this and `column` is off by a rotation-dependent constant,
 * so *every* placement is wrong while the screen looks perfectly normal.
 */
function normalise(cells: readonly CellOffset[]): readonly CellOffset[] {
  const minRow = Math.min(...cells.map((x) => x.row));
  const minCol = Math.min(...cells.map((x) => x.col));
  return cells
    .map((x) => c(x.row - minRow, x.col - minCol))
    .sort((a, b) => a.row - b.row || a.col - b.col);
}

const SHAPES: Readonly<Record<TetrominoKind, readonly (readonly CellOffset[])[]>> = (() => {
  const out = {} as Record<TetrominoKind, readonly (readonly CellOffset[])[]>;
  for (const kind of TETROMINO_KINDS) {
    const states: (readonly CellOffset[])[] = [];
    let current = BASIS[kind];
    for (let r = 0; r < ROTATIONS; r++) {
      states.push(normalise(current));
      current = rotateClockwise(current);
    }
    out[kind] = states;
  }
  return out;
})();

/** Cells occupied by a piece in a rotation. Rotation is taken modulo 4. */
export function cellsOf(kind: TetrominoKind, rotation: number): readonly CellOffset[] {
  return SHAPES[kind][((rotation % ROTATIONS) + ROTATIONS) % ROTATIONS];
}

/** Width of a rotation, in columns — used to bound the placement column. */
export function widthOf(kind: TetrominoKind, rotation: number): number {
  return Math.max(...cellsOf(kind, rotation).map((x) => x.col)) + 1;
}

/** Height of a rotation, in rows. */
export function heightOf(kind: TetrominoKind, rotation: number): number {
  return Math.max(...cellsOf(kind, rotation).map((x) => x.row)) + 1;
}
