import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

import type { RoomState } from '../../../core/api/models/room.model';
import { encodeHand, suitSymbol, type DoudizhuCard } from '../cards';
import { parseSeatView, type DoudizhuSeatView } from '../seat-view';

/** 一次动作 —— 直接就是 `Move.Text` 的内容(`bid:N` / `pass` / `play:<cards>`)。 */
export type DoudizhuAction = string;

/**
 * 斗地主的牌桌。
 *
 * 对战家族的**第四种**棋盘形状,而它是第一个不能用"黑 / 白"描述自己的:
 * 三个座位,而 `mySide` 那套 `'black' | 'white' | 'spectator'` 对第三个人无话可说。
 * 所以这个组件收的是 **`mySeat: number | null`** —— `null` 表示不占座位(围观者)。
 * 那不是与另三个棋盘的随意分歧:线上契约自 `generalize-match-contract` 起就说座位,
 * 而颜色只是棋盘家族在显示层的读法。
 *
 * **它不判任何合法性。** `add-web-klotski` 定下的尺子是:不问"客户端该不该知道规则",
 * 而问"知道了会不会造出一个能与服务端分叉的第二真源"。斗地主在这把尺子下**整个落在
 * 不该知道的一侧**:
 *
 *   - "这几张是什么牌型"要一整套牌型识别(单/对/三带/顺子/连对/飞机/四带二/炸弹),
 *   - "压不压得住"还要在此之上比大小,
 *   - 而两者都已经在服务端,并且是这一局唯一的判据。
 *
 * 在客户端再写一遍,就是一份会悄悄分叉的第二真源 —— 分叉在玩家眼里是"这游戏有 bug"。
 * 所以这里只做**不需要规则**的事:能选中自己的牌、非自己回合只读、出牌前至少选一张。
 * 代价是"这手压不住"要走一趟服务端才知道,而那一趟带回来的是有错误码的具体理由。
 *
 * 与另三个棋盘一样,它把动作**发出去**(`action` output),不自己调 hub。
 */
@Component({
  selector: 'app-card-table',
  standalone: true,
  imports: [TranslocoPipe],
  templateUrl: './card-table.html',
  styles: [':host { display: block; width: 100%; }'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CardTable {
  readonly state = input<RoomState | null>(null);

  /** 看这一桌的人坐第几号;`null` 表示围观者 / 尚未入座。 */
  readonly mySeat = input<number | null>(null);
  readonly submitting = input<boolean>(false);
  readonly readonly = input<boolean>(false);

  /** 一次动作的文本载荷。父组件转给 hub。 */
  readonly action = output<DoudizhuAction>();

  /** 叫分的四个选项。3 分是上限 —— 与服务端 `DoudizhuScoring.MaxBaseScore` 一致。 */
  protected readonly bids = [0, 1, 2, 3] as const;

  private readonly selected = signal<ReadonlySet<string>>(new Set());

  protected readonly view = computed<DoudizhuSeatView | null>(() =>
    parseSeatView(this.state()?.game?.seatView),
  );

  protected readonly isSpectator = computed(() => this.mySeat() === null);

  protected readonly myTurn = computed<boolean>(() => {
    const seat = this.mySeat();
    return seat !== null && this.state()?.game?.currentSeat === seat;
  });

  /** 动作被禁用的统一判据 —— 与另三个棋盘同一个形状。 */
  protected readonly actionsDisabled = computed<boolean>(
    () =>
      this.readonly() ||
      this.submitting() ||
      this.isSpectator() ||
      this.state()?.status !== 'Playing' ||
      !this.myTurn(),
  );

  protected readonly hand = computed<readonly DoudizhuCard[]>(() => this.view()?.myHand ?? []);

  /** 另外两个座位 —— 按座位号顺时针,从我的下一个开始。 */
  protected readonly others = computed<readonly { seat: number; count: number }[]>(() => {
    const view = this.view();
    if (!view) return [];
    const total = view.handCounts.length;
    const me = this.mySeat();
    const order = Array.from({ length: total }, (_, i) => i)
      .filter((seat) => seat !== me);
    if (me !== null) {
      // 从我的下一个座位开始,这样屏幕上的左右与桌上的顺序一致。
      order.sort((a, b) => ((a - me + total) % total) - ((b - me + total) % total));
    }
    return order.map((seat) => ({ seat, count: view.handCounts[seat] ?? 0 }));
  });

  protected readonly selectedCount = computed(() => this.selected().size);

  protected readonly canPlay = computed(() => !this.actionsDisabled() && this.selectedCount() > 0);

  /**
   * 首出时不能过牌 —— 这一条**不需要规则也判得出**:桌上没牌就是没牌。
   * 它是"不需要规则"那一侧的边界:再往前一步(这手压不压得住)就要牌型识别了。
   */
  protected readonly canPass = computed(
    () => !this.actionsDisabled() && this.view()?.tableCards != null,
  );

  protected isSelected(card: DoudizhuCard): boolean {
    return this.selected().has(card.code);
  }

  protected toggle(card: DoudizhuCard): void {
    if (this.actionsDisabled()) return;
    const next = new Set(this.selected());
    if (!next.delete(card.code)) next.add(card.code);
    this.selected.set(next);
  }

  protected symbol(card: DoudizhuCard): string {
    return suitSymbol(card.suit);
  }

  protected bid(points: number): void {
    if (this.actionsDisabled()) return;
    this.action.emit(`bid:${points}`);
  }

  protected pass(): void {
    if (!this.canPass()) return;
    this.action.emit('pass');
  }

  protected play(): void {
    if (!this.canPlay()) return;
    const chosen = this.hand().filter((card) => this.selected().has(card.code));
    this.action.emit(`play:${encodeHand(chosen)}`);
    // 选中态就地清掉,不等服务端回话:被拒绝时玩家要重选,而留着一手被拒的牌
    // 会让"再点一次提交"重复发同一个被拒的动作。
    this.selected.set(new Set());
  }
}
