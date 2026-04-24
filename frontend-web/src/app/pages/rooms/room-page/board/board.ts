import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  output,
} from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { MoveDto, RoomState, Stone } from '../../../../core/api/models/room.model';

const BOARD_SIZE = 15;

interface CellCoord {
  readonly row: number;
  readonly col: number;
}

@Component({
  selector: 'app-board',
  standalone: true,
  imports: [TranslocoPipe],
  templateUrl: './board.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Board {
  readonly state = input<RoomState | null>(null);
  readonly mySide = input<'black' | 'white' | 'spectator'>('spectator');
  readonly submitting = input<boolean>(false);
  readonly = input<boolean>(false);
  readonly cellClick = output<CellCoord>();

  protected readonly rows = Array.from({ length: BOARD_SIZE }, (_, i) => i);
  protected readonly cols = Array.from({ length: BOARD_SIZE }, (_, i) => i);

  private readonly grid = computed<Stone[][]>(() => {
    const board: Stone[][] = Array.from({ length: BOARD_SIZE }, () =>
      Array.from<Stone>({ length: BOARD_SIZE }).fill('Empty'),
    );
    const moves = this.state()?.game?.moves ?? [];
    for (const move of moves) {
      if (move.row >= 0 && move.row < BOARD_SIZE && move.col >= 0 && move.col < BOARD_SIZE) {
        board[move.row][move.col] = move.stone;
      }
    }
    return board;
  });

  protected readonly lastMove = computed<MoveDto | null>(() => {
    const moves = this.state()?.game?.moves ?? [];
    return moves.length > 0 ? moves[moves.length - 1] : null;
  });

  private readonly myTurn = computed<boolean>(() => {
    const side = this.mySide();
    const turn = this.state()?.game?.currentTurn;
    return (side === 'black' && turn === 'Black') || (side === 'white' && turn === 'White');
  });

  protected stoneAt(row: number, col: number): Stone {
    return this.grid()[row][col];
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
