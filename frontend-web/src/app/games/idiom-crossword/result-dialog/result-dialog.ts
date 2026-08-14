import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { CrosswordSolvedWord } from '../../../core/api/models/puzzle.model';

/** What the dialog needs to show. All of it comes from server responses. */
export interface CrosswordResultData {
  readonly stars: number;
  readonly durationMs: number | null;
  readonly mistakes: number;
  readonly hintsUsed: number;
  readonly newBest: boolean;
  readonly words: readonly CrosswordSolvedWord[];
  readonly isLastLevel: boolean;
}

/** What the player chose to do next. */
export type CrosswordResultAction = 'replay' | 'next' | 'levels';

/**
 * Completion dialog. CDK, not a hand-rolled overlay — focus trap, ESC, backdrop
 * and ARIA are all required and all free here. The prototype's `#overlay` div
 * had none of them.
 */
@Component({
  selector: 'app-crossword-result-dialog',
  standalone: true,
  imports: [TranslocoPipe],
  templateUrl: './result-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResultDialog {
  protected readonly data = inject<CrosswordResultData>(DIALOG_DATA);
  private readonly ref = inject<DialogRef<CrosswordResultAction>>(DialogRef);

  protected readonly starSlots = [0, 1, 2];

  protected duration(): string | null {
    if (this.data.durationMs === null) return null;
    const total = Math.round(this.data.durationMs / 1000);
    return `${Math.floor(total / 60)}:${(total % 60).toString().padStart(2, '0')}`;
  }

  protected close(action: CrosswordResultAction): void {
    this.ref.close(action);
  }
}
