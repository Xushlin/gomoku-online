import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  inject,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { ScoreRunResult } from '../../../core/api/models/score-run.model';
import { ScoreRunsApiService } from '../../../core/api/score-runs-api.service';
import { SoundService } from '../../../core/sound/sound.service';
import { TetrisBoard, type RenderedCell } from '../board/tetris-board';
import { COLUMNS, ROWS } from '../engine/field';
import { gravityIntervalMs, TetrisGame } from '../engine/game';
import { TETRIS_KEY } from '../game-key';
import { soundForStep, type TetrisProgress } from './announce';
import type { ConfirmsLeaving } from '../../../core/routing/leave-game.guard';

/**
 * `idle` is before the first run; `over` means the field topped out and we are
 * submitting or showing the result. `error` covers only the two network failures,
 * because a failed start and a failed submit are the only things that can go wrong
 * that the player has to act on.
 */
type Phase = 'idle' | 'starting' | 'playing' | 'paused' | 'submitting' | 'finished' | 'error';

const CELL_COUNT = ROWS * COLUMNS;

/** Before a run exists. Level is 1 because that is what an idle board displays. */
const IDLE_PROGRESS: TetrisProgress = { locks: 0, lines: 0, level: 1, over: false };

/**
 * 俄罗斯方块 play page.
 *
 * The client owns the entire rule set, which is the opposite of what
 * `add-web-xiangqi` decided — and both follow the same test. 象棋's move rules live
 * only on the server, so a TypeScript port would *create* a second source of truth.
 * Here a 60 fps falling block cannot round-trip, so the client has no choice; and
 * the server replays every placement, so a drifting client is *refused*, not
 * silently believed. See `engine/game.ts` for the invariant that keeps every
 * recorded placement replayable.
 *
 * This component owns only the run lifecycle and the timer. Rules are in `engine/`,
 * rendering is in `TetrisBoard`.
 */
