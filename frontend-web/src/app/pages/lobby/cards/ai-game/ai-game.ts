import { Dialog } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { RoomState } from '../../../../core/api/models/room.model';
import {
  CreateAiRoomDialog,
  type CreateAiRoomResult,
} from '../../dialogs/create-ai-room-dialog/create-ai-room-dialog';

@Component({
  selector: 'app-ai-game-card',
  standalone: true,
  imports: [TranslocoPipe],
  templateUrl: './ai-game.html',
  styles: [':host { display: block; width: 100%; }'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AiGameCard {
  private readonly dialog = inject(Dialog);
  private readonly router = inject(Router);

  protected open(): void {
    const ref = this.dialog.open<CreateAiRoomResult>(CreateAiRoomDialog, {
      ariaLabel: 'New AI game',
    });
    ref.closed.subscribe((result: RoomState | undefined) => {
      if (result) void this.router.navigateByUrl(`/rooms/${result.id}`);
    });
  }
}
