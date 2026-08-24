import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { RoomState } from '../../../../core/api/models/room.model';
import { GameCapabilitiesService } from '../../../../games/game-capabilities.service';

@Component({
  selector: 'app-room-sidebar',
  standalone: true,
  imports: [RouterLink, TranslocoPipe],
  templateUrl: './sidebar.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RoomSidebar {
  private readonly capabilities = inject(GameCapabilitiesService);

  readonly state = input<RoomState | null>(null);
  /**
   * 座位多于两个 —— 那样"黑方 / 白方"就说不通了,改说座位号。
   *
   * **它此前读的是 `seats.length`,而那是一条真缺陷。** 当时的注释写着「座位表就在这份
   * 快照里,不必去问注册表要 `seatCount`」—— 那句话回答的是**另一个问题**:
   * `seats` 只含**在座的**座位,所以 `seats.length` 是「坐了几个人」,不是「有几个座位」。
   * 两者在房间坐满**之前**不相等,于是一个等待中的三座位房间说出了「黑方 / 白方」。
   * 在浏览器里量到的:一个两人在座的斗地主房间,侧栏原文是 `Black: … White: …`。
   *
   * 现在读 `GET /api/games` 给的 `seatCount` —— 一个结构性事实。异步的账**已经付过了**:
   * `RoomPage.loading()` 里本来就含 `!capabilities.loaded()`,所以描述符到达之前整页是
   * 骨架屏,这里不会拿一个未知的座位数去渲染。
   */
  protected readonly seatCount = computed(
    () => this.capabilities.of(this.state()?.gameKey ?? '')?.seatCount ?? null,
  );

  protected readonly moreThanTwoSeats = computed(() => (this.seatCount() ?? 0) > 2);

  /**
   * 全部座位 —— 含**空的**。座位数已知之后才画得出来:在它之前,泛化那一支只画得出在座的人,
   * 于是一个还差一个人的三座位房间看不出自己还差一个。
   */
  protected readonly allSeats = computed(() => {
    const total = this.seatCount();
    if (total === null) return [];
    const taken = new Map((this.state()?.seats ?? []).map((s) => [s.index, s.player]));
    return Array.from({ length: total }, (_, index) => ({
      index,
      player: taken.get(index) ?? null,
    }));
  });

}
