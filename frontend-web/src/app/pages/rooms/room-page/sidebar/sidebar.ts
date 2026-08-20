import { Dialog } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { RoomState } from '../../../../core/api/models/room.model';
import { FIRST_SEAT } from '../../../../games/board-seats';
import { GameCapabilitiesService } from '../../../../games/game-capabilities.service';
import {
  ResignConfirmDialog,
  type ResignConfirmResult,
} from '../dialogs/resign-confirm-dialog';

@Component({
  selector: 'app-room-sidebar',
  standalone: true,
  imports: [RouterLink, TranslocoPipe],
  templateUrl: './sidebar.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RoomSidebar {
  /** 模板只能读组件成员,所以把这个显示层常量挂上来。 */
  protected readonly FIRST_SEAT = FIRST_SEAT;

  private readonly dialog = inject(Dialog);
  private readonly capabilities = inject(GameCapabilitiesService);

  readonly state = input<RoomState | null>(null);
  /**
   * 看这个房间的人坐第几号;`null` 表示不占座位(围观者 / 尚未入座)。
   *
   * **它此前是 `mySide: 'black' | 'white' | 'spectator'`,而那对三座位房间是错的** ——
   * 2 号座位上的人既不是 black 也不是 white,于是被当成围观者:辞局与离开按钮都不给他。
   * 座位号是线上契约自 `generalize-match-contract` 起就说的东西,而颜色只是棋盘家族在
   * 显示层的读法。
   */
  readonly mySeat = input<number | null>(null);
  readonly turnRemainingMs = input<number>(0);
  readonly canUrge = input<boolean>(false);

  readonly resign = output<void>();
  readonly leave = output<void>();
  readonly urge = output<void>();

  protected readonly isPlayer = computed(() => this.mySeat() !== null);

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

  protected readonly myTurn = computed(() => {
    const seat = this.mySeat();
    return seat !== null && this.state()?.game?.currentSeat === seat;
  });

  protected readonly countdownText = computed(() => {
    const ms = this.turnRemainingMs();
    const total = Math.max(0, Math.ceil(ms / 1000));
    const mm = Math.floor(total / 60);
    const ss = total % 60;
    return `${mm}:${ss.toString().padStart(2, '0')}`;
  });

  protected readonly countdownDanger = computed(() => this.turnRemainingMs() <= 10_000);

  protected openResignConfirm(): void {
    const ref = this.dialog.open<ResignConfirmResult>(ResignConfirmDialog, {
      ariaLabel: 'Resign confirmation',
    });
    ref.closed.subscribe((confirmed) => {
      if (confirmed === true) this.resign.emit();
    });
  }

  protected emitLeave(): void {
    this.leave.emit();
  }

  protected emitUrge(): void {
    this.urge.emit();
  }
}
