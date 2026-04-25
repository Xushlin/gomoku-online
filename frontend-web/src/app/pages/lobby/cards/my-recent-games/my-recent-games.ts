import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type {
  PagedResult,
  UserGameSummaryDto,
} from '../../../../core/api/models/user-profile.model';
import { UsersApiService } from '../../../../core/api/users-api.service';
import { AuthService } from '../../../../core/auth/auth.service';
import { LanguageService } from '../../../../core/i18n/language.service';

const MAX_ROWS = 5;

@Component({
  selector: 'app-my-recent-games-card',
  standalone: true,
  imports: [CommonModule, RouterLink, TranslocoPipe],
  templateUrl: './my-recent-games.html',
  styles: [':host { display: block; width: 100%; }'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MyRecentGamesCard {
  private readonly auth = inject(AuthService);
  private readonly users = inject(UsersApiService);
  private readonly router = inject(Router);
  protected readonly language = inject(LanguageService);

  protected readonly userId = computed(() => this.auth.user()?.id ?? null);
  protected readonly games = signal<readonly UserGameSummaryDto[]>([]);
  protected readonly loading = signal<boolean>(true);
  protected readonly error = signal<boolean>(false);
  // Bumped to force the effect to refire when retry() is invoked.
  private readonly retryTick = signal<number>(0);

  constructor() {
    effect((onCleanup) => {
      const id = this.userId();
      this.retryTick();
      if (!id) return;
      this.loading.set(true);
      this.error.set(false);
      const sub = this.users.getGames(id, 1, MAX_ROWS).subscribe({
        next: (r: PagedResult<UserGameSummaryDto>) => {
          this.games.set(r.items);
          this.loading.set(false);
        },
        error: () => {
          this.games.set([]);
          this.error.set(true);
          this.loading.set(false);
        },
      });
      onCleanup(() => sub.unsubscribe());
    });
  }

  protected retry(): void {
    this.retryTick.update((n) => n + 1);
  }

  protected opponentOf(g: UserGameSummaryDto): { id: string; username: string } {
    return g.black.id === this.userId() ? g.white : g.black;
  }

  protected resultKey(g: UserGameSummaryDto): string {
    if (g.result === 'Draw') return 'profile.result-draw';
    return g.winnerUserId === this.userId() ? 'profile.result-win' : 'profile.result-loss';
  }

  protected reasonKey(g: UserGameSummaryDto): string {
    switch (g.endReason) {
      case 'Connected5':
        return 'game.ended.reason-connected-5';
      case 'Resigned':
        return 'game.ended.reason-resigned';
      case 'TurnTimeout':
        return 'game.ended.reason-timeout';
    }
  }

  protected onRowClick(roomId: string): void {
    void this.router.navigateByUrl(`/replay/${roomId}`);
  }
}
