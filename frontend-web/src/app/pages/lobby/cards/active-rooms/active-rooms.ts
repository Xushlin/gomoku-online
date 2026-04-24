import { Dialog } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { RoomSummary } from '../../../../core/api/models/room.model';
import { RoomsApiService } from '../../../../core/api/rooms-api.service';
import { LobbyDataService } from '../../../../core/lobby/lobby-data.service';
import {
  CreateRoomDialog,
  type CreateRoomResult,
} from '../../dialogs/create-room-dialog/create-room-dialog';

@Component({
  selector: 'app-active-rooms-card',
  standalone: true,
  imports: [TranslocoPipe],
  templateUrl: './active-rooms.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ActiveRoomsCard {
  private readonly data = inject(LobbyDataService);
  private readonly rooms = inject(RoomsApiService);
  private readonly router = inject(Router);
  private readonly dialog = inject(Dialog);

  protected readonly slice = this.data.rooms;
  protected readonly navigating = signal<string | null>(null);

  protected refresh(): void {
    this.slice.refresh();
  }

  protected openCreateDialog(): void {
    const ref = this.dialog.open<CreateRoomResult>(CreateRoomDialog, {
      ariaLabel: 'Create room',
    });
    ref.closed.subscribe((result) => {
      if (result) {
        this.data.rooms.refresh();
        this.data.myRooms.refresh();
      }
    });
  }

  protected join(room: RoomSummary): void {
    if (this.navigating()) return;
    this.navigating.set(room.id);
    this.rooms.join(room.id).subscribe({
      next: () => {
        void this.router.navigate(['/rooms', room.id]);
      },
      error: (err: unknown) => {
        this.navigating.set(null);
        // 409 AlreadyInRoom is still success for navigation purposes.
        if (
          typeof err === 'object' &&
          err !== null &&
          'status' in err &&
          (err as { status: number }).status === 409
        ) {
          void this.router.navigate(['/rooms', room.id]);
        }
      },
    });
  }

  protected watch(room: RoomSummary): void {
    if (this.navigating()) return;
    this.navigating.set(room.id);
    this.rooms.spectate(room.id).subscribe({
      next: () => void this.router.navigate(['/rooms', room.id]),
      error: () => {
        this.navigating.set(null);
      },
    });
  }

  protected seatLabel(seat: RoomSummary['black']): string {
    return seat?.username ?? '';
  }

  protected trackRoom(_index: number, room: RoomSummary): string {
    return room.id;
  }
}
