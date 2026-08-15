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
import { LeaderboardApiService } from '../../../core/api/leaderboard-api.service';
import type { LeaderboardEntry } from '../../../core/api/models/leaderboard.model';
import { GameCatalogService } from '../../../games/game-catalog.service';

const PAGE_SIZE = 20;

/**
 * One game's ladder, at `/g/:gameKey/leaderboard`.
 *
 * `/g/<key>` is already the per-game namespace (`/g/tictactoe`,
 * `/g/idiom-crossword`), so the ladder follows it.
 *
 * The lobby's leaderboard card is deliberately untouched and stays pinned to
 * gomoku: it belongs to *gomoku's lobby*, and giving it a game switcher would
 * start generalising `/home`, which is a normative path in five web specs.
 * That leaves two entry points onto the same gomoku board for a while — a known
 * duplication that the lobby-generalisation step will collapse.
 */
@Component({
  selector: 'app-leaderboard-page',
  standalone: true,
  imports: [RouterLink, TranslocoPipe],
  templateUrl: './leaderboard-page.html',
  styles: [':host { display: block; width: 100%; }'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LeaderboardPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(LeaderboardApiService);
  private readonly catalog = inject(GameCatalogService);

  protected readonly gameKey = signal<string>('');
  protected readonly entries = signal<readonly LeaderboardEntry[]>([]);
  protected readonly total = signal(0);
  protected readonly page = signal(1);
  protected readonly loading = signal(true);
  protected readonly loadError = signal(false);

  protected readonly pageSize = PAGE_SIZE;

  /**
   * Display name from the manifest when we know the game, else the raw key.
   * An unregistered key is not an error here — it renders an explanatory empty
   * state, so it still needs something to put in the heading.
   */
  protected readonly titleKey = computed<string | null>(() => {
    const manifest = this.catalog.byKey(this.gameKey());
    return manifest?.titleKey ?? null;
  });

  protected readonly totalPages = computed(() =>
    Math.max(1, Math.ceil(this.total() / PAGE_SIZE)),
  );

  protected readonly isEmpty = computed(
    () => !this.loading() && !this.loadError() && this.entries().length === 0,
  );

  ngOnInit(): void {
    const key = this.route.snapshot.paramMap.get('gameKey') ?? '';
    this.gameKey.set(key);
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

  /** Rank comes from the server and is global — never recomputed from the row index. */
  protected trackEntry(_index: number, entry: LeaderboardEntry): string {
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
    this.api.getPage(this.gameKey(), page, PAGE_SIZE).subscribe({
      next: (result) => {
        this.entries.set(result.items);
        this.total.set(result.total);
        this.page.set(result.page);
        this.loading.set(false);
      },
      // Only a failed request is an error. An unrated or unregistered game key
      // comes back 200 + empty, and that renders as the explanatory empty
      // state — "nobody has played this yet" is a fact, not a fault.
      error: () => {
        this.entries.set([]);
        this.loading.set(false);
        this.loadError.set(true);
      },
    });
  }
}
