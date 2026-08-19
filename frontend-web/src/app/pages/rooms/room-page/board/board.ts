import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  output,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { MoveDto, RoomState, Stone } from '../../../../core/api/models/room.model';
import { seatStone, seatOfSide } from '../../../../games/board-seats';

/**
 * Gomoku's size, kept as the default so the two existing call sites (room page,
 * replay page) need no change. It used to be a file-level constant and therefore
 * the front-end twin of the backend's hardcoded `Board(15, 15, 5)` — which
 * `add-game-rules-registry` already turned into parameters.
 */
const DEFAULT_SIZE = 15;

interface CellCoord {
  readonly row: number;
  readonly col: number;
}

@Component({
  selector: 'app-board',
  standalone: true,
  imports: [TranslocoPipe],
  templateUrl: './board.html',
  styles: [':host { display: block; width: 100%; }'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Board {
  readonly state = input<RoomState | null>(null);
  readonly mySide = input<'black' | 'white' | 'spectator'>('spectator');
  readonly submitting = input<boolean>(false);
  readonly = input<boolean>(false);
  readonly cellClick = output<CellCoord>();

  /**
   * Board dimensions, supplied by the container.
   *
   * This component stays purely presentational: it does NOT know about `gameKey`
   * and does NOT inject `GameCatalogService`. Resolving a game key into a size is
   * the container's job — same split the rest of the app already follows.
   */
  readonly rows = input<number>(DEFAULT_SIZE);
  readonly cols = input<number>(DEFAULT_SIZE);

  protected readonly rowIndices = computed(() =>
    Array.from({ length: this.rows() }, (_, i) => i),
  );
  protected readonly colIndices = computed(() =>
    Array.from({ length: this.cols() }, (_, i) => i),
  );

  /** Star points are placed for a 15×15 board; on anything else they are noise. */
  protected readonly showStars = computed(
    () => this.rows() === DEFAULT_SIZE && this.cols() === DEFAULT_SIZE,
  );

  private readonly grid = computed<Stone[][]>(() => {
    const rowCount = this.rows();
    const colCount = this.cols();
    const board: Stone[][] = Array.from({ length: rowCount }, () =>
      Array.from<Stone>({ length: colCount }).fill('Empty'),
    );
    const moves = this.state()?.game?.moves ?? [];
    for (const move of moves) {
      // Out-of-range plies are dropped rather than thrown on. If the client's
      // idea of the size ever disagrees with the server's, the board should look
      // wrong — not blank the page.
      // A move with no square is not this board's business — a textual move
      // (成语接龙) reaching a grid renderer means the wrong component is mounted,
      // and skipping is the same "look wrong, never blank" rule as out-of-range.
      const { row, col } = move;
      if (row == null || col == null) continue;
      if (row >= 0 && row < rowCount && col >= 0 && col < colCount) {
        board[row][col] = seatStone(move.seat);
      }
    }
    return board;
  });

  protected readonly lastMove = computed<MoveDto | null>(() => {
    const moves = this.state()?.game?.moves ?? [];
    return moves.length > 0 ? moves[moves.length - 1] : null;
  });

  private readonly myTurn = computed<boolean>(() => {
    const mySeat = seatOfSide(this.mySide());
    return mySeat !== null && this.state()?.game?.currentSeat === mySeat;
  });

  protected stoneAt(row: number, col: number): Stone {
    return this.grid()[row]?.[col] ?? 'Empty';
  }

  protected cellDisabled(row: number, col: number): boolean {
    if (this.readonly()) return true;
    if (this.submitting()) return true;
    if (this.mySide() === 'spectator') return true;
    if (this.state()?.status !== 'Playing') return true;
    if (!this.myTurn()) return true;
    return this.stoneAt(row, col) !== 'Empty';
  }

  protected isLastMove(row: number, col: number): boolean {
    const last = this.lastMove();
    return last !== null && last.row === row && last.col === col;
  }

  protected handleClick(row: number, col: number): void {
    if (this.readonly()) return;
    if (this.cellDisabled(row, col)) return;
    this.cellClick.emit({ row, col });
  }
}
