import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';
import {
  DefaultHomeDataService,
  HomeDataService,
} from '../../core/lobby/home-data.service';
import { FindPlayerCard } from './cards/find-player/find-player';
import { GamesStrip } from './cards/games-strip/games-strip';
import { HeroCard } from './cards/hero/hero';
import { MyActiveRoomsCard } from './cards/my-active-rooms/my-active-rooms';
import { MyRecentGamesCard } from './cards/my-recent-games/my-recent-games';

/**
 * `/home` — the platform home, not any game's lobby.
 *
 * Every card here answers a question about the *account*: who am I, how many
 * people are online, which games am I in, what did I just play, who else is
 * here. Anything needing a game key lives at `/g/:gameKey/lobby`.
 *
 * Owns `HomeDataService` for the page lifetime so polling stops on navigate.
 */
@Component({
  selector: 'app-lobby',
  standalone: true,
  imports: [FindPlayerCard, GamesStrip, HeroCard, MyActiveRoomsCard, MyRecentGamesCard],
  templateUrl: './lobby.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [{ provide: HomeDataService, useClass: DefaultHomeDataService }],
})
export class Lobby {
  protected readonly auth = inject(AuthService);
}
