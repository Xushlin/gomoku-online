import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { GameEndReason, GameResult } from '../../../../core/api/models/room.model';

export interface GameEndedDialogData {
  readonly result: GameResult;
  readonly winnerUserId: string | null;
  readonly endReason: GameEndReason;
  readonly mySide: 'black' | 'white' | 'spectator';
}

export type GameEndedDialogResult = 'home' | 'stay';

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
    const { result, mySide } = this.data;
    if (result === 'Draw') return 'game.ended.title-draw';
    if (result === 'BlackWin' && mySide === 'black') return 'game.ended.title-win';
    if (result === 'WhiteWin' && mySide === 'white') return 'game.ended.title-win';
    return 'game.ended.title-lose';
  });

  protected readonly reasonKey = computed<string>(() => {
    switch (this.data.endReason) {
      case 'Connected5':
        return 'game.ended.reason-connected-5';
      case 'Resigned':
        return 'game.ended.reason-resigned';
      case 'TurnTimeout':
        return 'game.ended.reason-timeout';
    }
  });

  protected backToLobby(): void {
    this.dialogRef.close('home');
  }

  protected stay(): void {
    this.dialogRef.close('stay');
  }
}
