import { DialogRef } from '@angular/cdk/dialog';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslocoPipe } from '@jsverse/transloco';
import type {
  BotDifficulty,
  BotSide,
  RoomState,
} from '../../../../core/api/models/room.model';
import { RoomsApiService } from '../../../../core/api/rooms-api.service';
import { isProblemDetails } from '../../../../core/auth/problem-details';
import { mapProblemDetailsToForm } from '../../../auth/shared/problem-details.mapper';

export type CreateAiRoomResult = RoomState | undefined;

const NAME_PATTERN = /\S/;

@Component({
  selector: 'app-create-ai-room-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, TranslocoPipe],
  templateUrl: './create-ai-room-dialog.html',
  styles: [':host { display: block; }'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CreateAiRoomDialog {
  private readonly rooms = inject(RoomsApiService);
  private readonly dialogRef = inject<DialogRef<CreateAiRoomResult>>(DialogRef);
  private readonly fb = inject(FormBuilder);

  protected readonly submitting = signal(false);
  protected readonly bannerKey = signal<string | null>(null);

  protected readonly difficulties: readonly BotDifficulty[] = ['Easy', 'Medium', 'Hard'];
  protected readonly sides: readonly BotSide[] = ['Black', 'White'];

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
    difficulty: this.fb.nonNullable.control<BotDifficulty>('Medium', Validators.required),
    humanSide: this.fb.nonNullable.control<BotSide>('Black', Validators.required),
  });

  protected difficultyKey(d: BotDifficulty): string {
    return `lobby.ai-game.difficulty-${d.toLowerCase()}`;
  }

  protected sideKey(s: BotSide): string {
    return `lobby.ai-game.side-${s.toLowerCase()}`;
  }

  protected pickDifficulty(d: BotDifficulty): void {
    this.form.controls.difficulty.setValue(d);
  }

  protected pickSide(s: BotSide): void {
    this.form.controls.humanSide.setValue(s);
  }

  protected submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    this.bannerKey.set(null);
    const { name, difficulty, humanSide } = this.form.getRawValue();
    this.rooms.createAiRoom(name.trim(), difficulty, humanSide).subscribe({
      next: (state) => {
        this.submitting.set(false);
        this.dialogRef.close(state);
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
        if (!matched) this.bannerKey.set('lobby.ai-game.errors.generic');
        return;
      }
      if (err.status === 0) {
        this.bannerKey.set('lobby.ai-game.errors.network');
        return;
      }
    }
    this.bannerKey.set('lobby.ai-game.errors.generic');
  }
}
