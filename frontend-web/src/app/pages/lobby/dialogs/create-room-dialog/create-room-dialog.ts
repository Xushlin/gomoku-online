import { DialogRef } from '@angular/cdk/dialog';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslocoPipe } from '@jsverse/transloco';
import type { RoomSummary } from '../../../../core/api/models/room.model';
import { RoomsApiService } from '../../../../core/api/rooms-api.service';
import { isProblemDetails } from '../../../../core/auth/problem-details';
import { mapProblemDetailsToForm } from '../../../auth/shared/problem-details.mapper';

export type CreateRoomResult = RoomSummary | undefined;

const NAME_PATTERN = /\S/;

@Component({
  selector: 'app-create-room-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, TranslocoPipe],
  templateUrl: './create-room-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CreateRoomDialog {
  private readonly rooms = inject(RoomsApiService);
  private readonly dialogRef = inject<DialogRef<CreateRoomResult>>(DialogRef);
  private readonly fb = inject(FormBuilder);

  protected readonly submitting = signal(false);
  protected readonly bannerKey = signal<string | null>(null);

  protected readonly form = this.fb.nonNullable.group({
    name: [
      '',
      [
        Validators.required,
        Validators.minLength(3),
        Validators.maxLength(50),
        Validators.pattern(NAME_PATTERN),
      ],
    ],
  });

  protected submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    this.bannerKey.set(null);
    const { name } = this.form.getRawValue();
    this.rooms.create(name.trim()).subscribe({
      next: (room) => {
        this.submitting.set(false);
        this.dialogRef.close(room);
      },
      error: (err: unknown) => {
        this.submitting.set(false);
        this.handleError(err);
      },
    });
  }

  protected cancel(): void {
    this.dialogRef.close(undefined);
  }

  private handleError(err: unknown): void {
    if (err instanceof HttpErrorResponse) {
      if (err.status === 400 && isProblemDetails(err.error)) {
        const matched = mapProblemDetailsToForm(this.form, err.error);
        if (!matched) this.bannerKey.set('lobby.create-room.errors.generic');
        return;
      }
      if (err.status === 0) {
        this.bannerKey.set('lobby.create-room.errors.network');
        return;
      }
    }
    this.bannerKey.set('lobby.create-room.errors.generic');
  }
}
