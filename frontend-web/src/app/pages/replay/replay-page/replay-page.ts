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
      black: r.black,
      white: r.white,
      // 回放的 DTO 只有黑白两方 —— 它是两座位棋种的产物,而三座位棋种的回放要等
      // `GameReplayDto` 也改说座位。这里由两个已知的字段合成两个座位,而**不是给个空数组**:
      // 空数组会让棋盘以为"这局没人下",而它其实是"这个 DTO 说不出第三个人"。
      seats: [
        { index: FIRST_SEAT, player: r.black },
        { index: SECOND_SEAT, player: r.white },
      ],
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
