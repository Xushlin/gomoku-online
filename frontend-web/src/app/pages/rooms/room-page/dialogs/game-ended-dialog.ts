import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { GameEndReason, GameResult } from '../../../../core/api/models/room.model';
import { myOutcome } from '../outcome';

export interface GameEndedDialogData {
  readonly result: GameResult;
  readonly winnerUserId: string | null;
  readonly endReason: GameEndReason;
  /** The signed-in user's id — the dialog compares it with `winnerUserId`. */
  readonly myUserId: string | null;
  readonly roomId: string;
}

export type GameEndedDialogResult = 'home' | 'stay' | 'replay';

@Component({
  selector: 'app-game-ended-dialog',
  standalone: true,
  imports: [TranslocoPipe],
  templateUrl: './game-ended-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GameEndedDialog {
  protected readonly data = inject<GameEndedDialogData>(DIALOG_DATA);
  private readonly dialogRef = inject<DialogRef<GameEndedDialogResult>>(DialogRef);

  protected readonly titleKey = computed<string>(() => {
    // One judgement, shared with the win/lose sound — see `myOutcome`.
    switch (myOutcome(this.data, this.data.myUserId)) {
      case 'draw':
        return 'game.ended.title-draw';
      case 'win':
        return 'game.ended.title-win';
      case 'lose':
        return 'game.ended.title-lose';
    }
  });

  protected readonly reasonKey = computed<string>(() => {
    switch (this.data.endReason) {
      case 'Decided':
        return 'game.ended.reason-decided';
      case 'Resigned':
        return 'game.ended.reason-resigned';
      case 'TurnTimeout':
        return 'game.ended.reason-timeout';
    }
  });

  protected backToLobby(): void {
    this.dialogRef.close('home');
  }

  protected viewReplay(): void {
    this.dialogRef.close('replay');
  }

  protected stay(): void {
    this.dialogRef.close('stay');
  }
}
