import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

import type { RoomState } from '../../../core/api/models/room.model';
import { pipPath } from '../card-art';
import { encodeHand, suitSymbol, type DoudizhuCard } from '../cards';
import { parseSeatView, type DoudizhuSeatView } from '../seat-view';
import { relativeSeat, seatInitial, seatRing, type SeatDirection } from '../table-layout';
import { currentTrick, type TrickAction } from '../trick';

/** 一次动作 —— 直接就是 `Move.Text` 的内容(`bid:N` / `pass` / `play:<cards>`)。 */
export type DoudizhuAction = string;

/**
 * 底牌是三张。
 *
 * **这是客户端唯一一处写下的斗地主规则常量,而它只用于显示。** 叫分阶段服务端给 `kitty: null`
 * (还没翻开),而屏幕上要有三张背面朝上的牌 —— 那个「三」没有别的来源。它错了的表现是
 * 桌心多画或少画一张扑克牌,是最显眼的一种坏,而它 MUST NOT 参与任何判断。
 */
const KITTY_SIZE = 3;

/** 一个座位在牌桌上的样子 —— 全部来自快照,没有一处是推断的。 */
interface TableSeat {
  readonly seat: number;
  readonly direction: SeatDirection;
  readonly username: string;
  readonly initial: string;
  readonly count: number;
  /** 牌背的下标序列;`@for` 需要一个可迭代的东西,而张数本身不是。 */
  readonly backs: readonly number[];
  readonly isLandlord: boolean;
  readonly isTurn: boolean;
  /** 「不要」/「叫 2 分」—— 当前一轮里这个座位说的那句话。出牌不进气泡:牌在桌心。 */
  readonly bubbleKey: string | null;
  readonly bubblePoints: number;
}

/**
 * 斗地主的牌桌。
 *
 * 对战家族的**第四种**棋盘形状,而它是第一个不能用「黑 / 白」描述自己的:
 * 三个座位,而 `mySide` 那套 `'black' | 'white' | 'spectator'` 对第三个人无话可说。
 * 所以这个组件收的是 **`mySeat: number | null`** —— `null` 表示不占座位(围观者)。
 * 那不是与另三个棋盘的随意分歧:线上契约自 `generalize-match-contract` 起就说座位,
 * 而颜色只是棋盘家族在显示层的读法。
 *
 * **它不判任何合法性。** `add-web-klotski` 定下的尺子是:不问「客户端该不该知道规则」,
 * 而问「知道了会不会造出一个能与服务端分叉的第二真源」。斗地主在这把尺子下**整个落在
 * 不该知道的一侧**:
 *
 *   - 「这几张是什么牌型」要一整套牌型识别(单/对/三带/顺子/连对/飞机/四带二/炸弹),
 *   - 「压不压得住」还要在此之上比大小,
 *   - 而两者都已经在服务端,并且是这一局唯一的判据。
 *
 * 在客户端再写一遍,就是一份会悄悄分叉的第二真源 —— 分叉在玩家眼里是「这游戏有 bug」。
 * 所以这里只做**不需要规则**的事:能选中自己的牌、非自己回合只读、出牌前至少选一张。
 * 代价是「这手压不住」要走一趟服务端才知道,而那一趟带回来的是有错误码的具体理由。
 *
 * **画法上,这里也只有形状,没有颜色。** 纸面、边框、角标红黑、牌背、桌面全部来自
 * `--card-*` / `--felt-*`(`board-skins.css` 里每个皮肤一份),花色的形状来自
 * `public/cards/*.png`,路径写在 `card-table.css` 里。牌面**没有**用整张牌的位图:
 * 54 张定死的位图既不跟主题也不跟皮肤,而这正是 `add-web-xiangqi` 给象棋棋子判过的同一个案子。
 *
 * 与另三个棋盘一样,它把动作**发出去**(`action` output),不自己调 hub。
 */
