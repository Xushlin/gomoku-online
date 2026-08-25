import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  type Signal,
} from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { PuzzleApiService } from '../../../core/api/puzzle-api.service';
import type { PuzzleSubmitResult } from '../../../core/api/models/puzzle.model';
import { KlotskiBoard, type SlideTarget } from '../board/klotski-board';
import {
  applyMove,
  canSlide,
  initialPositions,
  isSolved,
  KLOTSKI_KEY,
  type KlotskiLayout,
  type KlotskiMove,
  type KlotskiPositions,
} from '../model';
import type { ConfirmsLeaving } from '../../../core/routing/leave-game.guard';

type Phase = 'loading' | 'playing' | 'solved' | 'not-found' | 'error';

/**
 * 华容道 play page.
 *
 * The whole game runs locally — the client owns the one rule 华容道 has — and the
 * server sees exactly two things: a hint request carrying the current position, and
 * one submission carrying the whole move list. `add-klotski` design D6 is why there
 * is no per-move round trip: the server would learn nothing from one, because it
 * replays the entire path at the end regardless.
 */
@Component({
  selector: 'app-klotski-play',
  standalone: true,
  imports: [KlotskiBoard, RouterLink, TranslocoPipe],
  templateUrl: './play.html',
  host: {
    '(document:keydown)': 'handleKey($event)',
  },
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KlotskiPlay implements ConfirmsLeaving {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(PuzzleApiService);

  protected readonly phase = signal<Phase>('loading');
  protected readonly layout = signal<KlotskiLayout | null>(null);
  protected readonly positions = signal<KlotskiPositions>({});
  protected readonly selected = signal<string | null>(null);
  protected readonly moves = signal<readonly KlotskiMove[]>([]);
  protected readonly hintsUsed = signal(0);
  protected readonly result = signal<PuzzleSubmitResult | null>(null);
  protected readonly errorKey = signal<string | null>(null);
  protected readonly busy = signal(false);

  protected readonly levelIndex = Number(this.route.snapshot.paramMap.get('index') ?? 0);
  protected readonly moveCount: Signal<number> = computed(() => this.moves().length);
  protected readonly stars = computed(() => this.result()?.stars ?? 0);
  protected readonly starList = computed(() => [1, 2, 3]);

  private attemptId: string | null = null;

  constructor() {
    this.load();
  }

  protected load(): void {
    this.phase.set('loading');
    this.errorKey.set(null);

    this.api.getLevel(KLOTSKI_KEY, this.levelIndex).subscribe({
      next: (level) => {
        const parsed = this.api.parseLayout<KlotskiLayout>(level.layoutJson);
        if (!parsed?.pieces?.length) {
          this.phase.set('error');
          return;
        }
        this.layout.set(parsed);
        this.positions.set(initialPositions(parsed));
        this.moves.set([]);
        this.hintsUsed.set(0);
        this.result.set(null);
        this.startAttempt();
      },
      error: (err: { status?: number }) => {
        this.phase.set(err?.status === 404 ? 'not-found' : 'error');
      },
    });
  }

  private startAttempt(): void {
    this.api.startAttempt(KLOTSKI_KEY, this.levelIndex).subscribe({
      next: (started) => {
        this.attemptId = started.attemptId;
        this.phase.set('playing');
      },
      error: () => this.phase.set('error'),
    });
  }

  protected handlePick(id: string): void {
    this.selected.set(this.selected() === id ? null : id);
  }

  protected handleSlide(target: SlideTarget): void {
    const id = this.selected();
    if (id === null) return;
    this.commit({ id, dr: target.dr, dc: target.dc });
  }

  protected handleKey(event: KeyboardEvent): void {
    if (this.phase() !== 'playing') return;

    if (event.key === 'Escape') {
      this.selected.set(null);
      return;
    }

    const id = this.selected();
    const layout = this.layout();
    if (id === null || layout === null) return;

    const deltas: Record<string, { dr: number; dc: number }> = {
      ArrowUp: { dr: -1, dc: 0 },
      ArrowDown: { dr: 1, dc: 0 },
      ArrowLeft: { dr: 0, dc: -1 },
      ArrowRight: { dr: 0, dc: 1 },
    };
    const delta = deltas[event.key];
    if (!delta) return;

    event.preventDefault();
    if (canSlide(layout, this.positions(), id, delta.dr, delta.dc)) {
      this.commit({ id, ...delta });
    }
  }

  /** Apply a slide locally, then submit if that finished the puzzle. */
  private commit(move: KlotskiMove): void {
    const layout = this.layout();
    if (!layout || this.phase() !== 'playing') return;

    const next = applyMove(this.positions(), move);
    this.positions.set(next);
    this.moves.set([...this.moves(), move]);

    if (isSolved(layout, next)) {
      this.selected.set(null);
      this.submit();
    }
  }

  protected requestHint(): void {
    const layout = this.layout();
    if (!layout || this.attemptId === null || this.busy() || this.phase() !== 'playing') return;

    this.busy.set(true);
    this.errorKey.set(null);

    // The server searches from where the player actually is, so it has to be told.
    const state = {
      pieces: layout.pieces.map((p) => ({
        id: p.id,
        row: this.positions()[p.id]?.row ?? p.row,
        col: this.positions()[p.id]?.col ?? p.col,
      })),
    };

    this.api.hint(this.attemptId, state).subscribe({
      next: (hint) => {
        this.busy.set(false);
        const move = hint.revealed as KlotskiMove | null;
        if (!move?.id) return;
        this.hintsUsed.update((n) => n + 1);
        this.selected.set(null);
        this.commit(move);
      },
      error: () => {
        this.busy.set(false);
        this.errorKey.set('klotski.error-hint-failed');
      },
    });
  }

  protected submit(): void {
    if (this.attemptId === null || this.busy()) return;

    this.busy.set(true);
    this.errorKey.set(null);

    this.api.submit(this.attemptId, { moves: this.moves() }).subscribe({
      next: (result) => {
        this.busy.set(false);
        this.result.set(result);
        // Stars come from the server. The client knows the move count but not the
        // level's minimum — and it should not, or the puzzle becomes a countdown.
        this.phase.set('solved');
      },
      error: () => {
        this.busy.set(false);
        this.errorKey.set('klotski.error-submit-failed');
      },
    });
  }

  protected restart(): void {
    this.load();
  }

  /**
   * 走过一步的关卡才拦。**每一步只在客户端**,通关时才 `submit(attemptId, { moves })`
   * —— 所以中途走人,走过的步数全部丢失。
   *
   * 一步没走的关卡 MUST NOT 弹框:点进去看一眼就走是正常操作,而每次都问会把这个
   * 确认框训练成「闭着眼睛点掉」的东西。
   */
  leaveWarningKey(): string | null {
    return this.phase() === 'playing' && this.moves().length > 0
      ? 'game.leave-confirm.klotski'
      : null;
  }

}
