import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { RoomSummary } from '../../../../core/api/models/room.model';
import { AuthService } from '../../../../core/auth/auth.service';
import { HomeDataService } from '../../../../core/lobby/home-data.service';


@Component({
  selector: 'app-my-active-rooms-card',
  standalone: true,
  imports: [TranslocoPipe],
  templateUrl: './my-active-rooms.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MyActiveRoomsCard {
  private readonly data = inject(HomeDataService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly slice = this.data.myRooms;

  protected refresh(): void {
    this.slice.refresh();
  }

  protected resume(room: RoomSummary): void {
    void this.router.navigate(['/rooms', room.id]);
  }

  /**
   * 「我在这个房间里是什么身份」。
   *
   * **它此前只比 `black` / `white`,而那是 0 号与 1 号座位的派生读法** —— 于是三座位房间里
   * 2 号座位上的人被标成「你是观战」,**在他自己的对局里**。这与
   * `fix-three-seat-membership` 在服务端修的是同一句话,只是那边的后果重得多
   * (他真的拿到了整个围观频道)。
   *
   * **「不在座位上」与「在第三个座位上」MUST NOT 得到同一个答案。**
   *
   * 座位号不进文案:大厅要回答的是「我在里面吗」,而「我坐第几号」是房间页的事 ——
   * 而且 `board-seats.ts` 自己写着那套「座位号 → 颜色」的读法只有棋盘家族可以调。
   */
  protected sideKey(room: RoomSummary): string {
    const myId = this.auth.user()?.id;
    const seated = myId !== undefined && room.seats.some((s) => s.player.id === myId);
    return seated ? 'lobby.my-rooms.you-are-seated' : 'lobby.my-rooms.you-are-spectator';
  }

  protected trackRoom(_index: number, room: RoomSummary): string {
    return room.id;
  }
}
