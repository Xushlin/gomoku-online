import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { GAME_REGISTRY } from '../../../../games';

/**
 * `/home`'s launcher strip.
 *
 * Not a second catalogue: `/games` lists all eight games including planned
 * ones, with descriptions and content-locale badges. This lists only what can
 * be played right now, so that landing after login and being in a game is
 * still one click — which it stopped being when the game-scoped cards moved
 * to `/g/:gameKey/lobby`.
 *
 * Reads the registry, so shipping a game never touches this file.
 */
@Component({
  selector: 'app-games-strip',
  standalone: true,
  imports: [RouterLink, TranslocoPipe],
  templateUrl: './games-strip.html',
  styles: [':host { display: block; width: 100%; }'],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class GamesStrip {
  protected readonly games = GAME_REGISTRY.filter(
    (g) => g.status === 'available' && !!g.launchRoute,
  );
}
