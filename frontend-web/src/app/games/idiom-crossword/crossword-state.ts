import { computed, signal, type Signal } from '@angular/core';
import type {
  CrosswordCell,
  CrosswordLayout,
  CrosswordSlot,
} from '../../core/api/models/puzzle.model';

/** `"row,col"` — the same key shape the server's solution uses. */
export function cellKey(row: number, col: number): string {
  return `${row},${col}`;
}

/** Expand a slot into its cells, in reading order. */
export function slotCells(slot: CrosswordSlot): CrosswordCell[] {
  const cells: CrosswordCell[] = [];
  for (let i = 0; i < slot.length; i++) {
    cells.push(
      slot.direction === 'Horizontal'
        ? { row: slot.row, col: slot.col + i }
        : { row: slot.row + i, col: slot.col },
    );
  }
  return cells;
}

/** What the player put in a cell, and which tray tile it came from. */
interface Placement {
  readonly char: string;
  readonly trayIndex: number;
}

/**
 * The board's presentation state: which cell holds which tile, which cells are
 * locked, which tray tiles are spent, and where the cursor is.
 *
 * Deliberately holds **no score**. Mistakes, hints and stars are server facts
 * and live on the container, read from responses — a client that keeps its own
 * count drifts from the server's the first time a request is retried, and then
 * the number on screen disagrees with the number that was awarded.
 *
 * Pure and framework-light on purpose: all the fiddly grid logic (cursor
 * advance, slot completion, tile return) is here where it can be unit-tested
 * without rendering anything.
 */
export class CrosswordState {
  private readonly _layout = signal<CrosswordLayout | null>(null);
  private readonly _placed = signal<ReadonlyMap<string, Placement>>(new Map());
  private readonly _locked = signal<ReadonlySet<string>>(new Set());
  private readonly _usedTiles = signal<ReadonlySet<number>>(new Set());
  private readonly _selected = signal<string | null>(null);

  readonly layout: Signal<CrosswordLayout | null> = this._layout.asReadonly();
  readonly placed = this._placed.asReadonly();
  readonly locked = this._locked.asReadonly();
  readonly usedTiles = this._usedTiles.asReadonly();
  readonly selected = this._selected.asReadonly();

  /** Pre-filled cells, keyed — they are locked from the start and never returned. */
  readonly given = computed<ReadonlyMap<string, string>>(() => {
    const layout = this._layout();
    const map = new Map<string, string>();
    for (const g of layout?.given ?? []) {
      map.set(cellKey(g.row, g.col), g.char);
    }
    return map;
  });

  /**
   * What each cell currently shows — pre-filled characters merged with placed
   * ones. This is what the board renders; the two sources are separate in state
   * because only one of them is returnable.
   */
  readonly chars = computed<ReadonlyMap<string, string>>(() => {
    const merged = new Map(this.given());
    for (const [key, placement] of this._placed()) {
      merged.set(key, placement.char);
    }
    return merged;
  });

  /** Cells that exist in the grid, in reading order. */
  readonly playableCells = computed<readonly CrosswordCell[]>(() => {
    const cells = [...(this._layout()?.cells ?? [])];
    return cells.sort((a, b) => a.row - b.row || a.col - b.col);
  });

  /** True once every cell holds a character (given, hinted, or placed). */
  readonly complete = computed(() => {
    const given = this.given();
    const placed = this._placed();
    return this.playableCells().every((c) => {
      const key = cellKey(c.row, c.col);
      return given.has(key) || placed.has(key);
    });
  });

  /** The character currently shown in a cell, or null. */
  charAt(key: string): string | null {
    return this.given().get(key) ?? this._placed().get(key)?.char ?? null;
  }

  /** Load a level and reset everything. */
  load(layout: CrosswordLayout): void {
    this._layout.set(layout);
    this._placed.set(new Map());
    this._locked.set(new Set(this.given().keys()));
    this._usedTiles.set(new Set());
    this._selected.set(this.firstEmpty());
  }

  /** Select a cell. Locked and given cells are not selectable. */
  select(key: string): void {
    if (this._locked().has(key)) return;
    this._selected.set(key);
  }

