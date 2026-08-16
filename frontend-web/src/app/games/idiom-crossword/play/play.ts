import { Dialog } from '@angular/cdk/dialog';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type {
  CrosswordLayout,
  CrosswordRevealedCell,
  CrosswordSlot,
  CrosswordSolvedWord,
} from '../../../core/api/models/puzzle.model';
import { PuzzleApiService } from '../../../core/api/puzzle-api.service';
import { cellKey, CrosswordState, slotCells } from '../crossword-state';
import { IDIOM_CROSSWORD_KEY } from '../game-key';
import { Grid } from '../grid/grid';
import { Tray } from '../tray/tray';
import {
  ResultDialog,
  type CrosswordResultAction,
  type CrosswordResultData,
} from '../result-dialog/result-dialog';

/** How long a wrong slot shakes before its tiles go back. */
const SHAKE_MS = 450;
/** How long an explanation slip stays on screen. */
const SLIP_MS = 3200;

/**
 * 成语纵横 play page.
 *
 * The server owns every number that scores: `mistakes` and `hintsUsed` are read
 * from responses and never incremented locally, and stars come from `submit`.
 * This component owns only what is on screen.
 */
@Component({
  selector: 'app-idiom-crossword-play',
  standalone: true,
  imports: [Grid, Tray, TranslocoPipe],
  templateUrl: './play.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Play {
  private readonly api = inject(PuzzleApiService);
  private readonly dialog = inject(Dialog);
  private readonly router = inject(Router);

  /** Bound from the route via `withComponentInputBinding`. */
  readonly index = input.required<string>();

  protected readonly board = new CrosswordState();

  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  protected readonly attemptId = signal<string | null>(null);
  protected readonly levelCount = signal(0);

  /** Server-owned counters. Displayed, never derived. */
  protected readonly mistakes = signal(0);
  protected readonly hintsUsed = signal(0);

  protected readonly solvedSlots = signal<ReadonlySet<number>>(new Set());
  protected readonly shaking = signal<ReadonlySet<string>>(new Set());
  protected readonly slips = signal<readonly CrosswordSolvedWord[]>([]);
  protected readonly busy = signal(false);

  private solvedWords: CrosswordSolvedWord[] = [];

  protected readonly levelIndex = computed(() => Number(this.index()));

  constructor() {
    // An effect, not the constructor body or ngOnInit: a required route input is
    // not available during construction, and "next level" navigates to a sibling
    // route that **reuses this component instance**, so ngOnInit would not fire
    // again. Keying the load on `levelIndex()` covers both.
    effect(() => {
      this.levelIndex();
      this.load();
    });
  }

  protected load(): void {
    this.loading.set(true);
    this.failed.set(false);
    this.solvedWords = [];
    this.solvedSlots.set(new Set());
    this.slips.set([]);
    this.mistakes.set(0);
    this.hintsUsed.set(0);

    // The list call is only for "is this the last level?" — cheap, and it keeps
    // the play page from having to be told by the router.
    this.api.listLevels(IDIOM_CROSSWORD_KEY).subscribe({
      next: (levels) => this.levelCount.set(levels.length),
      error: () => this.levelCount.set(0),
    });

    this.api.startAttempt(IDIOM_CROSSWORD_KEY, this.levelIndex()).subscribe({
      next: (attempt) => {
        const layout = this.api.parseLayout<CrosswordLayout>(attempt.layoutJson);
        if (!layout) {
          this.failed.set(true);
          this.loading.set(false);
          return;
        }
        this.attemptId.set(attempt.attemptId);
        this.board.load(layout);
        this.loading.set(false);
      },
      error: () => {
        this.failed.set(true);
        this.loading.set(false);
      },
    });
  }

  protected onCellTap(key: string): void {
    this.board.takeBack(key);
  }

  protected onTileTap(tileIndex: number): void {
    const layout = this.board.layout();
    if (!layout) return;

    const landed = this.board.place(tileIndex, layout.tray[tileIndex]);
    if (!landed) return;

    // Only slots that just became full are worth asking about. A placement that
    // completes two crossing slots fires two independent checks.
    for (const slot of this.board.filledSlots()) {
      if (this.solvedSlots().has(slot.index)) continue;
      if (!slotCells(slot).some((c) => cellKey(c.row, c.col) === landed)) continue;
      this.check(slot);
    }
  }

  private check(slot: CrosswordSlot): void {
    const attemptId = this.attemptId();
    if (!attemptId) return;

    const word = this.board.wordIn(slot);
    this.api.check<CrosswordSolvedWord>(attemptId, { slotIndex: slot.index, word }).subscribe({
      next: (result) => {
        this.mistakes.set(result.mistakes);

        if (result.isCorrect) {
          this.board.lockSlot(slot);
          this.solvedSlots.update((s) => new Set(s).add(slot.index));
          if (result.solved) {
            this.solvedWords.push(result.solved);
            this.showSlip(result.solved);
          }
          this.maybeSubmit();
          return;
        }

        this.shakeAndReturn(slot);
      },
      error: () => this.shakeAndReturn(slot),
    });
  }

  private shakeAndReturn(slot: CrosswordSlot): void {
    const keys = slotCells(slot)
      .map((c) => cellKey(c.row, c.col))
      .filter((k) => !this.board.locked().has(k));

    this.shaking.set(new Set(keys));
    setTimeout(() => {
      this.shaking.set(new Set());
      this.board.returnSlot(slot);
    }, SHAKE_MS);
  }

  private showSlip(word: CrosswordSolvedWord): void {
    this.slips.update((s) => [...s, word].slice(-2));
    setTimeout(() => this.slips.update((s) => s.filter((w) => w.index !== word.index)), SLIP_MS);
  }

  protected hint(): void {
    const attemptId = this.attemptId();
    if (!attemptId || this.busy()) return;

    this.busy.set(true);
    this.api.hint<CrosswordRevealedCell>(attemptId, this.board.hintState()).subscribe({
      next: (result) => {
        this.hintsUsed.set(result.hintsUsed);
        if (result.revealed) {
          this.board.applyHint(result.revealed.row, result.revealed.col, result.revealed.char);
          this.maybeSubmit();
        }
        this.busy.set(false);
      },
      error: () => this.busy.set(false),
    });
  }

  private maybeSubmit(): void {
    if (!this.board.complete()) return;
    const attemptId = this.attemptId();
    if (!attemptId) return;

    this.api.submit(attemptId, { cells: this.board.submission() }).subscribe({
      next: (result) => {
        this.mistakes.set(result.mistakes);
        this.hintsUsed.set(result.hintsUsed);
        if (result.isCorrect && result.stars !== null) {
          this.openResult(result.stars, result.durationMs, result.newBest);
        }
      },
    });
  }

  private openResult(stars: number, durationMs: number | null, newBest: boolean): void {
    const isLastLevel = this.levelIndex() >= this.levelCount() - 1;
    const data: CrosswordResultData = {
      stars,
      durationMs,
      mistakes: this.mistakes(),
      hintsUsed: this.hintsUsed(),
      newBest,
      words: [...this.solvedWords].sort((a, b) => a.index - b.index),
      isLastLevel,
    };

    this.dialog
      .open<CrosswordResultAction>(ResultDialog, { data })
      .closed.subscribe((action) => {
        if (action === 'replay') {
          this.load();
        } else if (action === 'next') {
          void this.router.navigateByUrl(
            `/g/idiom-crossword/levels/${this.levelIndex() + 1}`,
          );
        } else if (action === 'levels') {
          void this.router.navigateByUrl('/g/idiom-crossword');
        }
      });
  }
}
