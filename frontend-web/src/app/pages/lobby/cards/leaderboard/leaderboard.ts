import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import type { LeaderboardEntry } from '../../../../core/api/models/leaderboard.model';
import { LobbyDataService } from '../../../../core/lobby/lobby-data.service';

@Component({
  selector: 'app-leaderboard-card',
  standalone: true,
  imports: [RouterLink, TranslocoPipe],
  templateUrl: './leaderboard.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LeaderboardCard {
  private readonly data = inject(LobbyDataService);
  protected readonly slice = this.data.leaderboard;

  protected refresh(): void {
    this.slice.refresh();
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

  protected tierKey(rank: number): string | null {
    switch (rank) {
      case 1:
        return 'lobby.leaderboard.tier-gold';
      case 2:
        return 'lobby.leaderboard.tier-silver';
      case 3:
        return 'lobby.leaderboard.tier-bronze';
      default:
        return null;
    }
  }

  protected trackEntry(_index: number, entry: LeaderboardEntry): string {
    return entry.userId;
  }
}
