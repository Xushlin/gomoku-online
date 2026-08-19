import { describe, expect, it } from 'vitest';
import type { MoveDto } from '../../core/api/models/room.model';
import {
  BLACK,
  cellIndex,
  INITIAL_POSITION,
  lastMoveCaptured,
  pieceAt,
  positionAfter,
  RED,
  XIANGQI_COLS,
  XIANGQI_ROWS,
  type XiangqiPiece,
  type XiangqiPieceType,
  type XiangqiSide,
} from './position';

/**
 * The opening setup is a deliberate copy of the server's `XiangqiBoard.Initial()`
 * (see position.ts for why). A front-end test cannot diff it against the backend,
 * so these assertions go after the failure mode that actually happens: a mistyped
 * coordinate or a missing piece.
 */
function census(side: XiangqiSide): Record<XiangqiPieceType, number> {
  const counts = {
    general: 0,
    advisor: 0,
    elephant: 0,
    horse: 0,
    chariot: 0,
    cannon: 0,
    soldier: 0,
  };
  for (const cell of INITIAL_POSITION) {
    if (cell?.side === side) counts[cell.type]++;
  }
  return counts;
}

function move(fromRow: number, fromCol: number, row: number, col: number, ply = 1): MoveDto {
  return {
    ply,
    row,
    col,
    seat: 0,
    playedAt: '2026-08-16T12:00:00Z',
    fromRow,
    fromCol,
  };
}

describe('INITIAL_POSITION', () => {
  it('has 90 intersections and 32 pieces', () => {
    expect(INITIAL_POSITION).toHaveLength(XIANGQI_ROWS * XIANGQI_COLS);
    expect(INITIAL_POSITION.filter(Boolean)).toHaveLength(32);
  });

  it('gives each side the textbook complement', () => {
    const expected = {
      general: 1,
      advisor: 2,
      elephant: 2,
      horse: 2,
      chariot: 2,
      cannon: 2,
      soldier: 5,
    };
    expect(census(RED)).toEqual(expected);
    expect(census(BLACK)).toEqual(expected);
  });

  it('is mirror-symmetric about the general’s file', () => {
    // The real failure mode is one wrong column, and asymmetry is how it shows.
    for (let row = 0; row < XIANGQI_ROWS; row++) {
      for (let col = 0; col < XIANGQI_COLS; col++) {
        const here = pieceAt(INITIAL_POSITION, row, col);
        const mirrored = pieceAt(INITIAL_POSITION, row, XIANGQI_COLS - 1 - col);
        expect(mirrored, `(${row},${col}) has no mirror`).toEqual(here);
      }
    }
  });

  it('puts red at the bottom and black at the top', () => {
    // Orientation is the one thing that would make every rendered board wrong
    // while every individual piece looked right.
    INITIAL_POSITION.forEach((piece, i) => {
      if (!piece) return;
      const row = Math.floor(i / XIANGQI_COLS);
      if (piece.side === RED) expect(row).toBeGreaterThanOrEqual(5);
      else expect(row).toBeLessThanOrEqual(4);
    });
  });

  it('reads Stone.Black as red — the bet add-xiangqi placed', () => {
    // 象棋 is red-first and `Game` opens on Stone.Black, so Black *is* red here.
    // This assertion exists to stop someone "correcting" it.
    expect(RED).toBe('Black');
    expect(BLACK).toBe('White');
    expect(pieceAt(INITIAL_POSITION, 9, 4)).toEqual<XiangqiPiece>({ type: 'general', side: RED });
    expect(pieceAt(INITIAL_POSITION, 0, 4)).toEqual<XiangqiPiece>({ type: 'general', side: BLACK });
  });

  it('places the cannons and the front rank of soldiers', () => {
    expect(pieceAt(INITIAL_POSITION, 7, 1)?.type).toBe('cannon');
    expect(pieceAt(INITIAL_POSITION, 7, 7)?.type).toBe('cannon');
    expect(pieceAt(INITIAL_POSITION, 2, 1)?.type).toBe('cannon');
    expect(pieceAt(INITIAL_POSITION, 2, 7)?.type).toBe('cannon');

    for (const col of [0, 2, 4, 6, 8]) {
      expect(pieceAt(INITIAL_POSITION, 6, col)?.type, `red soldier at col ${col}`).toBe('soldier');
      expect(pieceAt(INITIAL_POSITION, 3, col)?.type, `black soldier at col ${col}`).toBe('soldier');
    }
    for (const col of [1, 3, 5, 7]) {
      expect(pieceAt(INITIAL_POSITION, 6, col)).toBeNull();
      expect(pieceAt(INITIAL_POSITION, 3, col)).toBeNull();
    }
  });
});

