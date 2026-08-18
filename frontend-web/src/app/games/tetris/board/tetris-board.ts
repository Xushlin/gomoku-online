import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { COLUMNS, ROWS } from '../engine/field';
import { cellsOf, type TetrominoKind } from '../engine/tetromino';

/** What a cell is showing. `ghost` is the hard-drop preview. */
export type CellState = 'empty' | 'locked' | 'active' | 'ghost';

/** One rendered cell. `key` is stable so `@for` never re-creates a row. */
export interface RenderedCell {
  readonly key: number;
  readonly state: CellState;
}

/**
 * The field, purely presentational — it renders the cells it is given and judges
 * nothing. All rule knowledge lives in `engine/`, which is where the tests point.
 *
 * Colours come from the theme tokens, never literals, so both colour modes and
 * every theme work without a per-theme branch here. Cells are `aria-hidden`: a
 * 200-cell grid read out one cell at a time is noise, and the status line above
 * the board carries the information a screen reader needs.
 */
@Component({
  selector: 'app-tetris-board',
  standalone: true,
  imports: [TranslocoPipe],
  templateUrl: './tetris-board.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TetrisBoard {
  /** Row-major cells, length `ROWS * COLUMNS`. */
  readonly cells = input.required<readonly RenderedCell[]>();

  /** The upcoming piece, for the preview panel. */
  readonly nextPiece = input<TetrominoKind | null>(null);

  protected readonly columns = COLUMNS;
  protected readonly rows = ROWS;

  /** A 4×4 preview of the next piece at rotation 0. */
  protected readonly previewCells = computed<readonly boolean[]>(() => {
    const kind = this.nextPiece();
    const grid = Array.from({ length: 16 }, () => false);
    if (!kind) return grid;
    for (const cell of cellsOf(kind, 0)) {
      grid[cell.row * 4 + cell.col] = true;
    }
    return grid;
  });

  protected cellClass(state: CellState): string {
    switch (state) {
      case 'locked':
        return 'bg-primary';
      case 'active':
        // Same hue, lighter — the falling piece is also identifiable by moving,
        // and a second hue here would need a token every board skin must define.
        return 'bg-primary/70 ring-primary ring-1 ring-inset';
      case 'ghost':
        // Outline only: a filled ghost reads as a locked block, and telling those
        // two apart at a glance is the whole point of showing it.
        return 'border-primary/50 border-2 border-dashed';
      default:
        return 'border-border/40 border';
    }
  }
}