@Component({
  selector: 'app-card-table',
  standalone: true,
  imports: [NgTemplateOutlet, TranslocoPipe],
  templateUrl: './card-table.html',
  styleUrl: './card-table.css',
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

  /** 叫分阶段桌心那三张背面朝上的底牌。 */
  protected readonly kittyBacks = Array.from({ length: KITTY_SIZE }, (_, i) => i);

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

  /** 当前一轮里每个座位做了什么 —— 从已经公开的 `moves` 算出来,不参与判断。 */
  private readonly trick = computed<readonly TrickAction[]>(() =>
    currentTrick(this.state()?.game?.moves),
  );

  /**
   * 全部座位,从我开始按环绕顺序排。
   *
   * 座位数取 `handCounts.length` —— 那是**规则**说的座位数(服务端逐座位裁剪时按它算),
   * 而 `state.seats` 是房间说的。两者在对局中相等,但前者才是这份局面自己的维度。
   */
  protected readonly seats = computed<readonly TableSeat[]>(() => {
    const view = this.view();
    const room = this.state();
    if (!view || !room) return [];
    const total = view.handCounts.length || room.seats.length;
    const me = this.mySeat();
    const current = room.game?.currentSeat;
    const trick = this.trick();
    return seatRing(me, total).map((seat) => {
      const count = view.handCounts[seat] ?? 0;
      const username = room.seats.find((s) => s.index === seat)?.player.username ?? '';
      const action = trick.find((a) => a.seat === seat);
      return {
        seat,
        direction: relativeSeat(seat, me, total),
        username,
        initial: seatInitial(username),
        count,
        backs: Array.from({ length: count }, (_, i) => i),
        isLandlord: seat === view.landlord,
        isTurn: current === seat && room.status === 'Playing',
        bubbleKey: bubbleKeyFor(action),
        bubblePoints: action?.kind === 'bid' ? action.points : 0,
      };
    });
  });

  /** 另外那些座位 —— 我自己画在下方,不在环绕的格子里。 */
  protected readonly others = computed<readonly TableSeat[]>(() =>
    this.seats().filter((s) => s.direction !== 'self'),
  );

  /** 我自己那一格;围观者没有。 */
  protected readonly mine = computed<TableSeat | null>(
    () => this.seats().find((s) => s.direction === 'self' && s.seat === this.mySeat()) ?? null,
  );

  /**
   * 桌心那手牌是从哪个方位飞来的。
   *
   * 纯函数,不量 DOM —— 所以在 jsdom 里可测,而且桌子在 375 px 下换了摆法方位仍然对。
   */
  protected readonly playedFrom = computed<SeatDirection>(() => {
    const view = this.view();
    const total = view?.handCounts.length ?? 0;
    if (!view || view.tableSeat === null) return 'self';
    return relativeSeat(view.tableSeat, this.mySeat(), total);
  });

  protected readonly selectedCount = computed(() => this.selected().size);

  protected readonly canPlay = computed(() => !this.actionsDisabled() && this.selectedCount() > 0);

  /**
   * 首出时不能过牌 —— 这一条**不需要规则也判得出**:桌上没牌就是没牌。
   * 它是「不需要规则」那一侧的边界:再往前一步(这手压不压得住)就要牌型识别了。
   */
  protected readonly canPass = computed(
    () => !this.actionsDisabled() && this.view()?.tableCards != null,
  );

  /** `--ddz-gaps` —— 扇形的间隔数。CSS 用它做分母,所以 0 张也得给 1。 */
  protected gaps(count: number): number {
    return Math.max(count - 1, 1);
  }

  /**
   * 无障碍标签 —— 「♣3」。
   *
   * 花色在屏幕上是一张图,所以读屏软件那一侧需要一个文字来源。这也是**唯一**还需要
   * `suitSymbol()` 的地方:牌面上的花色不再是字符,于是 `♥` 在某些平台被渲染成彩色 emoji
   * 的老问题在视觉上消失了,而在 aria-label 里它无所谓。
   */
  protected label(card: DoudizhuCard): string {
    return `${suitSymbol(card.suit)}${card.label}`;
  }

  /**
   * 花色的形状(SVG path);王没有花色,返回 `null`。
   *
   * 形状在 TS 里而颜色不在 —— `fill="currentColor"` 让它跟着牌面的 `color`,也就是
   * `--card-red` / `--card-black`。上一版是一张 PNG,而位图在每个皮肤下都一样。
   */
  protected pip(card: DoudizhuCard): string | null {
    return pipPath(card.suit);
  }

  protected isJoker(card: DoudizhuCard): boolean {
    return card.suit === 'none';
  }

  protected isSelected(card: DoudizhuCard): boolean {
    return this.selected().has(card.code);
  }

  protected toggle(card: DoudizhuCard): void {
    if (this.actionsDisabled()) return;
    const next = new Set(this.selected());
    if (!next.delete(card.code)) next.add(card.code);
    this.selected.set(next);
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
    // 会让「再点一次提交」重复发同一个被拒的动作。
    this.selected.set(new Set());
  }
}

/**
 * 气泡上那句话的 i18n key。
 *
 * 出牌**不进气泡** —— 那手牌就在桌心,而同一件事说两遍会让人去找两者的差别。
 */
function bubbleKeyFor(action: TrickAction | undefined): string | null {
  if (!action) return null;
  if (action.kind === 'pass') return 'doudizhu.bubble.pass';
  if (action.kind === 'bid') {
    return action.points === 0 ? 'doudizhu.bubble.no-bid' : 'doudizhu.bubble.bid';
  }
  return null;
}
