import { Dialog } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { RoomState } from '../../../../core/api/models/room.model';
import { FIRST_SEAT } from '../../../../games/board-seats';
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
