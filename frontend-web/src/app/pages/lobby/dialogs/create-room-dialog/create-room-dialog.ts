import { DialogRef } from '@angular/cdk/dialog';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslocoPipe } from '@jsverse/transloco';
import type { RoomSummary } from '../../../../core/api/models/room.model';
import { RoomsApiService } from '../../../../core/api/rooms-api.service';
import { isProblemDetails } from '../../../../core/auth/problem-details';
import { mapProblemDetailsToForm } from '../../../auth/shared/problem-details.mapper';
import { LOBBY_GAME_KEY } from '../../../../core/lobby/lobby-game-key';

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
  private readonly gameKey = inject(LOBBY_GAME_KEY);
  private readonly dialogRef = inject<DialogRef<CreateRoomResult>>(DialogRef);
  private readonly fb = inject(FormBuilder);

  /**
   * 这个大厅的棋种名的翻译键 —— placeholder 用它。
   *
   * **它此前是写死的「我的五子棋房」。** 大厅泛化之后 `/g/:gameKey/lobby` 是**一个棋种**的
   * 大厅,于是在挖坑的大厅里那句话点名了另一个棋种。没有任何测试断言过那句文案 ——
   * 而那正是它活下来的原因。
   *
   * 键从 `LOBBY_GAME_KEY` 拼出来,而那个 token 本来就注在这里(建房要它)。
   */
  protected readonly gameTitleKey = `games.${this.gameKey}.title`;

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
    this.rooms.create(name.trim(), this.gameKey).subscribe({
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