  /**
   * Tap a cell: if it holds a returnable tile, take it back; then select it.
   * Returns the tray index freed, or null.
   */
  takeBack(key: string): number | null {
    if (this._locked().has(key)) return null;

    const placement = this._placed().get(key);
    if (!placement) {
      this._selected.set(key);
      return null;
    }

    const placed = new Map(this._placed());
    placed.delete(key);
    this._placed.set(placed);

    const used = new Set(this._usedTiles());
    used.delete(placement.trayIndex);
    this._usedTiles.set(used);

    this._selected.set(key);
    return placement.trayIndex;
  }

  /**
   * Put tray tile `trayIndex` into the selected cell (or the first empty one).
   * Returns the cell it landed in, or null when there was nowhere to put it.
   */
  place(trayIndex: number, char: string): string | null {
    if (this._usedTiles().has(trayIndex)) return null;

    let key = this._selected();
    if (!key || this._locked().has(key) || this._placed().has(key)) {
      key = this.firstEmpty();
    }
    if (!key) return null;

    const placed = new Map(this._placed());
    placed.set(key, { char, trayIndex });
    this._placed.set(placed);

    const used = new Set(this._usedTiles());
    used.add(trayIndex);
    this._usedTiles.set(used);

    this._selected.set(this.nextEmptyAfter(key));
    return key;
  }

  /** Lock a solved slot's cells so they can no longer be edited. */
  lockSlot(slot: CrosswordSlot): void {
    const locked = new Set(this._locked());
    for (const cell of slotCells(slot)) {
      locked.add(cellKey(cell.row, cell.col));
    }
    this._locked.set(locked);
  }

  /** Fill and lock one cell from a server hint. Frees any tile that was there. */
  applyHint(row: number, col: number, char: string): number | null {
    const key = cellKey(row, col);
    const freed = this.takeBack(key);

    const placed = new Map(this._placed());
    placed.set(key, { char, trayIndex: -1 }); // -1: came from the server, not the tray
    this._placed.set(placed);

    const locked = new Set(this._locked());
    locked.add(key);
    this._locked.set(locked);

    this._selected.set(this.firstEmpty());
    return freed;
  }

  /** Return every tile of a slot that is not locked or given — the wrong-answer path. */
  returnSlot(slot: CrosswordSlot): number[] {
    const freed: number[] = [];
    for (const cell of slotCells(slot)) {
      const key = cellKey(cell.row, cell.col);
      if (this._locked().has(key)) continue;
      const index = this.takeBack(key);
      if (index !== null && index >= 0) freed.push(index);
    }
    return freed;
  }

  /** Slots whose cells are all filled — the ones worth asking the server about. */
  filledSlots(): readonly CrosswordSlot[] {
    const given = this.given();
    const placed = this._placed();
    return (this._layout()?.slots ?? []).filter((slot) =>
      slotCells(slot).every((c) => {
        const key = cellKey(c.row, c.col);
        return given.has(key) || placed.has(key);
      }),
    );
  }

  /** The characters currently sitting in a slot, in reading order. */
  wordIn(slot: CrosswordSlot): string {
    return slotCells(slot)
      .map((c) => this.charAt(cellKey(c.row, c.col)) ?? '')
      .join('');
  }

  /** Every filled cell, as the submission payload shape. */
  submission(): Record<string, string> {
    const cells: Record<string, string> = {};
    for (const cell of this.playableCells()) {
      const key = cellKey(cell.row, cell.col);
      const char = this.charAt(key);
      if (char) cells[key] = char;
    }
    return cells;
  }

  private firstEmpty(): string | null {
    const given = this.given();
    const placed = this._placed();
    for (const cell of this.playableCells()) {
      const key = cellKey(cell.row, cell.col);
      if (!given.has(key) && !placed.has(key)) return key;
    }
    return null;
  }

  /**
   * Prefer the next empty cell **within a slot the current cell belongs to**,
   * then fall back to the first empty cell anywhere — the prototype's behaviour,
   * and the reason filling an idiom feels continuous rather than scattered.
   */
  private nextEmptyAfter(key: string): string | null {
    const given = this.given();
    const placed = this._placed();
    const isEmpty = (k: string) => !given.has(k) && !placed.has(k);

    for (const slot of this._layout()?.slots ?? []) {
      const keys = slotCells(slot).map((c) => cellKey(c.row, c.col));
      const at = keys.indexOf(key);
      if (at === -1) continue;

      for (let i = at + 1; i < keys.length; i++) {
        if (isEmpty(keys[i])) return keys[i];
      }
      for (let i = 0; i < at; i++) {
        if (isEmpty(keys[i])) return keys[i];
      }
    }

    return this.firstEmpty();
  }
}
