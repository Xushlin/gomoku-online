import { DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

export type ResignConfirmResult = boolean;

@Component({
  selector: 'app-resign-confirm-dialog',
  standalone: true,
  imports: [TranslocoPipe],
  templateUrl: './resign-confirm-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResignConfirmDialog {
  private readonly dialogRef = inject<DialogRef<ResignConfirmResult>>(DialogRef);

  protected confirm(): void {
    this.dialogRef.close(true);
  }

  protected cancel(): void {
    this.dialogRef.close(false);
  }
}
