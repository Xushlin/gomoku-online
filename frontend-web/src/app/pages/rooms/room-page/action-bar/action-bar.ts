import { Dialog } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import type { RoomState } from '../../../../core/api/models/room.model';
import { FIRST_SEAT } from '../../../../games/board-seats';
import { GameCapabilitiesService } from '../../../../games/game-capabilities.service';
import {
  ResignConfirmDialog,
  type ResignConfirmResult,
} from '../dialogs/resign-confirm-dialog';

/**
 * 棋盘底下的操作条:**现在怎么样,以及我能做什么。**
 *
 * 这三样(回合指示、倒计时、玩家按钮)从侧栏搬过来,而搬的理由是量出来的。375 px 宽的
 * 房间页,棋盘 311×311 顶在 y=100。而在侧栏里,倒计时落在 **y=638**、三个按钮落在
 * **675 / 713 / 751**;一台 375×812 的手机减掉浏览器自己的界面只剩约 **700 px**,所以
 * **「认输」和「离开」在屏幕外**,倒计时贴在最下沿 —— **要认输得先滚过整块棋盘。**
 * iPhone SE 那一档可用高度约 550,四样全部在屏幕外。
 *
 * 放到棋盘下沿之后:操作条顶在 **427**,按钮在 **488**,最下沿 **532**。
 *
 * **所以位置的判据是 y 坐标,不是 `position` 属性。** 已经在第一屏了,所以不吸底:
 * 吸底要付 `env(safe-area-inset-bottom)`、要盖住内容,而斗地主 / 挖坑的牌桌**自己有一排
 * 出牌按钮**,两条操作条上下叠着就得让人想一下哪个是出牌。
 *
 * 牌桌那一排不搬:它贴着你的手牌,而选牌状态就在那里。这一条拿的是**房间级**动作。
 *
 * 按钮最小 44 px 高,而浏览器里量到的就是 44。搬之前是 **30 px** —— WCAG 2.2 SC 2.5.8
 * (AA)的底线是 24×24,所以旧尺寸**合规**;44 是 SC 2.5.5(AAA)与各家移动端指南的数,
 * 而这是一个用手指点的页面。
 */
@Component({
  selector: 'app-room-action-bar',
  standalone: true,
  imports: [TranslocoPipe],
  templateUrl: './action-bar.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RoomActionBar {
  /** 模板只能读组件成员,所以把这个显示层常量挂上来。 */
  protected readonly FIRST_SEAT = FIRST_SEAT;

  private readonly dialog = inject(Dialog);
  private readonly capabilities = inject(GameCapabilitiesService);

  readonly state = input<RoomState | null>(null);
  /**
   * 看这个房间的人坐第几号;`null` 表示不占座位(围观者 / 尚未入座)。
   *
   * **判据是座位号而不是颜色** —— 2 号座位上的人既不是 black 也不是 white,而按颜色判
   * 会把他当成围观者、于是辞局与离开都不给他。那是 `add-web-doudizhu` 把输入从 `mySide` 改成 `mySeat` 时修过的
   * 缺陷,跟着按钮一起搬过来。
   */
  readonly mySeat = input<number | null>(null);
  readonly turnRemainingMs = input<number>(0);
  readonly canUrge = input<boolean>(false);

  readonly resign = output<void>();
  readonly leave = output<void>();
  readonly urge = output<void>();

  protected readonly isPlayer = computed(() => this.mySeat() !== null);

  /** 对局正在进行 —— 「轮到谁 / 还剩多少」只有这时候才有意义。 */
  protected readonly playing = computed(() => {
    const room = this.state();
    return room?.status === 'Playing' && room.game !== null;
  });

  /**
   * 座位多于两个 —— 那样「黑方 / 白方」就说不通了,改说座位号。
   *
   * 判据是描述符的 `seatCount`,**不是 `state.seats.length`**:后者是「坐了几个人」,
   * 于是一个等待中的三座位房间会被当成两座位房间。侧栏为同一件事读同一个来源 ——
   * 两处都从 `GameCapabilitiesService` 读,所以它们不可能各自说出不同的数。
   */
  protected readonly moreThanTwoSeats = computed(
    () => (this.capabilities.of(this.state()?.gameKey ?? '')?.seatCount ?? 0) > 2,
  );

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