describe('positionAfter', () => {
  it('returns the opening setup for an empty history', () => {
    expect(positionAfter([])).toEqual(INITIAL_POSITION);
  });

  it('empties the origin and fills the destination', () => {
    const after = positionAfter([move(9, 0, 8, 0)]);

    expect(pieceAt(after, 9, 0)).toBeNull();
    expect(pieceAt(after, 8, 0)).toEqual<XiangqiPiece>({ type: 'chariot', side: RED });
  });

  it('removes a captured piece', () => {
    // Red cannon (7,1) shoots the black horse at (0,1) — the classic 炮打马.
    const after = positionAfter([move(7, 1, 0, 1)]);

    expect(pieceAt(after, 7, 1)).toBeNull();
    expect(pieceAt(after, 0, 1)).toEqual<XiangqiPiece>({ type: 'cannon', side: RED });
    expect(after.filter(Boolean)).toHaveLength(31);
  });

  it('does not mutate the opening setup', () => {
    const before = [...INITIAL_POSITION];
    positionAfter([move(9, 0, 8, 0), move(0, 0, 1, 0, 2)]);

    expect([...INITIAL_POSITION]).toEqual(before);
  });

  it('skips plies with no origin', () => {
    // That is the shape a *placement* game produces. A mismatched history should
    // draw a board that may be wrong, not blank the page.
    const placement: MoveDto = {
      ply: 1,
      row: 5,
      col: 5,
      seat: 0,
      playedAt: '2026-08-16T12:00:00Z',
      fromRow: null,
      fromCol: null,
    };

    expect(() => positionAfter([placement])).not.toThrow();
    expect(positionAfter([placement])).toEqual(INITIAL_POSITION);
  });

  it('skips out-of-range plies', () => {
    expect(positionAfter([move(99, 99, 0, 0)])).toEqual(INITIAL_POSITION);
    expect(positionAfter([move(9, 0, 99, 0)])).toEqual(INITIAL_POSITION);
  });

  it('applies a whole opening in order', () => {
    const after = positionAfter([
      move(7, 1, 7, 4, 1), // 红炮二平五
      move(0, 1, 2, 2, 2), // 黑马
      move(9, 1, 7, 2, 3), // 红马
    ]);

    expect(pieceAt(after, 7, 4)).toEqual<XiangqiPiece>({ type: 'cannon', side: RED });
    expect(pieceAt(after, 2, 2)).toEqual<XiangqiPiece>({ type: 'horse', side: BLACK });
    expect(pieceAt(after, 7, 2)).toEqual<XiangqiPiece>({ type: 'horse', side: RED });
    expect(after.filter(Boolean)).toHaveLength(32);
  });
});

describe('cellIndex', () => {
  it('is row-major', () => {
    expect(cellIndex(0, 0)).toBe(0);
    expect(cellIndex(0, 8)).toBe(8);
    expect(cellIndex(1, 0)).toBe(9);
    expect(cellIndex(9, 8)).toBe(89);
  });
});

/**
 * 红 soldier walks up its file, 黑 soldier walks down the same one, then 红 takes it.
 * Real 象棋: soldiers capture straight ahead, and 红 moves towards row 0.
 */
const SOLDIER_ADVANCE: MoveDto = {
  ply: 1,
  fromRow: 6,
  fromCol: 0,
  row: 5,
  col: 0,
  seat: 0,
  playedAt: 'x',
};
const BLACK_ADVANCE: MoveDto = {
  ply: 2,
  fromRow: 3,
  fromCol: 0,
  row: 4,
  col: 0,
  seat: 1,
  playedAt: 'x',
};
const THE_CAPTURE: MoveDto = {
  ply: 3,
  fromRow: 5,
  fromCol: 0,
  row: 4,
  col: 0,
  seat: 0,
  playedAt: 'x',
};

describe('lastMoveCaptured', () => {
  it('is false with no moves at all', () => {
    expect(lastMoveCaptured([])).toBe(false);
  });

  it('is false for a move onto an empty point', () => {
    expect(lastMoveCaptured([SOLDIER_ADVANCE])).toBe(false);
  });

  it('is true for a move onto an occupied point', () => {
    expect(lastMoveCaptured([SOLDIER_ADVANCE, BLACK_ADVANCE, THE_CAPTURE])).toBe(true);
  });

  it('looks at the last move only, not at whether any move ever captured', () => {
    const quietAfterwards: MoveDto = {
      ply: 4,
      fromRow: 0,
      fromCol: 1,
      row: 2,
      col: 2,
      seat: 1,
      playedAt: 'x',
    };

    expect(
      lastMoveCaptured([SOLDIER_ADVANCE, BLACK_ADVANCE, THE_CAPTURE, quietAfterwards]),
    ).toBe(false);
  });

  it('is false for a ply with no origin', () => {
    // The shape a placement game produces. Same rule `positionAfter` follows: a
    // mismatched history draws a board that may be wrong, it never throws.
    const placement: MoveDto = { ply: 1, row: 4, col: 0, seat: 0, playedAt: 'x' };

    expect(lastMoveCaptured([placement])).toBe(false);
  });

  it('is false for a destination off the board', () => {
    const nonsense: MoveDto = {
      ply: 1,
      fromRow: 6,
      fromCol: 0,
      row: 99,
      col: 0,
      seat: 0,
      playedAt: 'x',
    };

    expect(lastMoveCaptured([nonsense])).toBe(false);
  });
});
