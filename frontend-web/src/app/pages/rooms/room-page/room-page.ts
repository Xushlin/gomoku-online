import { HttpErrorResponse } from '@angular/common/http';
import { Dialog } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, computed, DestroyRef, effect, inject, OnDestroy, OnInit, signal, type WritableSignal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import type { GameEndedDto, MoveDto } from '../../../core/api/models/room.model';
import { RoomsApiService } from '../../../core/api/rooms-api.service';
import { AuthService } from '../../../core/auth/auth.service';
import { boardSizeFor } from '../../../games/board-size';
import { GameCapabilitiesService } from '../../../games/game-capabilities.service';
import { GameHubService } from '../../../core/realtime/game-hub.service';
import { SoundService } from '../../../core/sound/sound.service';
import type { SoundEventName } from '../../../core/sound/sound.tokens';
import { gameEntryRoute, PLATFORM_HOME } from '../../../games/game-entry-route';
import { GameCatalogService } from '../../../games/game-catalog.service';
import { CardTable } from '../../../games/cards/card-table/card-table';
import { ChainBoard } from '../../../games/idiom-chain/chain-board/chain-board';
import { DOUDIZHU_KEY } from '../../../games/doudizhu/game-key';
import { DOUDIZHU_TABLE } from '../../../games/doudizhu/seat-view';
import { WAKENG_KEY } from '../../../games/wakeng/game-key';
import { WAKENG_TABLE } from '../../../games/wakeng/seat-view';
import { moveKind } from '../../../games/cards/trick';
import { decodeHand, type PlayingCard } from '../../../games/cards/cards';

import { IDIOM_CHAIN_KEY } from '../../../games/idiom-chain/game-key';
import { XIANGQI_KEY } from '../../../games/xiangqi/game-key';
import { lastMoveCaptured } from '../../../games/xiangqi/position';
import { XiangqiBoard, type PieceMoveEvent } from '../../../games/xiangqi/board/xiangqi-board';
import { Board } from './board/board';
import { ChatPanel, type SendChatPayload } from './chat/chat-panel';
import { GameEndedDialog, type GameEndedDialogData, type GameEndedDialogResult } from './dialogs/game-ended-dialog';
import { hubErrorToKey, type HubErrorKey } from './hub-error.mapper';
import { myOutcome } from './outcome';
import { RoomActionBar } from './action-bar/action-bar';
import { RoomSidebar } from './sidebar/sidebar';
import { FIRST_SEAT, SECOND_SEAT } from '../../../games/board-seats';

const URGE_COOLDOWN_MS = 30_000;
const URGE_TOAST_MS = 4_000;
const ERROR_TOAST_MS = 3_000;
const TICK_MS = 1_000;

