import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  OnDestroy,
  OnInit,
  signal,
} from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { Board } from '../../rooms/room-page/board/board';
import { ChainBoard } from '../../../games/idiom-chain/chain-board/chain-board';
import { IDIOM_CHAIN_KEY } from '../../../games/idiom-chain/game-key';
import { XiangqiBoard } from '../../../games/xiangqi/board/xiangqi-board';
import { XIANGQI_KEY } from '../../../games/xiangqi/game-key';
import type {
  GameReplayDto,
  RoomState,
  Stone,
} from '../../../core/api/models/room.model';
import { boardSizeFor } from '../../../games/board-size';
import { GameCapabilitiesService } from '../../../games/game-capabilities.service';
import { RoomsApiService } from '../../../core/api/rooms-api.service';
import { LanguageService } from '../../../core/i18n/language.service';

const STEP_INTERVAL_MS = 700;
type Speed = 0.5 | 1 | 2;

@Component({
  selector: 'app-replay-page',
  standalone: true,
  imports: [Board, XiangqiBoard, ChainBoard, CommonModule, RouterLink, TranslocoPipe],
  templateUrl: './replay-page.html',
  styles: [':host { display: block; width: 100%; }'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReplayPage implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly rooms = inject(RoomsApiService);
  private readonly capabilities = inject(GameCapabilitiesService);
  protected readonly language = inject(LanguageService);

  protected readonly replay = signal<GameReplayDto | null>(null);
  protected readonly currentPly = signal<number>(0);
  protected readonly playing = signal<boolean>(false);
  protected readonly speed = signal<Speed>(1);
  protected readonly loading = signal<boolean>(true);
  protected readonly notFound = signal<boolean>(false);
  protected readonly notFinished = signal<boolean>(false);
  protected readonly loadError = signal<boolean>(false);

  protected roomId: string | null = null;

  protected readonly totalMoves = computed(() => this.replay()?.moves.length ?? 0);
  protected readonly atStart = computed(() => this.currentPly() === 0);
  protected readonly atEnd = computed(() => this.currentPly() >= this.totalMoves());
  protected readonly speeds: readonly Speed[] = [0.5, 1, 2];
  protected readonly playButtonKey = computed(() => {
    if (this.atEnd()) return 'replay.scrubber.replay';
    return this.playing() ? 'replay.scrubber.pause' : 'replay.scrubber.play';
  });

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
  protected readonly isXiangqi = computed(() => this.replay()?.gameKey === XIANGQI_KEY);

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
    const lastStone: Stone = slice.length > 0 ? slice[slice.length - 1].stone : 'White';
    const nextTurn: Stone = lastStone === 'Black' ? 'White' : 'Black';
    return {
      id: r.roomId,
      name: r.name,
      gameKey: r.gameKey,
      status: 'Finished',
      host: r.host,
      black: r.black,
      white: r.white,
      spectators: [],
      game: {
        id: 'replay',
        currentTurn: nextTurn,
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

  constructor() {
    effect((onCleanup) => {
      if (!this.playing()) return;
      const speed = this.speed();
      const id = setInterval(() => this.step(+1), STEP_INTERVAL_MS / speed);
      onCleanup(() => clearInterval(id));
    });
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
    this.fetch(id);
  }

  ngOnDestroy(): void {
    this.playing.set(false);
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

  protected first(): void {
    this.playing.set(false);
    this.currentPly.set(0);
  }

  protected last(): void {
    this.playing.set(false);
    this.currentPly.set(this.totalMoves());
  }

  protected step(delta: number): void {
    const next = this.currentPly() + delta;
    const clamped = Math.max(0, Math.min(this.totalMoves(), next));
    this.currentPly.set(clamped);
    if (clamped >= this.totalMoves()) {
      this.playing.set(false);
    }
  }

  protected onPrev(): void {
    this.playing.set(false);
    this.step(-1);
  }

  protected onNext(): void {
    this.playing.set(false);
    this.step(+1);
  }

  protected togglePlay(): void {
    if (this.atEnd()) {
      this.currentPly.set(0);
      this.playing.set(true);
      return;
    }
    this.playing.set(!this.playing());
  }

  protected setSpeed(s: Speed): void {
    this.speed.set(s);
  }

  protected onSeek(event: Event): void {
    const target = event.target as HTMLInputElement;
    const value = Number.parseInt(target.value, 10);
    if (Number.isNaN(value)) return;
    this.playing.set(false);
    this.currentPly.set(Math.max(0, Math.min(this.totalMoves(), value)));
  }

  protected goLive(): void {
    if (this.roomId) void this.router.navigateByUrl(`/rooms/${this.roomId}`);
  }
}
