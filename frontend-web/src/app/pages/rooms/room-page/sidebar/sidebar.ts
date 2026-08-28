import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { RoomState } from '../../../../core/api/models/room.model';
import { GameCapabilitiesService } from '../../../../games/game-capabilities.service';
import { GameCatalogService } from '../../../../games/game-catalog.service';
import { seatNaming } from '../../../../games/seat-labels';

@Component({
  selector: 'app-room-sidebar',
  standalone: true,
  imports: [RouterLink, TranslocoPipe],
  templateUrl: './sidebar.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RoomSidebar {
  private readonly capabilities = inject(GameCapabilitiesService);
  private readonly catalog = inject(GameCatalogService);

  readonly state = input<RoomState | null>(null);

  /**
   * 这个房间**有几个座位** —— 一个结构性事实,由 `GET /api/games` 给。
   *
   * **它此前读的是 `seats.length`,而那是一条真缺陷。** `seats` 只含**在座的**座位,
   * 所以 `seats.length` 是「坐了几个人」,不是「有几个座位」。两者在房间坐满**之前**
   * 不相等,于是一个等待中的三座位房间此前说出了「黑方 / 白方」。异步的账已经付过了:
   * `RoomPage.loading()` 里本来就含 `!capabilities.loaded()`。
   *
   * **它只回答「画几行」。** 「这些席位叫什么」是另一个问题,由 manifest 回答 ——
   * 而那两个问题此前由同一个数字回答,那正是象棋房把红方叫成黑方的原因。
   */
  protected readonly seatCount = computed(
    () => this.capabilities.of(this.state()?.gameKey ?? '')?.seatCount ?? null,
  );

  /**
   * 全部座位 —— 含**空的**,每个带自己的称呼。
   *
   * **这里只有一个循环了。** 此前是两支:座位数大于二走编号,否则走「黑方 / 白方」。
   * 那一支连带 `room.black` / `room.white` 两个派生读法一起删掉了 —— 三座位棋种就是
   * 「没声明席位名」的一种,不需要单独一支。**删得掉是这个判据换对了的证据。**
   *
   * 清单用 `byRoomKey` 取,不是 `byKey`:象棋残局是一个伴生键,它没有自己的清单,
   * 但它要用象棋的席位名。
   */
  protected readonly allSeats = computed(() => {
    const total = this.seatCount();
    if (total === null) return [];
    const manifest = this.catalog.byRoomKey(this.state()?.gameKey ?? '');
    const taken = new Map((this.state()?.seats ?? []).map((s) => [s.index, s.player]));
    return Array.from({ length: total }, (_, index) => ({
      index,
      player: taken.get(index) ?? null,
      naming: seatNaming(manifest, index, total),
    }));
  });
}