@Component({
  selector: 'app-tetris-play',
  standalone: true,
  imports: [TetrisBoard, RouterLink, TranslocoPipe],
  templateUrl: './play.html',
  host: {
    '(document:keydown)': 'handleKey($event)',
  },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TetrisPlay implements ConfirmsLeaving {
  private readonly api = inject(ScoreRunsApiService);
  private readonly sound = inject(SoundService);

  protected readonly phase = signal<Phase>('idle');
  protected readonly errorKey = signal<string | null>(null);
  protected readonly result = signal<ScoreRunResult | null>(null);

  /** Bumped on every engine mutation — the engine is a plain object, not a signal. */
  private readonly version = signal(0);
  private game: TetrisGame | null = null;
  private runId: string | null = null;
  private timer: ReturnType<typeof setTimeout> | null = null;
  /** Last announced progress — the same shape as `RoomPage`'s `previousMoveCount`. */
  private heard: TetrisProgress = IDLE_PROGRESS;

  protected readonly tetrisKey = TETRIS_KEY;

  protected readonly score = computed(() => (this.version(), this.game?.score ?? 0));
  protected readonly lines = computed(() => (this.version(), this.game?.lines ?? 0));
  protected readonly level = computed(() => (this.version(), this.game?.level ?? 1));
  protected readonly nextPiece = computed(() => (this.version(), this.game?.next ?? null));

  /** Row-major cells for the board. Ghost is drawn under the active piece. */
  protected readonly cells = computed<readonly RenderedCell[]>(() => {
    this.version();
    const game = this.game;
    const states: RenderedCell['state'][] = Array.from({ length: CELL_COUNT }, () => 'empty');
    if (game) {
      game.grid.forEach((row, r) =>
        row.forEach((filled, c) => {
          if (filled) states[r * COLUMNS + c] = 'locked';
        }),
      );
      for (const cell of game.ghostCells()) states[cell.row * COLUMNS + cell.col] = 'ghost';
      for (const cell of game.activeCells()) states[cell.row * COLUMNS + cell.col] = 'active';
    }
    return states.map((state, key) => ({ key, state }));
  });

  constructor() {
    inject(DestroyRef).onDestroy(() => this.stopTimer());
  }

  /** Open a run, then start the loop. No piece appears before the seed arrives. */
  protected start(): void {
    this.phase.set('starting');
    this.errorKey.set(null);
    this.result.set(null);

    this.api.start(TETRIS_KEY).subscribe({
      next: (run) => {
        this.runId = run.runId;
        this.game = new TetrisGame(run.seed);
        // Baseline before any sound: the first piece appearing is not an event.
        this.heard = this.progress();
        this.version.update((v) => v + 1);
        this.phase.set('playing');
        this.scheduleTick();
      },
      // No offline fallback: a run with a locally invented seed has nowhere to be
      // submitted, and the player would not find out until the end.
      error: () => {
        this.errorKey.set('tetris.error-start-failed');
        this.phase.set('error');
      },
    });
  }

  protected togglePause(): void {
    if (this.phase() === 'playing') {
      this.stopTimer();
      this.phase.set('paused');
    } else if (this.phase() === 'paused') {
      this.phase.set('playing');
      this.scheduleTick();
    }
  }

  protected move(delta: -1 | 1): void {
    if (!this.playable()) return;
    if (delta < 0) this.game!.moveLeft();
    else this.game!.moveRight();
    this.version.update((v) => v + 1);
  }

  protected rotate(): void {
    if (!this.playable()) return;
    this.game!.rotate();
    this.version.update((v) => v + 1);
  }

  protected softDrop(): void {
    if (!this.playable()) return;
    this.game!.softDrop();
    this.afterGravity();
  }

  protected hardDrop(): void {
    if (!this.playable()) return;
    this.game!.hardDrop();
    this.afterGravity();
  }

  protected handleKey(event: KeyboardEvent): void {
    if (event.key.toLowerCase() === 'p') {
      this.togglePause();
      event.preventDefault();
      return;
    }
    if (!this.playable()) return;

    switch (event.key) {
      case 'ArrowLeft':
        this.move(-1);
        break;
      case 'ArrowRight':
        this.move(1);
        break;
      case 'ArrowUp':
        this.rotate();
        break;
      case 'ArrowDown':
        this.softDrop();
        break;
      case ' ':
        this.hardDrop();
        break;
      default:
        return;
    }
    // Space scrolls the page and the arrows scroll it too — the game consumes both.
    event.preventDefault();
  }

  private playable(): boolean {
    return this.phase() === 'playing' && this.game !== null && !this.game.over;
  }

  private scheduleTick(): void {
    this.stopTimer();
    if (!this.playable()) return;
    this.timer = setTimeout(() => {
      if (!this.playable()) return;
      this.game!.tick();
      this.afterGravity();
    }, gravityIntervalMs(this.game!.level));
  }

  private afterGravity(): void {
    this.version.update((v) => v + 1);
    this.announce();
    if (this.game?.over) {
      this.stopTimer();
      this.submit();
      return;
    }
    this.scheduleTick();
  }

  /**
   * Submit and show the **server's** numbers.
   *
   * The running score on screen is a preview; the recorded one is whatever the
   * server's replay produces. Showing our own would mean displaying a score the
   * leaderboard never received.
   */
  private submit(): void {
    const runId = this.runId;
    const placements = this.game?.placements ?? [];
    if (!runId || placements.length === 0) {
      this.phase.set('finished');
      return;
    }

    this.phase.set('submitting');
    this.api.submit(runId, { placements }).subscribe({
      next: (result) => {
        this.result.set(result);
        this.phase.set('finished');
      },
      // No score is shown on failure — a number we made up is worse than none.
      error: () => {
        this.result.set(null);
        this.errorKey.set('tetris.error-submit-failed');
        this.phase.set('error');
      },
    });
  }

  /**
   * Play at most one sound for whatever the last gravity step did.
   *
   * Only called from {@link afterGravity}, which is the only path that can lock a
   * piece — a lateral move or a rotation changes nothing worth hearing.
   */
  private announce(): void {
    const after = this.progress();
    const event = soundForStep(this.heard, after);
    this.heard = after;
    if (event) this.sound.play(event);
  }

  private progress(): TetrisProgress {
    const game = this.game;
    if (!game) return IDLE_PROGRESS;
    return {
      locks: game.placements.length,
      lines: game.lines,
      level: game.level,
      over: game.over,
    };
  }

  private stopTimer(): void {
    if (this.timer !== null) {
      clearTimeout(this.timer);
      this.timer = null;
    }
  }

  /**
   * 一局进行中(含暂停、含正在提交)才拦 —— **成绩在结束时才提交**,中途走人这一局
   * 不计入排行。`submitting` 也算:提交还没回来就走,那一局同样落不了地。
   */
  leaveWarningKey(): string | null {
    const phase = this.phase();
    return phase === 'playing' || phase === 'paused' || phase === 'submitting'
      ? 'game.leave-confirm.tetris'
      : null;
  }

}
