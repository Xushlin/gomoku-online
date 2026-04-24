import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthService } from '../../../../core/auth/auth.service';
import { LobbyDataService } from '../../../../core/lobby/lobby-data.service';

@Component({
  selector: 'app-hero-card',
  standalone: true,
  imports: [TranslocoPipe],
  templateUrl: './hero.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HeroCard {
  protected readonly auth = inject(AuthService);
  private readonly data = inject(LobbyDataService);
  protected readonly onlineCount = this.data.onlineCount;
}
