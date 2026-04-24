import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';
import {
  DefaultLobbyDataService,
  LobbyDataService,
} from '../../core/lobby/lobby-data.service';
import { ActiveRoomsCard } from './cards/active-rooms/active-rooms';
import { HeroCard } from './cards/hero/hero';
import { LeaderboardCard } from './cards/leaderboard/leaderboard';
import { MyActiveRoomsCard } from './cards/my-active-rooms/my-active-rooms';

/**
 * Lobby container — owns the `LobbyDataService` for the page lifetime so
 * polling stops when the user navigates away. Four dumb cards bind to the
 * service's slices via DI.
 */
@Component({
  selector: 'app-lobby',
  standalone: true,
  imports: [ActiveRoomsCard, HeroCard, LeaderboardCard, MyActiveRoomsCard],
  templateUrl: './lobby.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [{ provide: LobbyDataService, useClass: DefaultLobbyDataService }],
})
export class Lobby {
  protected readonly auth = inject(AuthService);
}
