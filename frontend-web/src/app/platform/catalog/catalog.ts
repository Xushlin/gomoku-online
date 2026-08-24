import { ChangeDetectionStrategy, Component, computed, inject, OnInit } from '@angular/core';
import { GameEmblem } from '../../games/emblem/game-emblem';
import { RouterLink } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { GameCapabilitiesService } from '../../games/game-capabilities.service';
import { GameCatalogService } from '../../games/game-catalog.service';
import type { GameManifest } from '../../games/game-manifest';
import { LanguageService } from '../../core/i18n/language.service';

/**
 * Platform game catalogue. Renders one card per registry entry, so shipping a
 * game never touches this component.
 *
 * The card list itself still has no loading / empty / error states: the
 * registry is a static import, so there is nothing to fetch, fail, or come back
 * empty. The server capability layer on top of it *is* async, but it only ever
 * adds an affordance — before it resolves (or if it fails) no ladder links
 * render, which is exactly the pre-change page.
 */
@Component({
  selector: 'app-catalog',
  standalone: true,
  imports: [RouterLink, TranslocoPipe, GameEmblem],
  templateUrl: './catalog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Catalog implements OnInit {
  private readonly catalog = inject(GameCatalogService);
  private readonly capabilities = inject(GameCapabilitiesService);
  private readonly language = inject(LanguageService);

  protected readonly games = computed(() => this.catalog.all());

  ngOnInit(): void {
    this.capabilities.ensureLoaded();
  }

  /**
   * Whether this card gets a "leaderboard" link — playable today *and* the
   * server says the game is rated.
   *
   * Tic-tac-toe must not get one (it is unrated), and puzzle games must not
   * either (they have no `IGameRules`, so no capability at all — "not
   * applicable", not `false`). Both cases are covered by the same check
   * because a missing descriptor is falsy.
   *
   * This reads the server rather than a manifest flag on purpose: a stale
   * manifest copy would show a link to a permanently empty ladder, which looks
   * identical to a new game nobody has played. See the `GameDescriptor` docs.
   */
  protected hasLadder(game: GameManifest): boolean {
    return game.status === 'available' && this.capabilities.of(game.key)?.isRated === true;
  }

  /**
   * Whether this card gets a "high scores" link — playable today *and* in the
   * score-attack category.
   *
   * Unlike {@link hasLadder} this reads the manifest, and that is not a relapse
   * into client-side copies of server facts. `isRated` is a server judgement with
   * no client-side counterpart, and a stale copy of it points at a permanently
   * empty ladder. `category` is not a copy of anything: it is declared here, the
   * catalogue already groups by it, and a score-attack game has a score ladder by
   * definition. There is also no server flag to read — tetris has no `IGameRules`,
   * so `GET /api/games` does not describe it at all.
   */
  protected hasScoreBoard(game: GameManifest): boolean {
    return game.status === 'available' && game.category === 'score';
  }

  /** `catalog.category-match` / `-puzzle` / `-score`. */
  protected categoryKey(game: GameManifest): string {
    return `catalog.category-${game.category}`;
  }

  /**
   * True when the game's content does not exist in the locale the user is
   * reading the UI in — the idiom games are Chinese-content whatever the
   * chrome says. Drives the advisory badge; it does not block launching.
   */
  protected showsContentWarning(game: GameManifest): boolean {
    return !game.contentLocales.includes(this.language.current());
  }
}
