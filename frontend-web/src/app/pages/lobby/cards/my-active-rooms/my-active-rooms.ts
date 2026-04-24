import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { RoomSummary } from '../../../../core/api/models/room.model';
import { AuthService } from '../../../../core/auth/auth.service';
import { LobbyDataService } from '../../../../core/lobby/lobby-data.service';

type Side = 'black' | 'white' | 'spectator';

@Component({
  selector: 'app-my-active-rooms-card',
  standalone: true,
  imports: [TranslocoPipe],
  templateUrl: './my-active-rooms.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MyActiveRoomsCard {
  private readonly data = inject(LobbyDataService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly slice = this.data.myRooms;

  protected refresh(): void {
    this.slice.refresh();
  }

  protected resume(room: RoomSummary): void {
    void this.router.navigate(['/rooms', room.id]);
  }

  protected sideKey(room: RoomSummary): string {
    const myId = this.auth.user()?.id;
    const side: Side =
      myId && room.black?.id === myId
        ? 'black'
        : myId && room.white?.id === myId
          ? 'white'
          : 'spectator';
    return `lobby.my-rooms.you-are-${side}`;
  }

  protected trackRoom(_index: number, room: RoomSummary): string {
    return room.id;
  }
}