@Component({
  selector: 'app-room-page',
  standalone: true,
  imports: [Board, XiangqiBoard, ChainBoard, CardTable, ChatPanel, RoomSidebar,
    RoomActionBar, RouterLink, TranslocoPipe],
  templateUrl: './room-page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RoomPage implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly rooms = inject(RoomsApiService);
  private readonly auth = inject(AuthService);
  private readonly hub = inject(GameHubService);
  private readonly sound = inject(SoundService);
  private readonly dialog = inject(Dialog);
  private readonly capabilities = inject(GameCapabilitiesService);
  private readonly catalog = inject(GameCatalogService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly state = this.hub.state;
  protected readonly connectionStatus = this.hub.connectionStatus;
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly loadError = signal(false);
  protected readonly submittingMove = signal(false);
  protected readonly urgeToast = signal(false);
  protected readonly errorToastKey = signal<HubErrorKey | null>(null);
  protected readonly chatBannerKey = signal<string | null>(null);
  private readonly now = signal<number>(Date.now());
  private readonly urgeCooldownUntil = signal<number>(0);
  private gameEndedDialogOpen = false;
  private tickHandle: ReturnType<typeof setInterval> | null = null;
  private readonly timeouts = new Map<string, ReturnType<typeof setTimeout>>();
  private roomId: string | null = null;
  private lastStatus: ReturnType<GameHubService['connectionStatus']> = 'disconnected';
  /** Sentinel `-1` means "no observation yet" — first state hydration sets the
   * count without firing a sound. Subsequent increments fire `move-place`. */
  private previousMoveCount = -1;
  /**
   * 上一次看到的手牌张数,`-1` 表示还没观察过 —— 与 {@link previousMoveCount} 同一个哨兵。
   * 只有 0 → 非 0 那一次跳变算「发牌」;抢到地主后底牌进手(17 → 20)不算,那不是发牌。
   */
  private previousHandCount = -1;

  /**
   * Board dimensions for this room's game, from the server's descriptors.
   *
   * Resolving a game key into a size is the container's job — `Board` stays a
   * pure presentational component and never learns what `gameKey` means. The key
   * comes from the room DTO rather than the route, because three of the four ways
   * a player reaches this page carry no game in the URL.
   *
   * The descriptors arrive over HTTP, so {@link loading} holds until they land.
   * Otherwise a tic-tac-toe room would paint 15×15 for a frame and then snap to
   * 3×3 — the client knows the size is coming, and showing a wrong one meanwhile
   * is worse than showing the skeleton a moment longer.
   */
  protected readonly boardSize = computed(() =>
    boardSizeFor(this.capabilities, this.state()?.gameKey),
  );

  /** True until both the room and the server's game descriptors are in hand. */
  protected readonly loadingBoard = computed(
    () => this.loading() || !this.capabilities.loaded(),
  );

  /**
   * Which board renderer this room needs.
   *
   * A three-way `@if` in the template rather than a board-component registry. The
   * registries this app does keep (themes, locales, sound packs, board skins) exist
   * because adding an entry is a routine, expected operation. A board shape is not:
   * the match family has exactly these three, and 成语接龙 was the last one.
   *
   * **This comment used to say there were two and predict that "if a third shape
   * ever appears, extracting one then costs the same". The third shape arrived and
   * the prediction held**: it was one `@else if` and six lines, both bindings still
   * type-checked, while a registry would need dynamic components and would give up
   * compile-time checking of `(wordSay)`. The conclusion is unchanged and is now
   * measured rather than forecast.
   */
  /**
   * Where leaving this room goes — the game's own entry point.
   *
   * Falls back to `/home` on its own when the state has not arrived, which is
   * why the 404 path below spells out `PLATFORM_HOME` instead of relying on
   * that: there the fallback is the answer, not a stand-in for one.
   */
  protected readonly exitRoute = computed(() =>
    gameEntryRoute(this.catalog, this.state()?.gameKey),
  );

  protected readonly isXiangqi = computed(() => this.state()?.gameKey === XIANGQI_KEY);

  /**
   * Keyed off the game rather than off `boardSize() === null`, and that is the
   * point: "declared boardless" and "this client does not know the key" both make
   * `boardSizeFor` return nothing useful, but only the first has a renderer. An
   * unknown key still falls through to the default grid.
   */
  protected readonly isIdiomChain = computed(() => this.state()?.gameKey === IDIOM_CHAIN_KEY);

  /** 斗地主是第四种棋盘形状,而它是第一个用座位号而不是颜色描述自己的。 */
  protected readonly isDoudizhu = computed(() => this.state()?.gameKey === DOUDIZHU_KEY);

  protected readonly isWakeng = computed(() => this.state()?.gameKey === WAKENG_KEY);

  /** 服务端给的候选出法,已解成牌 —— 提示按钮点一次拉一次。 */
  protected readonly hints = signal<readonly (readonly PlayingCard[])[]>([]);

  /**
   * 上一次自动过牌是在第几手之后 —— 哨兵,防止同一个回合发两次 `pass`。
   *
   * 用「走了多少手」而不是一个布尔:一个布尔要在「轮到别人」时被清掉,而那需要另一个
   * effect 去清;手数是单调的,比一次就够。`add-card-sounds` 里那个发牌哨兵是同一个形状。
   */
  private autoPassedAfter = -1;

  /**
   * 牌类棋种走**同一个**牌桌组件,只是配置不同 —— 所以这个 `@if` 分支**不因挖坑增加一支**。
   *
   * `null` 表示这不是牌类棋种。
   */
  protected readonly cardTable = computed(() => {
    if (this.isDoudizhu()) return DOUDIZHU_TABLE;
    if (this.isWakeng()) return WAKENG_TABLE;
    return null;
  });

  /**
   * 我坐第几号座位;`null` 表示不占座位(围观者 / 尚未入座)。
   *
   * **读的是 `seats`,而不是 `black` / `white`** —— 后两个字段是 0 号与 1 号的派生读法,
   * 于是三座位房间里 2 号座位上的人在它们里面**根本不出现**,会被当成围观者。
   */
  protected readonly mySeat = computed<number | null>(() => {
    const s = this.state();
    const myId = this.auth.user()?.id;
    if (!s || !myId) return null;
    return s.seats.find((seat) => seat.player.id === myId)?.index ?? null;
  });

  /**
   * 棋盘家族的显示读法 —— **只给那三个两座位棋盘用**。
   *
   * 由 `mySeat` 派生,而不是自己再读一遍 `black` / `white`:同一个事实两处读法就是两个真源。
   * 座位号超过 1 的人在这里是 `'spectator'`,而那**不是**"他是围观者"的意思 ——
   * 那三个棋盘只认得两个座位,而斗地主的牌桌收的是 `mySeat`,不走这条路。
   */
  protected readonly mySide = computed<'black' | 'white' | 'spectator'>(() => {
    switch (this.mySeat()) {
      case FIRST_SEAT:
        return 'black';
      case SECOND_SEAT:
        return 'white';
      default:
        return 'spectator';
    }
  });

  protected readonly myTurn = computed<boolean>(() => {
    const seat = this.mySeat();
    return seat !== null && this.state()?.game?.currentSeat === seat;
  });
  protected readonly turnRemainingMs = computed<number>(() => {
    const g = this.state()?.game;
    if (!g) return 0;
    const started = Date.parse(g.turnStartedAt);
    return Number.isNaN(started) ? 0 : Math.max(0, started + g.turnTimeoutSeconds * 1_000 - this.now());
  });
  protected readonly canUrge = computed<boolean>(() => {
    const s = this.state();
    if (!s || s.status !== 'Playing' || this.mySeat() === null || this.myTurn()) return false;
    if (this.connectionStatus() !== 'connected') return false;
    return this.now() >= this.urgeCooldownUntil();
  });

  constructor() {
    effect(() => {
      // **要不起就替他过牌。** 判据是服务端算的 —— 见 `autoPassIfHopeless`。
      // 挂在 effect 上而不是某个事件回调上,是因为它要在**每一次快照变化**之后重问一遍:
      // 别人出了一手更大的牌之后,同一手手牌的答案会变。
      this.autoPassIfHopeless();
    });
    effect(() => {
      const ended = this.hub.gameEnded();
      if (!ended) return;
      if (!this.gameEndedDialogOpen) this.openGameEndedDialog(ended);
      this.playGameEndSound(ended);
    });
    effect(() => {
      const status = this.connectionStatus();
      if (this.lastStatus === 'reconnecting' && status === 'connected') void this.rehydrate();
      this.lastStatus = status;
    });
    effect(() => {
      const moves = this.state()?.game?.moves ?? [];
      const n = moves.length;
      if (this.previousMoveCount === -1) {
        this.previousMoveCount = n;
        return;
      }
      if (n > this.previousMoveCount) this.sound.play(this.moveSound(moves));
      this.previousMoveCount = n;
    });
    effect(() => {
      // 发牌只在牌**到手的那一刻**响一次:从「没有手牌」到「有手牌」的那一次跳变。
      //
      // 重新加载页面时,发牌**动画**会重播(牌的 DOM 节点是新建的,CSS 就放一次)——
      // 那是装饰;而重播声音是在报告一件没有发生的事。所以这里和 `previousMoveCount`
      // 一样用一个哨兵跳过第一次观察,于是刷新是静的,而真的发牌是响的。
      // **同一个事实驱动两者,但声音的触发条件更严。**
      const state = this.state();
      // 没有快照 = 什么都还没观察到。**哨兵必须被第一份真快照吃掉,而不是被 effect 的第一次
      // 运行吃掉** —— 第一版是后者,于是它在构造时先被 `state() === null` 消费掉,接着第一份
      // 真快照就成了「0 → 17」的跳变,打开一局进行中的牌局也会响。三条断言当场变红。
      if (!state) return;
      // 手牌张数走**这个棋种的**解析函数 —— 两个牌类棋种的 `seatView` 是两种形状,
      // 而拿斗地主那份去解挖坑的会静静得到 0(字段名对不上),于是发牌永远不响。
      const count = this.cardTable()?.parseView(state.game?.seatView)?.myHand.length ?? 0;
      if (this.previousHandCount === -1) {
        this.previousHandCount = count;
        return;
      }
      if (this.previousHandCount === 0 && count > 0) this.sound.play('card-deal');
      this.previousHandCount = count;
    });
  }

  /**
   * Which sound the move that just arrived earned.
   *
   * In 象棋 "he moved" and "he took my 車" are two different pieces of news, and the
   * client already knows which one it is: the board it draws is
   * `INITIAL_POSITION` + every ply, so whether the destination was occupied is a
   * fact it reads on every frame. Nothing new is computed and no second truth is
   * created — see {@link lastMoveCaptured}.
   *
   * A per-game branch rather than a registry: 象棋 is the only game here with
   * captures, and *a switch with one arm is a switch*. This component already
   * branches on `isXiangqi()` / `isIdiomChain()` to pick a board.
   *
   * Every other game plays `move-place`, 成语接龙 included — that event means "a
   * move landed", and what it sounds like is the pack's business.
   */
  /**
   * 点了提示 —— 去服务端要一份候选。
   *
   * **客户端不自己枚举。** 牌型识别与压牌比大小是这一局唯一的判据,而它在服务端;
   * 客户端算一遍就是一份会悄悄分叉的第二真源,而分叉在玩家眼里是「这游戏有 bug」。
   */
  /**
   * **要不起就替他过牌。**
   *
   * 判据是服务端算的 `seatView.canFollow` —— 客户端**不自己判**「我压不压得住」,
   * 那是这一局唯一判据的第二个副本。这里只是照服务端算出来的事实行动。
   *
   * 发出去的是一手**真的** `pass`:进走子历史、别人看得见「不要」、走与真人完全相同的路径。
   * MUST NOT 是「跳过这个座位」——「连续两家过牌清桌」数的就是 `pass`。
   *
   * 三个前提缺一不可:轮到他、桌上有牌(首出不许过牌)、而服务端说他要不起。
   * `canFollow` 在自由首出时恒为 true,所以那一格本来也不会命中。
   */
  private autoPassIfHopeless(): void {
    const room = this.state();
    const table = this.cardTable();
    if (!room || !table || this.submittingMove()) return;
    if (room.status !== 'Playing') return;

    const seat = this.mySeat();
    if (seat === null || room.game?.currentSeat !== seat) return;

    const view = table.parseView(room.game?.seatView);
    if (!view || view.phase !== 'Playing' || view.tableCards === null) return;
    if (view.canFollow) return;

    const played = room.game?.moves.length ?? 0;
    if (this.autoPassedAfter === played) return;
    this.autoPassedAfter = played;
    this.handleTextMove('pass');
  }

  protected requestHints(): void {
    const room = this.state();
    if (!room) return;
    this.rooms.getHints(room.id).subscribe({
      next: (h) => this.hints.set(h.plays.map((p) => decodeHand(p))),
      // 提示是一个可有可无的便利 —— 拉不到就是没有提示,不是一条错误路径。
      error: () => this.hints.set([]),
    });
  }

  private moveSound(moves: readonly MoveDto[]): SoundEventName {
    if (this.isXiangqi()) return lastMoveCaptured(moves) ? 'capture' : 'move-place';
    // 斗地主的**出牌**有自己的声音,而**叫分与不要**留在 `move-place` 上 ——
    // 于是不用看屏幕也听得出别人是出了牌还是过了牌。这是分两个事件的理由,不是副产品。
    if (this.cardTable() !== null) {
      const last = moves.at(-1);
      return last && moveKind(last) === 'play' ? 'card-play' : 'move-place';
    }
    return 'move-place';
  }

  private playGameEndSound(ended: GameEndedDto): void {
    switch (myOutcome(ended, this.auth.user()?.id)) {
      case 'draw':
        this.sound.play('game-draw');
        return;
      case 'win':
        this.sound.play('game-win');
        return;
      case 'lose':
        this.sound.play('game-lose');
        return;
    }
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.notFound.set(true);
      this.loading.set(false);
      return;
    }
    this.roomId = id;
    this.capabilities.ensureLoaded();
    if (!this.auth.isAuthenticated()) {
      void this.router.navigateByUrl('/login');
      return;
    }
    this.hub.urged$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.urgeToast.set(true);
      this.sound.play('urge');
      this.schedule('urge-toast', URGE_TOAST_MS, () => this.urgeToast.set(false));
    });
    this.hub.roomDissolved$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => void this.router.navigateByUrl(this.exitRoute()));
    this.tickHandle = setInterval(() => this.now.set(Date.now()), TICK_MS);
    void this.initialLoad(id);
  }

  ngOnDestroy(): void {
    if (this.tickHandle !== null) clearInterval(this.tickHandle);
    for (const h of this.timeouts.values()) clearTimeout(h);
    this.timeouts.clear();
    if (this.roomId) void this.hub.leaveRoom(this.roomId);
  }

  private async initialLoad(id: string): Promise<void> {
    this.loading.set(true);
    this.notFound.set(false);
    this.loadError.set(false);
    try {
      this.hub.applySnapshot(await firstValueFrom(this.rooms.getById(id)));
      await this.hub.joinRoom(id);
      if (this.mySeat() === null) await this.hub.joinSpectatorGroup(id);
    } catch (err) {
      if (err instanceof HttpErrorResponse && err.status === 404) this.notFound.set(true);
      else this.loadError.set(true);
    } finally {
      this.loading.set(false);
    }
  }

  private async rehydrate(): Promise<void> {
    const id = this.roomId;
    if (!id) return;
    try {
      await this.hub.joinRoom(id);
      if (this.mySeat() === null) await this.hub.joinSpectatorGroup(id);
      this.hub.applySnapshot(await firstValueFrom(this.rooms.getById(id)));
    } catch (err) {
      if (err instanceof HttpErrorResponse && err.status === 404) {
        // Not `exitRoute()`: the room never loaded, so there is no game
        // key to read. `/home` is the only honest answer here.
        void this.router.navigateByUrl(PLATFORM_HOME);
      }
    }
  }

  protected retryLoad(): void {
    if (this.roomId) void this.initialLoad(this.roomId);
  }

  protected retryConnection(): void {
    void this.hub.reconnect().catch(() => undefined);
  }

  protected handleCellClick(payload: { row: number; col: number }): void {
    this.submitMove((id) => this.hub.makeMove(id, payload.row, payload.col));
  }

  /**
   * Xiangqi's move is `from → to`, so it goes through `MovePiece` rather than
   * `MakeMove`. The failure path is identical, which is why both share `submitMove`.
   *
   * The board keeps its selection when this rejects — the component only clears it
   * when a ply actually lands. A refused move almost always means "wrong target",
   * not "wrong piece".
   */
  protected handleWordSay(word: string): void {
    this.submitMove((id) => this.hub.sayWord(id, word));
  }

  /**
   * 斗地主的一次动作 —— `bid:N` / `pass` / `play:<cards>`。
   *
   * 走的是同一个 hub 方法。**`sayWord` 的名字是为成语接龙起的,而它其实是通用的文本载荷路径**:
   * 服务端 `SayWord(roomId, text)` 只是构造 `MakeMoveCommand(Text:)`,不看棋种键。
   *
   * 没有为斗地主加第三个 hub 方法:同一个载荷两个入口,就是两处要一起维护的校验路径。
   * 也没有顺手把它改名 —— 改名是服务端 + 契约 + 规格三处的事,而它值得单独一个变更。
   * **触发条件:第三个走文本载荷的棋种落地那天**,那时"为某一个棋种起的名字"会变成三处误导。
   */
  protected handleTextMove(action: string): void {
    this.submitMove((id) => this.hub.sayWord(id, action));
  }

  protected handlePieceMove(payload: PieceMoveEvent): void {
    this.submitMove((id) =>
      this.hub.movePiece(id, payload.from.row, payload.from.col, payload.to.row, payload.to.col),
    );
  }

  private submitMove(send: (roomId: string) => Promise<void>): void {
    const id = this.roomId;
    if (!id || this.submittingMove()) return;
    this.submittingMove.set(true);
    send(id)
      .catch((err: unknown) => {
        const key = hubErrorToKey(err);
        this.flashError(key);
        if (key === 'game.errors.concurrent-move-refetched') {
          this.rooms.getById(id).subscribe({
            next: (s) => this.hub.applySnapshot(s),
            error: () => undefined,
          });
        }
      })
      .finally(() => this.submittingMove.set(false));
  }

  protected handleChatSend(payload: SendChatPayload): void {
    const id = this.roomId;
    if (!id) return;
    this.hub.sendChat(id, payload.content, payload.channel).catch((err: unknown) => {
      const message =
        typeof err === 'object' && err && 'message' in err
          ? String((err as { message?: unknown }).message ?? '')
          : '';
      if (/forbid|spectator/i.test(message)) {
        this.flash(this.chatBannerKey, 'game.chat.forbidden-error');
      } else {
        this.flashError(hubErrorToKey(err));
      }
    });
  }

  protected handleResign(): void {
    if (!this.roomId) return;
    this.rooms.resign(this.roomId).subscribe({
      error: () => this.flashError('game.errors.generic'),
    });
  }

  protected handleLeave(): void {
    const id = this.roomId;
    if (!id) return;
    // Host of a Waiting room must dissolve, not leave — backend rejects
    // POST /leave with HostCannotLeaveWaitingRoom in that exact shape.
    // Once dissolve fires, the server emits RoomDissolved, the existing
    // roomDissolved$ subscription navigates us to /home, and any spectators
    // get the same redirect.
    const state = this.state();
    const myId = this.auth.user()?.id;
    const isHostOfWaiting =
      state?.status === 'Waiting' && myId && state.host.id === myId;
    const op = isHostOfWaiting ? this.rooms.dissolve(id) : this.rooms.leave(id);
    op.subscribe({
      next: () => void this.router.navigateByUrl(this.exitRoute()),
      error: () => this.flashError('game.errors.generic'),
    });
  }

  protected handleUrge(): void {
    const id = this.roomId;
    if (!id || !this.canUrge()) return;
    const prev = this.urgeCooldownUntil();
    this.urgeCooldownUntil.set(Date.now() + URGE_COOLDOWN_MS);
    this.hub.urge(id).catch((err: unknown) => {
      const key = hubErrorToKey(err);
      if (key !== 'game.errors.urge-cooldown') this.urgeCooldownUntil.set(prev);
      this.flashError(key);
    });
  }

  private openGameEndedDialog(ended: GameEndedDto): void {
    if (!this.roomId) return;
    this.gameEndedDialogOpen = true;
    const data: GameEndedDialogData = {
      result: ended.result,
      winnerUserId: ended.winnerUserId,
      endReason: ended.endReason,
      myUserId: this.auth.user()?.id ?? null,
      roomId: this.roomId,
    };
    const ref = this.dialog.open<GameEndedDialogResult>(GameEndedDialog, { data });
    ref.closed.subscribe((outcome) => {
      this.gameEndedDialogOpen = false;
      if (outcome === 'home') void this.router.navigateByUrl(this.exitRoute());
      else if (outcome === 'replay' && this.roomId)
        void this.router.navigateByUrl(`/replay/${this.roomId}`);
    });
  }

  private flashError(key: HubErrorKey): void {
    this.flash(this.errorToastKey, key);
  }

  private flash<T>(sink: WritableSignal<T | null>, value: T, ttl = ERROR_TOAST_MS): void {
    sink.set(value);
    this.schedule(`flash-${String(value)}`, ttl, () => sink.set(null));
  }

  private schedule(key: string, ms: number, cb: () => void): void {
    const prev = this.timeouts.get(key);
    if (prev !== undefined) clearTimeout(prev);
    this.timeouts.set(key, setTimeout(cb, ms));
  }
}
