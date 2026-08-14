import { describe, expect, it } from 'vitest';
import type { CrosswordLayout } from '../../core/api/models/puzzle.model';
import { cellKey, CrosswordState, slotCells } from './crossword-state';

/**
 * 合而为一 across row 0, 合情合理 down column 0, sharing (0,0).
 * (0,0) is pre-filled, so the tray holds the other six characters.
 */
const LAYOUT: CrosswordLayout = {
  rows: 4,
  cols: 4,
  cells: [
    { row: 0, col: 0 },
    { row: 0, col: 1 },
    { row: 0, col: 2 },
    { row: 0, col: 3 },
    { row: 1, col: 0 },
    { row: 2, col: 0 },
    { row: 3, col: 0 },
  ],
  given: [{ row: 0, col: 0, char: '合' }],
  tray: ['而', '为', '一', '情', '合', '理'],
  slots: [
    { index: 0, row: 0, col: 0, direction: 'Horizontal', length: 4 },
    { index: 1, row: 0, col: 0, direction: 'Vertical', length: 4 },
  ],
};

function loaded(): CrosswordState {
  const state = new CrosswordState();
  state.load(LAYOUT);
  return state;
}

/** Place the tray tile carrying `char` (first unused match). */
function placeChar(state: CrosswordState, char: string): string | null {
  const index = LAYOUT.tray.findIndex((t, i) => t === char && !state.usedTiles().has(i));
  return state.place(index, char);
}

describe('slotCells', () => {
  it('expands a horizontal slot left to right', () => {
    expect(slotCells(LAYOUT.slots[0]).map((c) => cellKey(c.row, c.col))).toEqual([
      '0,0',
      '0,1',
      '0,2',
      '0,3',
    ]);
  });

  it('expands a vertical slot top to bottom', () => {
    expect(slotCells(LAYOUT.slots[1]).map((c) => cellKey(c.row, c.col))).toEqual([
      '0,0',
      '1,0',
      '2,0',
      '3,0',
    ]);
  });
});

describe('CrosswordState', () => {
  it('locks pre-filled cells and shows their characters on load', () => {
    const state = loaded();

    expect(state.locked().has('0,0')).toBe(true);
    expect(state.chars().get('0,0')).toBe('合');
    expect(state.charAt('0,0')).toBe('合');
  });

  it('selects the first empty cell on load, skipping the given one', () => {
    expect(loaded().selected()).toBe('0,1');
  });

  it('places a tile into the selected cell and marks the tile used', () => {
    const state = loaded();

    const landed = placeChar(state, '而');

    expect(landed).toBe('0,1');
    expect(state.chars().get('0,1')).toBe('而');
    expect(state.usedTiles().has(LAYOUT.tray.indexOf('而'))).toBe(true);
  });

  it('advances the cursor inside the current slot before leaving it', () => {
    const state = loaded();

    placeChar(state, '而'); // 0,1
    expect(state.selected()).toBe('0,2');

    placeChar(state, '为'); // 0,2
    expect(state.selected()).toBe('0,3');
  });

  it('falls back to the next empty cell anywhere once a slot is full', () => {
    const state = loaded();

    placeChar(state, '而');
    placeChar(state, '为');
    placeChar(state, '一');

    // Row 0 is full; the cursor moves on to the vertical slot.
    expect(state.selected()).toBe('1,0');
  });

  it('returns a tile to the tray when its cell is tapped', () => {
    const state = loaded();
    const index = LAYOUT.tray.indexOf('而');
    placeChar(state, '而');

    const freed = state.takeBack('0,1');

    expect(freed).toBe(index);
    expect(state.chars().has('0,1')).toBe(false);
    expect(state.usedTiles().has(index)).toBe(false);
  });

  it('refuses to return a given cell', () => {
    const state = loaded();

    expect(state.takeBack('0,0')).toBeNull();
    expect(state.chars().get('0,0')).toBe('合');
  });

  it('refuses to return a locked cell', () => {
    const state = loaded();
    placeChar(state, '而');
    placeChar(state, '为');
    placeChar(state, '一');
    state.lockSlot(LAYOUT.slots[0]);

    expect(state.takeBack('0,1')).toBeNull();
    expect(state.chars().get('0,1')).toBe('而');
  });

  it('reports a slot as filled only once every cell has a character', () => {
    const state = loaded();
    expect(state.filledSlots()).toHaveLength(0);

    placeChar(state, '而');
    placeChar(state, '为');
    expect(state.filledSlots()).toHaveLength(0);

    placeChar(state, '一');
    expect(state.filledSlots().map((s) => s.index)).toEqual([0]);
  });

  it('reads back the word currently sitting in a slot', () => {
    const state = loaded();
    placeChar(state, '而');
    placeChar(state, '为');
    placeChar(state, '一');

    expect(state.wordIn(LAYOUT.slots[0])).toBe('合而为一');
  });

  it('returns only the unlocked tiles of a wrong slot', () => {
    const state = loaded();
    placeChar(state, '而');
    placeChar(state, '为');
    placeChar(state, '一');

    const freed = state.returnSlot(LAYOUT.slots[0]);

    // The given 合 stays; the three placed tiles come back.
    expect(freed).toHaveLength(3);
    expect(state.chars().get('0,0')).toBe('合');
    expect(state.chars().has('0,1')).toBe(false);
  });

  it('fills and locks a hinted cell', () => {
    const state = loaded();

    state.applyHint(1, 0, '情');

    expect(state.chars().get('1,0')).toBe('情');
    expect(state.locked().has('1,0')).toBe(true);
    expect(state.takeBack('1,0')).toBeNull();
  });

  it('frees a tile that a hint overwrites', () => {
    const state = loaded();
    const index = LAYOUT.tray.indexOf('理');
    state.select('1,0');
    state.place(index, '理'); // wrong character, sitting where the hint lands

    state.applyHint(1, 0, '情');

    expect(state.usedTiles().has(index)).toBe(false);
    expect(state.chars().get('1,0')).toBe('情');
  });

  it('is complete only when every cell holds a character', () => {
    const state = loaded();
    expect(state.complete()).toBe(false);

    for (const char of ['而', '为', '一', '情', '合', '理']) {
      placeChar(state, char);
    }

    expect(state.complete()).toBe(true);
  });

  it('builds a submission of every filled cell, including given ones', () => {
    const state = loaded();
    placeChar(state, '而');

    const submission = state.submission();

    expect(submission['0,0']).toBe('合');
    expect(submission['0,1']).toBe('而');
    expect(Object.keys(submission)).toHaveLength(2);
  });

  it('holds no score — mistakes and hints are the server’s business', () => {
    const state = loaded() as unknown as Record<string, unknown>;

    expect(state['mistakes']).toBeUndefined();
    expect(state['hintsUsed']).toBeUndefined();
    expect(state['stars']).toBeUndefined();
  });
});
