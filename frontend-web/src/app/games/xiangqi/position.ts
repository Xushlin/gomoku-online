import type { MoveDto, Stone } from '../../core/api/models/room.model';

/** 10 rows, 9 columns — the standard 象棋 board. */
export const XIANGQI_ROWS = 10;
export const XIANGQI_COLS = 9;

/** The seven kinds of piece. Names are the western-neutral ones; the glyphs live in the board component. */
export type XiangqiPieceType =
  | 'general'
  | 'advisor'
  | 'elephant'
  | 'horse'
  | 'chariot'
  | 'cannon'
  | 'soldier';

/** A piece belongs to one of the two sides — never to `Empty`. */
export type XiangqiSide = Exclude<Stone, 'Empty'>;

export interface XiangqiPiece {
  readonly type: XiangqiPieceType;
  readonly side: XiangqiSide;
}

/** Row-major, length `XIANGQI_ROWS * XIANGQI_COLS`. `null` is an empty intersection. */
export type XiangqiPosition = readonly (XiangqiPiece | null)[];

/**
 * `Stone.Black` is **red** in 象棋.
 *
 * `Game` opens on `Stone.Black` and 象棋 is red-first, so reading Black as red is
 * what let `add-xiangqi` ship without touching the Domain at all: `Stone` has always
 * meant "first mover / second mover", and 红/黑 is how the display paints it.
 *
 * These two constants exist so that reading is never spelled out inline as a bare
 * `=== 'Black'`, which is where someone would eventually "fix" it.
 */
export const RED: XiangqiSide = 'Black';
export const BLACK: XiangqiSide = 'White';

const CELL_COUNT = XIANGQI_ROWS * XIANGQI_COLS;

/** Back-rank order, left to right. Mirror-symmetric about column 4 — the general's file. */
const BACK_RANK: readonly XiangqiPieceType[] = [
  'chariot',
  'horse',
  'elephant',
  'advisor',
  'general',
  'advisor',
  'elephant',
  'horse',
  'chariot',
];

/** Index of an intersection in the row-major cell array. */
export function cellIndex(row: number, col: number): number {
  return row * XIANGQI_COLS + col;
}

export function inBounds(row: number, col: number): boolean {
  return row >= 0 && row < XIANGQI_ROWS && col >= 0 && col < XIANGQI_COLS;
}

export function pieceAt(position: XiangqiPosition, row: number, col: number): XiangqiPiece | null {
  return inBounds(row, col) ? (position[cellIndex(row, col)] ?? null) : null;
}

function buildInitialPosition(): XiangqiPosition {
  const cells: (XiangqiPiece | null)[] = Array.from({ length: CELL_COUNT }, () => null);

  const place = (row: number, col: number, type: XiangqiPieceType, side: XiangqiSide): void => {
    cells[cellIndex(row, col)] = { type, side };
  };

  const placeSide = (backRank: number, cannonRank: number, soldierRank: number, side: XiangqiSide): void => {
    BACK_RANK.forEach((type, col) => place(backRank, col, type, side));
    place(cannonRank, 1, 'cannon', side);
    place(cannonRank, 7, 'cannon', side);
    for (let col = 0; col < XIANGQI_COLS; col += 2) {
      place(soldierRank, col, 'soldier', side);
    }
  };

  // 黑方 (Stone.White) on top, row 0 is its back rank; 红方 (Stone.Black) at the
  // bottom, row 9 is its back rank. Same orientation as the server's XiangqiBoard.Initial().
  placeSide(0, 2, 3, BLACK);
  placeSide(9, 7, 6, RED);

  return cells;
}

/**
 * The opening setup — 32 pieces.
 *
 * **This is a deliberate copy** of the server's `XiangqiBoard.Initial()`, and the
 * only one in this game. It exists because a 象棋 board cannot be derived from the
 * move history the way a gomoku board can: gomoku's every ply *places* a stone of a
 * known colour, so the history is the board. 象棋's plies are `from → to`, which say
 * nothing about where anything started.
 *
 * The copy is acceptable on this repo's own test — *whether a copy is acceptable
 * depends not on how small it is but on whether being wrong would ever be noticed.*
 * Being wrong here paints the whole board wrong on move zero, which is the most
 * visible failure mode there is; and the server rejects any move that only looks
 * legal on a wrong board. Both safety nets, the same pair that made `GameManifest.board`
 * tolerable.
 *
 * It also is not server *state*: the opening setup of 象棋 is a rule of the game,
 * as public and as fixed as "the board is 10×9".
 */
export const INITIAL_POSITION: XiangqiPosition = buildInitialPosition();

/**
 * The position after applying `moves` to the opening setup.
 *
 * Pure — callers get a fresh array and `INITIAL_POSITION` is never mutated.
 *
 * Plies with no origin (the shape a *placement* game produces) are skipped rather
 * than thrown on: a client that somehow receives a mismatched history should draw a
 * board that may be wrong, not blank the page. Same rule the gomoku board already
 * follows for out-of-range coordinates.
 *
 * The move itself is applied exactly the way the server applies it — clear the
 * origin, overwrite the destination — so a valid history reaches the same position
 * on both sides by the same steps.
 */
export function positionAfter(moves: readonly MoveDto[]): XiangqiPosition {
  const cells: (XiangqiPiece | null)[] = [...INITIAL_POSITION];

  for (const move of moves) {
    const { fromRow, fromCol, row, col } = move;
    if (fromRow == null || fromCol == null || row == null || col == null) continue;
    if (!inBounds(fromRow, fromCol) || !inBounds(row, col)) continue;

    const from = cellIndex(fromRow, fromCol);
    const to = cellIndex(row, col);
    cells[to] = cells[from];
    cells[from] = null;
  }

  return cells;
}
