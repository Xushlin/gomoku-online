import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type {
  ScoreLeaderboardEntry,
  ScoreWindow,
} from '../../../core/api/models/score-run.model';
import { SCORE_WINDOWS } from '../../../core/api/models/score-run.model';
import { ScoreRunsApiService } from '../../../core/api/score-runs-api.service';
import { GameCatalogService } from '../../../games/game-catalog.service';

const PAGE_SIZE = 20;

/**
 * A score-attack ladder, at `/g/:gameKey/scores`.
 *
 * Deliberately **not** `/g/:gameKey/leaderboard`. That page is the ELO ladder: its
 * rows are rating / wins / losses / draws and its data comes from
 * `/api/leaderboard`. These rows are score / lines / level / when, from
 * `/api/score-runs/leaderboard`. Folding both into one component would mean
 * branching its columns on the game's category — and two sets of columns are two
 * components.
 *
 * The route is parameterised rather than hardcoded to `tetris`, matching the ELO
 * ladder: an unregistered key renders an empty board rather than an error, because
 * on a collection endpoint "this game has no ladder" and "the ladder is empty" are
 * indistinguishable to the caller.
 *
 * `week` is a **natural** week — Monday 00:00 UTC — decided and reasoned on the
 * server (`ScoreWindows.StartOf`). The client only names the window.
 */
@Component({
  selector: 'app-score-leaderboard-page',
  standalone: true,
  imports: [RouterLink, TranslocoPipe],
  templateUrl: './score-leaderboard-page.html',
  styles: [':host { display: block; width: 100%; }'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ScoreLeaderboardPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(ScoreRunsApiService);
  private readonly catalog = inject(GameCatalogService);

  protected readonly gameKey = signal('');
  protected readonly window = signal<ScoreWindow>('week');
  protected readonly entries = signal<readonly ScoreLeaderboardEntry[]>([]);
  protected readonly total = signal(0);
  protected readonly page = signal(1);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);

  protected readonly windows = SCORE_WINDOWS;
  protected readonly pageSize = PAGE_SIZE;

  /** Display name from the manifest, or `null` for a key this client never heard of. */
  protected readonly titleKey = computed<string | null>(
    () => this.catalog.byKey(this.gameKey())?.titleKey ?? null,
  );

  protected readonly launchRoute = computed<string | null>(
    () => this.catalog.byKey(this.gameKey())?.launchRoute ?? null,
  );

  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.total() / PAGE_SIZE)));

  protected readonly isEmpty = computed(
    () => !this.loading() && !this.loadError() && this.entries().length === 0,
  );

  ngOnInit(): void {
    this.gameKey.set(this.route.snapshot.paramMap.get('gameKey') ?? '');
    this.fetch(1);
  }

  protected selectWindow(window: ScoreWindow): void {
    if (window === this.window()) return;
    this.window.set(window);
    this.fetch(1);
  }

  protected retry(): void {
    this.fetch(this.page());
  }

  protected prev(): void {
    if (this.page() > 1) this.fetch(this.page() - 1);
  }

  protected next(): void {
    if (this.page() < this.totalPages()) this.fetch(this.page() + 1);
  }

  /** Rank comes from the server and is global — never recomputed from the index. */
  protected trackEntry(_index: number, entry: ScoreLeaderboardEntry): string {
    return entry.userId;
  }

  protected tierIcon(rank: number): string | null {
    switch (rank) {
      case 1:
        return '🥇';
      case 2:
        return '🥈';
      case 3:
        return '🥉';
      default:
        return null;
    }
  }

  private fetch(page: number): void {
    this.loading.set(true);
    this.loadError.set(false);
    this.api.leaderboard(this.gameKey(), this.window(), page, PAGE_SIZE).subscribe({
      next: (result) => {
        this.entries.set(result.items);
        this.total.set(result.total);
        this.page.set(result.page);
        this.loading.set(false);
      },
      // Only a failed request is an error. A key nobody has played comes back
      // 200 + empty, and "nobody has played this yet" is a fact, not a fault.
      error: () => {
        this.entries.set([]);
        this.total.set(0);
        this.loading.set(false);
        this.loadError.set(true);
      },
    });
  }
}
