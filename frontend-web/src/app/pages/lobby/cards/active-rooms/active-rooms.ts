import { Dialog } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, inject, Injector, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { RoomSummary } from '../../../../core/api/models/room.model';
import { RoomsApiService } from '../../../../core/api/rooms-api.service';
import { LobbyDataService } from '../../../../core/lobby/lobby-data.service';
import { GameCapabilitiesService } from '../../../../games/game-capabilities.service';
import { GameEmblem } from '../../../../games/emblem/game-emblem';
import { GameCatalogService } from '../../../../games/game-catalog.service';
import type { EmblemShape } from '../../../../games/game-emblem';
import {
  CreateRoomDialog,
  type CreateRoomResult,
} from '../../dialogs/create-room-dialog/create-room-dialog';

@Component({
  selector: 'app-active-rooms-card',
  standalone: true,
  imports: [RouterLink, TranslocoPipe, GameEmblem],
  templateUrl: './active-rooms.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ActiveRoomsCard {
  private readonly data = inject(LobbyDataService);
  private readonly rooms = inject(RoomsApiService);
  private readonly router = inject(Router);
  private readonly dialog = inject(Dialog);
  private readonly injector = inject(Injector);

  protected readonly slice = this.data.rooms;
  protected readonly navigating = signal<string | null>(null);

  private readonly capabilities = inject(GameCapabilitiesService);

  private readonly catalog = inject(GameCatalogService);

  constructor() {
    // 座位总数来自 `GET /api/games`。这一句是必需的:目录服务是静态导入,而能力
    // 服务是一次 HTTP,两者刻意分开(见 GameCapabilitiesService 的文档)。
    this.capabilities.ensureLoaded();
  }

  /**
   * 这一行画哪个纹章。
   *
   * **走 `byRoomKey`,而这里此前是一张自己拼的「伴生键 → 主棋种」表。** 同一条解析规则
   * 有两份实现,迟早会与另一份不一致 —— 而这里不一致的样子是那一行的纹章变成一个
   * **空数组**:不抛、不报、不红,只是不见。现在只有一份,在目录服务上。
   */
  protected emblemOf(gameKey: string): readonly EmblemShape[] {
    return this.catalog.byRoomKey(gameKey)?.emblem ?? [];
  }

  /**
   * 这个房间**一共**有几个座位,座位数还没到达时返回 `null`。
   *
   * **`room.seats` 回答的是「坐上了几个」,不是「一共有几个」** —— 那句区别写在
   * `RoomSummary.seats` 的文档里,而它正是 `fix-lobby-seats` 修完之后 `publish-seat-count`
   * 还得再来一趟的原因:一个等待中的三座位房间,`seats` 里只有两项。
   *
   * 返回 `null` 而不是退化成 `seats.length`:后者会画出一个「满座」的等待房间,
   * 而那正是这一整条线要修掉的症状。
   */
  protected seatCountOf(gameKey: string): number | null {
    return this.capabilities.of(gameKey)?.seatCount ?? null;
  }

  /** 空位的下标,用来在模板里 `@for`。 */
  protected emptySeats(room: RoomSummary): readonly number[] {
    const total = this.seatCountOf(room.gameKey);
    if (total === null) {
      return [];
    }
    const empty = total - room.seats.length;
    return empty > 0 ? Array.from({ length: empty }, (_, i) => i) : [];
  }

  /** 首字 —— 圆片里放得下的全部内容。全名走 aria-label 与 title。 */
  protected initial(username: string): string {
    return [...username][0] ?? '?';
  }

  protected refresh(): void {
    this.slice.refresh();
  }

  protected openCreateDialog(): void {
    const ref = this.dialog.open<CreateRoomResult>(CreateRoomDialog, {
      ariaLabel: 'Create room',
      // The dialog reads LOBBY_GAME_KEY, which lives on this page's injector.
      // CDK would otherwise construct it against the root injector, where the
      // token does not exist.
      injector: this.injector,
    });
    ref.closed.subscribe((result) => {
      // Only this page's room list needs nudging. "My active rooms" moved to
      // `/home`, which is not mounted right now and fetches on its own mount —
      // there is nothing stale to correct.
      if (result) this.data.rooms.refresh();
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
