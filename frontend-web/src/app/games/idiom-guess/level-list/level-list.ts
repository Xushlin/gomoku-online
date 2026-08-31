import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { forkJoin } from 'rxjs';
import type { PuzzleLevelSummary, PuzzleProgress } from '../../../core/api/models/puzzle.model';
import { PuzzleApiService } from '../../../core/api/puzzle-api.service';
import { IDIOM_GUESS_KEY } from '../game-key';

type Phase = 'loading' | 'ready' | 'empty' | 'error';

/**
 * 猜成语的关卡列表。
 *
 * 解锁状态与最好成绩都来自服务端,客户端两样都不算 —— 与另外两个关卡游戏同一条。
 */
@Component({
  selector: 'app-idiom-guess-level-list',
  standalone: true,
  imports: [RouterLink, TranslocoPipe],
  templateUrl: './level-list.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class IdiomGuessLevelList {
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
      levels: this.api.listLevels(IDIOM_GUESS_KEY),
      progress: this.api.getProgress(IDIOM_GUESS_KEY),
    }).subscribe({
      next: ({ levels, progress }) => {
        this.levels.set(levels);
        this.progress.set(progress);
        this.phase.set(levels.length === 0 ? 'empty' : 'ready');
      },
      error: () => this.phase.set('error'),
    });
  }

  protected seconds(ms: number | null): number {
    return ms === null ? 0 : Math.round(ms / 1000);
  }
}
