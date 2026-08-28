import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { Board } from '../../rooms/room-page/board/board';
import { MoveScrubber } from '../../../platform/move-scrubber/move-scrubber';
import { ChainBoard } from '../../../games/idiom-chain/chain-board/chain-board';
import { IDIOM_CHAIN_KEY } from '../../../games/idiom-chain/game-key';
import { XiangqiBoard } from '../../../games/xiangqi/board/xiangqi-board';
import { isXiangqiFamily } from '../../../games/xiangqi/game-key';
import type { GameReplayDto, RoomState } from '../../../core/api/models/room.model';
import { boardSizeFor } from '../../../games/board-size';
import { GameCatalogService } from '../../../games/game-catalog.service';
import { seatNaming } from '../../../games/seat-labels';
import { GameCapabilitiesService } from '../../../games/game-capabilities.service';
import { RoomsApiService } from '../../../core/api/rooms-api.service';
import { LanguageService } from '../../../core/i18n/language.service';
import { FIRST_SEAT, SECOND_SEAT } from '../../../games/board-seats';

@Component({
  selector: 'app-replay-page',
  standalone: true,
  imports: [Board, XiangqiBoard, ChainBoard, CommonModule, MoveScrubber, RouterLink, TranslocoPipe],
  templateUrl: './replay-page.html',
  styles: [':host { display: block; width: 100%; }'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReplayPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly rooms = inject(RoomsApiService);
  private readonly capabilities = inject(GameCapabilitiesService);
  private readonly catalog = inject(GameCatalogService);
  protected readonly language = inject(LanguageService);

  protected readonly replay = signal<GameReplayDto | null>(null);
  protected readonly currentPly = signal<number>(0);
  protected readonly loading = signal<boolean>(true);
  protected readonly notFound = signal<boolean>(false);
  protected readonly notFinished = signal<boolean>(false);
  protected readonly loadError = signal<boolean>(false);

  protected roomId: string | null = null;

  protected readonly totalMoves = computed(() => this.replay()?.moves.length ?? 0);
  protected readonly atStart = computed(() => this.currentPly() === 0);
  protected readonly atEnd = computed(() => this.currentPly() >= this.totalMoves());

  /**
   * Board dimensions for the replayed game. Same resolution as the live room
   * page, and for the same reason: a replay link opened cold carries only a room
   * id, so the size has to come from the payload's game key — and the size itself
   * comes from the server, not from a client-side copy of it.
   */
  protected readonly boardSize = computed(() =>
    boardSizeFor(this.capabilities, this.replay()?.gameKey),
  );

  /**
   * 标题区的每一位玩家,每人带自己的席位称呼(象棋读作红 / 黑)。
   *
   * **两件事在这里合流,而它们此前各缺一半:** `per-game-seat-labels` 给了「怎么称呼」,
   * 但它当时只能读 `GameReplayDto` 的 `Black` / `White` 两个字段,所以自己在注释里写着
   * 「恰好两位,而那是 DTO 的形状」;`replay-every-seat` 把那个形状改成了座位表,
   * 于是这里既叫得对、也一个不少。
   *
   * 座位数取 `r.seats.length` 而不是描述符的 `seatCount`:回放只有 Finished 房间,
   * 坐满才开局,所以在这一页「有几个人」与「有几个座位」是同一个数。房间侧栏面对
   * 等待中的房间,那里两者会分叉 —— 判据不同是因为问题不同,不是漏抄。
   */
  protected readonly sides = computed(() => {
    const r = this.replay();
    if (!r) return [];
    const manifest = this.catalog.byRoomKey(r.gameKey);
    return r.seats.map((seat) => ({
      seat: seat.index,
      player: seat.player,
      naming: seatNaming(manifest, seat.index, r.seats.length),
    }));
  });

  /** True until both the replay and the server's game descriptors are in hand. */
  protected readonly loadingBoard = computed(
    () => this.loading() || !this.capabilities.loaded(),
  );

  /**
   * Which read-only renderer this replay needs — same `@if` the room page uses, and
   * for the same reason (see `RoomPage.isXiangqi`). The page still writes no
   * rendering code of its own; it just picks between two shared components instead
   * of always reaching for one.
   */
  /** 象棋**族** —— 残局是另一个键、同一块棋盘。见 `games/xiangqi/game-key.ts`。 */
  protected readonly isXiangqi = computed(() => isXiangqiFamily(this.replay()?.gameKey));

  /** Replaying a chain is the same list, read-only. */
  protected readonly isIdiomChain = computed(
    () => this.replay()?.gameKey === IDIOM_CHAIN_KEY,
  );

  /**
   * Synthesise a `RoomState`-shaped object so the existing Board component
   * can consume the replay frame without any changes. status='Finished'
   * forces the board into permanent read-only.
   */
  protected readonly boardState = computed<RoomState | null>(() => {
    const r = this.replay();
    if (!r) return null;
    const slice = r.moves.slice(0, this.currentPly());
    // 回放里"该谁走"**不参与任何判断**:`status` 是 `Finished`,棋盘只读,而
    // `mySide` 不传(默认 `spectator`),所以 `myTurn` 恒为 false。它只是把这一帧
    // 补成一个完整的 `RoomState`,好让棋盘组件一行不改地复用。
    //
    // 因此这里取"最后一手的下一个座位"的两座位算法,而 MUST NOT 有人拿它当真源:
    // 三座位棋种的下一手由规则决定(地主先出),不是 `+1 % 2`。
    const lastSeat = slice.length > 0 ? slice[slice.length - 1].seat : SECOND_SEAT;
    const nextSeat = lastSeat === FIRST_SEAT ? SECOND_SEAT : FIRST_SEAT;
    return {
      id: r.roomId,
      name: r.name,
      gameKey: r.gameKey,
      status: 'Finished',
      host: r.host,
      // **两个都给 null,而这是有意的。** 它们是 0 / 1 号座位的派生读法,合成一份出来
      // 就等于把地主叫成黑方;而**没有任何棋盘组件读它们**(grep 过四个棋盘目录),
      // 所以 null 既不骗人也不少画东西。将来真有人来读,拿到的是空,不是错的那个人。
      black: null,
      white: null,
      // 座位直接来自服务端,**不再由两个玩家字段拼**。此前那段合成恒为两条,于是
      // 三座位棋种的牌桌永远少画一家 —— 而症状是少一个人,不是一个报错。
      seats: r.seats,
      spectators: [],
      game: {
        id: 'replay',
        currentSeat: nextSeat,
        startedAt: r.startedAt,
        endedAt: r.endedAt,
        result: r.result,
        winnerUserId: r.winnerUserId,
        endReason: r.endReason,
        turnStartedAt: r.startedAt,
        turnTimeoutSeconds: 0,
        moves: slice,
      },
      chatMessages: [],
      createdAt: r.startedAt,
    };
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.notFound.set(true);
      this.loading.set(false);
      return;
    }
    this.roomId = id;
    this.capabilities.ensureLoaded();
    this.fetch(id);
  }

  private fetch(id: string): void {
    this.loading.set(true);
    this.notFound.set(false);
    this.notFinished.set(false);
    this.loadError.set(false);
    this.rooms.getReplay(id).subscribe({
      next: (r) => {
        this.replay.set(r);
        this.currentPly.set(0);
        this.loading.set(false);
      },
      error: (err: unknown) => {
        this.loading.set(false);
        if (err instanceof HttpErrorResponse) {
          if (err.status === 404) {
            this.notFound.set(true);
            return;
          }
          if (err.status === 409) {
            this.notFinished.set(true);
            return;
          }
        }
        this.loadError.set(true);
      },
    });
  }

  protected retry(): void {
    if (this.roomId) this.fetch(this.roomId);
  }

  /**
   * scrubber 请求跳到第 N 手。**当前半手的真源在这里**,因为页面还要用它切招法喂棋盘;
   * 钳制也在这里,所以一个越界的请求不会变成一帧越界的棋盘。
   */
  protected onScrub(ply: number): void {
    this.currentPly.set(Math.max(0, Math.min(this.totalMoves(), ply)));
  }

  protected goLive(): void {
    if (this.roomId) void this.router.navigateByUrl(`/rooms/${this.roomId}`);
  }
}
