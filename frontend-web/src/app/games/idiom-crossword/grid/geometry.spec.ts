import { describe, expect, it } from 'vitest';
import { cellSizeFor, gapFor, MAX_CELL_PX, MIN_CELL_PX } from './geometry';

describe('gapFor', () => {
  it('tightens the gap once a board gets wide', () => {
    expect(gapFor(4)).toBe(6);
    expect(gapFor(7)).toBe(6);
    expect(gapFor(8)).toBe(4);
    expect(gapFor(12)).toBe(4);
  });
});

describe('cellSizeFor', () => {
  it('caps at the maximum when there is room to spare', () => {
    expect(cellSizeFor(1200, 4, 6)).toBe(MAX_CELL_PX);
  });

  it('shrinks as the container narrows', () => {
    const wide = cellSizeFor(600, 10, 4);
    const narrow = cellSizeFor(360, 10, 4);

    expect(narrow).toBeLessThan(wide);
  });

  it('floors at the legible minimum instead of vanishing', () => {
    // 12 columns in a 320px container would be ~23px per cell without the floor.
    expect(cellSizeFor(320, 12, 4)).toBe(MIN_CELL_PX);
  });

  it('accounts for the gaps between cells, not just the cells', () => {
    // 4 columns, 3 gaps of 6px = 18px of the width is gap.
    expect(cellSizeFor(218, 4, 6)).toBe(50);
  });

  it('falls back to the maximum before the container has been measured', () => {
    // ResizeObserver has not fired yet; rendering at max avoids a visible jump
    // from tiny to correct on first paint.
    expect(cellSizeFor(0, 8, 4)).toBe(MAX_CELL_PX);
  });

  it('never returns a nonsensical size for a degenerate board', () => {
    expect(cellSizeFor(300, 0, 4)).toBe(MAX_CELL_PX);
  });

  it('keeps the largest shipped level inside a 375px viewport', () => {
    // Level 12 is 12×10. At 375px the board is allowed to scroll inside its own
    // container, but the cells must still be legible.
    const size = cellSizeFor(375, 12, gapFor(12));

    expect(size).toBeGreaterThanOrEqual(MIN_CELL_PX);
    expect(size).toBeLessThanOrEqual(MAX_CELL_PX);
  });
});
