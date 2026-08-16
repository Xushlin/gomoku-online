import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  input,
  output,
  signal,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { MoveDto, RoomState } from '../../../core/api/models/room.model';
import {
  BLACK,
  pieceAt,
  positionAfter,
  RED,
  XIANGQI_COLS,
  XIANGQI_ROWS,
  type XiangqiPiece,
  type XiangqiPieceType,
  type XiangqiSide,
} from '../position';

export interface BoardPoint {
  readonly row: number;
  readonly col: number;
}

export interface PieceMoveEvent {
  readonly from: BoardPoint;
  readonly to: BoardPoint;
}

/**
 * Piece glyphs.
 *
 * These are **graphics, not copy** — they are what a 象棋 piece looks like in every
 * locale, the way a chess knight is a horse's head everywhere. They therefore stay
 * here rather than in the locale files. The *spoken* name of each piece is a
 * different thing and does go through i18n (see `pieceNameKey`).
 */
const GLYPHS: Record<XiangqiSide, Record<XiangqiPieceType, string>> = {
  // Stone.Black is 红 — see position.ts and design D3.
  Black: {
    general: '帥',
    advisor: '仕',
    elephant: '相',
    horse: '傌',
    chariot: '俥',
    cannon: '炮',
    soldier: '兵',
  },
  White: {
    general: '將',
    advisor: '士',
    elephant: '象',
    horse: '馬',
    chariot: '車',
    cannon: '砲',
    soldier: '卒',
  },
};

/**
 * Grid lines, in a coordinate space where intersection (row, col) sits at (col, row).
 *
 * Built once as a compound path: ten ranks, two full outer files, seven files broken
 * by the river, and the two palaces' diagonals. The 兵/炮 corner brackets a printed
 * board carries are deliberately left out — they are decoration, and drawing them
 * would triple this path for no information.
 */
const GRID_PATH = (() => {
  const parts: string[] = [];
  for (let r = 0; r < XIANGQI_ROWS; r++) parts.push(`M0 ${r}H${XIANGQI_COLS - 1}`);
  parts.push(`M0 0V${XIANGQI_ROWS - 1}`, `M${XIANGQI_COLS - 1} 0V${XIANGQI_ROWS - 1}`);
  for (let c = 1; c < XIANGQI_COLS - 1; c++) parts.push(`M${c} 0V4`, `M${c} 5V${XIANGQI_ROWS - 1}`);
  parts.push('M3 0L5 2', 'M5 0L3 2', 'M3 7L5 9', 'M5 7L3 9');
  return parts.join(' ');
})();

/**
 * 中国象棋 board — 10×9 intersections, two-step piece movement.
 *
 * **Not a parameterisation of `Board`.** `Board` renders "is there a stone in this
 * cell" and moves in one click; this renders "which piece is on this intersection"
 * and moves in two. Folding them together would produce a presentational component
 * with a `gameKey` branch inside it, which is the shape this game exists to avoid.
 *
 * It judges **no** move legality (design D2). It does exactly two things that need
 * no rules: you can only pick up your own piece, and the board is read-only when it
 * is not your turn. Everything else is the server's call — an illegal target comes
 * back as a `HubException` and the container shows a toast. The alternative was a
 * TypeScript port of the rules, i.e. a second source of truth whose divergence would
 * read to the player as a bug and which nothing here would ever detect.
 *
 * Purely presentational: no `GameHubService`, no `GameCatalogService`, no router.
 */
