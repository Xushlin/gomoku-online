import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { forkJoin } from 'rxjs';
import type { PuzzleLevelSummary, PuzzleProgress } from '../../../core/api/models/puzzle.model';
import { PuzzleApiService } from '../../../core/api/puzzle-api.service';
import { KLOTSKI_KEY } from '../model';

type Phase = 'loading' | 'ready' | 'empty' | 'error';

/**
 * 华容道 level list.
 *
 * Unlock state and best scores come from the server; the client computes neither.
 * `PuzzleProgress.unlockedLevelIndex` is a derived query (`MAX(completed) + 1`),
 * not a stored counter — see the puzzle-core spec on why progress is not a column.
 */
@Component({
  selector: 'app-klotski-level-list',
  standalone: true,
  imports: [RouterLink, TranslocoPipe],
  templateUrl: './level-list.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class KlotskiLevelList {
  private readonly api = inject(PuzzleApiService);

  protected readonly phase = signal<Phase>('loading');
  protected readonly levels = signal<readonly PuzzleLevelSummary[]>([]);
  protected readonly progress = signal<PuzzleProgress | null>(null);
  protected readonly starList = [1, 2, 3];

  constructor() {
    this.load();
  }

  protected load(): void {
    this.phase.set('loading');
    forkJoin({
      levels: this.api.listLevels(KLOTSKI_KEY),
      progress: this.api.getProgress(KLOTSKI_KEY),
    }).subscribe({
      next: ({ levels, progress }) => {
        this.levels.set(levels);
        this.progress.set(progress);
        this.phase.set(levels.length === 0 ? 'empty' : 'ready');
      },
      error: () => this.phase.set('error'),
    });
  }

  protected difficultyKey(difficulty: number): string {
    return `klotski.difficulty-${Math.min(3, Math.max(1, difficulty))}`;
  }

  protected seconds(ms: number | null): number {
    return ms === null ? 0 : Math.round(ms / 1000);
  }
}
