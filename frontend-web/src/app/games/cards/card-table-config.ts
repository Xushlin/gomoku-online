import type { PlayingCard } from './cards';

/**
 * 牌桌**要画的那一份**局面 —— 两个牌类棋种的 `seatView` 归一到这里。
 *
 * **解析各一份,绘制只有一份**,而那正是「共享的是事实,还是形状」这把尺子给出的答案:
 * 斗地主的 `seatView` 有 `landlord` / `baseScore`,挖坑的有 `digger` / `bid` /
 * `firstBidder` / `firstBidderCard` —— 那是**两种形状**,所以两个解析函数;
 * 而「一桌三个人、每人一叠牌、桌心一手牌」是**同一件事**,所以一份绘制。
 */
export interface CardTableView {
  readonly phase: 'Bidding' | 'Playing' | 'Finished';

  /** 拿底牌那一家的座位(地主 / 挖坑者);还没定时 `null`。 */
  readonly roleSeat: number | null;

  /** 底分 / 叫分;还没定下来时 `0`。 */
  readonly bid: number;

  /** 已经叫过几次(含不叫 / 不挖)。 */
  readonly bidsMade: number;

  readonly myHand: readonly PlayingCard[];
  readonly handCounts: readonly number[];

  /** 底牌;还不该公开时 `null`。 */
  readonly kitty: readonly PlayingCard[] | null;

  readonly tableSeat: number | null;
  readonly tableCards: readonly PlayingCard[] | null;
  readonly winner: number | null;

  /**
   * 首叫者与他亮的那张牌 —— **挖坑独有**,斗地主为 `null`。
   *
   * 按挖坑的规则它本来就是明示的(它决定了谁首叫首出),而**服务端算得出**,
   * 所以客户端不该自己猜。
   */
  readonly firstBidder: { readonly seat: number; readonly card: PlayingCard } | null;

  /**
   * 「假如此刻轮到我,我出得起吗」—— **服务端算的**,客户端只照它行动。
   *
   * 斗地主今天恒 `false`(它还没有这个功能),而 `false` 在那里的含义是
   * **「没有这个信号」**而不是「你要不起」—— 所以自动过牌只在提供了它的棋种上生效,
   * 由那个棋种的 `seatView` 真的带着这个字段来决定。
   */
  readonly canFollow: boolean;
}

/**
 * 一个牌类棋种与另一个的**全部**差别。
 *
 * 它存在是因为牌桌只有一份:那 374 行 CSS 里的扇形公式被 shrink-to-fit 咬过**四次**,
 * 而它的不变量由 `check-styles.mjs` 按文件名钉着 —— 复制它就是复制一个已经出过四次错的
 * 公式,而那是一份**真的会分叉**的第二真源。两个游戏的差别是几个数和几个标签,
 * 那是**参数**,不是分歧。
 *
 * 与 `NInARowRules(key, rows, cols, winLength)` 让一字棋贡献零行判胜是同一个形状。
 */
export interface CardTableConfig {
  /** 底牌几张 —— 斗地主 3,挖坑 4。**只用于显示**(叫分阶段画几张牌背)。 */
  readonly kittySize: number;

  /** 叫分的翻译键前缀:`game.doudizhu` / `game.wakeng`。 */
  readonly i18nPrefix: string;

  /** 拿底牌那一家怎么称呼 —— 「地主」/「挖坑者」。 */
  readonly roleLabelKey: string;

  /** 要不要标出首叫者(仅挖坑)。 */
  readonly showsFirstBidder: boolean;

  /**
   * 手牌的**显示顺序**。
   *
   * **这不是一个凑数的配置项,是一处真缺陷的预防。** 服务端送来的 `myHand` 是
   * `Card.Encode` 的输出,也就是**编码顺序**(3、4、…、K、A、2)。斗地主的大小恰好就是
   * 这个顺序,所以它按原样渲染是对的;挖坑是 `3 > 2 > A > … > 4`,按原样渲染会把
   * **最强的那张牌放在最左边**、第二张是最弱的 4。
   */
  readonly compareForDisplay: (a: PlayingCard, b: PlayingCard) => number;

  /** 把这个棋种的 `seatView` 解成牌桌要画的那一份;解不出来返回 `null`。 */
  readonly parseView: (raw: string | null | undefined) => CardTableView | null;
}