@Component({
  selector: 'app-xiangqi-board',
  standalone: true,
  imports: [TranslocoPipe],
  templateUrl: './xiangqi-board.html',
  styles: [':host { display: block; width: 100%; }'],
  // Escape lives on the host rather than on the board div: keydown bubbles out of
  // whichever intersection has focus, and the host is not an interactive element
  // pretending to be focusable.
  host: { '(keydown.escape)': 'clearSelection()' },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class XiangqiBoard {
  readonly state = input<RoomState | null>(null);
  readonly mySide = input<'black' | 'white' | 'spectator'>('spectator');
  readonly submitting = input<boolean>(false);
  readonly = input<boolean>(false);
  readonly pieceMove = output<PieceMoveEvent>();

  protected readonly gridPath = GRID_PATH;
  protected readonly rowIndices = Array.from({ length: XIANGQI_ROWS }, (_, i) => i);
  protected readonly colIndices = Array.from({ length: XIANGQI_COLS }, (_, i) => i);

  /** The origin the player has picked up, if any. Cleared whenever a ply lands. */
  private readonly selected = signal<BoardPoint | null>(null);
  private previousMoveCount = -1;

  private readonly moves = computed<readonly MoveDto[]>(() => this.state()?.game?.moves ?? []);
  protected readonly position = computed(() => positionAfter(this.moves()));

  protected readonly lastMove = computed<MoveDto | null>(() => {
    const moves = this.moves();
    return moves.length > 0 ? moves[moves.length - 1] : null;
  });

  /** `Stone` value of the seat the viewer occupies; `null` for spectators. */
  private readonly myStone = computed<XiangqiSide | null>(() => {
    const side = this.mySide();
    if (side === 'black') return RED;
    if (side === 'white') return BLACK;
    return null;
  });

  private readonly myTurn = computed<boolean>(() => {
    const mine = this.myStone();
    return mine !== null && this.state()?.game?.currentTurn === mine;
  });

  protected readonly boardDisabled = computed<boolean>(
    () =>
      this.readonly() ||
      this.submitting() ||
      this.mySide() === 'spectator' ||
      this.state()?.status !== 'Playing' ||
      !this.myTurn(),
  );

  constructor() {
    // A landed ply — mine or the opponent's — makes the held origin meaningless.
    // Rejections do NOT land a ply, so a refused move keeps the piece in hand:
    // the player almost always wants a different target, not to hunt for the piece again.
    effect(() => {
      const n = this.moves().length;
      if (this.previousMoveCount !== -1 && n !== this.previousMoveCount) this.selected.set(null);
      this.previousMoveCount = n;
    });
  }

  protected pieceAtPoint(row: number, col: number): XiangqiPiece | null {
    return pieceAt(this.position(), row, col);
  }

  protected glyph(row: number, col: number): string {
    const piece = this.pieceAtPoint(row, col);
    return piece ? GLYPHS[piece.side][piece.type] : '';
  }

  protected isRed(row: number, col: number): boolean {
    return this.pieceAtPoint(row, col)?.side === RED;
  }

  protected isSelected(row: number, col: number): boolean {
    const sel = this.selected();
    return sel !== null && sel.row === row && sel.col === col;
  }

  protected isLastTo(row: number, col: number): boolean {
    const last = this.lastMove();
    return last !== null && last.row === row && last.col === col;
  }

  protected isLastFrom(row: number, col: number): boolean {
    const last = this.lastMove();
    return last != null && last.fromRow === row && last.fromCol === col;
  }

  /** Translation key for the spoken name of whatever is on this intersection. */
  protected pieceNameKey(row: number, col: number): string {
    const piece = this.pieceAtPoint(row, col);
    if (!piece) return 'xiangqi.board.empty';
    return `xiangqi.piece.${piece.side === RED ? 'red' : 'black'}-${piece.type}`;
  }

  /**
   * A point is reachable only when clicking it would do something: it holds one of
   * my pieces (pick up / re-pick), or a piece is already in hand (move there).
   */
  protected pointDisabled(row: number, col: number): boolean {
    if (this.boardDisabled()) return true;
    if (this.selected() !== null) return false;
    return this.pieceAtPoint(row, col)?.side !== this.myStone();
  }

  protected handleClick(row: number, col: number): void {
    if (this.pointDisabled(row, col)) return;

    if (this.isSelected(row, col)) {
      this.selected.set(null);
      return;
    }

    if (this.pieceAtPoint(row, col)?.side === this.myStone()) {
      // Another of my own pieces: re-pick it. Emitting here would be a request the
      // server is certain to refuse, and "capture your own piece" is not a thing.
      this.selected.set({ row, col });
      return;
    }

    const from = this.selected();
    if (from === null) return;
    this.pieceMove.emit({ from, to: { row, col } });
  }

  protected clearSelection(): void {
    this.selected.set(null);
  }
}
