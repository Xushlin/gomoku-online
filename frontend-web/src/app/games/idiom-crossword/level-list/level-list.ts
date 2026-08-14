import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { PuzzleApiService } from '../../../core/api/puzzle-api.service';
import type { PuzzleLevelSummary } from '../../../core/api/models/puzzle.model';
import { IDIOM_CROSSWORD_KEY } from '../game-key';

/**
 * 成语纵横 level picker.
 *
 * Lock state comes from the server's `unlocked` field and is never recomputed
 * here: which levels are open is part of the game's rules, and the client
 * holding a second opinion about that is how the two end up disagreeing.
 */
@Component({
  selector: 'app-idiom-crossword-level-list',
  standalone: true,
  imports: [RouterLink, TranslocoPipe],
  templateUrl: './level-list.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LevelList {
  private readonly api = inject(PuzzleApiService);

  protected readonly levels = signal<readonly PuzzleLevelSummary[] | null>(null);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.failed.set(false);
    this.api.listLevels(IDIOM_CROSSWORD_KEY).subscribe({
      next: (levels) => {
        this.levels.set(levels);
        this.loading.set(false);
      },
      error: () => {
        this.failed.set(true);
        this.loading.set(false);
      },
    });
  }

  /** Three slots, filled up to `bestStars`. Unplayed levels show three empty slots. */
  protected stars(level: PuzzleLevelSummary): readonly boolean[] {
    const earned = level.bestStars ?? 0;
    return [earned >= 1, earned >= 2, earned >= 3];
  }

  /** `m:ss`, or null when the level has never been completed. */
  protected duration(level: PuzzleLevelSummary): string | null {
    if (level.bestDurationMs === null) return null;
    const total = Math.round(level.bestDurationMs / 1000);
    const minutes = Math.floor(total / 60);
    const seconds = total % 60;
    return `${minutes}:${seconds.toString().padStart(2, '0')}`;
  }

  protected routeFor(level: PuzzleLevelSummary): string {
    return `/g/idiom-crossword/levels/${level.levelIndex}`;
  }

  /** Skeleton placeholders — a fixed count so the grid does not jump when data lands. */
  protected readonly skeletons = [0, 1, 2, 3, 4, 5];
}
